using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed class PassLootContainer : MonoBehaviour
    {
        public Transform movingPart;
        public Vector3 openOffset;
        public Vector3 openEuler;
        public bool locked;
        public string labelKey = "search.case";
        public bool Unlocked { get; private set; }
        public bool TargetOpen { get; private set; }
        public float Openness { get; private set; }
        Vector3 closedPosition;
        Quaternion closedRotation;

        void Awake()
        {
            if(movingPart)
            {
                closedPosition=movingPart.localPosition;
                closedRotation=movingPart.localRotation;
            }
            Unlocked=!locked;
        }

        internal void Open()
        {
            Unlocked=true;
            TargetOpen=true;
        }

        internal void Close() => TargetOpen=false;

        internal void Restore(bool unlocked)
        {
            Unlocked=unlocked || !locked;
            TargetOpen=false;
            SetPose(0);
        }

        void Update()
        {
            float next=Mathf.MoveTowards(Openness,TargetOpen ? 1 : 0,Time.deltaTime*3.25f);
            if(next!=Openness) SetPose(next);
        }

        void SetPose(float amount)
        {
            Openness=amount;
            if(!movingPart) return;
            float eased=Mathf.SmoothStep(0,1,amount);
            movingPart.SetLocalPositionAndRotation(closedPosition+openOffset*eased,
                closedRotation*Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(openEuler),eased));
        }
    }
}
