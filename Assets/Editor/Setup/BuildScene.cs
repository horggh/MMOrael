using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

public static class BuildScene
{
    private const string ScenePath = "Assets/Scenes/Campo.unity";
    private const string AnimDir = "Assets/Art/Animation";
    private const string CHAR = "Assets/Art/Sprites/Characters";
    private const string MON = "Assets/Art/Sprites/Monsters";
    private const string ENV = "Assets/Art/Sprites/Environment";

    private const float MapW = 40f;
    private const float MapH = 30f;

    /// Longe o bastante do ponto de partida para o jogador começar fora do alcance
    /// de percepção deles (5 unidades) e escolher quando entrar em combate.
    private static readonly Vector2[] WolfSpots =
    {
        new Vector2(8f, 4f),
        new Vector2(-9f, -4f),
        new Vector2(4f, -9f),
    };

    public static void Run()
    {
        // Limiar de corrida acompanha a velocidade de cada um: cavaleiro corre a 5,2,
        // lobo persegue a 3,2.
        var knight = BuildAnimator("Cavaleiro", CHAR, "cavaleiro", 3.6f, 3f, 8f, 12f, 14f);
        var wolf = BuildAnimator("Lobo", MON, "lobo", 2.4f, 3f, 8f, 12f, 12f);
        BuildWorld(knight, wolf);
    }

    // ---------- Animações ----------

    private static Sprite[] Frames(string dir, string sheet)
    {
        return AssetDatabase.LoadAllAssetsAtPath($"{dir}/{sheet}.png")
            .OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
            .ToArray();
    }

    private static AnimationClip MakeClip(string dir, string sheet, float fps, bool loop)
    {
        var frames = Frames(dir, sheet);
        if (frames.Length == 0)
        {
            Debug.LogError($"[Cena] sem quadros em {sheet} - o corte dos sprites falhou");
            return null;
        }

        var clip = new AnimationClip { frameRate = fps };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = frames.Select((f, i) => new ObjectReferenceKeyframe
        {
            time = i / fps,
            value = f
        }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        System.IO.Directory.CreateDirectory(AnimDir);
        AssetDatabase.CreateAsset(clip, $"{AnimDir}/{sheet}.anim");
        return clip;
    }

    private static AnimatorController BuildAnimator(string name, string dir, string prefix,
                                                    float runThreshold, float idleFps,
                                                    float walkFps, float runFps, float attackFps)
    {
        System.IO.Directory.CreateDirectory(AnimDir);
        string controllerPath = $"{AnimDir}/{name}.controller";
        AssetDatabase.DeleteAsset(controllerPath);

        var idle = MakeClip(dir, $"{prefix}_idle", idleFps, true);
        var walk = MakeClip(dir, $"{prefix}_walk", walkFps, true);
        var run = MakeClip(dir, $"{prefix}_run", runFps, true);
        var attack = MakeClip(dir, $"{prefix}_attack", attackFps, false);

        var c = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var sm = c.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
        var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
        var sRun = sm.AddState("Run"); sRun.motion = run;
        var sAtk = sm.AddState("Attack"); sAtk.motion = attack;
        sm.defaultState = sIdle;

        Link(sIdle, sWalk, AnimatorConditionMode.Greater, 0.1f);
        Link(sWalk, sIdle, AnimatorConditionMode.Less, 0.1f);
        Link(sWalk, sRun, AnimatorConditionMode.Greater, runThreshold);
        Link(sRun, sWalk, AnimatorConditionMode.Less, runThreshold);
        Link(sRun, sIdle, AnimatorConditionMode.Less, 0.1f);

        var any = sm.AddAnyStateTransition(sAtk);
        any.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        any.duration = 0.02f;
        any.canTransitionToSelf = false;

        var back = sAtk.AddTransition(sIdle);
        back.hasExitTime = true;
        back.exitTime = 0.95f;
        back.duration = 0.05f;

        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cena] Animator {name} criado: Idle/Walk/Run + Attack");
        return c;
    }

