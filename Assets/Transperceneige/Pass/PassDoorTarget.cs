using UnityEngine;
using Winterbourne.Demo;

namespace Winterbourne.Pass
{
    // A fixed jamb pull lets a passenger operate a leaf even when it is in its pocket.
    public sealed class PassDoorTarget : MonoBehaviour
    {
        public PassDoor hinge;
        public Car07SlidingDoor slider;

        // Prompt and use share one sightline. The grip layer is excluded from normal item raycasts.
        public static bool Resolve(Camera eye, float reach, out PassDoor hinge, out Car07SlidingDoor slider)
        {
            hinge = null;
            slider = null;
            if (!eye) return false;
            var ray = new Ray(eye.transform.position, eye.transform.forward);
            bool hitPanel = Physics.Raycast(ray, out var panel, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float gripDistance = hitPanel ? panel.distance + .002f : reach;
            if (Physics.Raycast(ray, out var grip, gripDistance, 1 << 2, QueryTriggerInteraction.Collide) &&
                grip.collider.TryGetComponent(out PassDoorTarget target))
            {
                hinge = target.hinge;
                slider = target.slider;
                return hinge || slider;
            }
            if (!hitPanel) return false;
            hinge = panel.collider.GetComponentInParent<PassDoor>();
            slider = panel.collider.GetComponentInParent<Car07SlidingDoor>();
            return hinge || slider;
        }
    }
}
