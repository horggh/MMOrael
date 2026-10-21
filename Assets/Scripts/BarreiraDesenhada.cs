using UnityEngine;

/// Barreira desenhada à mão no editor (RaelMMO > Mapa > Desenhar barreira): uma linha que o
/// personagem e os monstros não atravessam. No jogo é invisível; na edição aparece em vermelho.
/// Os pontos ficam no EdgeCollider2D (dá para ajustar com "Edit Collider" no Inspector).
[RequireComponent(typeof(EdgeCollider2D))]
public class BarreiraDesenhada : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        var edge = GetComponent<EdgeCollider2D>();
        if (edge == null || edge.pointCount < 2) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        var pts = edge.points;
        for (int i = 1; i < pts.Length; i++)
            Gizmos.DrawLine(transform.TransformPoint(pts[i - 1] + edge.offset), transform.TransformPoint(pts[i] + edge.offset));
    }
}
