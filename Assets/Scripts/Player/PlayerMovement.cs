using UnityEngine;
using UnityEngine.InputSystem;

/// Movimento em terceira pessoa relativo à câmera: W anda para onde a câmera olha.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Velocidade (m/s)")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 7f;

    [Header("Giro")]
    [Tooltip("Tempo aproximado, em segundos, para o personagem virar na direção do movimento.")]
    public float turnSmoothTime = 0.12f;

    [Header("Física")]
    public float gravity = -20f;

    private CharacterController _controller;
    private Transform _camera;
    private Animator _animator;
    private float _turnVelocity;
    private float _verticalVelocity;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>();
        if (Camera.main != null) _camera = Camera.main.transform;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = ReadMoveInput(keyboard);
        bool running = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        Vector3 motion = Vector3.zero;

        if (input.sqrMagnitude > 0.01f)
        {
            // Direção desejada no plano do chão, a partir da orientação da câmera.
            float cameraYaw = _camera != null ? _camera.eulerAngles.y : 0f;
            float targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg + cameraYaw;

            float angle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetAngle, ref _turnVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 direction = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            motion = direction * (running ? runSpeed : walkSpeed);
        }

        // Mantém o personagem colado ao chão em vez de acumular queda indefinidamente.
        _verticalVelocity = _controller.isGrounded
            ? -2f
            : _verticalVelocity + gravity * Time.deltaTime;

        motion.y = _verticalVelocity;
        _controller.Move(motion * Time.deltaTime);

        if (_animator != null)
        {
            float horizontalSpeed = new Vector2(motion.x, motion.z).magnitude;
            _animator.SetFloat(SpeedParam, horizontalSpeed, 0.1f, Time.deltaTime);
        }
    }

    private static Vector2 ReadMoveInput(Keyboard keyboard)
    {
        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;

        return new Vector2(x, y).normalized;
    }
}
