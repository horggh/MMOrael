using UnityEngine;
using UnityEngine.InputSystem;

/// Posicionar coisas no mapa. Três modos:
///  - Item: usar um item posicionável (baú, obra de casa, piso, papel de parede, janela,
///    raspador). Uma prévia segue o mouse: verde onde dá, vermelha onde não dá.
///  - Martelo (habilidades Mover e Recolher, com o Martelo de construção equipado):
///    escolher um móvel (baú, janela, portal) no mapa. Mover leva para outro lugar; Recolher guarda
///    na bolsa (o baú leva junto tudo o que tem dentro).
///  - Mover: o móvel escolhido segue o mouse com as mesmas regras; clique solta no lugar
///    novo, sem passar pelo inventário. Direito/Esc devolve ao lugar de antes.
/// Clique direito ou Esc sai do modo (no Mover, só cancela o movimento).
public class Placement : MonoBehaviour
{
    private enum Mode { None, Item, Hammer, Move }

    private static Placement _instance;
    private static int _placedFrame = -1;

    /// Posicionando agora: o clique é do posicionamento, não um passo ou golpe.
    public static bool Active => _instance != null && _instance._mode != Mode.None;
    /// Também no quadro em que algo foi colocado, para o mesmo clique não virar movimento.
    public static bool BlocksInput => Active || Time.frameCount == _placedFrame;

    private Mode _mode;
    private Inventory _bag;
    private string _id;          // item usado (no Mover: o id do móvel)
    private int _slot = -1;      // espaço da bolsa de onde o item sai (-1 = qualquer)
    private string _place;       // "bau", "casa", "piso", "parede", "janela", "raspar", "martelo"
    private Vector2Int _size;    // quadrados ocupados na grade
    private SpriteRenderer _ghost;
    private bool _valid;
    private Transform _player;
    // O clique direito que abriu o modo (no inventário) chega ao sistema de input um quadro
    // depois da interface; sem esperar o botão ser solto, ele mesmo cancelaria.
    private bool _waitRelease;

    // Martelo / Mover
    private bool _collect;   // habilidade Recolher (senão, Mover)
    private StorageChest _hoverChest, _moveChest;
    private WallDecor _hoverDecor, _moveDecor;
    private Portal _hoverPortal, _movePortal;
    private SpriteRenderer _tinted;

    public static void Begin(Inventory bag, string id, int slot)
    {
        var def = ItemDB.Get(id);
        if (def == null || string.IsNullOrEmpty(def.place)) return;
        if (_instance == null) _instance = new GameObject("Posicionamento").AddComponent<Placement>();
        var p = _instance;
        p.EndMove(false);
        p.ClearTint();
        p._bag = bag;
        p._player = bag.transform;
        p._waitRelease = true;
        p._slot = slot;
        p._mode = Mode.Item;
        p.SetTarget(id, def.place);
    }

    /// Habilidades do martelo: escolher o móvel para mover (collect = false) ou recolher.
    public static void BeginHammer(Inventory bag, bool collect)
    {
        if (_instance == null) _instance = new GameObject("Posicionamento").AddComponent<Placement>();
        var p = _instance;
        p.EndMove(false);
        p.ClearTint();
        p._bag = bag;
        p._player = bag.transform;
        p._waitRelease = false;
        p._slot = -1;
        p._collect = collect;
        p.BackToHammer();
    }

    /// Martelo na mão (as habilidades só valem com ele equipado).
    private bool HammerEquipped()
    {
        var eq = _bag.GetComponent<Equipment>();
        return eq != null && ItemDB.Get(eq.Get(EquipSlot.Weapon))?.tool == "martelo";
    }

    /// Prepara prévia e tamanho para o tipo de coisa que vai ser posicionada.
    private void SetTarget(string id, string place)
    {
        _id = id;
        _place = place;
        bool house = place == "casa";
        _size = house ? Building.Footprint : place == "portal" ? Portal.Footprint
              : place == "janela" ? WallDecor.SizeOf(id) : Vector2Int.one;
        _ghost.sprite = house ? Building.SpriteFor("casa", 1)
                      : place == "piso" || place == "parede" ? FloorTiles.SpriteFor(id)
                      : place == "raspar" ? Square
                      : place == "janela" ? WallDecor.SpriteFor(id)
                      : place == "portal" ? Portal.StageSprite(1)
                      : StorageChest.ClosedSprite;
        _ghost.enabled = true;
    }