    private static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float v)
    {
        var t = from.AddTransition(to);
        t.AddCondition(mode, v, "Speed");
        t.hasExitTime = false;
        t.duration = 0.1f;
    }

    // ---------- Mundo ----------

    private static void BuildWorld(AnimatorController controller, AnimatorController wolfController)
    {
        // Ordena os sprites pelo eixo Y: quem está mais abaixo aparece na frente.
        // Precisa valer nos dois lugares — o Renderer2D da URP sobrepõe a configuração
        // do projeto, e deixá-lo no modo padrão faz a ordenação por eixo ser ignorada.
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 0f);
        FixRenderer2DSorting();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = new GameObject("Chao");
        var gr = ground.AddComponent<SpriteRenderer>();
        gr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ENV}/grama_base.png");
        gr.drawMode = SpriteDrawMode.Tiled;
        gr.size = new Vector2(MapW, MapH);
        // A ordem dos objetos varia com o Y (±480 neste mapa); o chão precisa ficar
        // seguramente abaixo de todos eles.
        gr.sortingOrder = -10000;

        var props = new GameObject("Vegetacao");
        var rng = new System.Random(2026);
        float hw = MapW / 2f - 1f, hh = MapH / 2f - 1f;
        Occupied.Clear();
        // Reserva os pontos dos lobos antes de espalhar a vegetação, senão uma árvore
        // pode nascer exatamente onde o lobo aparece.
        Occupied.AddRange(WolfSpots);

        // Árvore desenhada tem 41px contra 36px do personagem: fica quase do mesmo
        // tamanho. Escala 2 (inteira, para os pixels virarem blocos 2x2 regulares)
        // deixa a copa ~2,3x a altura dele.
        Scatter(props, rng, "arvore", 5, 26, hw, hh, collide: true, scale: 2f, spacing: 2.6f);
        Scatter(props, rng, "arbusto", 7, 40, hw, hh, collide: false, spacing: 1.1f);
        Scatter(props, rng, "grama_alta", 4, 120, hw, hh, collide: false, spacing: 0.7f);

        var decos = AssetDatabase.FindAssets("t:Sprite", new[] { ENV })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.Contains("/deco_"))
            .ToArray();
        for (int i = 0; i < 90 && decos.Length > 0; i++)
        {
            if (!TryFindSpot(rng, hw, hh, 0.5f, out var pos)) continue;
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(decos[rng.Next(decos.Length)]);
            var go = new GameObject("deco");
            go.transform.SetParent(props.transform);
            go.transform.position = pos;
            var dsr = go.AddComponent<SpriteRenderer>();
            dsr.sprite = sp;
            dsr.sortingOrder = YSortRenderer.OrderFor(pos.y);
        }

        var player = new GameObject("Cavaleiro");
        player.transform.position = Vector3.zero;
        var psr = player.AddComponent<SpriteRenderer>();
        psr.sprite = Frames(CHAR, "cavaleiro_idle").FirstOrDefault();
        var anim = player.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var col = player.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.45f, 0.35f);
        col.offset = new Vector2(0f, 0.18f);   // só os pés colidem, não a espada
        var ps = player.AddComponent<CharacterStats>();
        ps.level = 1;
        ps.strength = 5; ps.agility = 5; ps.vitality = 5;
        ps.intelligence = 1; ps.dexterity = 5; ps.luck = 3;
        var playerHealth = player.AddComponent<Health>();
        player.AddComponent<PlayerController2D>();
        player.AddComponent<YSortRenderer>();  // ele se move, então a ordem é recalculada a cada quadro
        player.AddComponent<CombatHUD>().player = playerHealth;

        var wolves = new GameObject("Lobos");
        var wolfSprite = Frames(MON, "lobo_idle").FirstOrDefault();
        for (int i = 0; i < WolfSpots.Length; i++)
        {
            var w = new GameObject($"Lobo_{i + 1}");
            w.transform.SetParent(wolves.transform);
            w.transform.position = WolfSpots[i];
            w.AddComponent<SpriteRenderer>().sprite = wolfSprite;
            w.AddComponent<Animator>().runtimeAnimatorController = wolfController;

            var wb = w.AddComponent<Rigidbody2D>();
            wb.gravityScale = 0f;
            wb.freezeRotation = true;
            wb.mass = 1.5f;   // um pouco mais pesado: o jogador não o empurra com o corpo

            // Corpo comprido e baixo: colisor horizontal só na altura das patas.
            var wc = w.AddComponent<CapsuleCollider2D>();
            wc.direction = CapsuleDirection2D.Horizontal;
            wc.size = new Vector2(1.1f, 0.3f);
            wc.offset = new Vector2(0f, 0.15f);

            var ws = w.AddComponent<CharacterStats>();
            ws.level = 3;
            ws.strength = 4; ws.agility = 6; ws.vitality = 2;
            ws.intelligence = 1; ws.dexterity = 4; ws.luck = 2;
            w.AddComponent<Health>();
            w.AddComponent<WolfAI>();
            w.AddComponent<YSortRenderer>();
        }

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.10f, 0.14f, 0.12f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var pp = camGo.AddComponent<PixelPerfectCamera>();
        pp.assetsPPU = CameraFollow2D.pixelsPerUnit;
        pp.refResolutionX = 480;
        pp.refResolutionY = 270;
        pp.upscaleRT = false;
        var follow = camGo.AddComponent<CameraFollow2D>();
        follow.target = player.transform;
        // (pixelsPerUnit da câmera agora é fixo em 64 no CameraFollow2D)

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Cena] {ScenePath} criada: mapa {MapW}x{MapH}, " +
                  $"{props.transform.childCount} objetos de cenario");
    }

    /// O modo de ordenação do Renderer2D não é exposto na API pública, mas é um campo
    /// serializado do asset. 3 = eixo customizado.
    private static void FixRenderer2DSorting()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Renderer2D.asset");
        if (data == null)
        {
            Debug.LogWarning("[Cena] Renderer2D.asset nao encontrado");
            return;
        }

        var so = new SerializedObject(data);
        var mode = so.FindProperty("m_TransparencySortMode");
        var axis = so.FindProperty("m_TransparencySortAxis");
        if (mode == null)
        {
            Debug.LogWarning("[Cena] campo de ordenacao nao encontrado no Renderer2D");
            return;
        }

        mode.intValue = 3;
        if (axis != null) axis.vector3Value = new Vector3(0f, 1f, 0f);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);
        Debug.Log("[Cena] Renderer2D ajustado para ordenar pelo eixo Y");
    }

    private static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    /// Espaço livre ao redor do ponto onde o jogador nasce. Sem isso a vegetação
    /// nasce em cima dele e parece que o personagem está "dentro" da moita.
    private const float SpawnClear = 2.5f;

    private static readonly List<Vector2> Occupied = new List<Vector2>();

    private static bool TryFindSpot(System.Random rng, float hw, float hh, float spacing, out Vector3 pos)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var p = new Vector2(Rand(rng, -hw, hw), Rand(rng, -hh, hh));
            if (p.magnitude < SpawnClear) continue;
            if (Occupied.Any(o => (o - p).sqrMagnitude < spacing * spacing)) continue;
            Occupied.Add(p);
            pos = new Vector3(p.x, p.y, 0f);
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    private static void Scatter(GameObject parent, System.Random rng, string prefix,
                                int variants, int count, float hw, float hh, bool collide,
                                float scale = 1f, float spacing = 0.8f)
    {
        for (int i = 0; i < count; i++)
        {
            string path = $"{ENV}/{prefix}_{rng.Next(1, variants + 1):00}.png";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) continue;
            if (!TryFindSpot(rng, hw, hh, spacing, out var pos)) continue;

            var go = new GameObject(prefix);
            go.transform.SetParent(parent.transform);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            // Cenário não se move: a ordem pode ser calculada uma vez, aqui.
            sr.sortingOrder = YSortRenderer.OrderFor(pos.y);

            if (collide)
            {
                // Colisor só no tronco: a copa é passável, senão a árvore vira um muro.
                var c = go.AddComponent<CircleCollider2D>();
                c.radius = 0.18f;
                c.offset = new Vector2(0f, 0.15f);
            }
        }
    }
}
