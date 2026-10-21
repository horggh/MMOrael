using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Decorar por dentro os prédios da cidade (o jogador não mexe neles; só na própria casa).
/// Selecione o prédio na cena e use o menu: ele vira construção pronta (se ainda não for),
/// ganha um "Interior - nome" na área afastada onde o jogo monta a sala, e a Scene vai até lá.
/// Coloque a decoração com a janela Decoração (ela coloca no meio da Scene); no Play, o que
/// estiver dentro do contorno aparece dentro do prédio.
public static class PredioInterior
{
    [MenuItem("RaelMMO/Prédios/Editar interior do prédio selecionado")]
    private static void Edit()
    {
        var go = Selection.activeGameObject;
        var sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null)
        {
            EditorUtility.DisplayDialog("Prédio", "Selecione um prédio na cena.", "OK");
            return;
        }
        var (b, molde) = Garantir(go, sr);

        // A Scene vai para o meio da sala; a decoração nova entra aqui. Guarde-a dentro do
        // objeto "Interior - ..." para achar depois.
        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            Vector2 c = HouseInterior.RoomRect(b.interiorId, b.corredor).center;
            view.LookAt(new Vector3(c.x, c.y, 0f), view.rotation, 8f);
        }
        Selection.activeGameObject = molde.gameObject;
    }

    // ---------- replicar a decoração de uma sala de porta para as outras ----------

    /// RaelMMO > Prédios > Replicar decoração de porta: a sala de porta decorada (a selecionada,
    /// ou a que tem mais decoração) é copiada para a sala de cada porta do mapa. As portas que
    /// ainda não tinham sala ganham uma. As que já têm decoração: pergunta se pula ou substitui.
    [MenuItem("RaelMMO/Prédios/Replicar decoração de porta para as outras portas")]
    private static void ReplicarPortas()
    {
        // Portas do mapa (fora das salas, que ficam de x = 1000 em diante).
        var portas = new System.Collections.Generic.List<GameObject>();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
            if (sr.sprite != null && sr.sprite.texture != null && sr.sprite.texture.name.StartsWith("porta_") &&
                sr.transform.position.x < 900f)
                portas.Add(sr.gameObject);
        if (portas.Count < 2)
        {
            EditorUtility.DisplayDialog("Replicar decoração", "Precisa de pelo menos 2 portas no mapa.", "OK");
            return;
        }

        var salas = new System.Collections.Generic.List<(GameObject porta, Building b, InteriorMolde m)>();
        foreach (var p in portas)
        {
            var (b, m) = Garantir(p, p.GetComponent<SpriteRenderer>());
            salas.Add((p, b, m));
        }

        // Origem: a sala selecionada, senão a mais decorada.
        var sel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<InteriorMolde>() : null;
        var origem = salas.Find(s => s.m == sel);
        if (origem.m == null)
        {
            int best = -1;
            foreach (var s in salas)
            {
                int n = Decoracao(s.m).Count;
                if (n > best) { best = n; origem = s; }
            }
        }
        var pecas = Decoracao(origem.m);
        if (pecas.Count == 0)
        {
            EditorUtility.DisplayDialog("Replicar decoração", "Nenhuma sala de porta tem decoração ainda.", "OK");
            return;
        }

        int jaDecoradas = 0;
        foreach (var s in salas) if (s.m != origem.m && Decoracao(s.m).Count > 0) jaDecoradas++;
        bool substituir = false;
        if (jaDecoradas > 0)
        {
            int r = EditorUtility.DisplayDialogComplex("Replicar decoração",
                $"Copiar {pecas.Count} peças de \"{origem.m.name}\" para {salas.Count - 1} salas de porta.\n" +
                $"{jaDecoradas} delas já têm decoração.", "Pular as decoradas", "Cancelar", "Substituir tudo");
            if (r == 1) return;
            substituir = r == 2;
        }

        Vector2 o0 = HouseInterior.OriginFor(origem.m.interiorId);
        int feitas = 0;
        foreach (var s in salas)
        {
            if (s.m == origem.m) continue;
            var existentes = Decoracao(s.m);
            if (existentes.Count > 0)
            {
                if (!substituir) continue;
                foreach (var e in existentes) Undo.DestroyObjectImmediate(e.gameObject);
            }
            Vector3 off = HouseInterior.OriginFor(s.m.interiorId) - o0;
            foreach (var src in pecas)
            {
                var copia = Object.Instantiate(src.gameObject, src.position + off, src.rotation, s.m.transform);
                copia.name = src.name;
                copia.transform.localScale = src.lossyScale;
                // Porta ou escada dentro da sala: o número da sala dela é dado no Play.
                foreach (var bb in copia.GetComponentsInChildren<Building>()) Object.DestroyImmediate(bb);
                Undo.RegisterCreatedObjectUndo(copia, "Replicar decoração");
            }
            feitas++;
        }
        EditorSceneManager.MarkSceneDirty(origem.m.gameObject.scene);
        EditorUtility.DisplayDialog("Replicar decoração",
            $"Decoração de \"{origem.m.name}\" copiada para {feitas} salas de porta. Salve a cena (Ctrl+S).", "OK");
    }

    /// Peças de decoração de uma sala: os objetos (de cima) dentro da área da sala.
    public static System.Collections.Generic.List<Transform> Decoracao(InteriorMolde m)
    {
        var area = HouseInterior.RoomRect(m.interiorId, m.corredor);
        area.xMin -= 1f; area.yMin -= 1.5f; area.xMax += 1f; area.yMax += 1f;
        var set = new System.Collections.Generic.HashSet<Transform>();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
        {
            if (!area.Contains(sr.transform.position)) continue;
            var t = sr.transform;
            // Sobe até o objeto de cima da peça (sem passar da sala nem de grupos fora dela).
            while (t.parent != null && t.parent != m.transform && t.parent.GetComponent<InteriorMolde>() == null &&
                   area.Contains(t.parent.position))
                t = t.parent;
            if (t.GetComponent<InteriorMolde>() == null) set.Add(t);
        }
        return new System.Collections.Generic.List<Transform>(set);
    }

    /// Prédio (Building) e sala (InteriorMolde) do objeto; cria o que faltar.
    private static (Building, InteriorMolde) Garantir(GameObject go, SpriteRenderer sr)
    {
        var b = go.GetComponent<Building>();
        if (b == null)
        {
            b = Undo.AddComponent<Building>(go);
            b.SetupFromArt(sr.sprite);
            int next = 1000;
            foreach (var other in Object.FindObjectsByType<Building>())
                if (other != b) next = Mathf.Max(next, other.interiorId);
            b.interiorId = next + 1;
        }

        string name = "Interior - " + (string.IsNullOrEmpty(b.displayName) ? go.name : b.displayName) + " #" + b.interiorId;
        InteriorMolde molde = null;
        foreach (var m in Object.FindObjectsByType<InteriorMolde>())
            if (m.interiorId == b.interiorId) molde = m;
        if (molde == null)
        {
            var root = GameObject.Find("Interiores") ?? new GameObject("Interiores");
            var room = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(room, "Interior do prédio");
            room.transform.SetParent(root.transform);
            Vector2 o = HouseInterior.OriginFor(b.interiorId);
            room.transform.position = new Vector3(o.x, o.y, 0f);
            molde = room.AddComponent<InteriorMolde>();
            molde.interiorId = b.interiorId;
            molde.corredor = b.corredor;
            // Sala nova já nasce com a decoração básica do tipo dela (se houver).
            InteriorMoldeEditor.Carregar(molde, perguntar: false);
        }
        molde.corredor = b.corredor;
        return (b, molde);
    }

    /// Volta a Scene para o mapa (no personagem), de onde estiver (sala de prédio, masmorra...).
    [MenuItem("RaelMMO/Ir para o mapa %#m")]
    private static void ToMap()
    {
        var pc = Object.FindAnyObjectByType<PlayerController2D>();
        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        Vector3 at = pc != null ? pc.transform.position : Vector3.zero;
        if (pc != null) Selection.activeGameObject = pc.gameObject;
        view.LookAt(at, view.rotation, 8f);
    }

    [MenuItem("RaelMMO/Prédios/Voltar para o prédio deste interior")]
    private static void Back()
    {
        var go = Selection.activeGameObject;
        var molde = go != null ? go.GetComponentInParent<InteriorMolde>() : null;
        if (molde == null) { EditorUtility.DisplayDialog("Prédio", "Selecione o objeto \"Interior - ...\".", "OK"); return; }
        foreach (var b in Object.FindObjectsByType<Building>())
        {
            if (b.interiorId != molde.interiorId) continue;
            Selection.activeGameObject = b.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            return;
        }
    }
}
