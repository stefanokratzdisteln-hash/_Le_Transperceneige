using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Winterbourne.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-20)]
    public sealed class Car07Walker : MonoBehaviour
    {
        public Camera eyeCamera;
        [Min(0.1f)] public float walkingSpeed = 1.8f;
        [Min(0.01f)] public float lookSensitivity = 0.10f;
        [Min(0.1f)] public float interactionDistance = 2f;

        CharacterController controller;
        Vector3 spawnLocal;
        Quaternion spawnRotation;
        float yaw, pitch, targetYaw, targetPitch, fallSpeed;
        Vector3 planarVelocity, inheritedVelocity;
        float groundGrace, jumpBuffer;
        const float Gravity = 16f, JumpHeight = .68f;
        bool ownsCursor;
        // Optional hooks for a journey scene; the standalone carriage keeps its original controls.
        public System.Func<bool> useOverride;
        public System.Func<bool> pointerOverControl;
        public System.Func<bool> jumpOverride;
        public bool automaticRespawn = true;
        public bool externalMotion;
        public float runningSpeed;
        public float JumpAge { get; private set; } = float.PositiveInfinity;
        public Vector3 MoveIntent { get; private set; }
        public Vector3 PlanarVelocity => planarVelocity + inheritedVelocity;
        public bool HasMouse => ownsCursor;
        public float ViewPitch => pitch;
        bool overlayOpen;

        public void Overlay(bool open)
        {
            overlayOpen = open;
            if (open) ReleaseCursor();
        }

        public void Place(Transform support, Vector3 localPosition, Quaternion localRotation, float eyePitch)
        {
            controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetParent(support, false);
            transform.SetLocalPositionAndRotation(localPosition, localRotation);
            yaw = targetYaw = localRotation.eulerAngles.y;
            pitch = targetPitch = eyePitch;
            fallSpeed = 0;
            ClearMovement();
            if (eyeCamera) eyeCamera.transform.localRotation = Quaternion.Euler(eyePitch, 0, 0);
            controller.enabled = true;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (!eyeCamera)
                eyeCamera = GetComponentInChildren<Camera>(true);
            spawnLocal = transform.localPosition;
            spawnRotation = transform.localRotation;
            yaw = targetYaw = transform.localEulerAngles.y;
        }

        void Update()
        {
            ReadInput(out Vector2 walk, out Vector2 look, out bool click, out bool escape, out bool useDoor,
                      out bool sprint, out bool jump);

            if (escape || !Application.isFocused || overlayOpen)
                ReleaseCursor();
            else if (click && !ownsCursor && !(pointerOverControl?.Invoke() ?? false))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                ownsCursor = true;
                look = Vector2.zero;
            }

            if (ownsCursor && Cursor.lockState != CursorLockMode.Locked)
                ReleaseCursor();

            float dt = Time.deltaTime;
            if (ownsCursor)
            {
                targetYaw = Mathf.Repeat(targetYaw + look.x * lookSensitivity, 360f);
                targetPitch = Mathf.Clamp(targetPitch - look.y * lookSensitivity, -82f, 82f);
                float blend = 1f - Mathf.Exp(-27f * dt);
                yaw = Mathf.LerpAngle(yaw, targetYaw, blend);
                pitch = Mathf.Lerp(pitch, targetPitch, blend);
                transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (eyeCamera)
                    eyeCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                if (useDoor && !externalMotion)
                    Interact();
            }
            else
                walk = Vector2.zero;

            MoveCharacter(walk, ownsCursor && sprint, dt, ownsCursor && jump);
            float spawnHeight = transform.parent ? transform.parent.TransformPoint(spawnLocal).y : spawnLocal.y;
            if (automaticRespawn && transform.position.y < spawnHeight - 3.5f)
                Respawn();
        }

        // Both input backends and the fixed-step route walkthrough drive the same capsule.
        public void MoveCharacter(Vector2 axes, bool sprint, float dt, bool jump = false)
        {
            if (externalMotion || !controller.enabled || dt <= 0) return;
            MoveIntent = Vector3.ClampMagnitude(transform.right * axes.x + transform.forward * axes.y, 1f);
            bool grounded = controller.isGrounded && fallSpeed <= 0;
            if (grounded) groundGrace = .10f;
            else groundGrace = Mathf.Max(0, groundGrace - dt);
            if (jump)
            {
                if (jumpOverride != null) jumpOverride();
                else TryGroundJump();
            }
            // A contextual jump may just have switched to a scripted landing or flight.
            if (externalMotion || !controller.enabled) return;
            if (jumpBuffer > 0 && grounded && fallSpeed <= 0) Launch();
            jumpBuffer = Mathf.Max(0, jumpBuffer - dt);
            float speed = sprint ? Mathf.Max(walkingSpeed, runningSpeed) : walkingSpeed;
            bool buildingSprint = sprint && speed > walkingSpeed && planarVelocity.magnitude >= walkingSpeed * .95f &&
                                  Vector3.Dot(planarVelocity.normalized, MoveIntent) > .86f;
            float acceleration = grounded ? (buildingSprint ? 6.67f : 16f) : 6f;
            planarVelocity = Vector3.MoveTowards(planarVelocity, MoveIntent * speed, acceleration * dt);
            if (grounded)
                inheritedVelocity = Vector3.MoveTowards(inheritedVelocity, Vector3.zero, 20f * dt);
            float next = grounded && fallSpeed <= 0 ? -1.2f : Mathf.Max(fallSpeed - Gravity * dt, -30f);
            float vertical = (next + fallSpeed) * .5f;
            fallSpeed = next;
            var collision = controller.Move((PlanarVelocity + Vector3.up * vertical) * dt);
            if ((collision & CollisionFlags.Above) != 0) fallSpeed = Mathf.Min(fallSpeed, 0f);
            if ((collision & CollisionFlags.Below) != 0 && fallSpeed <= 0)
            {
                fallSpeed = -1.2f;
                if (JumpAge > .1f) JumpAge = float.PositiveInfinity;
            }
            JumpAge += dt;
        }

        public bool TryGroundJump()
        {
            if (externalMotion || !controller.enabled || overlayOpen) return false;
            if (fallSpeed > 0 || (!controller.isGrounded && groundGrace <= 0))
            {
                jumpBuffer = .11f;
                return false;
            }
            Launch();
            return true;
        }

        void Launch()
        {
            fallSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
            JumpAge = 0;
            groundGrace = jumpBuffer = 0;
        }

        public void LeaveMovingSupport(Vector3 velocity) => inheritedVelocity = velocity;

        void ClearMovement()
        {
            planarVelocity = inheritedVelocity = MoveIntent = Vector3.zero;
            groundGrace = jumpBuffer = 0;
            JumpAge = float.PositiveInfinity;
        }

        void Interact()
        {
            if (useOverride != null && useOverride()) return;
            if (!eyeCamera)
                return;
            Transform eye = eyeCamera.transform;
            if (Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, interactionDistance,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var door = hit.collider.GetComponentInParent<Car07SlidingDoor>();
                if (door)
                    door.Toggle(controller);
            }
        }

        public void Respawn()
        {
            controller.enabled = false;
            transform.localPosition = spawnLocal;
            transform.localRotation = spawnRotation;
            controller.enabled = true;
            yaw = targetYaw = spawnRotation.eulerAngles.y;
            pitch = targetPitch = fallSpeed = 0f;
            ClearMovement();
            if (eyeCamera)
                eyeCamera.transform.localRotation = Quaternion.identity;
        }

        void ReleaseCursor()
        {
            if (!ownsCursor)
                return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ownsCursor = false;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                ReleaseCursor();
        }

        void OnDisable() => ReleaseCursor();

        static void ReadInput(out Vector2 walk, out Vector2 look, out bool click, out bool escape, out bool useDoor,
                              out bool sprint, out bool jump)
        {
            walk = look = Vector2.zero;
            click = escape = useDoor = sprint = jump = false;
#if ENABLE_INPUT_SYSTEM
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) walk.y += 1;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) walk.y -= 1;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) walk.x += 1;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) walk.x -= 1;
                escape = keys.escapeKey.wasPressedThisFrame;
                useDoor = keys.eKey.wasPressedThisFrame;
                sprint = keys.shiftKey.isPressed;
                jump = keys.spaceKey.wasPressedThisFrame;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                click = mouse.leftButton.wasPressedThisFrame;
                look = mouse.delta.ReadValue();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) walk.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) walk.y -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) walk.x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) walk.x -= 1;
            escape = Input.GetKeyDown(KeyCode.Escape);
            useDoor = Input.GetKeyDown(KeyCode.E);
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            jump = Input.GetKeyDown(KeyCode.Space);
            click = Input.GetMouseButtonDown(0);
            look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
        }
    }
}
