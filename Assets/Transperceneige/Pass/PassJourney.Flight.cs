using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed partial class PassJourney
    {
        const float FlightGravity=9.81f, GlideSink=4.2f, OpeningBrake=20f;
        Vector3 airVelocity, steerDirection, approachOrigin, steerOffset;
        float nextSurvey;
        bool clearingParapet;
        public bool CanopyWasDeployed { get; private set; }
        public bool Airborne => Phase==JourneyPhase.Jumping || Phase==JourneyPhase.UnderCanopy;

        void ResetFlight()
        {
            airVelocity=steerDirection=steerOffset=Vector3.zero;
            flightAge=fallRate=nextSurvey=0;
            descentPlanned=false;
            clearingParapet=false;
            CanopyWasDeployed=false;
        }

        void BeginFlight(Vector3 worldVelocity,bool tailLeap=false)
        {
            ResetFlight();
            ResetLocomotion();
            SetInventory(false);
            Board(false);
            walker.externalMotion=true;
            body.enabled=false;
            airVelocity=worldVelocity;
            clearingParapet=tailLeap;
            Phase=JourneyPhase.Jumping;
            noticeUntil=-1;
        }

        bool TryGorgeFall()
        {
            if(Phase!=JourneyPhase.Running || InHub || AtLanding || InStartMenu ||
                walker.externalMotion || !body.enabled || body.isGrounded) return false;
            Vector3 feet=walker.transform.position;
            float chainage=-feet.z;
            if(chainage<Bridge || chainage>Break+40 || walker.VerticalSpeed>-.25f) return false;
            // A gap beside/beyond the formation is a real jump too. Keep the ordinary
            // motor for a drop onto the intact deck (and for platform/room jumps).
            if(Aboard && Mathf.Abs(feet.x)<-PlatformEdge+.13f && feet.y>.5f &&
               chainage>=Distance-TrainLength-.25f && chainage<=Distance+.25f) return false;
            if(PassAtmosphere.GroundHeight(feet.x,chainage)>feet.y-4.5f ||
                Physics.Raycast(feet+Vector3.up*.06f,Vector3.down,4.35f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) return false;
            Vector3 velocity=walker.PlanarVelocity;
            if(Aboard) velocity-=consist.forward*Speed;
            velocity.y=walker.VerticalSpeed;
            BeginFlight(velocity);
            return true;
        }

        void ReadFlightInput(Vector2 axes,bool spacePressed)
        {
            if(!Airborne) return;
            SetFlightSteering(axes);
            if(spacePressed) TryDeployParachute();
        }

        public void SetFlightSteering(Vector2 axes)
        {
            if(!Airborne) return;
            axes=Vector2.ClampMagnitude(axes,1);
            steerDirection=Vector3.ProjectOnPlane(walker.transform.right*axes.x+walker.transform.forward*axes.y,Vector3.up);
        }

        public bool TryDeployParachute()
        {
            if(Phase!=JourneyPhase.Jumping || Terminal || InventoryOpen || !HasParachute) return false;
            CanopyWasDeployed=true;
            Phase=JourneyPhase.UnderCanopy;
            fallRate=-airVelocity.y;
            if(canopy) canopy.SetActive(true);
            PlanDescent(walker.transform.position);
            approachOrigin=touchdown;
            nextSurvey=flightAge+.25f;
            Tell("伞已展开");
            return true;
        }

        // Sweep the feet along the true trajectory and sample the triangulated terrain
        // when close to it. A roof, platform or bridge deck can receive an impact too.
        bool HitGround(Vector3 from,Vector3 to,out Vector3 feet)
        {
            feet=to;
            Vector3 step=to-from;
            if(step.y>=0) return false;
            if(Physics.Raycast(from+Vector3.up*.015f,step.normalized,out var swept,step.magnitude+.025f,
                               Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && swept.normal.y>.30f)
            {
                feet=swept.point+Vector3.up*.045f;
                return true;
            }
            float floor=PassAtmosphere.GroundHeight(to.x,-to.z);
            if(to.y>floor+4f) return false;
            float top=Mathf.Max(floor+5,from.y+.25f);
            var probe=new Vector3(to.x,top,to.z);
            if(Physics.Raycast(probe,Vector3.down,out var hit,top-floor+7,
                               Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) floor=hit.point.y;
            if(to.y>floor+.045f || from.y<floor-.18f) return false;
            feet.y=floor+.045f;
            return true;
        }

        void TouchSurface(Vector3 feet,float speed)
        {
            walker.transform.position=feet;
            if(canopy) canopy.SetActive(false);
            ApplyGroundImpact(speed);
            if(Terminal) return;
            if(CanopyWasDeployed && SampleBank(feet,out var bank) && FindGateOnBank(bank,out gateFoot))
            {
                LandSafely(bank+Vector3.up*.045f);
                return;
            }
            // Touching a deck or roof ends this flight at the actual impact point.
            // Its occupant can continue on foot; a subsequent drop is evaluated afresh.
            Phase=JourneyPhase.Running;
            Aboard=false;
            walker.externalMotion=false;
            walker.Place(null,feet,walker.transform.rotation,walker.ViewPitch);
            Physics.SyncTransforms();
        }

        void Fly(float dt)
        {
            flightAge+=dt;
            if(Phase==JourneyPhase.UnderCanopy){GlideToBank(dt);return;}
            Vector3 p=walker.transform.position;
            float previousYVelocity=airVelocity.y;
            Vector3 next=p+airVelocity*dt+Vector3.down*(.5f*FlightGravity*dt*dt);
            airVelocity.y=Mathf.Max(-52f,airVelocity.y-FlightGravity*dt);
            if(HitGround(p,next,out var impact)){
                float fraction=Mathf.InverseLerp(p.y,next.y,impact.y);
                TouchSurface(impact,-Mathf.Lerp(previousYVelocity,airVelocity.y,fraction));
            }
            else walker.transform.position=next;
        }
    }
}
