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

    private SpriteRenderer _renderer;
    private float _lastY = float.NaN;

    public static int OrderFor(float y) => Mathf.RoundToInt(-y * Precision);

    private void Awake() => _renderer = GetComponent<SpriteRenderer>();

    private void LateUpdate()
    {
        float y = transform.position.y;
        if (Mathf.Approximately(y, _lastY)) return;
        _lastY = y;
        _renderer.sortingOrder = OrderFor(y);
    }
}
