using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Profundidade como nos jogos 2D atuais (Stardew Valley, Eastward):
/// - Objeto alto (prédio, árvore, decoração grande) fica transparente quando o personagem
///   está ATRÁS dele (os pés acima da base do objeto e o corpo dentro do desenho).
/// - Camada "Por cima" do mapa (copas, telhados pintados em tilemap, CamadaPorCima) some
///   quando o personagem passa por baixo.
/// - Atrás de um prédio, aparece a silhueta do personagem por cima de tudo.
/// A ordem de desenho continua pelo pé de cada objeto (YSortRenderer).
public class Profundidade : MonoBehaviour
{
    [Tooltip("Transparência do objeto com o personagem atrás.")]
    public float alphaAtras = 0.4f;
    [Tooltip("Altura mínima (quadrados) para o objeto ficar transparente.")]
    public float alturaMinima = 1.8f;
    public float velocidade = 8f;

    private static Profundidade _instance;
    private readonly Dictionary<SpriteRenderer, float> _alpha = new Dictionary<SpriteRenderer, float>();
    private SpriteRenderer _silhueta, _corpo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (_instance == null) _instance = new GameObject("Profundidade").AddComponent<Profundidade>();
    }

    private void LateUpdate()
    {
        var pc = PlayerController2D.Main;
        if (pc == null) return;
        Vector2 pes = pc.transform.position;
        Vector3 peito = (Vector3)pes + Vector3.up * 0.8f;
        float k = 1f - Mathf.Exp(-velocidade * Time.deltaTime);
        bool atrasDePredio = false;

        foreach (var ys in YSortRenderer.All)
        {
            var sr = ys.Renderer;
            if (sr == null || ys.Personagem || sr.sprite == null) continue;
            var b = sr.bounds;
            bool alto = b.size.y >= alturaMinima;
            bool atras = alto && pes.y > ys.transform.position.y + ys.sortOffset + 0.05f &&
                         b.Contains(new Vector3(peito.x, peito.y, b.center.z));
            if (atras && b.size.y >= 3f) atrasDePredio = true;
            float alvo = atras ? alphaAtras : 1f;
            _alpha.TryGetValue(sr, out float atual);
            if (!_alpha.ContainsKey(sr)) atual = sr.color.a;
            if (Mathf.Abs(atual - alvo) < 0.005f && !_alpha.ContainsKey(sr) && alvo >= 1f) continue;
            atual = Mathf.Lerp(atual, alvo, k);
            var c = sr.color;
            c.a = atual;
            sr.color = c;
            if (alvo >= 1f && atual > 0.995f) { c.a = 1f; sr.color = c; _alpha.Remove(sr); }
            else _alpha[sr] = atual;
        }

        foreach (var tm in CamadaPorCima.All)
        {
            bool embaixo = tm.Tilemap.HasTile(tm.Tilemap.WorldToCell(peito));
            var c = tm.Tilemap.color;
            c.a = Mathf.Lerp(c.a, embaixo ? 0.3f : 1f, k);
            tm.Tilemap.color = c;
        }

        Silhueta(pc, atrasDePredio);
    }

    /// Cópia translúcida do personagem por cima de tudo enquanto ele está atrás de um prédio.
    private void Silhueta(PlayerController2D pc, bool mostrar)
    {
        if (_corpo == null) _corpo = pc.GetComponent<SpriteRenderer>();
        if (_silhueta == null)
        {
            var go = new GameObject("Silhueta");
            go.transform.SetParent(pc.transform, false);
            _silhueta = go.AddComponent<SpriteRenderer>();
            _silhueta.sortingOrder = 32000;
            _silhueta.color = new Color(0.75f, 0.88f, 1f, 0.38f);
        }
        _silhueta.enabled = mostrar && _corpo != null;
        if (!_silhueta.enabled) return;
        _silhueta.sprite = _corpo.sprite;
        _silhueta.flipX = _corpo.flipX;
    }
}
