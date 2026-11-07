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
        bool ownsCursor;
        // Optional hooks for a journey scene; the standalone carriage keeps its original controls.
        public System.Func<bool> useOverride;
        public bool automaticRespawn = true;
        public bool externalMotion;
        public float runningSpeed;
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
            ReadInput(out Vector2 walk, out Vector2 look, out bool click, out bool escape, out bool useDoor);

            if (escape || !Application.isFocused || overlayOpen)
                ReleaseCursor();
            else if (click && !ownsCursor)
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
                if (useDoor)
                    Interact();
            }
            else
                walk = Vector2.zero;

            if (externalMotion || !controller.enabled)
                return;
            float speed = walkingSpeed;
            if (runningSpeed > walkingSpeed && ownsCursor)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed) speed = runningSpeed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed = runningSpeed;
#endif
            }
            Vector3 planar = transform.right * walk.x + transform.forward * walk.y;
            fallSpeed = controller.isGrounded && fallSpeed <= 0f
                ? -1.2f : Mathf.Max(fallSpeed - 15f * dt, -25f);
            controller.Move((Vector3.ClampMagnitude(planar, 1f) * speed + Vector3.up * fallSpeed) * dt);
            float spawnHeight = transform.parent ? transform.parent.TransformPoint(spawnLocal).y : spawnLocal.y;
            if (automaticRespawn && transform.position.y < spawnHeight - 3.5f)
                Respawn();
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

        static void ReadInput(out Vector2 walk, out Vector2 look, out bool click, out bool escape, out bool useDoor)
        {
            walk = look = Vector2.zero;
            click = escape = useDoor = false;
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
            click = Input.GetMouseButtonDown(0);
            look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
        }
    }
}
