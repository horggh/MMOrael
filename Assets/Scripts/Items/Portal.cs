using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Portal de teleporte: totem de pedra com 3 etapas (Resources/Props/portal_1..3.png):
/// inativo, ativando e ativo. Clicar nele faz o personagem ir até lá e o desperta: as
/// runas acendem, o vórtice abre e ele vira o PONTO DE RETORNO ao morrer. Só um portal é o
/// ponto de retorno por vez (o anterior adormece).
/// No futuro: teleportar para outras cidades a partir de um portal ativo.
public class Portal : MonoBehaviour
{
    // Desenho com 79 px de largura: com 40 px por unidade fica com ~2 quadrados de largura.
    private const float PPU = 40f;
    public static readonly Vector2Int Footprint = new Vector2Int(2, 1);
    public const float Reach = 1.5f;
    private const float WakeTime = 1.2f;

    /// Todos os portais no mapa (para o jogo salvo).
    public static readonly List<Portal> All = new List<Portal>();
    /// Portal que é o ponto de retorno agora (null = o início do mapa).
    public static Portal Current { get; private set; }

    /// É o ponto de retorno (salvo).
    public bool active;

    private static readonly Sprite[] Stages = new Sprite[3];
    private static Portal _pending;
    private SpriteRenderer _sr;
    private BoxCollider2D _col;
    private Transform _player;
    private bool _waking;
    private float _messageUntil;
    private string _message;

    public static Sprite StageSprite(int stage)
    {
        if (Stages[stage - 1] != null) return Stages[stage - 1];
        var tex = Resources.Load<Texture2D>($"Props/portal_{stage}");
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        return Stages[stage - 1] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), PPU);
    }

    public static Portal Create(Vector2 position, bool active = false)
    {
        var go = new GameObject("Portal de teleporte");
        go.transform.position = position;
        var p = go.AddComponent<Portal>();
        if (active) p.SetActive(false);
        return p;
    }

    private void Awake()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = StageSprite(1);
        gameObject.AddComponent<YSortRenderer>();
        // Sólido só na base de pedra.
        _col = gameObject.AddComponent<BoxCollider2D>();
        _col.size = new Vector2(Footprint.x - 0.4f, Footprint.y - 0.4f);
        _col.offset = new Vector2(0f, (Footprint.y - 0.4f) / 2f);
    }

    private void OnEnable()
    {
        All.Add(this);
        if (active) Current = this;   // também depois de recompilar no meio do Play
    }

    private void OnDisable() => All.Remove(this);

    private void OnDestroy()
    {
        if (Current == this) { Current = null; RefreshRespawn(); }
        if (_pending == this) _pending = null;
    }

    /// Onde o personagem renasce: na frente do portal ativo, ou no início do mapa.
    public static void RefreshRespawn()
    {
        var pc = FindAnyObjectByType<PlayerController2D>();
        if (pc == null) return;
        if (Current != null) pc.SetRespawn((Vector2)Current.transform.position + Vector2.down * 0.6f);
        else pc.ResetRespawn();
    }

    /// Pedra de retorno: leva o personagem para a frente do portal ativo (sai de casa antes).
    public static bool TeleportPlayer()
    {
        if (Current == null) return false;
        var pc = FindAnyObjectByType<PlayerController2D>();
        if (pc == null) return false;
        if (HouseInterior.Inside != null) HouseInterior.Exit();
        pc.Teleport((Vector2)Current.transform.position + Vector2.down * 0.6f);
        Current.Say("Retorno!");
        SaveGame.Save();
        return true;
    }

    /// Vira o ponto de retorno. animate = mostra as runas acendendo antes de o vórtice abrir.
    public void SetActive(bool animate)
    {
        if (Current != null && Current != this) Current.Sleep();
        Current = this;
        active = true;
        RefreshRespawn();
        if (animate) StartCoroutine(Wake());
        else _sr.sprite = StageSprite(3);
    }

    private void Sleep()
    {
        active = false;
        _waking = false;
        _sr.sprite = StageSprite(1);
        _sr.color = Color.white;
    }

    private IEnumerator Wake()
    {
        _waking = true;
        _sr.sprite = StageSprite(2);
        yield return new WaitForSeconds(WakeTime);
        _waking = false;
        if (!active) yield break;
        _sr.sprite = StageSprite(3);
        Say("Ponto de retorno salvo");
    }

    private void Say(string text)
    {
        _message = text;
        _messageUntil = Time.time + 2.5f;
    }

    /// Esconde enquanto o martelo leva o portal para outro lugar.
    public void SetHidden(bool hidden)
    {
        _sr.enabled = !hidden;
        _col.enabled = !hidden;
    }

    private void Update()
    {
        // Ativo: brilho pulsando devagar.
        if (active && !_waking)
        {
            float k = 0.88f + 0.12f * Mathf.Sin(Time.time * 2.2f);
            _sr.color = new Color(k, k, 1f, 1f);
        }

        if (_player == null)
        {
            var pc = FindAnyObjectByType<PlayerController2D>();
            if (pc == null) return;
            _player = pc.transform;
        }
        if (!_sr.enabled) return;

        var mouse = Mouse.current;
        bool overUI = CombatHUD.PointerOverUI || ItemUI.PointerOverUI || Building.PointerOverUI || Placement.BlocksInput;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUI && Camera.main != null)
        {
            Vector2 p = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            var b = _sr.bounds;
            if (b.Contains(new Vector3(p.x, p.y, b.center.z))) _pending = this;
            else if (_pending == this) _pending = null;
        }

        if (_pending == this && Vector2.Distance(_player.position, _col.ClosestPoint(_player.position)) <= Reach)
        {
            _pending = null;
            if (active) Say("Este já é o seu ponto de retorno");
            else
            {
                SetActive(true);
                SaveGame.Save();
            }
        }
    }

    private void OnGUI()
    {
        if (_message == null || Time.time > _messageUntil || Camera.main == null) return;
        Vector3 top = Camera.main.WorldToScreenPoint(new Vector3(transform.position.x, _sr.bounds.max.y + 0.2f, 0f));
        if (top.z < 0) return;
        var r = new Rect(top.x - 150, Screen.height - top.y - 10 * UIKit.Px, 300, 10 * UIKit.Px);
        UIKit.Label(r, _message, 8, new Color(0.55f, 0.9f, 1f), TextAnchor.MiddleCenter);
    }
}
