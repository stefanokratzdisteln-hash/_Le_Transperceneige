using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Winterbourne.Demo;

namespace Winterbourne.Pass
{
    // The walking camera remains the gameplay eye. The following camera frames
    // the point seen by that eye, so the crosshair, range and wall occlusion
    // agree when the player reaches for a door, parcel or ladder.
    [DefaultExecutionOrder(110)]
    public sealed class PassDetectivePlayer : MonoBehaviour
    {
        public const int BodyLayer=30;
        public Transform avatar;
        public Camera followingCamera;
        public PassJourney journey;
        Car07Walker walker;
        Camera eye;
        bool preferThird=true,orbitHeld,following;
        float orbitYaw,orbitPitch,boom;
        Transform previousSupport;
        Vector3 previousLocal;
        readonly RaycastHit[] obstructions=new RaycastHit[24];
        public bool ThirdPersonActive=>following;
        public bool ViewingCharacter=>following && Mathf.Abs(Mathf.DeltaAngle(0,orbitYaw))>8;
        public bool PrefersThirdPerson=>preferThird;
        public Camera DisplayCamera=>following?followingCamera:eye;
        public Transform Avatar=>avatar;

        void Awake()
        {
            walker=GetComponent<Car07Walker>();
            eye=walker.eyeCamera;
            eye.cullingMask&=~(1<<BodyLayer);
            followingCamera.cullingMask=eye.cullingMask|(1<<BodyLayer);
            ChooseCamera(false);
        }

        void OnEnable()
        {
            if(walker)walker.consumeOrbitLook=OrbitLook;
        }

        bool OrbitLook(Vector2 delta)
        {
            if(!preferThird || ChestCloseup || journey.InventoryOpen || !RightButton())return false;
            orbitHeld=true;
            orbitYaw=Mathf.Repeat(orbitYaw+delta.x*walker.lookSensitivity*1.6f+180,360)-180;
            orbitPitch=Mathf.Clamp(orbitPitch-delta.y*walker.lookSensitivity,-40,48);
            return true;
        }

        bool ChestCloseup=>journey.WorkingChest || (journey.OpenContainer && journey.OpenContainer.TapChest);

        static bool RightButton()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        static bool ChangeView()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current!=null && Keyboard.current.vKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.V);
#else
            return false;
#endif
        }

        void ChooseCamera(bool third)
        {
            following=third;
            followingCamera.enabled=third;
            eye.enabled=!third;
        }

        float ClearDistance(Vector3 from,Vector3 direction,float requested)
        {
            int count=Physics.SphereCastNonAlloc(from,.135f,direction,obstructions,requested,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float limit=requested;
            for(int i=0;i<count;i++)
            {
                var hit=obstructions[i];
                if(!hit.transform.IsChildOf(transform))
                    limit=Mathf.Min(limit,Mathf.Max(0,hit.distance-.055f));
            }
            return limit;
        }

        void LateUpdate()
        {
            if(!walker || !journey || !eye || !followingCamera)return;
            if(Application.isFocused && !journey.InventoryOpen && !ChestCloseup && !journey.InStartMenu && ChangeView())
                preferThird=!preferThird;
            bool orbit=orbitHeld && RightButton();
            if(!orbit)
            {
                orbitYaw=Mathf.LerpAngle(orbitYaw,0,1-Mathf.Exp(-8*Time.deltaTime));
                orbitPitch=Mathf.Lerp(orbitPitch,0,1-Mathf.Exp(-8*Time.deltaTime));
            }
            orbitHeld=false;
            if(!preferThird || ChestCloseup) {ChooseCamera(false);boom=0;return;}

            var feet=transform.position;
            var center=feet+Vector3.up*(walker.IsCrouched?.76f:1.39f);
            var basis=Quaternion.Euler(Mathf.Clamp(walker.ViewPitch*.35f+orbitPitch,-40,52),
                transform.eulerAngles.y+orbitYaw,0);
            var offset=basis*new Vector3(.29f,.27f,-2.15f);
            float clear=ClearDistance(center,offset.normalized,offset.magnitude);
            bool clipped=clear<(following?.43f:.52f);
            bool transported=previousSupport!=transform.parent ||
                (previousLocal-transform.localPosition).sqrMagnitude>3f;
            previousSupport=transform.parent;
            previousLocal=transform.localPosition;
            if(clipped) { ChooseCamera(false);boom=clear;return; }
            boom=transported || boom<=0 ? clear : Mathf.Min(clear,Mathf.MoveTowards(boom,clear,Time.deltaTime*4f));
            var lens=center+offset.normalized*boom;
            var sight=eye.transform;
            var aim=sight.position+sight.forward*7;
            if(Physics.Raycast(sight.position,sight.forward,out var hit,7,Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide) && !hit.transform.IsChildOf(transform))
                aim=hit.point;
            float orbitWeight=orbit?1:Mathf.Clamp01(Mathf.Abs(orbitYaw)/28f);
            aim=Vector3.Lerp(aim,feet+Vector3.up*(walker.IsCrouched?.75f:1.08f),orbitWeight);
            var facing=aim-lens;
            if(facing.sqrMagnitude<.001f) facing=sight.forward;
            followingCamera.transform.SetPositionAndRotation(lens,Quaternion.LookRotation(facing,Vector3.up));
            followingCamera.fieldOfView=eye.fieldOfView;
            followingCamera.farClipPlane=eye.farClipPlane;
            followingCamera.clearFlags=eye.clearFlags;
            followingCamera.backgroundColor=eye.backgroundColor;
            ChooseCamera(true);
        }

        void OnDisable()
        {
            if(walker && walker.consumeOrbitLook==OrbitLook)walker.consumeOrbitLook=null;
            if(eye && followingCamera)ChooseCamera(false);
        }
    }
}
