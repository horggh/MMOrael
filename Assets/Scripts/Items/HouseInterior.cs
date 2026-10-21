using UnityEngine;

/// Interior da casa: segundo mapa, montado na mesma cena numa área afastada (x = 1000 em
/// diante, um lugar por casa), assim o mundo de fora continua andando (plantas crescem,
/// obras avançam). Os móveis colocados dentro (baú etc.) ficam no lugar daquela casa e
/// são salvos como os de fora.
/// Entrar pelo botão "Entrar" da janela da casa pronta; sair pisando na porta, embaixo.
///
/// Por enquanto a arte é provisória: piso com a madeira do kit e paredes em cor sólida.
public class HouseInterior : MonoBehaviour
{
    // Sala em unidades do mapa (1 unidade = 32 px, um quadrado da grade).
    // Parede do fundo com 4 quadrados de altura: é personalizável (papel de parede, e depois
    // quadros, janelas e adornos). O piso continua com 12x6.
    public const float RoomW = 12f, RoomH = 10f, WallTop = 4f, Wall = 0.5f, DoorW = 2f;
    // Corredor (o andar de cima das escadas): comprido de lado a lado e baixo.
    public const float CorridorW = 24f, CorridorH = 7f, CorridorWallTop = 3f;

    /// Tamanho da sala (largura, altura, altura da parede do fundo): casa ou corredor.
    public static Vector3 SizeFor(bool corredor) =>
        corredor ? new Vector3(CorridorW, CorridorH, CorridorWallTop) : new Vector3(RoomW, RoomH, WallTop);

    /// Área inteira da sala de um interior (piso e parede do fundo).
    public static Rect RoomRect(int id, bool corredor)
    {
        var s = SizeFor(corredor);
        Vector2 o = OriginFor(id);
        return new Rect(o.x - s.x / 2f, o.y, s.x, s.y);
    }

    // Sala atual.
    private static Vector3 _size = new Vector3(RoomW, RoomH, WallTop);
    private static float W => _size.x;
    private static float H => _size.y;
    private static float Top => _size.z;
    private const float FirstX = 1000f, Spacing = 40f;
    private static Vector2 Origin;   // meio da parede de baixo da sala da casa atual

    /// Construção em que o personagem está agora (null = do lado de fora).
    public static Building Inside { get; private set; }

    private static HouseInterior _room;
    private static Sprite _pixel;
    // Guardados no próprio objeto: se o Unity recompilar no meio do Play, os campos estáticos
    // somem e são refeitos a partir daqui (OnEnable).
    [SerializeField] private Building _house;
    [SerializeField] private Vector2 _origin;
    [SerializeField] private Transform _player;
    private float _exitArmed;   // só deixa sair depois de se afastar da porta ao entrar

    private void OnEnable()
    {
        if (_house == null) return;
        _room = this;
        Inside = _house;
        Origin = _origin;
        _size = SizeFor(_house.corredor);
    }

    /// Ponto logo acima da porta, onde o personagem aparece ao entrar.
    public static Vector2 EntryPoint => Origin + new Vector2(0f, Wall + 0.8f);

    /// Piso livre da sala atual (sem a parede do fundo): onde dá para colocar móveis.
    public static Rect Floor => new Rect(Origin.x - W / 2f, Origin.y, W, H - Top);

    /// Parede do fundo da sala atual: onde vai papel de parede (e, no futuro, quadros e adornos).
    public static Rect BackWall => new Rect(Origin.x - W / 2f, Origin.y + H - Top, W, Top);

    /// Cada casa tem a sala num lugar só dela, pelo número do interior (inteiro: fica na grade).
    private static Vector2 OriginFor(Building house) => OriginFor(house.interiorId);

    /// Meio da parede de baixo da sala do interior número id (o editor decora os prédios aqui).
    public static Vector2 OriginFor(int id) => new Vector2(FirstX + id * Spacing, 0f);

    /// Dentro de um prédio da cidade: a decoração é do mapa (feita no editor), o jogador não mexe.
    public static bool InCityBuilding => Inside != null && Inside.IsCity;

    public static void Enter(Building house, Vector2? playerPos = null)
    {
        var pc = FindAnyObjectByType<PlayerController2D>();
        if (pc == null) return;
        if (_room != null) Destroy(_room.gameObject);
        Origin = OriginFor(house);
        _size = SizeFor(house.corredor);
        _room = Build();
        Inside = house;
        _room._house = house;
        _room._origin = Origin;
        _room._player = pc.transform;
        _room._exitArmed = 0f;
        // Posição salva fora da sala (o número do interior mudou, ex.: prédio passou pelo menu
        // "Editar interior"): entra pela porta em vez de cair no vazio.
        var room = new Rect(Origin.x - W / 2f, Origin.y, W, H);
        if (playerPos.HasValue && !room.Contains(playerPos.Value)) playerPos = null;
        if (house.Passage) DecoracaoBasica(house.corredor);
        Teleport(pc.transform, playerPos ?? EntryPoint);
        SaveGame.Save();
    }

