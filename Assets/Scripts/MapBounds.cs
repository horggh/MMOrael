using UnityEngine;
using UnityEngine.SceneManagement;

/// Limite do mapa: quatro paredes invisíveis em volta da área jogável. Substitui a muralha
/// de árvores na borda (centenas de objetos com imagem e colisor) por 4 colisores parados,
/// que custam praticamente nada e não deixam frestas.
///
/// Fica num objeto da cena ("Limites do mapa"): a área é editável no Inspector ou arrastando
/// as alças das bordas na janela Scene (MapBoundsEditor), e aparece lá em vermelho.
[ExecuteAlways]
public class MapBounds : MonoBehaviour
{
    [Tooltip("Área em que o personagem pode andar (x, y = canto de baixo à esquerda).")]
    public Rect playable = Rect.MinMaxRect(-19.5f, -14.7f, 19.5f, 14.2f);
    [Tooltip("Espessura das paredes, do lado de fora da área.")]
    public float thickness = 2f;

    private static MapBounds _active;
    /// Todas as áreas da cena (uma cena pode ter vários mapas, ligados por portais).
    private static readonly System.Collections.Generic.List<MapBounds> _all = new System.Collections.Generic.List<MapBounds>();

    /// O ponto está dentro de alguma área jogável da cena.
    public static bool AnyContains(Vector2 p)
    {
        if (_all.Count == 0) return Default.Contains(p);
        foreach (var b in _all) if (b.playable.Contains(p)) return true;
        return false;
    }
    private static readonly Rect Default = Rect.MinMaxRect(-19.5f, -14.7f, 19.5f, 14.2f);

    /// Área jogável do mapa em uso (plantio e a volta ao mapa do jogo salvo também usam).
    public static Rect Playable => _active != null ? _active.playable : Default;

    private void OnEnable()
    {
        _active = this;
        if (!_all.Contains(this)) _all.Add(this);
        if (Application.isPlaying) BuildColliders();
    }

    private void OnDisable()
    {
        _all.Remove(this);
        if (_active == this) _active = _all.Count > 0 ? _all[_all.Count - 1] : null;
    }

    /// Cena sem o objeto (jogo antigo): cria um com a área padrão.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInScene()
    {
        if (SceneManager.GetActiveScene().name != "Campo" || FindAnyObjectByType<MapBounds>() != null) return;
        new GameObject("Limites do mapa").AddComponent<MapBounds>();
    }

    private void BuildColliders()
    {
        foreach (var old in GetComponents<BoxCollider2D>()) Destroy(old);
        foreach (var w in Walls())
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            // As paredes ficam em coordenadas do mundo: o objeto pode estar em qualquer lugar.
            box.offset = w.center - (Vector2)transform.position;
            box.size = w.size;
        }
    }

    /// As 4 paredes, do lado de fora da área e encostadas nela; as do topo e da base passam
    /// dos cantos para não sobrar fresta na diagonal.
    public Rect[] Walls()
    {
        var r = playable;
        float t = thickness;
        return new[]
        {
            Box(new Vector2(r.center.x, r.yMax + t / 2f), new Vector2(r.width + t * 2f, t)),
            Box(new Vector2(r.center.x, r.yMin - t / 2f), new Vector2(r.width + t * 2f, t)),
            Box(new Vector2(r.xMin - t / 2f, r.center.y), new Vector2(t, r.height)),
            Box(new Vector2(r.xMax + t / 2f, r.center.y), new Vector2(t, r.height)),
        };
    }

    private static Rect Box(Vector2 center, Vector2 size) => new Rect(center - size / 2f, size);

    private void OnValidate()
    {
        playable.width = Mathf.Max(1f, playable.width);
        playable.height = Mathf.Max(1f, playable.height);
        thickness = Mathf.Max(0.1f, thickness);
        if (Application.isPlaying && isActiveAndEnabled) BuildColliders();
    }

    // Vermelho translúcido na janela Scene (editando ou jogando); invisível no jogo.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.22f);
        foreach (var w in Walls()) Gizmos.DrawCube(w.center, w.size);
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.9f);
        foreach (var w in Walls()) Gizmos.DrawWireCube(w.center, w.size);
        Gizmos.DrawWireCube(playable.center, playable.size);
    }
}
