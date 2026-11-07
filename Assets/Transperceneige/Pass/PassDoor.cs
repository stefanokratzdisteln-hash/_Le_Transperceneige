using UnityEngine;

namespace Winterbourne.Pass
{
    public enum PassDoorKind { Passenger, Chase, Escape }

    /// <summary>Hinged rolling-stock leaf. The fixed threshold remains in the carriage frame.</summary>
    [DisallowMultipleComponent]
    public sealed class PassDoor : MonoBehaviour
    {
        public PassDoorKind kind;
        public Transform threshold;
        public Transform car;
        [SerializeField] Quaternion closedPose = Quaternion.identity;
        [SerializeField] Quaternion openPose = Quaternion.identity;
        [SerializeField] BoxCollider panel;
        [SerializeField, Min(.1f)] float travelTime = .62f;
        [SerializeField, Range(0,1)] float fraction;
        [SerializeField] bool targetOpen;
        CharacterController passenger;
        public bool IsOpen => fraction >= .97f;
        public bool TargetOpen => targetOpen;
        public float Openness => fraction;

        public void Configure(PassDoorKind usage, Transform body, Transform jamb,
                              Quaternion closed, Quaternion open, BoxCollider solid)
        {
            kind = usage;
            car = body;
            threshold = jamb;
            closedPose = closed;
            openPose = open;
            panel = solid;
            Restore(0, false);
        }

        void Start()
        {
            passenger = FindFirstObjectByType<CharacterController>();
        }

        public void SetOpen(bool open, bool immediate = false)
        {
            targetOpen = open;
            if (immediate) Restore(open ? 1 : 0, open);
        }

        public void Restore(float openness, bool opening)
        {
            fraction = Mathf.Clamp01(openness);
            targetOpen = opening;
            transform.localRotation = Rotation(fraction);
        }

        Quaternion Rotation(float t) => Quaternion.Slerp(closedPose,openPose,t*t*(3f-2f*t));

        public bool Occupied(CharacterController controller)
        {
            if (!controller || !controller.enabled || !threshold) return false;
            Vector3 centre = threshold.InverseTransformPoint(controller.transform.TransformPoint(controller.center));
            return Mathf.Abs(centre.x) < controller.radius + .13f &&
                   Mathf.Abs(centre.z) < controller.radius + .51f &&
                   Mathf.Abs(centre.y) < (controller.height * .5f + 1.05f);
        }

        public bool NeedsDepartureClearance(CharacterController controller)
        {
            if(Occupied(controller)) return true;
            if(fraction<.001f) return false;
            for(int i=0;i<=10;i++)
                if(SweepsPassenger(Rotation(fraction*i/10f),controller)) return true;
            return false;
        }

        // Test the proposed pose before moving. Blocking the approaching leaf also protects
        // the landing in front of the hinge, outside the narrow doorway occupancy rectangle.
        bool SweepsPassenger(Quaternion next,CharacterController occupant)
        {
            if (!occupant || !occupant.enabled || !panel) return false;
            Quaternion world = transform.parent ? transform.parent.rotation * next : next;
            Quaternion delta = world * Quaternion.Inverse(transform.rotation);
            Vector3 boxCentre = transform.position + delta * (panel.transform.TransformPoint(panel.center) - transform.position);
            Vector3 person = occupant.transform.TransformPoint(occupant.center);
            Vector3 q = Quaternion.Inverse(delta * panel.transform.rotation) * (person-boxCentre);
            Vector3 half = Vector3.Scale(panel.size,panel.transform.lossyScale) * .5f;
            float dx = Mathf.Max(0,Mathf.Abs(q.x)-half.x);
            float dz = Mathf.Max(0,Mathf.Abs(q.z)-half.z);
            return Mathf.Abs(q.y)<half.y + occupant.height/2 &&
                   dx*dx+dz*dz < Mathf.Pow(occupant.radius+.015f,2);
        }

        void Update()
        {
            float target = targetOpen ? 1 : 0;
            if (Mathf.Approximately(fraction,target)) return;
            if (!targetOpen && Occupied(passenger)) return;
            float proposed = Mathf.MoveTowards(fraction,target,Time.deltaTime/travelTime);
            Quaternion next = Rotation(proposed);
            if (SweepsPassenger(next,passenger)) return;
            fraction=proposed;
            transform.localRotation=next;
        }
    }
}
