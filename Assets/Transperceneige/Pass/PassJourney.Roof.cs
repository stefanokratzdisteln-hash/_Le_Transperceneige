using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed partial class PassJourney
    {
        PassRoofLadder[] ladders;
        PassRoofLadder activeLadder;
        int ladderTarget, ladderDirection;
        PassTunnelClearance[] tunnelZones;
        public bool ClimbingRoofLadder => activeLadder != null;
        public bool OnRoof => Aboard && !Terminal && !ClimbingRoofLadder && ladders!=null && ladders.Length>0 &&
            ladders[0].IsReady && walker.transform.position.y>=ladders[0].roofLanding.position.y-.32f;
        public bool TunnelAhead => IsNearBore(false);
        public bool InRoofTunnel => IsNearBore(true);

        bool TryTargetLadder(out PassRoofLadder ladder, out bool descend)
        {
            ladder=null;
            descend=false;
            if (!Aboard || Terminal || InStartMenu || InventoryOpen || walker.externalMotion ||
                !body.enabled || ladders==null) return false;
            Vector3 feet=walker.transform.position;
            Transform eye=walker.eyeCamera.transform;
            foreach (var candidate in ladders)
            {
                if (!candidate.IsReady) continue;
                descend=feet.y>candidate.upperRung.position.y-.45f;
                Vector3 landing=(descend ? candidate.roofLanding : candidate.lowerLanding).position;
                Vector3 towardLanding=landing-feet;
                if (Mathf.Abs(towardLanding.y)>.38f ||
                    Vector3.ProjectOnPlane(towardLanding,Vector3.up).sqrMagnitude>1.32f*1.32f) continue;
                // Aim at the hatch from the roof, or face the rungs in the aisle.
                Vector3 focus=(descend ? candidate.upperRung : candidate.lowerRung).position;
                if (!descend) focus.y=eye.position.y+.12f;
                Vector3 horizontal=Vector3.ProjectOnPlane(focus-eye.position,Vector3.up);
                Vector3 facing=Vector3.ProjectOnPlane(eye.forward,Vector3.up);
                if (horizontal.sqrMagnitude>.035f && Vector3.Dot(facing.normalized,horizontal.normalized)<.28f &&
                    !(descend && Vector3.Dot(eye.forward,(focus-eye.position).normalized)>.82f)) continue;
                // Reject requests through a bulkhead or from a neighbouring compartment.
                Vector3 atHip=Vector3.up*.63f;
                if (!descend && Physics.Linecast(feet+atHip,landing+atHip,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                ladder=candidate;
                return true;
            }
            return false;
        }

        void StartLadder(PassRoofLadder ladder,bool descend)
        {
            if (!descend && TouchesSoffit(ladder.roofLanding.position,body.height))
            {
                Tell("roof.blocked");
                return;
            }
            activeLadder=ladder;
            ladderDirection=descend ? -1 : 1;
            ladderTarget=descend ? 3 : 0;
            // Stop existing sprint/jump momentum; retain the rider's train-relative stance and view.
            walker.Place(consist,walker.transform.localPosition,walker.transform.localRotation,walker.ViewPitch);
            walker.externalMotion=true;
        }

        void StepLadder(float dt)
        {
            // Each move resolves against the carriage. Moving the whole consist later in the
            // frame carries the player and these same anchors by exactly the same distance.
            float budget=1.7f*dt;
            while (budget>.001f)
            {
                Vector3 target=activeLadder.StopAt(ladderTarget).position;
                Vector3 start=walker.transform.position;
                Vector3 step=Vector3.ClampMagnitude(target-start,budget);
                if (step.sqrMagnitude>0.000001f) body.Move(step);
                Vector3 end=walker.transform.position;
                budget-=step.magnitude;
                if ((target-end).sqrMagnitude>.105f*.105f)
                {
                    if ((end-start).sqrMagnitude<step.sqrMagnitude*.02f)
                    {
                        EndLadder();
                        Tell("roof.blocked");
                    }
                    return;
                }
                ladderTarget+=ladderDirection;
                if (ladderTarget<0 || ladderTarget>3)
                {
                    EndLadder();
                    return;
                }
            }
        }

        void EndLadder()
        {
            activeLadder=null;
            walker.externalMotion=false;
        }

        // Drive() may advance several simulation ticks before SyncTrain() publishes the
        // consist's new transform. Project the actual rider offset into the present tick.
        Vector3 RidingFeet(Vector3 worldFeet)
        {
            worldFeet.z+=NoseDatum-Distance-consist.position.z;
            return worldFeet;
        }

        bool TouchesSoffit(Vector3 worldFeet,float height)
        {
            if (!Aboard || tunnelZones==null) return false;
            Vector3 feet=RidingFeet(worldFeet);
            float radius=body.radius;
            foreach (var zone in tunnelZones)
            {
                if (!zone.soffit) continue;
                Bounds duct=zone.soffit.bounds;
                // The closest point of the vertical capsule axis to this structural box.
                var axis=feet;
                axis.y=Mathf.Clamp(duct.center.y,feet.y+radius,feet.y+height-radius);
                if (duct.SqrDistance(axis)<radius*radius) return true;
            }
            return false;
        }

        bool RoomToStand() => !TouchesSoffit(walker.transform.position,walker.StandingHeight);

        void CheckTunnelImpact()
        {
            if (Terminal || !body.enabled || Airborne || !TouchesSoffit(walker.transform.position,body.height)) return;
            LastImpactDamage=Health;
            Finish(JourneyPhase.TunnelImpact);
        }

        bool IsNearBore(bool inside)
        {
            if (!OnRoof || tunnelZones==null) return false;
            float riderZ=RidingFeet(walker.transform.position).z;
            foreach (var zone in tunnelZones)
            {
                if (!zone.soffit) continue;
                Bounds duct=zone.soffit.bounds;
                if (inside ? riderZ>=duct.min.z && riderZ<=duct.max.z :
                    riderZ>duct.max.z && riderZ<=duct.max.z+80f) return true;
            }
            return false;
        }
    }
}