    private static Sprite _square;
    /// Quadrado de 1 unidade, para destacar a célula no modo de raspar.
    private static Sprite Square => _square != null ? _square
        : _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

    private void Awake()
    {
        _ghost = gameObject.AddComponent<SpriteRenderer>();
        _ghost.sortingOrder = 30000;   // por cima de tudo
        _ghost.enabled = false;
    }

    private void Exit()
    {
        EndMove(false);
        ClearTint();
        _mode = Mode.None;
        _id = null;
        _ghost.enabled = false;
    }

    private void Update()
    {
        if (_mode == Mode.None) return;
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || Camera.main == null || _bag == null) { Exit(); return; }
        // O item acabou, ou o martelo saiu da mão.
        if (_mode == Mode.Item && _bag.Count(_id) == 0) { Exit(); return; }
        if (_mode == Mode.Hammer && !HammerEquipped()) { Exit(); return; }

        bool cancel = kb != null && kb.escapeKey.wasPressedThisFrame;
        if (_waitRelease) { if (!mouse.rightButton.isPressed) _waitRelease = false; }
        else cancel |= mouse.rightButton.wasPressedThisFrame;

        Vector2 world = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        bool click = mouse.leftButton.wasPressedThisFrame &&
                     !ItemUI.PointerOverUI && !CombatHUD.PointerOverUI && !Building.PointerOverUI;

        if (_mode == Mode.Hammer) { UpdateHammer(world, click, cancel); return; }

        if (cancel)
        {
            if (_mode == Mode.Move) { EndMove(false); BackToHammer(); }
            else Exit();
            return;
        }

        // Encaixa na grade de 1x1 do mapa (a mesma da plantação). O mouse marca o meio da
        // fileira da frente; a base do desenho fica na borda de baixo dos quadrados.
        Vector2Int cell = FarmGrid.CellOf(world);
        var origin = new Vector2Int(cell.x - _size.x / 2, cell.y);
        Vector2 pos = new Vector2(origin.x + _size.x / 2f, origin.y);
        if (_place == "piso" || _place == "parede" || _place == "raspar") pos = FarmGrid.Center(cell);   // o quadrado inteiro
        else if (_place == "janela") pos = WallDecor.CenterOf(origin, _size);
        else if (_place == "bau") pos = ChestSnap(world);
        _ghost.transform.position = pos;

        _valid = _place == "bau" ? ChestFree(pos) : AreaFree(origin);
        if (_place == "raspar")
            _ghost.color = _valid ? new Color(1f, 0.85f, 0.35f, 0.45f) : new Color(1f, 0.3f, 0.3f, 0.3f);
        else
            _ghost.color = _valid ? new Color(0.6f, 1f, 0.6f, 0.65f) : new Color(1f, 0.4f, 0.4f, 0.65f);

        if (!click || !_valid) return;
        _placedFrame = Time.frameCount;

        if (_mode == Mode.Move)
        {
            // Solta no lugar novo: o mesmo móvel, com tudo dentro, sem passar pela bolsa.
            if (_moveChest != null) _moveChest.transform.position = pos;
            if (_moveDecor != null) _moveDecor.MoveTo(origin);
            if (_movePortal != null)
            {
                _movePortal.transform.position = pos;
                if (_movePortal.active) Portal.RefreshRespawn();   // o retorno acompanha o portal
            }
            EndMove(true);
            SaveGame.Save();
            BackToHammer();
            return;
        }

        if (_place == "raspar")
        {
            // Ferramenta: não se gasta. O piso ou papel raspado volta para a bolsa; o modo continua.
            string scraped = FloorTiles.At(cell);
            FloorTiles.Remove(cell);
            _bag.Add(scraped, 1);
            SaveGame.Save();
            return;
        }

