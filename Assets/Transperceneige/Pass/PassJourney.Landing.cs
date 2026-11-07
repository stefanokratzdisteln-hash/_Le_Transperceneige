using System;
using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed partial class PassJourney
    {
        bool descentPlanned;
        Vector3 touchdown, gateFoot;
        static readonly float[] GateDistances={5.1f,4.25f,6.15f};
        static readonly float[] BankOffsets={0f,3.2f,-3.2f,7.4f,-7.4f};

        static bool IcyWater(Vector3 p)
        {
            float station=-p.z;
            if(station>=1136 && station<=1590 && Mathf.Abs(p.x-PassAtmosphere.TributaryX(station))<5.55f) return true;
            return station>=1420 && station<=2202 && Mathf.Abs(p.x-PassAtmosphere.RiverX(station))<13.2f;
        }

        bool SampleBank(Vector3 p, out Vector3 surface)
        {
            surface=default;
            // The surveyed eastern strip is beyond the widest masonry footing (7m)
            // and inside the start of the decorative boulder and spruce belts (42m).
            // This also excludes scenery-only masonry that has no physics collider.
            if(p.x<9f || p.x>37.5f || IcyWater(p)) return false;
            float y=PassAtmosphere.GroundHeight(p.x,-p.z);
            var top=new Vector3(p.x,y+5,p.z);
            if(!Physics.Raycast(top,Vector3.down,out var hit,11,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                return false;
            // Only the collision mesh of the snowbank counts. A bridge pier, refuge
            // roof or rock hit first is an obstruction, even if its top looks flat.
            var mesh=hit.collider as MeshCollider;
            if(!mesh || !mesh.sharedMesh || !mesh.sharedMesh.name.StartsWith("Strath ",StringComparison.Ordinal) ||
               hit.normal.y < Mathf.Cos((body.slopeLimit-1f)*Mathf.Deg2Rad)) return false;
            surface=hit.point;
            return ClearStanding(surface+Vector3.up*.06f);
        }

        bool FindGateOnBank(Vector3 feet,out Vector3 arch)
        {
            arch=default;
            float bestGrade=float.PositiveInfinity;
            foreach(float metres in GateDistances)
            for(int i=0;i<16;i++)
            {
                Vector3 direction=Quaternion.AngleAxis(i*22.5f,Vector3.up)*Vector3.left;
                Vector3 previous=feet;
                bool walkable=true;
                int steps=Mathf.CeilToInt(metres/.55f);
                float spacing=metres/steps;
                for(int j=1;j<=steps;j++)
                {
                    Vector3 p=feet+direction*(spacing*j);
                    if(!SampleBank(p,out var next) || Mathf.Abs(next.y-previous.y)>spacing*.72f)
                    {walkable=false;break;}
                    previous=next;
                }
                if(!walkable) continue;
                // Both jambs stand on the same bank. Include the portal and the
                // capsule path in clearance checks so a tree or pier cannot trap it.
                if(!BookcaseFooting(previous,direction) || Vector3.Distance(previous,feet)>6.95f) continue;
                float grade=Mathf.Abs(previous.y-feet.y)/metres+.035f*(1-Vector3.Dot(direction,Vector3.left))
                    +.035f*Mathf.Abs(metres-5.1f);
                if(grade<bestGrade) {bestGrade=grade;arch=previous;}
                if(bestGrade<.085f) return true;
            }
            return bestGrade<float.PositiveInfinity;
        }

        bool BookcaseFooting(Vector3 centre,Vector3 direction)
        {
            // The sliding bookcase parks to the left and in front of the rear panel;
            // test all four corners of that asymmetric envelope, not just the threshold.
            var low=returnGate && returnGate.cover ? returnGate.cover.groundMinimum : new Vector2(-2.37f,-.46f);
            var high=returnGate && returnGate.cover ? returnGate.cover.groundMaximum : new Vector2(.86f,.25f);
            var right=Vector3.Cross(Vector3.up,direction);
            float min=centre.y,max=centre.y;
            for(int i=0;i<4;i++)
            {
                Vector3 corner=centre+right*((i&1)==0 ? low.x : high.x)+direction*((i&2)==0 ? low.y : high.y);
                if(!SampleBank(corner,out var bank)) return false;
                min=Mathf.Min(min,bank.y); max=Mathf.Max(max,bank.y);
            }
            return max-min <= .57f;
        }

        // Public so the route survey can verify the complete release window in Edit/Play
        // Mode, including the steep transition to the gorge on the earliest possible jump.
        public bool TryFindLandingPair(Vector3 canopyPosition,out Vector3 feet,out Vector3 arch)
        {
            feet=arch=default;
            Vector3 wind=canopyPosition;
            float fall=2;
            for(int i=0;i<180;i++)
            {
                fall=Mathf.MoveTowards(fall,4.2f,.375f);
                wind+=new Vector3(2.35f,-fall,-1.05f)*.25f;
                if(wind.y<=PassAtmosphere.GroundHeight(wind.x,-wind.z)+.09f) break;
            }
            // Prefer the uncorrected drift. If it reaches the steep mouth of the
            // gorge, continue down-valley until the canopy can deliver a safe approach.
            wind.x=Mathf.Clamp(wind.x,14f,32f);
            for(int down=0;down<=120;down+=5)
            foreach(float across in BankOffsets)
            {
                var p=wind+new Vector3(across,0,-down);
                if(SampleBank(p,out feet) && FindGateOnBank(feet,out arch)) return true;
            }
            return false;
        }

        void PlanDescent(Vector3 canopyPosition)
        {
            descentPlanned=TryFindLandingPair(canopyPosition,out touchdown,out gateFoot);
        }

        void SteerApproach(Vector3 position,float dt)
        {
            if(steerDirection.sqrMagnitude<.001f || position.y-touchdown.y<6f) return;
            steerOffset=Vector3.ClampMagnitude(steerOffset+steerDirection*(5f*dt),22f);
            if(flightAge<nextSurvey) return;
            nextSurvey=flightAge+.45f;
            Vector3 desired=approachOrigin+steerOffset;
            desired.x=Mathf.Clamp(desired.x,12f,34f);
            if(SampleBank(desired,out var bank) && FindGateOnBank(bank,out var gate))
            {
                touchdown=bank;
                gateFoot=gate;
            }
        }

        void GlideToBank(float dt)
        {
            Vector3 p=walker.transform.position;
            float previousRate=fallRate;
            fallRate=Mathf.MoveTowards(fallRate,GlideSink,(fallRate>GlideSink ? OpeningBrake : FlightGravity)*dt);
            // Opening the pack cannot erase speed or rescue someone already at impact.
            float sink=.5f*(previousRate+fallRate);
            bool inflated=fallRate<GlideSink+.15f;
            if(!descentPlanned && flightAge>=nextSurvey)
            {
                PlanDescent(p);
                if(descentPlanned) approachOrigin=touchdown;
                nextSurvey=flightAge+.75f;
            }
            Vector3 target=touchdown+Vector3.up*.045f;
            Vector3 desired=new Vector3(2.35f,0,-1.05f);
            if(descentPlanned)
            {
                SteerApproach(p,dt);
                target=touchdown+Vector3.up*.045f;
                Vector3 delta=Vector3.ProjectOnPlane(target-p,Vector3.up);
                float timeToGround=Mathf.Max(.18f,(p.y-target.y)/GlideSink);
                desired=Vector3.ClampMagnitude(delta/timeToGround,11.5f);
            }
            float clearance=descentPlanned ? p.y-target.y : p.y-PassAtmosphere.GroundHeight(p.x,-p.z);
            desired+=4.2f*Mathf.InverseLerp(4f,9f,clearance)*steerDirection;
            // Pulling immediately still carries the jumper out past the steelwork.
            // Only then can steering turn the canopy back toward the railway.
            if(clearingParapet){
                if(p.x>=10f) clearingParapet=false;
                else desired.x=Mathf.Max(desired.x,4.2f);
            }
            Vector3 horizontal=Vector3.MoveTowards(Vector3.ProjectOnPlane(airVelocity,Vector3.up),
                desired,9f*dt);
            airVelocity=horizontal+Vector3.down*fallRate;
            Vector3 next=p+horizontal*dt+Vector3.down*(sink*dt);
            // Keep a piloted approach in the surveyed lane when steering toward the steelwork.
            if(descentPlanned && p.x>=10f) next.x=Mathf.Clamp(next.x,10f,Mathf.Max(36f,p.x));

            float remaining=descentPlanned ? Vector3.ProjectOnPlane(target-next,Vector3.up).magnitude : float.PositiveInfinity;
            if(inflated && descentPlanned)
            {
                if(remaining<.28f && next.y<=target.y+.12f)
                {
                    // A flare covers at most a foot, over the verified dry touchdown site.
                    LandSafely(target);
                    return;
                }
                // Stay clear of the rising bank in the final approach, only after the
                // opening has actually arrested the fall. An unopened/late canopy gets
                // the normal contact test and its actual vertical impact speed.
                if(remaining>=.28f) next.y=Mathf.Max(next.y,PassAtmosphere.GroundHeight(next.x,-next.z)+.42f);
            }
            if(HitGround(p,next,out var impact))
            {
                float fraction=Mathf.InverseLerp(p.y,next.y,impact.y);
                TouchSurface(impact,Mathf.Lerp(previousRate,fallRate,fraction));
                return;
            }
            walker.transform.position=next;
        }

        void LandSafely(Vector3 feet)
        {
            location=JourneyLocation.Valley;
            Phase=JourneyPhase.Won;
            Speed=0; Aboard=false;
            if(canopy) canopy.SetActive(false);
            walker.externalMotion=false;
            ResetLocomotion();
            SetInventory(false);
            var direction=Vector3.ProjectOnPlane(gateFoot-feet,Vector3.up);
            var facing=Quaternion.LookRotation(direction.normalized,Vector3.up);
            if(returnGate)
            {
                returnGate.transform.SetPositionAndRotation(gateFoot+Vector3.up*.025f,facing);
                returnGate.gameObject.SetActive(true);
            }
            // Centre the doorway at first sight even on the rising approach bank.
            float eyeHeight=walker.eyeCamera.transform.localPosition.y;
            float lookPitch=-Mathf.Atan2(gateFoot.y+1.37f-feet.y-eyeHeight,direction.magnitude)*Mathf.Rad2Deg;
            walker.Place(null,feet,facing,Mathf.Clamp(lookPitch,-31f,31f));
            Physics.SyncTransforms();
            Tell("landing.safe");
        }
    }
}
