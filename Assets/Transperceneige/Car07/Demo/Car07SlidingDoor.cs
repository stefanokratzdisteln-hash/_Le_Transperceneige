using UnityEngine;

namespace Winterbourne.Demo
{
    [DisallowMultipleComponent]
    public sealed class Car07SlidingDoor : MonoBehaviour
    {
        [Tooltip("Travel from the modelled open pose to the closed pose, in parent-local coordinates.")]
        public Vector3 closedOffset;
        [Min(.1f)] public float travelSeconds = .85f;

        Vector3 openPosition;
        Collider[] leafColliders;
        CharacterController operatorController;
        float travel;
        bool closing;
        public float ClosedFraction => travel;
        public bool TargetClosed => closing;
        public void RestorePose(float closedFraction, bool close)
        {
            operatorController = null;
            travel = Mathf.Clamp01(closedFraction);
            closing = close;
            transform.localPosition = openPosition + closedOffset * Mathf.SmoothStep(0f, 1f, travel);
        }

        void Awake()
        {
            openPosition = transform.localPosition;
            leafColliders = GetComponentsInChildren<Collider>(true);
        }

        public void Toggle(CharacterController player)
        {
            if (!player)
                return;
            operatorController = player;
            closing = !closing;
        }

        void Update()
        {
            float target = closing ? 1f : 0f;
            if (travel == target)
                return;

            float next = Mathf.MoveTowards(travel, target, Time.deltaTime / Mathf.Max(.1f, travelSeconds));
            Vector3 local = openPosition + closedOffset * Mathf.SmoothStep(0f, 1f, next);
            Vector3 world = transform.parent ? transform.parent.TransformPoint(local) : local;
            if (closing && IsObstructed(world - transform.position))
            {
                closing = false;
                return;
            }
            travel = next;
            transform.localPosition = local;
        }

        bool IsObstructed(Vector3 step)
        {
            if (!operatorController || !operatorController.enabled)
                return false;
            Bounds player = operatorController.bounds;
            player.Expand(.025f);
            foreach (Collider leaf in leafColliders)
            {
                if (!leaf.enabled || !leaf.gameObject.activeInHierarchy || leaf.isTrigger)
                    continue;
                Bounds swept = leaf.bounds;
                swept.Encapsulate(swept.min + step);
                swept.Encapsulate(leaf.bounds.max + step);
                if (swept.Intersects(player))
                    return true;
            }
            return false;
        }
    }
}
