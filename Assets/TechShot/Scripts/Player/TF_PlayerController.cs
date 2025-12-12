using UnityEngine;
using UnityEngine.InputSystem;

namespace ToxicFactory.TechShot.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class TF_PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float crouchSpeed = 2f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -25f;
        [SerializeField, Tooltip("Усилитель гравитации при падении для более естественного ускорения вниз")]
        private float fallMultiplier = 2.0f;
        [SerializeField, Tooltip("Максимальная скорость падения")]
        private float terminalVelocity = -60f;
        
        [Header("Camera")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float minPitch = -89f;
        [SerializeField] private float maxPitch = 89f;
        
        [Header("Crouch")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.0f;
        [SerializeField] private float crouchTransitionSpeed = 8f;
        
        [Header("Ground Check")]
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask groundMask = ~0;
        
        // Components
        private CharacterController _controller;
        private PlayerInput _playerInput;
        
        // Input Actions
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;
        
        // State
        private Vector3 _velocity;
        private float _cameraPitch;
        private bool _isGrounded;
        private bool _isSprinting;
        private bool _isCrouching;
        private float _targetHeight;
        private bool _isPaused;
        
        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _playerInput = GetComponent<PlayerInput>();
            
            if (cameraPivot == null)
                cameraPivot = transform.Find("CameraPivot");
            
            // Get input actions
            var playerMap = _playerInput.actions.FindActionMap("Player");
            _moveAction = playerMap.FindAction("Move");
            _lookAction = playerMap.FindAction("Look");
            _jumpAction = playerMap.FindAction("Jump");
            _sprintAction = playerMap.FindAction("Sprint");
            _crouchAction = playerMap.FindAction("Crouch");
            
            _targetHeight = standingHeight;
        }
        
        private void Start()
        {
            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        private void Update()
        {
            if (_isPaused)
            {
                return;
            }

            HandleGroundCheck();
            HandleMovement();
            HandleLook();
            HandleCrouch();
            ApplyGravity();
        }
        
        private void HandleGroundCheck()
        {
            _isGrounded = Physics.CheckSphere(
                transform.position + Vector3.up * 0.1f,
                groundCheckDistance,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
            
            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }
        }
        
        private void HandleMovement()
        {
            Vector2 input = _moveAction.ReadValue<Vector2>();
            _isSprinting = _sprintAction.IsPressed() && !_isCrouching;
            
            float currentSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? sprintSpeed : walkSpeed);
            
            Vector3 move = transform.right * input.x + transform.forward * input.y;
            _controller.Move(move * currentSpeed * Time.deltaTime);
            
            // Jump
            if (_jumpAction.WasPressedThisFrame() && _isGrounded && !_isCrouching)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        
        private void HandleLook()
        {
            Vector2 lookInput = _lookAction.ReadValue<Vector2>();
            
            // Horizontal rotation (player body)
            transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
            
            // Vertical rotation (camera pivot)
            _cameraPitch -= lookInput.y * mouseSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, minPitch, maxPitch);
            
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_cameraPitch, 0, 0);
            }
        }
        
        private void HandleCrouch()
        {
            if (_crouchAction.WasPressedThisFrame())
            {
                _isCrouching = !_isCrouching;
                _targetHeight = _isCrouching ? crouchingHeight : standingHeight;
            }
            
            // Smooth height transition
            float currentHeight = _controller.height;
            if (!Mathf.Approximately(currentHeight, _targetHeight))
            {
                float newHeight = Mathf.MoveTowards(currentHeight, _targetHeight, crouchTransitionSpeed * Time.deltaTime);
                _controller.height = newHeight;
                _controller.center = new Vector3(0, newHeight * 0.5f, 0);
                
                // Adjust camera pivot
                if (cameraPivot != null)
                {
                    cameraPivot.localPosition = new Vector3(0, newHeight - 0.2f, 0);
                }
            }
        }
        
        private void ApplyGravity()
        {
            float g = gravity;
            if (_velocity.y < 0f)
            {
                g *= fallMultiplier;
            }

            _velocity.y += g * Time.deltaTime;
            _velocity.y = Mathf.Max(_velocity.y, terminalVelocity);
            _controller.Move(_velocity * Time.deltaTime);
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        // Escape to unlock cursor
        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                _isPaused = !_isPaused;
                if (_isPaused)
                {
                    _velocity = Vector3.zero;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
    }
}
