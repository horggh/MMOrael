using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupPlayerAndCamera
{
    private const string ScenePath = "Assets/Scenes/Map_StarterField.unity";
    private const string PlayerName = "Player_Knight";

    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = GameObject.Find(PlayerName);
        if (player == null)
        {
            Debug.LogError($"[Setup] '{PlayerName}' nao encontrado na cena.");
            return;
        }

        // --- Controlador de personagem dimensionado para 1,70 m ---
        var controller = player.GetComponent<CharacterController>();
        if (controller == null) controller = player.AddComponent<CharacterController>();
        controller.height = 1.70f;
        controller.radius = 0.30f;
        controller.center = new Vector3(0f, 0.85f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = 0.35f;

        var movement = player.GetComponent<PlayerMovement>();
        if (movement == null) movement = player.AddComponent<PlayerMovement>();
        movement.walkSpeed = 3.5f;
        movement.runSpeed = 7f;

        // --- Camera orbital ---
        var camGo = Camera.main != null ? Camera.main.gameObject : GameObject.Find("Main Camera");
        if (camGo == null)
        {
            Debug.LogError("[Setup] Main Camera nao encontrada.");
            return;
        }

        var orbit = camGo.GetComponent<OrbitCamera>();
        if (orbit == null) orbit = camGo.AddComponent<OrbitCamera>();
        orbit.target = player.transform;
        orbit.targetHeight = 1.2f;
        orbit.distance = 12f;
        orbit.yaw = 45f;
        orbit.pitch = 35f;

        // A camera nao pode carregar posicao fixa da montagem inicial do mapa.
        camGo.transform.position = player.transform.position + new Vector3(0f, 8f, -9f);

        // --- Garante chao colidivel ---
        var ground = GameObject.Find("Ground_StarterField");
        if (ground != null && ground.GetComponent<Collider>() == null)
        {
            ground.AddComponent<MeshCollider>();
            Debug.Log("[Setup] Collider adicionado ao chao.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Setup] PRONTO: CharacterController + PlayerMovement no personagem, OrbitCamera na camera.");
        Debug.Log($"[Setup] Chao tem collider: {(ground != null && ground.GetComponent<Collider>() != null)}");
    }
}
