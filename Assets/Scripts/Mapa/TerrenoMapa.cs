using UnityEngine;
using UnityEngine.Tilemaps;

/// Terreno pintado de um mapa (pincel de terrenos, RaelMMO > Mapa > Pincel de terreno).
///
/// Grade dupla ("dual grid"): o mapa guarda, por quadrado, QUAL terreno ele é (tilemap
/// "Dados", invisível); o desenho fica em uma camada por terreno, deslocada meio quadrado, e
/// cada peça é escolhida pelos 4 quadrados de dados que se encontram nos cantos dela (16
/// peças por terreno, Tools/gen_terrenos.py). As camadas seguem a ordem de ids (a primeira
/// embaixo): os terrenos de cima cobrem a borda dos de baixo.
/// A água tem colisão (a forma do desenho), então o personagem não entra nela.
public class TerrenoMapa : MonoBehaviour
{
    [Tooltip("Ids dos terrenos, na ordem das camadas (o primeiro fica embaixo).")]
    public string[] ids;
    public Tilemap dados;
    public Tilemap[] camadas;
    [Tooltip("Um marcador por terreno (o que o tilemap Dados guarda em cada quadrado).")]
    public TileBase[] marcadores;
    [Tooltip("16 peças por terreno, em sequência: peca[terreno * 16 + índice dos cantos].")]
    public TileBase[] pecas;

    public int IndexOf(TileBase marker) => System.Array.IndexOf(marcadores, marker);

    /// Terreno do quadrado de dados (−1 = nenhum).
    public int TerrenoEm(Vector3Int c) => dados != null ? IndexOf(dados.GetTile(c)) : -1;

    /// Terreno utilizável (os dados e as camadas existem: uma camada apagada à mão é ignorada).
    public bool Valido => dados != null && camadas != null && marcadores != null && pecas != null;

    /// Põe (ou tira, com t < 0) o terreno nos quadrados e redesenha as peças em volta.
    public void Pintar(System.Collections.Generic.IEnumerable<Vector3Int> cells, int t)
    {
        var mexidos = new System.Collections.Generic.HashSet<Vector3Int>();
        foreach (var c in cells)
        {
            dados.SetTile(c, t >= 0 ? marcadores[t] : null);
            // Este quadrado é canto de 4 peças: (x,y), (x+1,y), (x,y+1), (x+1,y+1).
            for (int dy = 0; dy <= 1; dy++)
                for (int dx = 0; dx <= 1; dx++)
                    mexidos.Add(new Vector3Int(c.x + dx, c.y + dy, 0));
        }
        foreach (var p in mexidos) Redesenhar(p);
    }

    /// Peça (x,y) de cada camada pelos cantos: BL = dados(x−1,y−1), BR = (x,y−1),
    /// TL = (x−1,y), TR = (x,y). Índice = BL + 2·BR + 4·TL + 8·TR.
    public void Redesenhar(Vector3Int p)
    {
        int bl = TerrenoEm(new Vector3Int(p.x - 1, p.y - 1, 0)), br = TerrenoEm(new Vector3Int(p.x, p.y - 1, 0));
        int tl = TerrenoEm(new Vector3Int(p.x - 1, p.y, 0)), tr = TerrenoEm(new Vector3Int(p.x, p.y, 0));
        for (int t = 0; t < camadas.Length; t++)
        {
            if (camadas[t] == null) continue;   // camada apagada
            int idx = (bl == t ? 1 : 0) | (br == t ? 2 : 0) | (tl == t ? 4 : 0) | (tr == t ? 8 : 0);
            camadas[t].SetTile(p, idx == 0 ? null : pecas[t * 16 + idx]);
        }
    }

    /// Redesenha tudo a partir dos dados (depois de trocar as peças, por exemplo).
    public void RedesenharTudo()
    {
        foreach (var c in camadas) if (c != null) c.ClearAllTiles();
        var b = dados.cellBounds;
        for (int y = b.yMin; y <= b.yMax; y++)
            for (int x = b.xMin; x <= b.xMax; x++)
                Redesenhar(new Vector3Int(x, y, 0));
    }
}
