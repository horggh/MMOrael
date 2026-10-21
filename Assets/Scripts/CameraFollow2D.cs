using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D;

/// Segue o alvo com leve atraso. Arredonda a posição para o pixel da TELA: sem isso a
/// câmera para "entre pixels" e a imagem treme. (Antes arredondava para o pixel da arte, que
/// na escala 2x-4x são vários pixels de tela: a câmera andava aos saltos enquanto o personagem
/// deslizava, e o andar parecia travado.)
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.12f;
    // Padrão do jogo: 64 px por quadrado (o valor salvo na cena, antigo, é ignorado).
    public const int pixelsPerUnit = 64;

    private Vector3 _velocity;

    // Zoom out pela roda do mouse. O nível 0 é o padrão, com o Pixel Perfect. Os outros
    // mostram mais terreno ajustando o tamanho da visão direto: mudar a resolução de
    // referência do Pixel Perfect não muda nada quando a janela do jogo é pequena (a escala
    // inteira dele fica em 1x em todos os níveis).
    private static readonly float[] ZoomFactors = { 1f, 1.5f, 2f };
    /// Nível -1: aproxima do personagem. Metade da visão = cada pixel da arte com o dobro do
    /// tamanho do padrão, em escala inteira (continua nítido, sem tremer).
    public const int MinZoom = -1;
    private const float ZoomInFactor = 0.5f;

    private static float Factor(int level) => level < 0 ? ZoomInFactor : ZoomFactors[level];
    private const int RefWidth = 960, RefHeight = 540;

    /// Nível atual (-1 = aproximado, 0 = padrão, 1 e 2 = afastado). Salvo no jogo salvo.
    public static int Zoom { get; private set; }
    private PixelPerfectCamera _pp;
    private Camera _cam;

    /// Aplica o nível de zoom (do jogo salvo ou da roda do mouse).
    public static void SetZoom(int level)
    {
        Zoom = Mathf.Clamp(level, MinZoom, ZoomFactors.Length - 1);
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.ApplyZoom();
    }

    private static CameraFollow2D _main;

    private void Awake()
    {
        // Só uma câmera de jogo: uma cópia (ex.: o mapa copiado junto com ela) mostraria a tela
        // enquanto o clique é calculado pela outra, e o personagem andaria para o lado errado.
        if (_main != null && _main != this && _main.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"Há mais de uma câmera na cena: \"{name}\" foi desligada. Apague a cópia na Hierarchy.", this);
            gameObject.SetActive(false);
            return;
        }
        _main = this;
        // Segue sempre o personagem de verdade (a cópia dele, se houver, é desligada).
        var pc = FindAnyObjectByType<PlayerController2D>();
        if (pc != null) target = pc.transform;
    }

    private void Start()
    {
        _cam = GetComponent<Camera>();
        if (PlayerController2D.Main != null) target = PlayerController2D.Main.transform;
        ApplyZoom();
        Snap();
    }

    private void ApplyZoom()
    {
        if (_pp == null) _pp = GetComponent<PixelPerfectCamera>();
        if (_pp != null)
        {
            // Mesma área visível de antes (480x270 a 32 px = 960x540 a 64 px).
            _pp.assetsPPU = pixelsPerUnit;
            _pp.refResolutionX = RefWidth;
            _pp.refResolutionY = RefHeight;
        }
        if (_cam == null) _cam = GetComponent<Camera>();
        if (_pp != null) _pp.enabled = Zoom == 0;
        if (Zoom != 0 && _cam != null) _cam.orthographicSize = BaseSize() * Factor(Zoom);
    }

    /// Tamanho da visão do nível padrão: o mesmo que o Pixel Perfect calcula para esta tela.
    private float BaseSize()
    {
        int scale = Mathf.Max(1, Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight));
        return Screen.height / (2f * scale * pixelsPerUnit);
    }

    private void Update()
    {
        if (Zoom != 0 && _cam != null) _cam.orthographicSize = BaseSize() * Factor(Zoom);

        // Roda do mouse: para baixo afasta, para cima aproxima. Sobre janelas a roda é delas.
        var mouse = Mouse.current;
        if (mouse == null || ItemUI.PointerOverUI || CombatHUD.PointerOverUI || Building.PointerOverUI) return;
        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.01f) return;
        int next = Mathf.Clamp(Zoom + (wheel < 0 ? 1 : -1), MinZoom, ZoomFactors.Length - 1);
        if (next == Zoom) return;
        SetZoom(next);
        SaveGame.Save();
    }

    /// Vai direto para o alvo, sem deslizar (ao entrar ou sair de um mapa).
    public void Snap()
    {
        if (target == null) return;
        _velocity = Vector3.zero;
        transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);

        float perUnit = _cam != null && _cam.orthographic ? Screen.height / (2f * _cam.orthographicSize) : pixelsPerUnit;
        pos.x = Mathf.Round(pos.x * perUnit) / perUnit;
        pos.y = Mathf.Round(pos.y * perUnit) / perUnit;
        transform.position = pos;
    }
}
