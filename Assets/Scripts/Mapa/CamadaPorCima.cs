using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Tilemap que fica por cima do personagem (copas, telhados, arcos pintados em tile). Some
/// quando o personagem passa por baixo dele (Profundidade).
[RequireComponent(typeof(Tilemap))]
public class CamadaPorCima : MonoBehaviour
{
    public static readonly List<CamadaPorCima> All = new List<CamadaPorCima>();
    public Tilemap Tilemap { get; private set; }

    private void OnEnable()
    {
        Tilemap = GetComponent<Tilemap>();
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable() => All.Remove(this);
}
