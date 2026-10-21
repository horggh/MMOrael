using UnityEngine;

/// Portal entre mapas (decoração portal_mapa, entrada_caverna, entrada_caverna_escura): pisar no redemoinho leva o personagem para a
/// frente do portal de destino, que pode estar em outra área da mesma cena. Ligue dois
/// portais pelo menu RaelMMO > Mapa > Ligar os 2 portais selecionados (ida e volta).
/// O redemoinho é um gatilho (não bloqueia); as colunas de pedra têm colisão.
[RequireComponent(typeof(CircleCollider2D))]
public class MapPortal : MonoBehaviour
{
    [Tooltip("Portal para onde este leva.")]
    public MapPortal destino;
    [Tooltip("Meio do redemoinho, acima do pé do desenho (unidades).")]
    public float alturaDoMeio = 1.4f;
    [Tooltip("Onde o personagem aparece ao chegar: abaixo do pé do portal (unidades).")]
    public float saida = 0.6f;
    [Tooltip("Distância do meio até cada lado sólido (colunas do portal, pedras da caverna).")]
    public float colunas = 0.9f;
    [Tooltip("Largura de cada lado sólido.")]
    public float larguraColunas = 0.35f;

    // Quem acabou de chegar não volta na hora: só depois de sair do gatilho.
    private static float _blockedUntil;

    private void Reset() => Setup();
    private void Awake() => Setup();

    /// Entrada de caverna (desenho mais largo, túnel mais alto): meio e lados próprios.
    public void ComoEntradaDeCaverna()
    {
        alturaDoMeio = 1.5f;
        colunas = 1.35f;
        larguraColunas = 1.1f;
        Setup();
    }

    public void Setup()
    {
        var c = GetComponent<CircleCollider2D>();
        c.isTrigger = true;
        c.radius = 0.55f;
        c.offset = new Vector2(0f, alturaDoMeio - 0.4f);
        // Lados sólidos (o personagem entra pela frente); refeitos com as medidas atuais.
        var boxes = GetComponents<BoxCollider2D>();
        for (int i = 0; i < 2; i++)
        {
            var b = i < boxes.Length ? boxes[i] : gameObject.AddComponent<BoxCollider2D>();
            b.size = new Vector2(larguraColunas, 0.4f);
            b.offset = new Vector2((i == 0 ? -1f : 1f) * colunas, 0.45f);
        }
    }

    public Vector2 Saida => (Vector2)transform.position + Vector2.down * saida;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (destino == null || Time.time < _blockedUntil) return;
        var pc = other.GetComponentInParent<PlayerController2D>();
        if (pc == null) return;
        _blockedUntil = Time.time + 1f;
        var t = pc.transform;
        Vector2 to = destino.Saida;
        t.position = new Vector3(to.x, to.y, t.position.z);
        var body = pc.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = to; body.linearVelocity = Vector2.zero; }
        pc.ClearOrders();
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.Snap();
        SaveGame.Save();
    }

    private void OnDrawGizmos()
    {
        if (destino == null) return;
        Gizmos.color = new Color(0.6f, 0.4f, 1f);
        Vector3 a = transform.position + Vector3.up * alturaDoMeio, b = destino.transform.position + Vector3.up * destino.alturaDoMeio;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.3f);
    }
}
