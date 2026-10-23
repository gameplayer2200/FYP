using UnityEngine;

namespace PlayerControllers
{
    /// <summary>
    /// FPP/TPP controller with crouching and automatic placeholder model generation.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FPP_PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 3f;
        public float runSpeed = 6f;
        public float crouchSpeed = 1.5f;
        public float gravity = -12f;

        [Header("Jump")]
        public bool canJump = false;
        public float jumpHeight = 1.2f;

        [Header("Crouch")]
        public float crouchHeight = 1f;
        public KeyCode crouchKey = KeyCode.LeftControl;
        public float crouchTransitionSpeed = 10f;

        [Header("View (Camera)")]
        public Transform cameraTransform;
        public float mouseSensitivity = 2f;
        public float maxLookAngle = 80f;
        public bool invertMouseY = false;

        [Header("Third Person Mode (TPP)")]
        public bool isThirdPerson = false;
        public Vector3 tppOffset = new Vector3(0f, 1.8f, -3.0f);
        public KeyCode viewToggleKey = KeyCode.V;

        [Header("Character Model")]
        public GameObject characterModel;
        public bool hideModelInFPP = true;

        [Header("Head Bobbing (FPP only)")]
        public bool headBobEnabled = true;
        public float bobFrequency = 1.8f;
        public float bobAmplitude = 0.04f;

        CharacterController _cc;
        float _verticalVelocity;
        float _cameraPitch;
        float _bobTimer;
        Vector3 _cameraLocalOrigin;
        float _defaultHeight;
        bool _isCrouching;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _defaultHeight = _cc.height;

            if (cameraTransform != null) _cameraLocalOrigin = cameraTransform.localPosition;
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SetupModel();
        }

        void SetupModel()
        {
            if (characterModel == null)
            {
                characterModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                characterModel.name = "Character_Placeholder";
                characterModel.transform.SetParent(this.transform);
                characterModel.transform.localScale = new Vector3(0.4f, _defaultHeight, 0.2f);
                characterModel.transform.localPosition = new Vector3(0f, _defaultHeight / 2f, 0f);

                Destroy(characterModel.GetComponent<BoxCollider>());
                
                Renderer rend = characterModel.GetComponent<Renderer>();
                rend.material = new Material(Shader.Find("Standard"));
                rend.material.color = Color.gray;
            }
        }

        void Update()
        {
            HandleCursorLock();
            HandleViewToggle();
            HandleCrouch();
            HandleMouseLook();
            HandleMovement();
            UpdateCameraView();
        }

        void HandleViewToggle()
        {
            if (Input.GetKeyDown(viewToggleKey))
            {
                isThirdPerson = !isThirdPerson;
            }
        }

        void HandleCrouch()
        {
            if (Input.GetKeyDown(crouchKey)) _isCrouching = true;
            if (Input.GetKeyUp(crouchKey)) _isCrouching = false;

            float targetHeight = _isCrouching ? crouchHeight : _defaultHeight;
            _cc.height = Mathf.Lerp(_cc.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            _cc.center = new Vector3(0f, _cc.height / 2f, 0f);

            // Adjust camera origin based on current height
            _cameraLocalOrigin.y = _cc.height - 0.2f;

            if (characterModel != null)
            {
                characterModel.transform.localScale = new Vector3(0.4f, _cc.height, 0.2f);
                characterModel.transform.localPosition = new Vector3(0f, _cc.height / 2f, 0f);
            }
        }

        void HandleCursorLock()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Cursor.lockState != CursorLockMode.Locked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertMouseY ? 1f : -1f);

            transform.Rotate(Vector3.up, mouseX);
            _cameraPitch = Mathf.Clamp(_cameraPitch + mouseY, -maxLookAngle, maxLookAngle);

            if (cameraTransform != null)
                cameraTransform.localEulerAngles = new Vector3(_cameraPitch, 0f, 0f);
        }

        void HandleMovement()
        {
            bool isGrounded = _cc.isGrounded;
            if (isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            float speed = walkSpeed;
            if (_isCrouching) speed = crouchSpeed;
            else if (Input.GetKey(KeyCode.LeftShift)) speed = runSpeed;

            Vector3 move = transform.right * h + transform.forward * v;
            if (move.magnitude > 1f) move.Normalize();
            move *= speed;

            if (canJump && Input.GetButtonDown("Jump") && isGrounded && !_isCrouching)
                _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);

            _verticalVelocity += gravity * Time.deltaTime;
            move.y = _verticalVelocity;

            _cc.Move(move * Time.deltaTime);
        }

        void UpdateCameraView()
        {
            if (cameraTransform == null) return;

            if (characterModel != null && hideModelInFPP)
            {
                characterModel.SetActive(isThirdPerson);
            }

            if (isThirdPerson)
            {
                cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, tppOffset, Time.deltaTime * crouchTransitionSpeed);
            }
            else
            {
                if (headBobEnabled) HandleHeadBob();
                else cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, _cameraLocalOrigin, Time.deltaTime * crouchTransitionSpeed);
            }
        }

        void HandleHeadBob()
        {
            bool isMoving = _cc.velocity.magnitude > 0.2f && _cc.isGrounded;

            if (isMoving)
            {
                _bobTimer += Time.deltaTime * bobFrequency;
                float bobOffset = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * bobAmplitude;
                cameraTransform.localPosition = _cameraLocalOrigin + Vector3.up * bobOffset;
            }
            else
            {
                _bobTimer = 0f;
                cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, _cameraLocalOrigin, Time.deltaTime * 8f);
            }
        }
    }
}