using UnityEngine;

/// Marca, na cena, a sala interna de um prédio da cidade (criada pelo menu RaelMMO > Prédios >
/// Editar interior do prédio selecionado). Só desenha o contorno no editor: piso, parede do
/// fundo e porta, no lugar onde o jogo monta a sala. A decoração colocada ali aparece dentro
/// do prédio no Play.
public class InteriorMolde : MonoBehaviour
{
    public int interiorId;
    [Tooltip("Sala de corredor (andar de cima de uma escada): comprida e baixa.")]
    public bool corredor;

    private void OnDrawGizmos()
    {
        Vector2 o = HouseInterior.OriginFor(interiorId);
        var size = HouseInterior.SizeFor(corredor);
        float w = size.x, h = size.y, top = size.z;
        float left = o.x - w / 2f;
        // Piso livre (onde vão móveis), parede do fundo (quadros, janelas) e porta.
        Box(new Rect(left, o.y, w, h - top), new Color(0.9f, 0.7f, 0.3f));
        Box(new Rect(left, o.y + h - top, w, top), new Color(0.4f, 0.7f, 1f));
        Box(new Rect(o.x - HouseInterior.DoorW / 2f, o.y - HouseInterior.Wall, HouseInterior.DoorW, HouseInterior.Wall), Color.green);
        Box(new Rect(left - HouseInterior.Wall, o.y - HouseInterior.Wall, w + 2 * HouseInterior.Wall, h + HouseInterior.Wall), Color.gray);
    }

    private static void Box(Rect r, Color c)
    {
        Gizmos.color = c;
        Gizmos.DrawWireCube(r.center, r.size);
        Gizmos.color = new Color(c.r, c.g, c.b, 0.08f);
        Gizmos.DrawCube(r.center, r.size);
    }
}
