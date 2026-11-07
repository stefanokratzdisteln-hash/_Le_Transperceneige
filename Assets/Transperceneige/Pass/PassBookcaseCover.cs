using UnityEngine;

namespace Winterbourne.Pass
{
    // The front bookcase slides across the stationary rear gate.
    [DisallowMultipleComponent]
    public sealed class PassBookcaseCover : MonoBehaviour
    {
        public Transform leaf;
        public BoxCollider panel, frontPull, rearPull;
        public Vector3 closedOffset;
        // Overall bookcase footprint at both ends of travel, relative to the fixed gate.
        public Vector2 groundMinimum=new Vector2(-2.37f,-.46f), groundMaximum=new Vector2(.86f,.25f);
        [Min(.1f)] public float travelSeconds=1.12f;
        Vector3 openPosition;
        bool initialized;
        public float ClosedFraction { get; private set; }
        public bool TargetClosed { get; private set; }
        public bool AllowsPassage => !TargetClosed && ClosedFraction<=.035f;

        void Awake() => RememberOpenPose();

        void RememberOpenPose()
        {
            if(initialized || !leaf) return;
            openPosition=leaf.localPosition;
            initialized=true;
        }

        public void Restore(float fraction,bool closing)
        {
            RememberOpenPose();
            TargetClosed=closing;
            ClosedFraction=Mathf.Clamp01(fraction);
            if(leaf) leaf.localPosition=Pose(ClosedFraction);
        }

        public void Toggle() => TargetClosed=!TargetClosed;
        Vector3 Pose(float t) => openPosition+closedOffset*Mathf.SmoothStep(0,1,t);

        public bool InSight(Camera eye,float reach,BoxCollider threshold)
        {
            if(!isActiveAndEnabled || !eye || !panel) return false;
            var ray=new Ray(eye.transform.position,eye.transform.forward);
            if(Physics.Raycast(ray,out var hit,reach,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider==panel || hit.collider.transform.IsChildOf(leaf)) return true;
                reach=hit.distance+.006f;
            }
            return Hit(frontPull,ray,reach) || Hit(rearPull,ray,reach) || Hit(threshold,ray,reach);
        }

        static bool Hit(BoxCollider target,Ray ray,float reach)
            => target && target.enabled && target.Raycast(ray,out _,reach);

        bool Occupied(CharacterController traveller,Vector3 movement)
        {
            if(!traveller || !traveller.enabled) return false;
            var orientation=Quaternion.Inverse(panel.transform.rotation);
            var centre=panel.transform.TransformPoint(panel.center)+movement*.5f;
            Vector3 relative=orientation*(traveller.transform.TransformPoint(traveller.center)-centre);
            Vector3 shift=orientation*movement;
            var half=Vector3.Scale(panel.size,panel.transform.lossyScale)*.5f;
            half+=new Vector3(Mathf.Abs(shift.x),Mathf.Abs(shift.y),Mathf.Abs(shift.z))*.5f;
            float dx=Mathf.Max(0,Mathf.Abs(relative.x)-half.x);
            float dz=Mathf.Max(0,Mathf.Abs(relative.z)-half.z);
            float clearance=traveller.radius+.035f;
            return Mathf.Abs(relative.y)<half.y+traveller.height*.5f &&
                   dx*dx+dz*dz<clearance*clearance;
        }

        public void MoveCover(CharacterController traveller,float dt)
        {
            if(!leaf || !panel || !panel.enabled || dt<=0) return;
            float end=TargetClosed ? 1f : 0f;
            if(Mathf.Approximately(ClosedFraction,end)) return;
            float next=Mathf.MoveTowards(ClosedFraction,end,dt/travelSeconds);
            Vector3 local=Pose(next);
            Vector3 shift=leaf.parent.TransformVector(local-leaf.localPosition);
            if(Occupied(traveller,shift)) return;
            ClosedFraction=next;
            leaf.localPosition=local;
            Physics.SyncTransforms();
        }
    }
}
