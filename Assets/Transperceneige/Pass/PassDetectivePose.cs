using System;
using System.Collections.Generic;
using UnityEngine;
using Winterbourne.Demo;

namespace Winterbourne.Pass
{
    [DefaultExecutionOrder(85)]
    public sealed class PassDetectivePose : MonoBehaviour
    {
        public Car07Walker walker;
        public PassJourney journey;
        sealed class Joint
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 pitchAxis,rollAxis;

            public void Bend(float pitch,float roll=0)
            {
                bone.localRotation=Quaternion.AngleAxis(roll,rollAxis)*Quaternion.AngleAxis(pitch,pitchAxis)*rest;
            }
        }

        Joint hips,spine,head,leftThigh,rightThigh,leftShin,rightShin,leftFoot,rightFoot;
        Joint leftArm,rightArm,leftForearm,rightForearm;
        Vector3 hipsAtRest;
        float anklesAtRest,phase,motion,crouch,armLift;

        void Awake()
        {
            var rig=new Dictionary<string,Transform>(StringComparer.Ordinal);
            foreach(var t in GetComponentsInChildren<Transform>(true))
            {
                string name=t.name;
                int separator=Math.Max(name.LastIndexOf(':'),name.LastIndexOf('|'));
                rig[name.Substring(separator+1).Trim()]=t;
            }
            Joint Bind(string name)
            {
                if(!rig.TryGetValue(name,out var t))throw new InvalidOperationException("Missing detective bone "+name);
                return new Joint {bone=t, rest=t.localRotation, pitchAxis=t.parent.InverseTransformDirection(transform.right),
                    rollAxis=t.parent.InverseTransformDirection(transform.forward)};
            }
            hips=Bind("Hips");spine=Bind("Spine");head=Bind("Head");
            leftThigh=Bind("LeftUpperLeg");rightThigh=Bind("RightUpperLeg");
            leftShin=Bind("LeftLowerLeg");rightShin=Bind("RightLowerLeg");
            leftFoot=Bind("LeftFoot");rightFoot=Bind("RightFoot");
            leftArm=Bind("LeftUpperArm");rightArm=Bind("RightUpperArm");
            leftForearm=Bind("LeftLowerArm");rightForearm=Bind("RightLowerArm");
            hipsAtRest=hips.bone.localPosition;
            anklesAtRest=LowestAnkle();
        }

        float LowestAnkle()=>Mathf.Min(transform.InverseTransformPoint(leftFoot.bone.position).y,
                                      transform.InverseTransformPoint(rightFoot.bone.position).y);

        void LateUpdate()
        {
            if(!walker || hips==null)return;
            float dt=Time.deltaTime;
            var localVelocity=walker.transform.InverseTransformDirection(walker.PlanarVelocity);
            float speed=new Vector2(localVelocity.x,localVelocity.z).magnitude;
            float blend=1-Mathf.Exp(-11*dt);
            bool flying=journey && journey.Airborne;
            bool climbing=journey && journey.ClimbingRoofLadder;
            motion=Mathf.Lerp(motion,flying?0:Mathf.Clamp01(speed/.9f),blend);
            crouch=Mathf.Lerp(crouch,walker.IsCrouched?1:0,blend);
            phase+=dt*(climbing?6.4f:Mathf.Lerp(6.5f,11,Mathf.InverseLerp(1,5.8f,speed)))*Mathf.Max(motion,climbing?1:0);
            armLift=Mathf.Lerp(armLift,journey && journey.Phase==JourneyPhase.UnderCanopy?1:0,blend);
            float forward=speed>.07f?Mathf.Clamp(localVelocity.z/speed,-1,1):1;
            float side=speed>.07f?localVelocity.x/speed:0;
            float stride=Mathf.Lerp(22,37,Mathf.InverseLerp(1.8f,5.8f,speed))*Mathf.Lerp(1,.58f,crouch);
            float walking=Mathf.Sin(phase)*motion;
            float swing=climbing?Mathf.Sin(phase)*17:walking*stride*forward;
            float bodyFold=climbing?23:crouch*60;
            float kneeFold=climbing?46:crouch*109;
            hips.bone.localPosition=hipsAtRest;
            hips.Bend(0,walking*1.75f);
            spine.Bend(crouch*19+motion*Mathf.Lerp(2,8,Mathf.InverseLerp(1,5.8f,speed)));
            head.Bend(Mathf.Clamp(walker.ViewPitch*.36f,-19,18)-crouch*12);
            leftThigh.Bend(-bodyFold-swing,walking*side*14);
            rightThigh.Bend(-bodyFold+swing,-walking*side*14);
            float leftKnee=kneeFold+Mathf.Max(0,-walking*forward)*stride*.8f;
            float rightKnee=kneeFold+Mathf.Max(0,walking*forward)*stride*.8f;
            leftShin.Bend(leftKnee);rightShin.Bend(rightKnee);
            leftFoot.Bend(bodyFold-leftKnee+swing*.67f);
            rightFoot.Bend(bodyFold-rightKnee-swing*.67f);
            leftArm.Bend(swing*.58f-crouch*17-armLift*108-(climbing?84:0),armLift*-28);
            rightArm.Bend(-swing*.58f-crouch*17-armLift*108-(climbing?84:0),armLift*28);
            leftForearm.Bend(-6-crouch*22-Mathf.Max(0,-swing)*.42f-armLift*19);
            rightForearm.Bend(-6-crouch*22-Mathf.Max(0,swing)*.42f-armLift*19);
            float drop=anklesAtRest-LowestAnkle();
            hips.bone.localPosition=hipsAtRest+hips.bone.parent.InverseTransformVector(transform.up*drop);
        }
    }
}
