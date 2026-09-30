using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupAnimatedPlayer
{
    private const string ScenePath = "Assets/Scenes/Map_StarterField.unity";
    private const string ModelPath = "Assets/Art/Models/Characters/Medieval.fbx";
    private const string ControllerPath = "Assets/Art/Animation/PlayerAnimator.controller";
    private const string PlayerName = "Player_Knight";
    private const float TargetHeight = 1.70f;

    private static float PathX(float z) =>
        28f * Mathf.Sin(z * 0.018f) + 12f * Mathf.Sin(z * 0.045f + 1.3f);

    public static void Setup()
    {
        ConfigureImporter();

        var clips = LoadClips();
        Debug.Log($"[AnimPlayer] {clips.Count} clipes encontrados: {string.Join(", ", clips.Keys.OrderBy(k => k))}");

        var idle = FindClip(clips, "Idle_Sword", "Idle_Neutral", "Idle");
        var walk = FindClip(clips, "Walk");
        var run = FindClip(clips, "Run", "Run_Forward");

        if (idle == null || walk == null || run == null)
        {
            Debug.LogError($"[AnimPlayer] Clipes essenciais faltando. idle={idle?.name} walk={walk?.name} run={run?.name}");
            return;
        }

        Debug.Log($"[AnimPlayer] Usando idle='{idle.name}', walk='{walk.name}', run='{run.name}'");

        var controller = BuildController(idle, walk, run);
        SwapPlayer(controller);
    }

    private static void ConfigureImporter()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[AnimPlayer] Importer nao encontrado para {ModelPath}");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.SaveAndReimport();
    }

    private static Dictionary<string, AnimationClip> LoadClips()
    {
        var result = new Dictionary<string, AnimationClip>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                result[clip.name] = clip;
            }
        }
        return result;
    }

    /// Os clipes vêm nomeados como "CharacterArmature|Run". Compara só a parte depois da barra,
    /// exigindo igualdade exata — senão "Run" casaria com "Run_Left" dependendo da ordem do dicionário.
    private static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, params string[] names)
    {
        foreach (var wanted in names)
        {
            foreach (var kv in clips.OrderBy(k => k.Key))
            {
                int bar = kv.Key.LastIndexOf('|');
                string shortName = bar >= 0 ? kv.Key.Substring(bar + 1) : kv.Key;

                if (shortName.Equals(wanted, System.StringComparison.OrdinalIgnoreCase))
                {
                    return kv.Value;
                }
            }
        }

        return null;
    }

    private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        System.IO.Directory.CreateDirectory("Assets/Art/Animation");
        AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        var walkState = sm.AddState("Walk");
        walkState.motion = walk;
        var runState = sm.AddState("Run");
        runState.motion = run;

        sm.defaultState = idleState;

        // Limiares casam com walkSpeed=3.5 e runSpeed=7 de PlayerMovement.
        Link(idleState, walkState, AnimatorConditionMode.Greater, 0.2f);
        Link(walkState, idleState, AnimatorConditionMode.Less, 0.2f);
        Link(walkState, runState, AnimatorConditionMode.Greater, 5f);
        Link(runState, walkState, AnimatorConditionMode.Less, 5f);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AnimPlayer] AnimatorController criado em {ControllerPath}");
        return controller;
    }

    private static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        var t = from.AddTransition(to);
        t.AddCondition(mode, threshold, "Speed");
        t.hasExitTime = false;
        t.duration = 0.15f;
    }

    private static void SwapPlayer(AnimatorController controller)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var old = GameObject.Find(PlayerName);
        if (old != null) Object.DestroyImmediate(old);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = PlayerName;
        go.transform.localScale = Vector3.one;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        // Escala a partir da altura real medida do modelo.
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float nativeHeight = b.size.y;
        go.transform.localScale = Vector3.one * (TargetHeight / nativeHeight);

        b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        float z = 0f;
        go.transform.position = new Vector3(PathX(z), -b.min.y, z);
        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        var cc = go.AddComponent<CharacterController>();
        cc.height = TargetHeight;
        cc.radius = 0.30f;
        cc.center = new Vector3(0f, TargetHeight / 2f, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.35f;

        go.AddComponent<PlayerMovement>();

        var camGo = Camera.main != null ? Camera.main.gameObject : GameObject.Find("Main Camera");
        var orbit = camGo.GetComponent<OrbitCamera>();
        if (orbit == null) orbit = camGo.AddComponent<OrbitCamera>();
        orbit.target = go.transform;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AnimPlayer] PRONTO: personagem animado (altura nativa {nativeHeight:F2} m -> {TargetHeight:F2} m) na trilha.");
    }
}
