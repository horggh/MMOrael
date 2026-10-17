using UnityEngine;

/// Um piso ou papel de parede pintado num quadrado. Guarda o próprio id e quadrado e se
/// inscreve em FloorTiles ao ligar: se o Unity recompilar no meio do Play (o que apaga as
/// listas estáticas), os pisos se reinscrevem sozinhos e não somem do jogo salvo.
public class FloorTile : MonoBehaviour
{
    public string id;
    public Vector2Int cell;

    private void OnEnable() => FloorTiles.Register(this);
    private void OnDisable() => FloorTiles.Unregister(this);
}
