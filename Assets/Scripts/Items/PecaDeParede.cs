using UnityEngine;

/// Papel de parede ou janela fixos do mapa (colocados no editor pela janela Decoração, abas
/// Paredes e Janelas), para decorar por dentro os prédios da cidade. Usa os mesmos desenhos
/// do jogo (Resources/Walls e Resources/Windows), mas não vai no save: faz parte da cena.
/// O desenho é montado ao ligar, no editor e no Play.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class PecaDeParede : MonoBehaviour
{
    public string id;
    public bool janela;

    private void OnEnable() => Build();
    private void OnValidate() => Build();

    private void Build()
    {
        if (string.IsNullOrEmpty(id)) return;
        var sr = GetComponent<SpriteRenderer>();
        sr.sprite = janela ? WallDecor.SpriteFor(id) : FloorTiles.SpriteFor(id);
        sr.sortingOrder = janela ? WallDecor.WindowOrder : FloorTiles.WallOrder;

        // Janela com vidro vazado: o céu por trás, como no jogo.
        if (!janela || transform.Find("Céu") != null) return;
        var skyTex = Resources.Load<Texture2D>("Windows/" + id + "_ceu");
        if (skyTex == null) return;
        skyTex.filterMode = FilterMode.Point;
        var sky = new GameObject("Céu").AddComponent<SpriteRenderer>();
        sky.gameObject.hideFlags = HideFlags.DontSave;   // refeito ao abrir; não vai para a cena
        sky.transform.SetParent(transform, false);
        sky.sprite = Sprite.Create(skyTex, new Rect(0, 0, skyTex.width, skyTex.height), new Vector2(0.5f, 0.5f), 64f);
        sky.sortingOrder = WallDecor.SkyOrder;
    }
}
