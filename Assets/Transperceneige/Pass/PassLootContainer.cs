using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed class PassLootContainer : MonoBehaviour
    {
        public Transform movingPart;
        public Vector3 openOffset,openEuler;
        public bool locked;
        public string labelKey="search.case";
        [Tooltip("Zero for ordinary carriage drawers and existing fitted cupboards.")]
        public int tapsRequired;
        public Transform otherDoor,lockWork,leftHasp,leftGlove,rightGlove,leftSleeve,rightSleeve,leftUpper,rightUpper;
        public Transform[] leftDigits,rightDigits;
        public Vector3 braceLocal,knobInLock;
        public GameObject handsRoot;
        public Vector3 gripLocal;

        public bool TapChest => tapsRequired>0;
        public bool HandsVisible => handsRoot && handsRoot.activeSelf;
        public bool Unlocked { get; private set; }
        public bool TargetOpen { get; private set; }
        public float Openness { get; private set; }
        Vector3 closedPosition;
        Quaternion closedRotation;
        AudioSource voice;
        AudioClip gloveSound, latchSound, hingeSound;
        int strokes;
        float strokeTime, arrival,withdrawAt=-1;
        Transform eye;
        float turnDegrees;

        void Awake()
        {
            if(movingPart)
            {
                closedPosition=movingPart.localPosition;
                closedRotation=movingPart.localRotation;
            }
            Unlocked=!locked;
            if(TapChest) voice=GetComponent<AudioSource>();
        }

        internal void BeginWork(Transform cameraEye)
        {
            eye=cameraEye;
            turnDegrees=0;
            strokes=0;
            arrival=Time.unscaledTime;
            strokeTime=arrival-1;
            withdrawAt=-1;
            handsRoot?.SetActive(true);
            PositionHands();
        }

        internal void Stroke(int count)
        {
            strokes=count;
            strokeTime=Time.unscaledTime;
            if(voice)
            {
                if(!gloveSound) gloveSound=PassChestSound.Create(otherDoor ? 1 : 0);
                voice.pitch= .95f+.09f*count/tapsRequired;
                voice.PlayOneShot(gloveSound,.46f);
            }
            if(count==tapsRequired)
            {
                withdrawAt=Time.unscaledTime+.12f;
                if(voice)
                {
                    if(!latchSound) latchSound=PassChestSound.Create(2);
                    if(!hingeSound) hingeSound=PassChestSound.Create(otherDoor ? 4 : 3);
                    voice.PlayOneShot(latchSound,.54f);
                    voice.PlayOneShot(hingeSound,.38f);
                }
            }
        }

        internal void Open()
        {
            Unlocked=true;
            TargetOpen=true;
        }

        internal void Close()
        {
            if(!TapChest) { TargetOpen=false; return; }
            StopWork();
            TargetOpen=Unlocked;
        }

        void StopWork()
        {
            eye=null;strokes=0;withdrawAt=-1;
            if(handsRoot)handsRoot.SetActive(false);
            if(lockWork)lockWork.localRotation=RestLock();
            if(leftHasp)leftHasp.localRotation=RestLock();
        }

        internal void Restore(bool unlocked)
        {
            Unlocked=unlocked || !locked;
            TargetOpen=TapChest && Unlocked;
            StopWork();
            SetPose(TargetOpen ? 1 : 0);
        }

        void Update()
        {
            float next=Mathf.MoveTowards(Openness,TargetOpen ? 1 : 0,Time.deltaTime*(TapChest ? 2.72f : 3.25f));
            if(next!=Openness) SetPose(next);
        }

        void LateUpdate()
        {
            if(!eye)return;
            if(withdrawAt>=0 && Time.unscaledTime>=withdrawAt+.19f) {StopWork();return;}
            PositionHands();
        }

        Quaternion RestLock() => otherDoor
            ? Quaternion.Euler(0,0,Unlocked ? 58 : 0)
            : Quaternion.Euler(Unlocked ? 88 : 0,0,0);

        void PositionHands()
        {
            float now=Time.unscaledTime;
            float reach=Mathf.SmoothStep(0,1,(now-arrival)/.30f);
            if(withdrawAt>=0)reach*=1-Mathf.SmoothStep(0,1,(now-withdrawAt)/.19f);
            float age=now-strokeTime;
            float pulse=age>=0 && age<.19f ? Mathf.Sin(Mathf.PI*age/.19f) : 0;
            float desired=(otherDoor ? 58 : 88)*(float)strokes/tapsRequired;
            turnDegrees=Mathf.MoveTowards(turnDegrees,desired,310*Time.unscaledDeltaTime);
            if(lockWork)lockWork.localRotation=otherDoor
                ? Quaternion.Euler(0,0,turnDegrees)
                : Quaternion.Euler(turnDegrees,0,0);
            if(leftHasp)leftHasp.localRotation=Quaternion.Euler(turnDegrees*.82f,0,0);
            PositionArm(leftGlove,leftSleeve,leftUpper,leftDigits,-1,reach,pulse*.22f);
            PositionArm(rightGlove,rightSleeve,rightUpper,rightDigits,1,reach,pulse);
        }

        static void Curl(Transform[] joints,int side,float amount)
        {
            if(joints==null || joints.Length!=15)return;
            for(int finger=0;finger<4;finger++)
            {
                int i=finger*3;
                float fold=amount*(finger==0?.94f:1+.095f*finger);
                joints[i].localRotation=Quaternion.Euler(27*fold,-side*(finger-.2f)*fold*3f,0);
                joints[i+1].localRotation=Quaternion.Euler(51*fold,0,0);
                joints[i+2].localRotation=Quaternion.Euler(30*fold,0,0);
            }
            joints[12].localRotation=Quaternion.Euler(3*amount,side*24*amount,side*11*amount);
            joints[13].localRotation=Quaternion.Euler(9*amount,side*18*amount,0);
            joints[14].localRotation=Quaternion.Euler(9*amount,side*4*amount,0);
        }

        static void Sleeve(Transform segment, Vector3 distal, Vector3 proximal, float length)
        {
            if(!segment)return;
            Vector3 aim=distal-proximal;
            if(aim.sqrMagnitude<.00001f)return;
            segment.SetPositionAndRotation(distal,Quaternion.LookRotation(aim,Vector3.up));
            // Meshes have wrist and elbow origins and separate tailoring.
            // Only small seam overlap is permitted. No telescoping coat.
            segment.localScale=new Vector3(1,1,Mathf.Clamp(aim.magnitude/length,.95f,1.045f));
        }

        void PositionArm(Transform glove,Transform forearm,Transform upper,Transform[] joints,
            int side,float reach,float pulse)
        {
            if(!glove || !forearm || !upper)return;
            Vector3 contact=side<0
                ? transform.TransformPoint(braceLocal)
                : lockWork.TransformPoint(knobInLock);
            if(side<0 && leftHasp)contact=leftHasp.TransformPoint(knobInLock);
            Vector3 outward=-transform.forward;
            Vector3 fingers=transform.TransformDirection(new Vector3(-side*(side<0?.42f:.54f),.76f,.35f));
            Quaternion grasp=Quaternion.LookRotation(fingers,outward+.12f*Vector3.up)*
                Quaternion.Euler(0,0,side*(side<0 ? -12 : (6+5*pulse)));
            float folded=(side<0?.56f:.83f)*reach+.12f*pulse*reach;
            Curl(joints,side,folded);
            // Centre of the thumb–index pinch in the sculpt's wrist coordinate system.
            // It stays on the physical handle as the disk turns. Dorsal leather
            // faces the player and the tip clearance stays out of the marble.
            Vector3 pinch=new Vector3(-side*.016f,-.023f,.111f);
            Vector3 held=contact+outward*.006f-grasp*pinch;
            Vector3 rest=eye.TransformPoint(new Vector3(side*.158f,-.265f,.245f));
            glove.SetPositionAndRotation(Vector3.Lerp(rest,held,reach),grasp);

            // Two rigid anatomical segments. A player looking down while standing
            // bends at the upper body; shoulder reach is bounded by both bones.
            const float fore=.264f, arm=.278f;
            Vector3 shoulder=eye.TransformPoint(new Vector3(side*.184f,-.235f,-.10f));
            Vector3 toShoulder=shoulder-glove.position;
            float distance=Mathf.Clamp(toShoulder.magnitude,.065f,fore+arm-.017f);
            Vector3 axis=toShoulder.sqrMagnitude>.001f ? toShoulder.normalized : outward;
            shoulder=glove.position+axis*distance;
            Vector3 pole=Vector3.ProjectOnPlane(transform.right*side*.62f+Vector3.down*.75f,axis).normalized;
            float along=Mathf.Clamp((fore*fore+distance*distance-arm*arm)/(2*distance),-fore,fore);
            float bend=Mathf.Sqrt(Mathf.Max(0,fore*fore-along*along));
            Vector3 elbow=glove.position+axis*along+pole*bend;
            Sleeve(forearm,glove.position,elbow,fore);
            Sleeve(upper,elbow-(elbow-glove.position).normalized*.014f,shoulder,arm);
        }

        void SetPose(float amount)
        {
            Openness=amount;
            if(!movingPart)return;
            float eased=Mathf.SmoothStep(0,1,amount);
            movingPart.SetLocalPositionAndRotation(closedPosition+openOffset*eased,
                closedRotation*Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(openEuler),eased));
            if(otherDoor)otherDoor.localRotation=Quaternion.Euler(0,-openEuler.y*eased,0);
        }

        void OnDestroy()
        {
            if(gloveSound)Destroy(gloveSound);
            if(latchSound)Destroy(latchSound);
            if(hingeSound)Destroy(hingeSound);
        }
    }
}
