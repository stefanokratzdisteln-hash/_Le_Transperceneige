using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed class PassPortal : MonoBehaviour
    {
        public enum Kind { Board, Station, Return }
        public Kind destination;
        public PassJourney journey;
        public TextMesh plaque;
        [Min(0f)] public float rearPlaqueDepth=.281f;
        public BoxCollider threshold;
        public PassBookcaseCover cover;
        Vector3 frontPosition;
        Quaternion frontRotation;
        bool reverse;
        Light passageLight;
        float litIntensity;

        void Awake()
        {
            Transform lamp=transform.Find("Light from the hidden passage");
            if(lamp)
            {
                passageLight=lamp.GetComponent<Light>();
                if(passageLight) litIntensity=passageLight.intensity;
            }
            if (!plaque) return;
            frontPosition=plaque.transform.localPosition;
            frontRotation=plaque.transform.localRotation;
        }

        void OnEnable()
        {
            PassText.Changed += RefreshLabel;
            RefreshLabel();
        }

        void OnDisable() => PassText.Changed -= RefreshLabel;

        void Update()
        {
            if(cover && journey && !journey.InStartMenu && !journey.Terminal)
            {
                cover.MoveCover(journey.PlayerBody,Time.deltaTime);
                if(passageLight)
                    passageLight.intensity=litIntensity*Mathf.SmoothStep(0,1,1-cover.ClosedFraction);
            }
        }

        void LateUpdate()
        {
            if (!plaque || !journey || !journey.walker || !journey.walker.eyeCamera) return;
            float side=transform.InverseTransformPoint(journey.walker.eyeCamera.transform.position).z;
            if (Mathf.Abs(side)<.05f || (side>0)==reverse) return;
            reverse=side>0;
            var position=frontPosition;
            if (reverse) position.z=rearPlaqueDepth;
            plaque.transform.SetLocalPositionAndRotation(position,
                reverse ? Quaternion.Euler(0,180,0)*frontRotation : frontRotation);
        }

        public void RefreshLabel()
        {
            if (!plaque) return;
            string key = destination == Kind.Board ? "portal.sleeping" : "portal.hut";
            if (destination == Kind.Board && journey)
            {
                if (journey.MissedTrainInHub) key = "portal.closed";
                else if (journey.VisitingStation) key = "portal.platform";
            }
            plaque.text = PassText.Get(key);
        }

        public bool Contains(CharacterController traveller)
        {
            if (!isActiveAndEnabled || !threshold || !threshold.enabled || !traveller || !traveller.enabled) return false;
            if (cover && !cover.AllowsPassage) return false;
            Vector3 middle = traveller.transform.TransformPoint(traveller.center);
            Vector3 delta = threshold.transform.InverseTransformPoint(middle) - threshold.center;
            Vector3 half = threshold.size * .5f;
            return Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y + traveller.height * .5f &&
                   Mathf.Abs(delta.z) <= half.z + traveller.radius;
        }
    }
}
