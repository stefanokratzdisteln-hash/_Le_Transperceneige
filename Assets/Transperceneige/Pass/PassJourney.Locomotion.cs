using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed partial class PassJourney
    {
        public BoxCollider platformFloor;
        readonly Collider[] clearance = new Collider[32];
        readonly RaycastHit[] supportHits = new RaycastHit[16];
        PassDoor boardingDoor;
        Vector3 landingPoint;
        bool climbingPlatform, platformLocated;

        void ResetLocomotion()
        {
            boardingAge = -1;
            climbingPlatform = false;
            boardingDoor = null;
            activeLadder = null;
        }

        public bool TryJump()
        {
            if (Phase==JourneyPhase.Jumping) return TryDeployParachute();
            if (Terminal || InventoryOpen || !body.enabled || walker.externalMotion) return false;
            if (EscapeJumpReady())
            {
                BeginFlight(new Vector3(7.5f,4.8f,-2.4f),true);
                return true;
            }
            if (PlatformLedge(out var ledge, out bool trapped))
            {
                if (trapped)
                {
                    // Inside the coupling/underframe a capsule cannot stand up along
                    // the usual lifting path. Recover to this bay on the same platform.
                    walker.Place(null, ledge, walker.transform.rotation, walker.ViewPitch);
                    Tell("已回到站台");
                    return true;
                }
                boardingStart = walker.transform.position;
                landingPoint = ledge;
                boardingAge = 0;
                climbingPlatform = true;
                walker.externalMotion = true;
                return true;
            }
            return walker.TryGroundJump();
        }

        bool EscapeJumpReady()
        {
            if (!Aboard || !rear || !rear.IsOpen || !EscapeReady || Phase != JourneyPhase.Running) return false;
            Vector3 offset = rear.car.InverseTransformVector(walker.transform.position - escapeLaunch.position);
            if (offset.z < -.36f || offset.z > .88f || Mathf.Abs(offset.x) > .86f ||
                Mathf.Abs(offset.y) > .48f) return false;
            Vector3 intent = walker.MoveIntent.sqrMagnitude > .1f ? walker.MoveIntent : walker.transform.forward;
            Vector3 local = rear.car.InverseTransformDirection(intent);
            return local.z > .2f || Mathf.Abs(local.x) > .68f;
        }

        string MovementPrompt()
        {
            if (walker.externalMotion || !body.enabled) return "";
            if (EscapeJumpReady())
                return PassText.Get(HasParachute ? "空格   跃入峡谷 · 伞包已就绪" : "空格   跃入峡谷 · 未携带伞包");
            if (PlatformLedge(out _, out bool trapped)) return PassText.Get(trapped ? "空格   脱困并返回站台" : "空格   攀回站台");
            if (!Aboard && BoardingDoor(false))
                return PassText.Get("朝门移动 + 空格   跳上列车");
            return "";
        }

        public void ReconcileBoarding()
        {
            if (InHub || InStartMenu || AtLanding || Terminal || boardingAge >= 0 || !body.enabled || walker.externalMotion) return;
            if(TryGorgeFall()) return;
            Vector3 feet = walker.transform.position;
            if (!Aboard && walker.JumpAge > .025f && walker.JumpAge < .72f)
            {
                var door = BoardingDoor(true);
                if (door)
                {
                    BeginBoarding(door);
                    return;
                }
            }

            // A support ray resolves which object actually carries the feet. A jump
            // inside a coach retains its reference frame while there is air below it.
            var support = SurfaceBelow(feet, .13f);
            if (Aboard)
            {
                if (support && !support.transform.IsChildOf(consist))
                {
                    Board(false);
                    walker.LeaveMovingSupport(-consist.forward * Speed);
                }
                else if (Mathf.Abs(consist.InverseTransformPoint(feet).x) > -PlatformEdge+.13f ||
                         (feet.y < 1.05f && InStation(feet)))
                {
                    Board(false);
                    walker.LeaveMovingSupport(-consist.forward * Speed);
                }
            }
            else if (support && support.transform.IsChildOf(consist) && feet.y >= 1.31f &&
                     Mathf.Abs(consist.InverseTransformPoint(feet).x) < -PlatformEdge+.06f)
            {
                // The roof, bogies and underside are not a boarding surface.
                if (feet.y <= 1.59f && NearOpenThreshold(feet)) Board(true);
            }
        }

        Collider SurfaceBelow(Vector3 feet, float depth)
        {
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * .085f, Vector3.down,
                supportHits, depth + .085f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider best = null;
            for (int i = 0; i < count; i++)
            {
                var hit = supportHits[i];
                if (hit.collider == body || hit.normal.y < .55f || hit.distance >= nearest) continue;
                best = hit.collider;
                nearest = hit.distance;
            }
            return best;
        }

        bool NearOpenThreshold(Vector3 feet)
        {
            foreach (var d in doors)
            {
                if (d.ExteriorSide == 0 || d.Openness < .72f) continue;
                Vector3 q = d.car.InverseTransformVector(feet - d.threshold.position);
                float outside = q.x * d.ExteriorSide;
                float footboardReach = d.ExteriorSide < 0 ? Mathf.Abs(PlatformEdge - d.threshold.position.x) + .07f : .68f;
                if (Mathf.Abs(q.z) < .64f && outside > -.68f && outside < footboardReach) return true;
            }
            return false;
        }

        PassDoor BoardingDoor(bool requireApproach)
        {
            if (Phase != JourneyPhase.Chase && Phase != JourneyPhase.Dwell) return null;
            var feet = walker.transform.position;
            PassDoor best = null;
            float closest = float.PositiveInfinity;
            foreach (var d in doors)
            {
                int side = d.ExteriorSide;
                if (side == 0 || !d.IsOpen || !DoorAvailable(d)) continue;
                Vector3 q = d.car.InverseTransformVector(feet - d.threshold.position);
                float lateral = q.x * side;
                if (lateral < -.15f || lateral > 1.2f || Mathf.Abs(q.z) > .58f) continue;
                float rise = DoorLanding(d).y - feet.y;
                if (rise > .93f || rise < -.77f) continue;
                if (requireApproach && Vector3.Dot(walker.MoveIntent, -side * d.car.right) < .12f)
                    continue;
                float score = Mathf.Abs(q.z) + Mathf.Abs(lateral);
                if (score < closest && ClearStanding(DoorLanding(d)))
                {
                    closest = score;
                    best = d;
                }
            }
            return best;
        }

        Vector3 DoorLanding(PassDoor d)
        {
            if (d == chase && chaseArrival) return chaseArrival.position;
            Vector3 local = d.car.InverseTransformPoint(d.threshold.position);
            local.x -= d.ExteriorSide * .48f;
            local.y = 1.405f;
            return d.car.TransformPoint(local);
        }

        bool ClearStanding(Vector3 feet)
        {
            float radius = body.radius - .025f;
            Vector3 lower = feet + Vector3.up * (radius + .055f);
            Vector3 upper = feet + Vector3.up * (body.height - radius - .025f);
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, clearance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (clearance[i] != body) return false;
            return count < clearance.Length;
        }

        void BeginBoarding(PassDoor d)
        {
            boardingDoor = d;
            boardingStart = consist.InverseTransformPoint(walker.transform.position);
            landingPoint = consist.InverseTransformPoint(DoorLanding(d));
            boardingAge = 0;
            climbingPlatform = false;
            walker.externalMotion = true;
            Board(true);
            Tell(d == chase ? "已抓住车尾扶手" : "已抓住车门扶手");
        }

        void StepAboard(float dt)
        {
            boardingAge += dt;
            float t = Mathf.Clamp01(boardingAge / (climbingPlatform ? .62f : .42f));
            // Raise the shoes above the sill before carrying them horizontally over
            // the edge. Move the real capsule so a pillar or closing leaf still blocks it.
            float across = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, 1f, t));
            float lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .37f));
            Vector3 target = Vector3.Lerp(boardingStart, landingPoint, across);
            target.y = Mathf.Lerp(boardingStart.y, landingPoint.y + .055f, lift) - .055f * across;
            var world = climbingPlatform ? target : consist.TransformPoint(target);
            body.Move(world - walker.transform.position);
            if (t < 1) return;
            var end = climbingPlatform ? landingPoint : consist.TransformPoint(landingPoint);
            bool arrived = Vector3.Distance(walker.transform.position, end) < .22f;
            if (!climbingPlatform && !arrived)
            {
                Vector3 q = boardingDoor.car.InverseTransformVector(walker.transform.position - boardingDoor.threshold.position);
                if (q.x * boardingDoor.ExteriorSide > -.05f) Board(false);
            }
            // Preserve the collision-resolved position, including an interrupted pull-up.
            var parent = Aboard ? consist : null;
            var feet = walker.transform.position;
            walker.Place(parent, parent ? parent.InverseTransformPoint(feet) : feet, walker.transform.localRotation, walker.ViewPitch);
            walker.externalMotion = false;
            ResetLocomotion();
        }

        bool InStation(Vector3 feet)
        {
            return -feet.z >= PlatformStart && -feet.z <= PlatformEnd;
        }

        bool PlatformLedge(out Vector3 landing, out bool trapped)
        {
            landing = default;
            trapped = false;
            if (Aboard || (Phase != JourneyPhase.Dwell && Phase != JourneyPhase.Chase)) return false;
            Vector3 feet = walker.transform.position;
            if (!InStation(feet)) return false;
            if (!platformFloor && !platformLocated)
            {
                platformLocated = true;
                var surface = GameObject.Find("Fellbridge Station/Platform structure/Platform floor");
                if (surface) platformFloor = surface.GetComponent<BoxCollider>();
            }
            if (!platformFloor) return false;
            Bounds b = platformFloor.bounds;
            float rise = b.max.y - feet.y;
            float gap = feet.x - b.max.x;
            if (rise < .31f || rise > 2.05f || gap < -.32f || gap > -PlatformEdge+1.46f ||
                feet.z < b.min.z + .62f || feet.z > b.max.z - .62f) return false;
            landing = new Vector3(b.max.x - .56f, b.max.y + .035f, feet.z);
            if (!ClearStanding(landing)) return false;
            // An overhanging sill, running gear or occupied joint makes lifting a
            // standing capsule vertically impossible. This is restricted to the station.
            trapped = !ClearStanding(new Vector3(feet.x, b.max.y + .065f, feet.z)) ||
                (gap > .95f && Mathf.Abs(consist.InverseTransformPoint(feet).x) < 1.46f &&
                 -feet.z >= Distance - TrainLength && -feet.z <= Distance);
            if (trapped) return true;
            if (gap > 1.02f) return false;
            Vector3 intent = walker.MoveIntent.sqrMagnitude > .1f ? walker.MoveIntent : walker.transform.forward;
            return intent.x < -.18f;
        }
    }
}