        // Tira da bolsa: do espaço usado, se ainda for este item (baú recolhido traz os itens).
        string data = null;
        if (_slot >= 0 && _slot < _bag.slots.Length && _bag.slots[_slot].id == _id) data = _bag.TakeOne(_slot);
        else _bag.Remove(_id, 1);
        _slot = -1;

        if (_place == "piso" || _place == "parede")
        {
            // Trocar um piso devolve o antigo para a bolsa. O modo continua para pintar vários
            // quadrados seguidos (cancela sozinho quando os pisos acabam).
            string old = FloorTiles.At(cell);
            FloorTiles.Set(cell, _id);
            if (old != null) _bag.Add(old, 1);
            SaveGame.Save();
            return;
        }
        if (_place == "janela") WallDecor.Create(_id, origin);
        else if (_place == "portal") Portal.Create(pos);
        else if (_place == "casa") Building.Create("casa", pos);
        else StorageChest.Create(pos).LoadContents(data);
        SaveGame.Save();   // algo novo no mundo: grava já
        Exit();
    }

    // ---------- Martelo ----------

    private void UpdateHammer(Vector2 world, bool click, bool cancel)
    {
        if (cancel) { Exit(); return; }
        _ghost.enabled = false;

        // Móvel sob o mouse: o que está mais à frente (mais abaixo na tela).
        _hoverChest = null;
        _hoverDecor = null;
        _hoverPortal = null;
        float bestY = float.MaxValue;
        foreach (var pt in Portal.All)
        {
            var sr = pt.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.enabled || !sr.bounds.Contains(new Vector3(world.x, world.y, sr.bounds.center.z))) continue;
            if (pt.transform.position.y < bestY) { bestY = pt.transform.position.y; _hoverPortal = pt; }
        }
        foreach (var c in StorageChest.All)
        {
            var sr = c.GetComponent<SpriteRenderer>();
            if (sr == null || !sr.enabled || !sr.bounds.Contains(new Vector3(world.x, world.y, sr.bounds.center.z))) continue;
            if (c.transform.position.y < bestY) { bestY = c.transform.position.y; _hoverChest = c; _hoverPortal = null; }
        }
        if (_hoverChest == null && _hoverPortal == null)
            foreach (var d in WallDecor.All)
            {
                var sr = d.GetComponent<SpriteRenderer>();
                if (sr != null && sr.bounds.Contains(new Vector3(world.x, world.y, sr.bounds.center.z))) _hoverDecor = d;
            }

        var target = _hoverPortal != null ? _hoverPortal.GetComponent<SpriteRenderer>()
                   : _hoverChest != null ? _hoverChest.GetComponent<SpriteRenderer>()
                   : _hoverDecor != null ? _hoverDecor.GetComponent<SpriteRenderer>() : null;
        if (target != _tinted)
        {
            ClearTint();
            _tinted = target;
            if (_tinted != null) _tinted.color = new Color(1f, 0.85f, 0.45f);   // destaque dourado
        }

        if (!click) return;
        if (target == null)
        {
            // Clique fora de um móvel: sai do modo martelo, e os próximos cliques voltam a
            // andar e atacar (antes o modo ficava ligado e o personagem parecia travado).
            Exit();
            return;
        }
        _placedFrame = Time.frameCount;
        if (_collect) Collect();
        else StartMove();
    }

    /// Recolhe o móvel para a bolsa. O baú vai com tudo o que tem dentro.
    private void Collect()
    {
        if (_hoverPortal != null)
        {
            if (_bag.Add("portal", 1) == 0) { HUD("Bolsa cheia"); return; }
            ClearTint();
            Destroy(_hoverPortal.gameObject);   // se era o retorno, volta a ser o início do mapa
        }
        else if (_hoverChest != null)
        {
            var chest = _hoverChest;
            var stack = new ItemStack { id = "bau", count = 1, data = chest.SaveContents() };
            bool ok = stack.HasData ? _bag.AddStack(stack) : _bag.Add("bau", 1) > 0;
            if (!ok) { HUD("Bolsa cheia"); return; }
            ClearTint();
            chest.Close();
            Destroy(chest.gameObject);
        }
        else if (_hoverDecor != null)
        {
            if (_bag.Add(_hoverDecor.id, 1) == 0) { HUD("Bolsa cheia"); return; }
            ClearTint();
            Destroy(_hoverDecor.gameObject);
        }
        SaveGame.Save();
    }

    private void StartMove()
    {
        ClearTint();
        _mode = Mode.Move;
        if (_hoverPortal != null)
        {
            _movePortal = _hoverPortal;
            _movePortal.SetHidden(true);
            SetTarget("portal", "portal");
        }
        else if (_hoverChest != null)
        {
            _moveChest = _hoverChest;
            _moveChest.SetHidden(true);   // não bloqueia o próprio lugar enquanto se move
            SetTarget("bau", "bau");
        }
        else
        {
            _moveDecor = _hoverDecor;
            _moveDecor.SetHidden(true);
            SetTarget(_moveDecor.id, "janela");
        }
    }

    /// Termina o movimento: o móvel volta a aparecer (no lugar novo ou no antigo).
    private void EndMove(bool placed)
    {
        if (_moveChest != null) _moveChest.SetHidden(false);
        if (_moveDecor != null) _moveDecor.SetHidden(false);
        if (_movePortal != null) _movePortal.SetHidden(false);
        _moveChest = null;
        _moveDecor = null;
        _movePortal = null;
    }

    private void BackToHammer()
    {
        _mode = Mode.Hammer;
        _id = "martelo";
        _place = "martelo";
        _ghost.enabled = false;
    }

    private void ClearTint()
    {
        if (_tinted != null) _tinted.color = Color.white;
        _tinted = null;
    }

    private string _message;
    private float _messageUntil;

    private void HUD(string text)
    {
        _message = text;
        _messageUntil = Time.time + 1.5f;
    }

    // ---------- regras de lugar livre ----------

    /// Todos os quadrados livres e o personagem fora da área. Fora de casa, livre é como para
    /// arar (dentro do mapa, sem canteiro nem obstáculo); dentro, é o piso da sala sem nada
    /// sólido em cima. Casa não se constrói dentro de casa.
    private bool AreaFree(Vector2Int origin)
    {
        bool inside = HouseInterior.Inside != null;
        if (inside && (_place == "casa" || _place == "portal")) return false;
        if (_place == "raspar") return FloorTiles.At(origin) != null;   // só onde há piso ou papel
        if (_place == "janela")
        {
            // Inteira na parede do fundo, sem encostar em outra janela (a que se move não conta).
            if (!inside) return false;
            for (int x = 0; x < _size.x; x++)
                for (int y = 0; y < _size.y; y++)
                {
                    var c = new Vector2Int(origin.x + x, origin.y + y);
                    if (!HouseInterior.BackWall.Contains(FarmGrid.Center(c)) || WallDecor.At(c, _moveDecor) != null) return false;
                }
            return true;
        }
        if (_place == "parede")
            return inside && HouseInterior.BackWall.Contains(FarmGrid.Center(origin)) && FloorTiles.At(origin) != _id;
        if (_place == "piso")
        {
            // Piso é plano: vale embaixo de móveis e árvores; só não repete o mesmo piso.
            Vector2 center = FarmGrid.Center(origin);
            bool onFloor = inside ? HouseInterior.Floor.Contains(center) : FarmGrid.InMap(center);
            return onFloor && FloorTiles.At(origin) != _id;
        }
        for (int x = 0; x < _size.x; x++)
            for (int y = 0; y < _size.y; y++)
            {
                var cell = new Vector2Int(origin.x + x, origin.y + y);
                if (inside ? !RoomCellFree(cell) : !FarmGrid.CanTill(cell)) return false;
            }
        var area = new Rect(origin.x - 0.4f, origin.y - 0.4f, _size.x + 0.8f, _size.y + 0.8f);
        return !area.Contains((Vector2)_player.position);
    }

    /// Baú: livre, em passos de 1 pixel do mapa (1/32), com ímã nos baús vizinhos — perto de
    /// um, encosta exatamente do lado dele, na mesma linha.
    private Vector2 ChestSnap(Vector2 mouse)
    {
        var pos = new Vector2(Mathf.Round(mouse.x * 32f) / 32f, Mathf.Round((mouse.y - StorageChest.Depth / 2f) * 32f) / 32f);
        float w = StorageChest.Width;
        StorageChest best = null;
        float bestGap = 0.4f;   // distância máxima para o ímã puxar
        foreach (var c in StorageChest.All)
        {
            if (c == _moveChest) continue;
            Vector2 o = c.transform.position;
            if (Mathf.Abs(o.y - pos.y) > 0.5f) continue;
            float gap = Mathf.Abs(Mathf.Abs(o.x - pos.x) - w);
            if (gap < bestGap) { bestGap = gap; best = c; }
        }
        if (best != null)
        {
            Vector2 o = best.transform.position;
            pos = new Vector2(o.x + (pos.x >= o.x ? w : -w), o.y);
        }
        return pos;
    }

    /// A base do baú (largura exata x fundo) sem nada sólido, dentro do mapa ou do piso da
    /// sala, fora de canteiro e longe do personagem.
    private bool ChestFree(Vector2 pos)
    {
        float w = StorageChest.Width, d = StorageChest.Depth;
        var rect = new Rect(pos.x - w / 2f, pos.y, w, d);
        bool inside = HouseInterior.Inside != null;
        if (inside)
        {
            var floor = HouseInterior.Floor;
            if (!floor.Contains(rect.min) || !floor.Contains(rect.max)) return false;
        }
        else if (!FarmGrid.InMap(rect.min) || !FarmGrid.InMap(rect.max)) return false;

        // Um pouco menor que a base: encostar no vizinho não conta como sobrepor.
        foreach (var col in Physics2D.OverlapBoxAll(rect.center, rect.size - new Vector2(0.06f, 0.06f), 0f))
            if (!col.isTrigger && col.attachedRigidbody == null) return false;
        for (float x = rect.xMin + 0.05f; x < rect.xMax; x += 0.5f)
            if (FarmGrid.At(FarmGrid.CellOf(new Vector2(x, rect.center.y))) != null) return false;
        var near = new Rect(rect.x - 0.3f, rect.y - 0.3f, rect.width + 0.6f, rect.height + 0.6f);
        return !near.Contains((Vector2)_player.position);
    }

    private static bool RoomCellFree(Vector2Int c)
    {
        Vector2 center = FarmGrid.Center(c);
        if (!HouseInterior.Floor.Contains(center)) return false;
        foreach (var col in Physics2D.OverlapBoxAll(center, Vector2.one * 0.8f, 0f))
            if (!col.isTrigger && col.attachedRigidbody == null) return false;   // parede, baú…
        return true;
    }

    private void OnGUI()
    {
        if (_mode == Mode.None) return;
        Vector2 m = Event.current.mousePosition;
        string text = _mode switch
        {
            Mode.Hammer => _collect ? "Recolher: clique no móvel  ·  clique no chão ou botão direito encerra"
                                    : "Mover: clique no móvel  ·  clique no chão ou botão direito encerra",
            Mode.Move => "Clique para soltar aqui  ·  botão direito devolve ao lugar",
            _ => _place == "raspar" ? "Clique para raspar  ·  botão direito encerra"
                                    : "Clique para colocar  ·  botão direito cancela",
        };
        bool ok = _mode == Mode.Hammer ? _tinted != null : _valid;
        UIKit.Label(new Rect(m.x + 18, m.y + 18, 500, 20 * UIKit.Px), text, 7, ok ? UIKit.Cream : new Color(1f, 0.55f, 0.5f));
        if (_message != null && Time.time < _messageUntil)
            UIKit.Label(new Rect(m.x + 18, m.y + 18 + 12 * UIKit.Px, 300, 20 * UIKit.Px), _message, 8, new Color(1f, 0.55f, 0.5f));
    }
}
