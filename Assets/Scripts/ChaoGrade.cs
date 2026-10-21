using UnityEngine;

/// Chão encaixado na grade: cada ladrilho da textura ocupa exatamente 1 quadrado, o canto
/// de baixo à esquerda fica num canto de quadrado e o tamanho é em quadrados inteiros.
/// Funciona editando: ao mover ou esticar, volta para o quadrado mais perto.
/// (RaelMMO > Chão > Alinhar selecionados à grade põe isto em qualquer chão já colocado.)
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ChaoGrade : MonoBehaviour
{
    private SpriteRenderer _sr;

    private void OnEnable() => _sr = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (!Application.isPlaying) Snap();   // no jogo o chão não se mexe
    }

    /// Ajusta escala, tamanho e posição. Devolve se mudou algo.
    public bool Snap()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null || _sr.sprite == null) return false;
        float cell = FarmGrid.Cell;
        bool changed = false;

        // Escala: 1 ladrilho (o sprite inteiro) = 1 quadrado.
        Vector2 tile = _sr.sprite.bounds.size;
        var scale = new Vector3(cell / tile.x, cell / tile.y, 1f);
        if ((transform.localScale - scale).sqrMagnitude > 1e-8f) { transform.localScale = scale; changed = true; }

        // Tamanho (modo Tiled/Sliced): número inteiro de ladrilhos.
        if (_sr.drawMode != SpriteDrawMode.Simple)
        {
            var s = _sr.size;
            var whole = new Vector2(Mathf.Max(1f, Mathf.Round(s.x / tile.x)) * tile.x, Mathf.Max(1f, Mathf.Round(s.y / tile.y)) * tile.y);
            if ((whole - s).sqrMagnitude > 1e-8f) { _sr.size = whole; changed = true; }
        }

        // Posição: o canto de baixo à esquerda no canto de quadrado mais perto.
        Vector2 min = _sr.bounds.min;
        var target = new Vector2(Mathf.Round(min.x / cell) * cell, Mathf.Round(min.y / cell) * cell);
        Vector2 delta = target - min;
        if (delta.sqrMagnitude > 1e-8f) { transform.position += (Vector3)delta; changed = true; }
        return changed;
    }
}
