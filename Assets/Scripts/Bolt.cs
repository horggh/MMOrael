using UnityEngine;

/// Virote da besta: voa em linha reta e acerta o primeiro que tiver vida no caminho.
public class Bolt : MonoBehaviour
{
    public float speed = 14f;

    private CharacterStats _from;
    private Health _owner;
    private Vector2 _dir, _start;
    private float _range, _knockback, _mult;
    private bool _skill;
    private static Sprite _sprite;

    public static void Fire(CharacterStats from, Health owner, Vector2 start, Vector2 dir, float range,
                            float knockback, float mult, bool skill)
    {
        var go = new GameObject("Virote");
        go.transform.position = start;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = BoltSprite();
        sr.sortingOrder = 30000;
        var b = go.AddComponent<Bolt>();
        b._from = from; b._owner = owner; b._dir = dir.normalized; b._start = start;
        b._range = range; b._knockback = knockback; b._mult = mult; b._skill = skill;
    }

    private void Update()
    {
        Vector2 p = (Vector2)transform.position + _dir * speed * Time.deltaTime;
        transform.position = p;
        foreach (var h in Health.All)
        {
            if (h == null || h == _owner || h.IsDead) continue;
            if (Vector2.Distance(p, (Vector2)h.transform.position + Vector2.up * 0.35f) > 0.5f) continue;
            Combat.Apply(_from, h, _start, _knockback, _mult, _skill);
            Destroy(gameObject);
            return;
        }
        if (Vector2.Distance(p, _start) > _range) Destroy(gameObject);
    }

    /// Haste de madeira com ponta de metal e penas, desenhada aqui (16x3, 32 px por unidade).
    private static Sprite BoltSprite()
    {
        if (_sprite != null) return _sprite;
        var t = new Texture2D(16, 3) { filterMode = FilterMode.Point };
        var clear = new Color(0, 0, 0, 0);
        var wood = new Color32(120, 78, 40, 255);
        var tip = new Color32(200, 205, 215, 255);
        var feather = new Color32(220, 60, 50, 255);
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 3; y++)
                t.SetPixel(x, y, clear);
        for (int x = 2; x < 13; x++) t.SetPixel(x, 1, wood);
        for (int x = 13; x < 16; x++) t.SetPixel(x, 1, tip);
        t.SetPixel(14, 0, tip); t.SetPixel(14, 2, tip);
        for (int x = 0; x < 4; x++) { t.SetPixel(x, 0, feather); t.SetPixel(x, 2, feather); }
        t.Apply();
        _sprite = Sprite.Create(t, new Rect(0, 0, 16, 3), new Vector2(0.5f, 0.5f), 32f);
        return _sprite;
    }
}
