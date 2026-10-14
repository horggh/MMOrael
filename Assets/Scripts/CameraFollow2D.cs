using UnityEngine;

/// Segue o alvo com leve atraso. Arredonda a posição para a grade de pixels:
/// sem isso a câmera para "entre pixels" e a imagem inteira treme ao andar.
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.12f;
    public int pixelsPerUnit = 32;

    private Vector3 _velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        Vector3 pos = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);

        pos.x = Mathf.Round(pos.x * pixelsPerUnit) / pixelsPerUnit;
        pos.y = Mathf.Round(pos.y * pixelsPerUnit) / pixelsPerUnit;
        transform.position = pos;
    }
}
