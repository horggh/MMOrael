using UnityEngine;
using UnityEngine.InputSystem;

/// Barra de atalhos com 10 posições ligadas às teclas 1–0.
/// Cada posição guarda uma referência (id de item agora, habilidades no futuro):
/// o item continua no inventário, a barra só aponta para ele.
public class ActionBar : MonoBehaviour
{
    public const int Size = 10;
    public string[] slots = new string[Size];
    public const string SkillPrefix = "skill:";

    private Inventory _bag;

    private static readonly Key[] Keys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0,
    };

    private void Awake()
    {
        _bag = GetComponent<Inventory>();
        // Primeira vez: poção já no atalho 1.
        if (string.IsNullOrEmpty(slots[0])) slots[0] = "pocao_vermelha";
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || UIKit.Typing) return;
        for (int i = 0; i < Size; i++)
        {
            if (kb[Keys[i]].wasPressedThisFrame) Trigger(i);
        }
    }

    public void Trigger(int i)
    {
        string id = slots[i];
        if (string.IsNullOrEmpty(id)) return;

        // Habilidades ficam na barra com prefixo "skill:"; o resto é item.
        if (id.StartsWith(SkillPrefix))
        {
            var su = GetComponent<SkillUser>();
            if (su != null) su.Request(id.Substring(SkillPrefix.Length));
        }
        else if (_bag != null) _bag.Use(id);
    }
}