    /// Sala de porta/escada que não foi decorada no editor: usa a decoração básica salva
    /// (Resources/DecoracaoBasica, botão no Inspector da sala). Some junto com a sala ao sair.
    private static void DecoracaoBasica(bool corredor)
    {
        var area = new Rect(Origin.x - W / 2f, Origin.y, W, H);
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
            if (sr.transform.root != _room.transform && area.Contains(sr.transform.position)) return;   // já decorada
        var prefab = Resources.Load<GameObject>("DecoracaoBasica/DecoracaoBasica_" + (corredor ? "Corredor" : "Sala"));
        if (prefab == null) return;
        foreach (Transform t in prefab.transform)
        {
            var c = Object.Instantiate(t.gameObject, (Vector3)Origin + t.localPosition, t.rotation, _room.transform);
            c.transform.localScale = t.localScale;
        }
    }

    public static void Exit()
    {
        var house = Inside;
        Inside = null;
        if (_room != null) { Destroy(_room.gameObject); _room = null; }
        var pc = FindAnyObjectByType<PlayerController2D>();
        if (pc != null && house != null)
        {
            Vector2 front = (Vector2)house.transform.position + Vector2.down * 0.6f;   // na frente da porta
            // Porta ou escada dentro de outra sala (ex.: a escada de um prédio): volta para ela.
            foreach (var b in Building.All)
            {
                if (b == house || !b.Done) continue;
                if (RoomRect(b.interiorId, b.corredor).Contains(house.transform.position))
                {
                    Enter(b, front);
                    return;
                }
            }
            Teleport(pc.transform, front);
        }
        SaveGame.Save();
    }

    private static void Teleport(Transform player, Vector2 pos)
    {
        player.position = new Vector3(pos.x, pos.y, player.position.z);
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = pos; body.linearVelocity = Vector2.zero; }
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.Snap();
    }

    private void Update()
    {
        if (_player == null) return;
        Vector2 p = _player.position;
        var door = new Rect(Origin.x - DoorW / 2f, Origin.y - 0.6f, DoorW, Wall + 0.5f);
        if (!door.Contains(p)) { _exitArmed = 1f; return; }
        if (_exitArmed > 0f) Exit();
    }

    // ---------- montagem da sala ----------

    private static HouseInterior Build()
    {
        var root = new GameObject("Interior da Casa").AddComponent<HouseInterior>();
        var t = root.transform;
        float left = Origin.x - W / 2f, bottom = Origin.y;

        // Piso de tábuas (a madeira do kit, repetida).
        var floorTex = Resources.Load<Texture2D>("UI/Kit/madeira");
        if (floorTex != null)
        {
            floorTex.filterMode = FilterMode.Point;
            floorTex.wrapMode = TextureWrapMode.Repeat;
            var floor = Sprite.Create(floorTex, new Rect(0, 0, floorTex.width, floorTex.height),
                                      new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            var sr = Part(t, "Piso", new Vector2(Origin.x, bottom + H / 2f), floor, -10000);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(W, H);
        }

        var wallColor = new Color(0.42f, 0.28f, 0.18f);
        var trimColor = new Color(0.25f, 0.16f, 0.1f);

        // Parede do fundo, alta, com rodapé escuro.
        Block(t, "Parede fundo", new Rect(left, bottom + H - Top, W, Top), wallColor, -9000, true);
        Block(t, "Rodapé", new Rect(left, bottom + H - Top, W, 0.15f), trimColor, -8980, false);
        // Laterais.
        Block(t, "Parede esquerda", new Rect(left - Wall, bottom, Wall, H), trimColor, -8000, true);
        Block(t, "Parede direita", new Rect(left + W, bottom, Wall, H), trimColor, -8000, true);
        // Frente, com o vão da porta no meio (fica na frente do personagem).
        float side = (W - DoorW) / 2f;
        Block(t, "Parede frente esq.", new Rect(left - Wall, bottom - Wall, side + Wall, Wall), trimColor, 9000, true);
        Block(t, "Parede frente dir.", new Rect(Origin.x + DoorW / 2f, bottom - Wall, side + Wall, Wall), trimColor, 9000, true);
        // Porta: escuro no vão e um capacho para indicar a saída.
        Block(t, "Porta", new Rect(Origin.x - DoorW / 2f, bottom - Wall, DoorW, Wall), new Color(0.1f, 0.07f, 0.05f), 9000, false);
        Block(t, "Capacho", new Rect(Origin.x - 0.6f, bottom + 0.05f, 1.2f, 0.4f), new Color(0.55f, 0.35f, 0.2f), -9500, false);
        // Barreira invisível abaixo da porta: sair é pelo capacho, não andando para o vazio.
        var stop = new GameObject("Limite porta").AddComponent<BoxCollider2D>();
        stop.transform.SetParent(t);
        stop.transform.position = new Vector2(Origin.x, bottom - Wall - 0.3f);
        stop.size = new Vector2(DoorW, 0.3f);
        return root;
    }

    private static SpriteRenderer Part(Transform parent, string name, Vector2 pos, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private static void Block(Transform parent, string name, Rect r, Color color, int order, bool solid)
    {
        if (_pixel == null)
            _pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f,
                                   0, SpriteMeshType.FullRect);
        var sr = Part(parent, name, r.center, _pixel, order);
        sr.drawMode = SpriteDrawMode.Sliced;   // permite definir o tamanho direto
        sr.size = r.size;
        sr.color = color;
        if (!solid) return;
        var col = sr.gameObject.AddComponent<BoxCollider2D>();
        col.size = r.size;
    }

    private void OnGUI()
    {
        UIKit.Label(new Rect(0, 12, Screen.width, 12 * UIKit.Px), "Casa  ·  saia pela porta, embaixo", 8, UIKit.Cream, TextAnchor.MiddleCenter);
    }
}
