using UnityEngine;

/// Mostra o Drake parado na cena durante a edição (o desenho de verdade é montado pelo
/// DrakeBoss no Play, a partir de Monsters/MVP/Resources/Drake).
[ExecuteAlways]
public class DrakePreview : MonoBehaviour
{
    private void OnEnable()
    {
        if (Application.isPlaying) return;
        var sr = GetComponentInChildren<SpriteRenderer>();
        var tex = Resources.Load<Texture2D>("Drake/parado");
        if (sr == null || tex == null) return;
        tex.filterMode = FilterMode.Point;
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 4f / tex.height), 64f);
        sprite.hideFlags = HideFlags.DontSave;   // só para ver na edição; não vai para a cena
        sr.sprite = sprite;
        sr.sortingOrder = YSortRenderer.OrderFor(transform.position.y);
    }
}
