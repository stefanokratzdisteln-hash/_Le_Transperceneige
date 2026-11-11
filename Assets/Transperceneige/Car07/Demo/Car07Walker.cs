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
        const float Gravity = 16f, JumpHeight = 1.08f, CrouchingHeight = 1f;
        readonly Collider[] overheadHits = new Collider[16];
        Vector3 standingCenter;
        float standingEyeHeight;
        bool tuckedForJump, eyeChangingHeight;
        public float StandingHeight { get; private set; }
        public bool IsCrouched { get; private set; }
        public bool CrouchSelected { get; private set; }
        bool ownsCursor;
        // Optional hooks for a journey scene; the standalone carriage keeps its original controls.
        public System.Func<bool> useOverride;
        public System.Func<bool> pointerOverControl;
        public System.Func<bool> jumpOverride;
        public System.Func<bool> canStandOverride;
        public System.Func<bool> canCrouchDuringExternalMotion;
        public System.Action<Vector2,bool> externalInput;
        public System.Action<float> groundImpact;
        public bool automaticRespawn = true;
        public bool externalMotion;
        public float runningSpeed;
        public float JumpAge { get; private set; } = float.PositiveInfinity;
        public Vector3 MoveIntent { get; private set; }
        public Vector3 PlanarVelocity => planarVelocity + inheritedVelocity;
        public float VerticalSpeed => fallSpeed;
        public bool HasMouse => ownsCursor;
        public System.Func<Vector2,bool> consumeOrbitLook;
        public float ViewPitch => pitch;
        bool overlayOpen;
        int resumeFrame = -1;
        Transform chestFrame;
        Vector3 chestLookPoint;
        Vector2 chestLookOffset;
        float chestFov, ordinaryFov;
        bool restoreFov, stanceBeforeChest;

        public void BeginChestInspection(Transform relativeTo, Vector3 localFocus, float targetFov)
        {
            if (!relativeTo || !eyeCamera || !controller.enabled) return;
            if (!chestFrame)
            {
                stanceBeforeChest = CrouchSelected;
                if (!restoreFov) ordinaryFov = eyeCamera.fieldOfView;
            }
            restoreFov = false;
            chestFrame = relativeTo;
            chestLookPoint = localFocus;
            chestLookOffset = Vector2.zero;
            chestFov = Mathf.Clamp(targetFov, 28f, Mathf.Max(ordinaryFov,84f));
            yaw = transform.localEulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, eyeCamera.transform.localEulerAngles.x);
            SetCrouched(true);
            eyeChangingHeight = true;
        }

        public void EndChestInspection()
        {
            if (!chestFrame) return;
            chestFrame = null;
            restoreFov = true;
            // A shelf or low ceiling can keep the passenger crouched until clear.
            SetCrouched(stanceBeforeChest);
            eyeChangingHeight = true;
            targetYaw = yaw;
            targetPitch = pitch;
        }

        public void SetChestInspectionFocus(Vector3 localFocus) => chestLookPoint = localFocus;

        void UpdateChestView(float dt)
        {
            if (!eyeCamera || (!chestFrame && !restoreFov)) return;
            float blend = 1f - Mathf.Exp(-13f * dt);
            float field = chestFrame ? chestFov : ordinaryFov;
            eyeCamera.fieldOfView = Mathf.Lerp(eyeCamera.fieldOfView, field, blend);
            if (!chestFrame)
            {
                if (Mathf.Abs(eyeCamera.fieldOfView - field) < .05f)
                {
                    eyeCamera.fieldOfView = field;
                    restoreFov = false;
                }
                return;
            }
            Vector3 aim = chestFrame.TransformPoint(chestLookPoint) - eyeCamera.transform.position;
            if (transform.parent) aim = transform.parent.InverseTransformDirection(aim);
            if (aim.sqrMagnitude < .0001f) return;
            targetYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg + chestLookOffset.x;
            float slope = -Mathf.Asin(Mathf.Clamp(aim.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(slope + chestLookOffset.y, -82f, 82f);
            yaw = Mathf.LerpAngle(yaw, targetYaw, blend);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            eyeCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void ResumeLook()
        {
            if (!Application.isFocused || overlayOpen) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ownsCursor = true;
            resumeFrame = Time.frameCount;
        }

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
            StandingHeight = controller.height;
            standingCenter = controller.center;
            if (eyeCamera) standingEyeHeight = eyeCamera.transform.localPosition.y;
            spawnLocal = transform.localPosition;
            spawnRotation = transform.localRotation;
            yaw = targetYaw = transform.localEulerAngles.y;
        }

        void Update()
        {
            ReadInput(out Vector2 walk, out Vector2 look, out bool click, out bool escape, out bool useDoor,
                      out bool sprint, out bool jump, out bool crouch);

            if ((escape && Time.frameCount != resumeFrame) || !Application.isFocused || overlayOpen)
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
                if(consumeOrbitLook!=null && consumeOrbitLook(look)) look=Vector2.zero;
                if (chestFrame)
                    chestLookOffset += new Vector2(look.x, -look.y) * lookSensitivity;
                else
                {
                    targetYaw = Mathf.Repeat(targetYaw + look.x * lookSensitivity, 360f);
                    targetPitch = Mathf.Clamp(targetPitch - look.y * lookSensitivity, -82f, 82f);
                    float blend = 1f - Mathf.Exp(-27f * dt);
                    yaw = Mathf.LerpAngle(yaw, targetYaw, blend);
                    pitch = Mathf.Lerp(pitch, targetPitch, blend);
                    transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    if (eyeCamera)
                        eyeCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }
                if (useDoor && !externalMotion)
                    Interact();
            }
            else
                walk = Vector2.zero;

            MoveCharacter(walk, ownsCursor && sprint, dt, ownsCursor && jump, ownsCursor && crouch);
            float spawnHeight = transform.parent ? transform.parent.TransformPoint(spawnLocal).y : spawnLocal.y;
            if (automaticRespawn && transform.position.y < spawnHeight - 3.5f)
                Respawn();
        }

        // Both input backends and the fixed-step route walkthrough drive the same capsule.
        public void MoveCharacter(Vector2 axes, bool sprint, float dt, bool jump = false, bool toggleCrouch = false)
        {
            if (dt <= 0) return;
            if (externalMotion)
            {
                if (toggleCrouch && !overlayOpen && (canCrouchDuringExternalMotion?.Invoke() ?? false)) ToggleCrouch();
                UpdateEyeHeight(dt);
                UpdateChestView(dt);
                externalInput?.Invoke(axes,jump);
                return;
            }
            if (!controller.enabled) return;
            MoveIntent = Vector3.ClampMagnitude(transform.right * axes.x + transform.forward * axes.y, 1f);
            bool grounded = controller.isGrounded && fallSpeed <= 0;
            if (toggleCrouch && !overlayOpen) ToggleCrouch();
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
            if (CrouchSelected) speed = walkingSpeed * .7f;
            bool buildingSprint = sprint && speed > walkingSpeed && planarVelocity.magnitude >= walkingSpeed * .95f &&
                                  Vector3.Dot(planarVelocity.normalized, MoveIntent) > .86f;
            float acceleration = grounded ? (buildingSprint ? 6.67f : 16f) : 6f;
            planarVelocity = Vector3.MoveTowards(planarVelocity, MoveIntent * speed, acceleration * dt);
            if (grounded)
                inheritedVelocity = Vector3.MoveTowards(inheritedVelocity, Vector3.zero, 20f * dt);
            float next = grounded && fallSpeed <= 0 ? -1.2f : Mathf.Max(fallSpeed - Gravity * dt, -30f);
            float priorSpeed=fallSpeed, priorHeight=transform.position.y;
            float vertical = (next + fallSpeed) * .5f;
            fallSpeed = next;
            var collision = controller.Move((PlanarVelocity + Vector3.up * vertical) * dt);
            if ((collision & CollisionFlags.Above) != 0) fallSpeed = Mathf.Min(fallSpeed, 0f);
            if ((collision & CollisionFlags.Below) != 0 && fallSpeed <= 0)
            {
                // The floor may stop us part way through this step. Base damage on
                // the speed at that height rather than after the rest of the frame.
                float drop=Mathf.Max(0,priorHeight-transform.position.y);
                float impactSpeed=Mathf.Min(-fallSpeed,Mathf.Sqrt(priorSpeed*priorSpeed+2*Gravity*drop));
                fallSpeed = -1.2f;
                if (JumpAge > .1f) JumpAge = float.PositiveInfinity;
                if(!grounded) groundImpact?.Invoke(impactSpeed);
            }
            if (tuckedForJump && fallSpeed <= 0 && CanStand())
            {
                tuckedForJump = false;
                ApplyStance(false);
            }
            UpdateEyeHeight(dt);
            UpdateChestView(dt);
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
            // Inside the saloon there is room for the jumper's knees, but not a
            // full standing capsule between a dining table and the roof lining.
            if (!IsCrouched && OverheadBlocked(JumpHeight))
            {
                tuckedForJump = true;
                ApplyStance(true);
            }
            fallSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
            JumpAge = 0;
            groundGrace = jumpBuffer = 0;
        }

        public bool ToggleCrouch() => SetCrouched(!IsCrouched);

        public bool SetCrouched(bool crouched)
        {
            if (!crouched && IsCrouched && !CanStand()) return false;
            tuckedForJump = false;
            CrouchSelected = crouched;
            ApplyStance(crouched);
            return true;
        }

        bool CanStand() => !OverheadBlocked(StandingHeight - controller.height) && (canStandOverride?.Invoke() ?? true);

        bool OverheadBlocked(float additionalHeight)
        {
            if (additionalHeight <= 0) return false;
            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            Vector3 top = transform.TransformPoint(controller.center + Vector3.up * (controller.height * .5f));
            Vector3 start = top - transform.up * radius;
            int count = Physics.OverlapCapsuleNonAlloc(start, start + transform.up * (additionalHeight * Mathf.Abs(scale.y)),
                Mathf.Max(.01f, radius - .012f), overheadHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (overheadHits[i] != controller && !overheadHits[i].transform.IsChildOf(transform)) return true;
            return count == overheadHits.Length;
        }

        void ApplyStance(bool crouched)
        {
            if (IsCrouched == crouched) return;
            float height = crouched ? Mathf.Clamp(CrouchingHeight, 2f * controller.radius, StandingHeight) : StandingHeight;
            controller.height = height;
            controller.center = standingCenter - Vector3.up * ((StandingHeight - height) * .5f);
            IsCrouched = crouched;
            eyeChangingHeight = true;
        }

        void UpdateEyeHeight(float dt)
        {
            if (!eyeChangingHeight || !eyeCamera) return;
            Vector3 eye = eyeCamera.transform.localPosition;
            float target = standingEyeHeight - (StandingHeight - controller.height);
            if (chestFrame && IsCrouched)
                target = Mathf.Max(controller.radius + .08f, target - .20f);
            eye.y = Mathf.MoveTowards(eye.y, target, 5f * dt);
            eyeCamera.transform.localPosition = eye;
            eyeChangingHeight = eye.y != target;
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
            SetCrouched(false);
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
                              out bool sprint, out bool jump, out bool crouch)
        {
            walk = look = Vector2.zero;
            click = escape = useDoor = sprint = jump = crouch = false;
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
                crouch = keys.leftCtrlKey.wasPressedThisFrame || keys.rightCtrlKey.wasPressedThisFrame;
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
            crouch = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
            click = Input.GetMouseButtonDown(0);
            look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
        }
    }
}
