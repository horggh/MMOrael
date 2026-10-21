using UnityEngine;

/// Define quem aparece na frente numa vista de cima: quanto mais abaixo na tela,
/// mais à frente. Como o pivô dos sprites fica na base, transform.position.y é a
/// linha dos pés — exatamente o critério certo de profundidade.
///
/// A ordem explícita tem prioridade sobre a ordenação automática do pipeline, então
/// o resultado não depende de configuração de render.
[RequireComponent(typeof(SpriteRenderer))]
public class YSortRenderer : MonoBehaviour
{
    public const int Precision = 32;

    [Tooltip("Sobe a linha que decide a profundidade (em unidades). Ex.: prédio com calçada " +
             "desenhada na frente: a linha fica na base das paredes, não na beira da calçada.")]
    public float sortOffset;

    private SpriteRenderer _renderer;
    private float _lastY = float.NaN;

    /// Todos os objetos ordenados pela altura (a Profundidade usa para deixar transparente o
    /// que está na frente do personagem).
    public static readonly System.Collections.Generic.List<YSortRenderer> All = new System.Collections.Generic.List<YSortRenderer>();
    public SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();
    /// Personagem ou monstro (não fica transparente).
    public bool Personagem { get; private set; }

    public static int OrderFor(float y) => Mathf.RoundToInt(-y * Precision);

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        Personagem = GetComponentInParent<Health>() != null || GetComponentInParent<PlayerController2D>() != null;
    }

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() => All.Remove(this);

    private void LateUpdate()
    {
        float y = transform.position.y + sortOffset;
        if (Mathf.Approximately(y, _lastY)) return;
        _lastY = y;
        _renderer.sortingOrder = OrderFor(y);
    }
}
