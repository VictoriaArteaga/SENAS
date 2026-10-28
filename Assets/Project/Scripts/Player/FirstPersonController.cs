using UnityEngine;
using UnityEngine.InputSystem;

namespace Lumina.Player
{
    /// <summary>
    /// Controlador básico en primera persona: WASD + ratón, gravedad y colisiones
    /// mediante CharacterController. Lee los dispositivos con el Input System.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Transform que rota en vertical (la cámara). Si está vacío se usa la primera Camera hija.")]
        [SerializeField] private Transform cameraPivot;

        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float gravity = -20f;

        [Header("Cámara")]
        [Tooltip("Grados por unidad de movimiento del ratón.")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float maxLookAngle = 80f;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;

        public bool CursorLocked { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
        }

        private void Start()
        {
            SetCursorLocked(true);
        }

        private void Update()
        {
            HandleCursor();

            if (CursorLocked) Look();
            Move();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SetCursorLocked(false);
        }

        private void HandleCursor()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (CursorLocked && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (!CursorLocked && mouse != null && mouse.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
        }

        private void Look()
        {
            var mouse = Mouse.current;
            if (mouse == null || cameraPivot == null) return;

            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;

            transform.Rotate(Vector3.up, delta.x, Space.Self);

            _pitch = Mathf.Clamp(_pitch - delta.y, -maxLookAngle, maxLookAngle);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move()
        {
            Vector2 input = Vector2.zero;
            bool sprint = false;

            var keyboard = Keyboard.current;
            if (CursorLocked && keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                sprint = keyboard.leftShiftKey.isPressed;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = horizontal + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        public void SetCursorLocked(bool locked)
        {
            CursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
