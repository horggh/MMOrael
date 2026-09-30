using UnityEngine;
using UnityEngine.InputSystem;

/// Câmera isométrica que orbita o alvo, no espírito de Ragnarok/Albion:
/// botão direito (ou do meio) arrasta para girar, roda do mouse aproxima.
public class OrbitCamera : MonoBehaviour
{
    [Header("Alvo")]
    public Transform target;
    [Tooltip("Altura do ponto observado, a partir da base do alvo.")]
    public float targetHeight = 1.2f;

    [Header("Distância")]
    public float distance = 12f;
    public float minDistance = 4f;
    public float maxDistance = 30f;
    public float zoomSpeed = 2.5f;

    [Header("Rotação")]
    public float yaw = 45f;
    public float pitch = 35f;
    public float minPitch = 10f;
    public float maxPitch = 80f;
    public float orbitSensitivity = 0.22f;

    [Header("Suavização")]
    public float followSmoothTime = 0.08f;

    [Header("Colisão")]
    [Tooltip("Impede a câmera de atravessar objetos com colisor (árvores, terreno).")]
    public bool avoidClipping = true;
    public float collisionRadius = 0.35f;

    private Vector3 _followVelocity;
    private Vector3 _currentPivot;
    private float _currentDistance;

    private void Start()
    {
        if (target != null) _currentPivot = target.position + Vector3.up * targetHeight;
        _currentDistance = distance;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleOrbitInput();
        HandleZoomInput();

        Vector3 desiredPivot = target.position + Vector3.up * targetHeight;
        _currentPivot = Vector3.SmoothDamp(_currentPivot, desiredPivot, ref _followVelocity, followSmoothTime);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = rotation * Vector3.back;

        float targetDistance = distance;
        if (avoidClipping &&
            Physics.SphereCast(_currentPivot, collisionRadius, direction, out RaycastHit hit, distance))
        {
            targetDistance = Mathf.Max(minDistance * 0.5f, hit.distance);
        }

        // Aproxima na hora (para não atravessar), mas afasta suavemente.
        _currentDistance = targetDistance < _currentDistance
            ? targetDistance
            : Mathf.Lerp(_currentDistance, targetDistance, Time.deltaTime * 4f);

        transform.position = _currentPivot + direction * _currentDistance;
        transform.rotation = rotation;
    }

    private void HandleOrbitInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (!mouse.rightButton.isPressed && !mouse.middleButton.isPressed) return;

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * orbitSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, minPitch, maxPitch);
    }

    private void HandleZoomInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomSpeed, minDistance, maxDistance);
    }
}
