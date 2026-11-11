using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Winterbourne.Demo;
using Winterbourne.Pass;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    // -executeMethod Winterbourne.Editor.PassExtractionReview.Run [-extractionCase station|canyon|crash|shortfall]
    // Follows the player's normal movement, aim, F, jump, open-parachute, gate and
    // settlement entry points. No fixture places the player, opens a door or supplies an item.
    [InitializeOnLoad]
    internal static partial class PassExtractionReview
    {
        const string Flag="Winterbourne.ExtractionReview";
        const float Dt=1f/30;
        static readonly Stack<IEnumerator> steps=new Stack<IEnumerator>();
        static PassJourney route;
        static Car07Walker walker;
        static Transform house;
        static int lastFrame=-1;
        static string action;
        static float clockAtBoarding;
        static bool checkTimetable;
#if ENABLE_INPUT_SYSTEM
        static Keyboard keyboard;
#endif
        static Vector3 Feet=>walker.transform.position;
        static Transform Eye=>walker.eyeCamera.transform;

        static PassExtractionReview()=>EditorApplication.update+=Tick;
        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            SessionState.SetBool(Flag,true);
            SessionState.SetString(Flag+".start",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false))return;
            try
            {
                if((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Flag+".start","")).ToUniversalTime()).TotalMinutes>18)
                    throw new TimeoutException("Extraction walkthrough: "+action);
                if(EditorApplication.isPaused)EditorApplication.isPaused=false;
                EditorApplication.QueuePlayerLoopUpdate();
                if(!EditorApplication.isPlaying||Time.frameCount<6||lastFrame==Time.frameCount)return;
                lastFrame=Time.frameCount;
                if(steps.Count==0)steps.Push(Cases());
                while(steps.Count>0)
                {
                    var top=steps.Peek();
                    if(!top.MoveNext()){steps.Pop();continue;}
                    if(top.Current is IEnumerator inner){steps.Push(inner);continue;}
                    return;
                }
                SessionState.EraseBool(Flag);
                Debug.Log("EXTRACTION PASS: walked full route with original capsule and real inventory");
                EditorApplication.Exit(0);
            }
            catch(Exception e)
            {
                SessionState.EraseBool(Flag);
                Debug.LogError("EXTRACTION FAILED while "+action+(walker?$" at feet {Feet:F2}, local {route.consist.InverseTransformPoint(Feet):F2}, phase {route.Phase}, mileage {route.Distance:F1}, remaining {route.RunSeconds:F1}":""));
                Debug.LogException(e);EditorApplication.Exit(1);
            }
        }
        static void Check(bool ok,string message)=>PassBuild.Check(ok,"EXTRACTION: "+message);
        static void Step(Vector3 direction,bool jump=false,bool sprint=false)
        {
            float previousDistance=route.Distance;
            var previousPhase=route.Phase;
            bool tailWasClear=route.EscapeReady;
            var axes=walker.transform.InverseTransformDirection(direction);
            walker.MoveCharacter(new Vector2(axes.x,axes.z),sprint,Dt,jump);
            route.ReconcileBoarding();
            route.AdvanceExpedition(Dt);
            route.Advance(Dt);
            if(!checkTimetable)return;
            float elapsed=clockAtBoarding-route.RunSeconds;
            void Milestone(string name,float expected)
            {
                Check(Mathf.Abs(elapsed-expected)<3.5f,$"{name} at {elapsed:F1}s; expected {expected:F0}s");
                Debug.Log($"EXTRACTION TIMETABLE: {name} after {elapsed:F1}s");
            }
            if(previousPhase!=JourneyPhase.Dwell && route.Phase==JourneyPhase.Dwell)Milestone("arrive",215);
            if(previousPhase==JourneyPhase.Dwell && route.Phase==JourneyPhase.Chase)Milestone("depart",335);
            if(previousDistance<PassJourney.Bridge && route.Distance>=PassJourney.Bridge)Milestone("bridge",438);
            if(!tailWasClear && route.EscapeReady)Milestone("rear escape available",480);
            if(previousDistance<PassJourney.Break && route.Distance>=PassJourney.Break)Milestone("broken span",558);
        }
        static void Aim(Vector3 world)
        {
            Vector3 to=world-Eye.position;
            var flat=Vector3.ProjectOnPlane(to,Vector3.up);
            if(flat.sqrMagnitude>.0001f)walker.transform.rotation=Quaternion.LookRotation(flat);
            Eye.rotation=Quaternion.LookRotation(to);
        }
        static Vector3 Horizontal(Vector3 v){v.y=0;return v;}
        static IEnumerator Press(bool close=false)
        {
#if ENABLE_INPUT_SYSTEM
            if(keyboard!=null)
            {
                if(!route.InventoryOpen)walker.ResumeLook();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(close?Key.Escape:Key.F));
                for(int i=0;i<2;i++){Step(Vector3.zero);yield return null;}
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                for(int i=0;i<2;i++){Step(Vector3.zero);yield return null;}
                yield break;
            }
#endif
            if(close) route.CloseSearch(); else route.UseFocused();
            yield return null;
        }
        static IEnumerator Straight(Func<Vector3> destination, Func<bool> done=null)
        {
            for(int i=0;i<550;i++)
            {
                if(done!=null && done())yield break;
                var gap=Horizontal(destination()-Feet);
                if(gap.magnitude<.055f && done==null)yield break;
                Step(Vector3.ClampMagnitude(gap*5f,1));
                if(i%12==0)yield return null;
            }
            throw new InvalidOperationException("Blocked on straight passage to "+destination().ToString("F2"));
        }
        static bool Sees(Vector3 eye,Collider[] colliders,Component fixture,out Vector3 aim)
        {
            aim=default;
            foreach(var c in colliders)
            {
                if(!c.enabled||!c.gameObject.activeInHierarchy)continue;
                var b=c.bounds;
                foreach(var p in new[]{b.center,b.ClosestPoint(eye),b.center+Vector3.up*(b.extents.y*.7f)})
                {
                    var ray=p-eye; float distance=ray.magnitude;
                    if(distance>walker.interactionDistance-.15f||distance<.015f)continue;
                    if(Physics.Raycast(eye,ray,out var hit,distance+.035f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide) &&
                        (hit.transform==fixture.transform || hit.transform.IsChildOf(fixture.transform)))
                    {aim=p;return true;}
                }
            }
            return false;
        }
        static IEnumerator OpenDoorAhead(Vector3 toward,Action didOpen)
        {
            var hitList=Physics.SphereCastAll(Feet+Vector3.up*1.03f,.18f,toward,1.30f,Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
            foreach(var h in hitList)
            {
                var hinge=h.collider.GetComponentInParent<PassDoor>();
                var slider=h.collider.GetComponentInParent<Car07SlidingDoor>();
                if(!(hinge && hinge.Interior && !hinge.TargetOpen) && !(slider && slider.TargetClosed))continue;
                Aim(h.collider.bounds.center);
                if(!PassDoorTarget.Resolve(walker.eyeCamera,walker.interactionDistance,out var found,out var slide) ||
                    found!=hinge || slide!=slider)continue;
                yield return Press();
                if(hinge && !hinge.TargetOpen || slider && slider.TargetClosed)continue;
                for(int f=0;f<70 && (hinge ? !hinge.IsOpen : slider.ClosedFraction>.03f);f++)
                { Step(Vector3.zero);yield return null; }
                didOpen();yield break;
            }
        }
        static IEnumerator Walk(Transform frame,float floor,Func<Vector3,bool> goal,string label,
            Func<Vector3,bool> navigationGoal=null)
        {
            action="walking to "+label;
            bool aboard=frame==route.consist;
            Vector3 lastWaypoint=Vector3.zero;
            for(int attempt=0;attempt<10;attempt++)
            {
                if(goal(Feet))yield break;
                Physics.SyncTransforms();
                var grid=aboard ? new PassWalkGrid(frame,route.PlayerBody,-2.55f,2.55f,-route.NoseDatum,
                    route.TrainLength-route.NoseDatum,floor,true) :
                    new PassWalkGrid(frame,route.PlayerBody,-9.15f,6.40f,-165f,102f,floor,false);
                List<Vector3> path;
                try {path=grid.Path(navigationGoal ?? goal);}
                catch(Exception e){throw new InvalidOperationException("No walking route to "+label,e);}
                bool replan=false;
                int tick=0;
                foreach(var p in path)
                {
                    lastWaypoint=p;
                    int stalled=0;
                    for(int f=0;f<1800;f++)
                    {
                        Vector3 gap=Horizontal(frame.TransformPoint(p)-Feet);
                        if(goal(Feet))yield break;
                        if(gap.sqrMagnitude<.078f*.078f && p!=path[path.Count-1])break;
                        if(gap.sqrMagnitude<.003f*.003f)break;
                        if(aboard && tick%4==0)
                        {
                            yield return OpenDoorAhead(gap.normalized,()=>replan=true);
                            if(replan)break;
                        }
                        Vector3 before=frame.InverseTransformPoint(Feet);
                        Step(Vector3.ClampMagnitude(gap*12f,1),sprint:gap.sqrMagnitude>2.4f*2.4f);
                        tick++;
                        float travelled=Horizontal(frame.InverseTransformPoint(Feet)-before).magnitude;
                        stalled=travelled<.001f ? stalled+1 : 0;
                        if(stalled>6){replan=true;break;}
                        if(tick%14==0)yield return null;
                    }
                    if(replan)break;
                }
                if(goal(Feet))yield break;
            }
            throw new InvalidOperationException("Capsule did not reach "+label+"; final planned point "+lastWaypoint.ToString("F3"));
        }
        static IEnumerator WalkPoint(Transform frame,Vector3 local,string label,float tolerance=.16f)
        {
            yield return Walk(frame,local.y,p=>Horizontal(frame.InverseTransformPoint(p)-local).sqrMagnitude<tolerance*tolerance,label);
        }
        static IEnumerator ClimbStation(bool up)
        {
            float from=up?0f:3.42f;
            float z=up?-3.08f:4.66f;
            yield return WalkPoint(house,new Vector3(-7.88f,from+.025f,z),"station staircase",.18f);
            yield return Straight(()=>house.TransformPoint(new Vector3(-7.88f,(up?3.42f:0)+.025f,up?4.68f:-3.10f)));
            Check(Mathf.Abs(house.InverseTransformPoint(Feet).y-(up?3.42f:0))<.19f,"staircase did not change floor");
        }
        static IEnumerator ApproachChestFront(PassLootContainer box, Transform frame, float floor)
        {
            // The latch and the two hand sockets are on the front. A ray from
            // the narrow end can identify a case but cannot operate its lock.
            var workSpot = new Vector3(0,0,box.otherDoor ? -.805f : -.74f);
            bool AtFront(Vector3 feet)
            {
                Vector3 local = box.transform.InverseTransformPoint(feet);
                return Mathf.Abs(local.x)<.22f && Mathf.Abs(local.z-workSpot.z)<.15f;
            }
            yield return Walk(frame,floor,AtFront,"lock face of "+box.name);
            yield return Straight(()=>box.transform.TransformPoint(workSpot));
            for(int i=0;i<12;i++) Step(Vector3.zero);
            Vector3 contact=box.transform.TransformPoint(box.gripLocal);
            Aim(contact);
            Physics.SyncTransforms();
            Debug.Log("CHEST APPROACH "+box.name+" at "+box.transform.InverseTransformPoint(Feet).ToString("F2"));
        }
#if ENABLE_INPUT_SYSTEM
        static IEnumerator CheckTravellingChestPosture(PassLootContainer box)
        {
            // Temporarily hand control back to the normal player Update so the
            // physical Ctrl press is read by the same walker as in a live game.
            walker.enabled=true;
            walker.ResumeLook();
            Check(walker.IsCrouched && route.WorkingChest,"a live front latch must crouch on F");
            yield return Press();
            int retained=route.ChestStrokes;
            Check(retained==1,"first click did not seat the latch");
            var feet=box.transform.InverseTransformPoint(Feet);
            foreach(var key in new[]{Key.RightCtrl,Key.LeftCtrl})
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                for(int i=0;i<13;i++)
                {
                    route.AdvanceExpedition(Dt);route.Advance(Dt);yield return null;
                }
                Check(walker.IsCrouched==(key==Key.LeftCtrl) && route.OpenContainer==box &&
                    route.WorkingChest && route.ChestStrokes==retained && box.HandsVisible,
                    "native Ctrl did not preserve the working latch at "+box.name);
                Vector3 shift=box.transform.InverseTransformPoint(Feet)-feet;
                Check(new Vector2(shift.x,shift.z).magnitude<.07f,"stance moved the passenger across the cabin");
            }
            walker.enabled=false;
            Debug.Log("TREASURY NATIVE CTRL: both postures worked the same lock and kept "+retained+" stroke at "+box.name);
        }
#endif
        static IEnumerator Collect(string id)
        {
            var item=Object.FindObjectsByType<PassPickup>(FindObjectsSortMode.None).FirstOrDefault(p=>p.itemId==id);
            Check(item,"No active candidate for "+id);
            bool aboard=item.transform.IsChildOf(route.consist);
            Check(route.Aboard==aboard,"Wrong place for "+item.siteId);
            var frame=aboard?route.consist:house;
            bool upstairs=!aboard && frame.InverseTransformPoint(item.transform.position).y>3.42f;
            if(!aboard && upstairs!=(frame.InverseTransformPoint(Feet).y>3.15f))yield return ClimbStation(upstairs);
            var container=item.GetComponentInParent<PassLootContainer>();
            Component fixture=container?(Component)container:item;
            var colliders=fixture.GetComponentsInChildren<Collider>();
            bool lowBerth=aboard && item.transform.IsChildOf(route.consist.Find("Car08")) && item.transform.position.y<2.10f;
            if(lowBerth)
            {
                // Approach a low valise with an eye line below the upper bunk.
                var aisle=frame.InverseTransformPoint(item.transform.position);
                aisle.x=.91f;aisle.y=1.405f;
                yield return WalkPoint(frame,aisle,"aisle beside the lower berth",.23f);
                walker.SetCrouched(true);
                for(int i=0;i<12;i++)Step(Vector3.zero);
            }
            // A previous low-berth search can leave the camera in its transition
            // to standing height. Sample its current offset at each waypoint.
            Vector3 EyeFrom(Vector3 feet) => feet + (Eye.position - Feet);
            bool Ready(Vector3 p)
            {
                return Horizontal(p-fixture.transform.position).sqrMagnitude<7 &&
                    Sees(EyeFrom(p),colliders,fixture,out _);
            }
            bool ClearView(Vector3 p)
            {
                if(!Ready(p))return false;
                var eye=EyeFrom(p);
                // Avoid a ray that just grazes a shelf or a bunk edge on one
                // exact lattice point. Shoes and head settle a few mm in play.
                return Sees(eye+frame.right*.035f,colliders,fixture,out _) &&
                    Sees(eye-frame.right*.035f,colliders,fixture,out _) &&
                    Sees(eye+Vector3.up*.025f,colliders,fixture,out _) &&
                    Sees(eye-Vector3.up*.025f,colliders,fixture,out _);
            }
            if(container && container.TapChest)
                yield return ApproachChestFront(container,frame,aboard?1.405f:upstairs?3.445f:.025f);
            else
            {
                for(int attempt=0;attempt<4;attempt++)
                {
                    yield return Walk(frame,aboard?1.405f:upstairs?3.445f:.025f,Ready,id+" at "+item.siteId,ClearView);
                    for(int i=0;i<14;i++)Step(Vector3.zero);
                    if(Ready(Feet))break;
                }
            }
            action="taking "+item.siteId;
            if(FleetImport.Argument("-extractionCase")=="portraits")
                yield return PhotographArticle(item);
            if (!Sees(Eye.position,colliders,fixture,out var where))
            {
                var target=container && container.TapChest ? container.transform.TransformPoint(container.gripLocal) : fixture.transform.position;
                var ray=target-Eye.position;
                var obstructions=Physics.RaycastAll(Eye.position,ray.normalized,ray.magnitude+.05f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide).OrderBy(hit=>hit.distance)
                    .Select(hit=>$"{hit.collider.name} at {hit.distance:F2} m");
                throw new InvalidOperationException($"No pickup sightline to {item.siteId}. Lock ray from {Eye.position:F2}: {string.Join(", ",obstructions)}");
            }
            Aim(container && container.TapChest ? container.transform.TransformPoint(container.gripLocal) : where);
            Physics.Raycast(Eye.position,Eye.forward,out var aimHit,walker.interactionDistance,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
            int previous=route.CarriedValue;
            yield return Press();
            if(container)
            {
                Check(route.OpenContainer==container,"F did not open "+fixture.name+"; aimed at "+
                    (aimHit.collider?aimHit.collider.name:"none")+" from "+Eye.position.ToString("F2")+"; "+route.Notice);
#if ENABLE_INPUT_SYSTEM
                if(container.TapChest && keyboard!=null && FleetImport.Argument("-extractionCase")=="treasuries")
                    yield return CheckTravellingChestPosture(container);
#endif
                double chestDeadline=Time.realtimeSinceStartupAsDouble+15;
                while(route.WorkingChest && Time.realtimeSinceStartupAsDouble<chestDeadline)
                {
                    if(!route.RevealingChest)yield return Press();
                    else { Step(Vector3.zero);yield return null; }
                }
                Check(!route.WorkingChest,"Chest did not finish its physical reveal");
                double unlockDeadline=Time.realtimeSinceStartupAsDouble+10;
                while(route.PickingLock && Time.realtimeSinceStartupAsDouble<unlockDeadline)
                {
                    Step(Vector3.zero);
                    if(route.LockPosition>.56f&&route.LockPosition<.71f)yield return Press();
                    yield return null;
                }
                Check(!route.PickingLock,"lock never released");
                for(int i=0;i<20;i++){Step(Vector3.zero);yield return null;}
                int index=Enumerable.Range(0,route.SearchCount).First(i=>route.SearchItem(i)==item);
                route.SelectSearchItem(index);
                yield return Press();
                if(route.SearchOpen)yield return Press(true);
            }
            Check(Enumerable.Range(0,8).Any(i=>route.Slot(i)==id) && route.CarriedValue==previous+PassLootCatalogue.Value(id),
                "Pickup did not update the actual pack: "+id);
            if(lowBerth)walker.SetCrouched(false);
            Debug.Log($"EXTRACTION ITEM {item.siteId}: {id}, pack {route.CarriedValue}, remaining {route.RunSeconds:F1}s");
        }
        static IEnumerator UseDoor(PassDoor door)
        {
            bool targeted=false;
            var pulls=door.car.GetComponentsInChildren<PassDoorTarget>().Where(t=>t.hinge==door).Select(t=>t.GetComponent<Collider>());
            var targets=pulls.Concat(door.GetComponentsInChildren<Collider>()).ToArray();
            foreach(var t in targets)
            {
                foreach(var p in new[]{t.bounds.center,t.ClosestPoint(Eye.position),door.threshold.position})
                {
                    Aim(p);
                    if(!PassDoorTarget.Resolve(walker.eyeCamera,walker.interactionDistance,out var found,out _) || found!=door)continue;
                    targeted=true;break;
                }
                if(targeted)break;
            }
            Check(targeted,$"could not sight {door.name} ({door.threshold.position:F2}) from {Eye.position:F2}");
            if(!door.TargetOpen)yield return Press();
            Check(door.TargetOpen,"F did not open "+door.name+"; "+route.Notice);
            for(int i=0;i<70&&!door.IsOpen;i++){Step(Vector3.zero);yield return null;}
            Check(door.IsOpen,"Door swing obstructed "+door.name);
        }
        static IEnumerator WaitFor(Func<bool> ready)
        {
            for(int i=0;i<22000&&!ready();i++)
            {
                Step(Vector3.zero);
                if(route.Terminal)break;
                if(i%60==0)yield return null;
            }
            Check(ready(),"missed "+action);
        }
        static IEnumerator PlatformExit(int car=7)
        {
            var door=route.consist.Find($"Car{car:00}").GetComponentsInChildren<PassDoor>()
                .Where(d=>d.kind==PassDoorKind.Passenger && d.ExteriorSide<0)
                .OrderBy(d=>Mathf.Abs(-d.threshold.position.z+PassJourney.Stop-route.Distance-PassFittings.StationHouseChainage)).First();
            var pos=route.consist.InverseTransformPoint(door.threshold.position);
            // Stand beside the sill before the physical door crossing. Allow
            // the width of a navigation cell plus the capsule's settling skin.
            yield return WalkPoint(route.consist,new Vector3(-.94f,1.405f,pos.z),$"{car:00} platform vestibule",.22f);
            action="waiting for Fellbridge";yield return WaitFor(()=>route.Phase==JourneyPhase.Dwell);
            yield return UseDoor(door);
            yield return Straight(()=>new Vector3(PassJourney.PlatformEdge-.54f,1.405f,door.threshold.position.z));
            Check(!route.Aboard,"still parented to train on platform");
        }
        static IEnumerator EnterGate(PassPortal gate,Func<bool> arrived)
        {
            action="walking into "+gate.name;
            yield return Straight(()=>gate.threshold.bounds.center,()=>
                arrived() || (route.TryEnterPortal(gate) && arrived()));
            Check(arrived(),"gate did not reach destination");
        }
        static IEnumerator ReturnFromStation()
        {
            if(house.InverseTransformPoint(Feet).y>3.15f)yield return ClimbStation(false);
            var p=house.InverseTransformPoint(route.stationGate.threshold.bounds.center);p.y=.025f;
            yield return WalkPoint(house,p,"booking hall bookcase",.55f);
            yield return EnterGate(route.stationGate,()=>route.InHub);
        }
        static IEnumerator TailJourney(bool stay)
        {
            var tail=route.consist.GetComponentsInChildren<PassDoor>().Single(d=>d.kind==PassDoorKind.Escape);
            var q=route.consist.InverseTransformPoint(tail.threshold.position);
            yield return WalkPoint(route.consist,new Vector3(0,1.405f,q.z-.96f),"09 rear vestibule");
            action="waiting at the tail";
            if(stay){ yield return WaitFor(()=>route.Terminal); yield break; }
            yield return WaitFor(()=>route.EscapeReady);
            yield return UseDoor(tail);
            yield return Straight(()=>route.escapeLaunch.position);
            Aim(Feet+route.consist.forward*4+Vector3.up);
            for(int i=0;i<8&&!route.Airborne;i++)Step(route.consist.forward,true);
            Check(route.Airborne,"Space did not launch");
            Step(Vector3.zero);Step(Vector3.zero,true);
            Check(route.CanopyWasDeployed,"second Space did not deploy");
            yield return WaitFor(()=>route.AtLanding);
            yield return EnterGate(route.returnGate,()=>route.InHub);
        }
        static IEnumerator Cases()
        {
            route=Object.FindAnyObjectByType<PassJourney>();walker=route.walker;
            house=GameObject.Find("Fellbridge Station/Fellbridge station house").transform;
            Time.captureFramerate=30;
            route.ManualClock=true;walker.enabled=false;
#if ENABLE_INPUT_SYSTEM
            if(Environment.GetCommandLineArgs().Contains("-extractionNativeKeys"))
            {
                EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                keyboard=InputSystem.AddDevice<Keyboard>();
                for(int i=0;i<3;i++)yield return null;
                Check(Application.isFocused,"the native input editor requires Game view focus");
            }
#endif
            string only=FleetImport.Argument("-extractionCase");
            if(only=="detective")
            {
                yield return DetectivePortraits();
                yield break;
            }
            if(only=="chestPhotos")
            {
                yield return WalkChestPhotos();
                yield break;
            }
            if(only=="portraits")
            {
                yield return CabinetPortraits();
                yield break;
            }
            if(only=="treasuries")
            {
                yield return TreasuryJourneys();
                yield break;
            }
            if(only=="passage"||only=="landing")
            {
                route.NewGame();
                yield return EnterGate(route.hubGate,()=>route.Aboard);
                yield return Collect("parachute");
                if(only=="passage") {yield return PlatformExit();yield return ReturnFromStation();}
                else yield return TailJourney(false);
                yield break;
            }
            foreach(string name in new[]{"station","canyon","crash","shortfall"})
            {
                if(only!=null&&only!=name)continue;
                action="starting "+name;
                checkTimetable=false;
                route.NewGame();
                // Exercise a reported placement with genuine new-game rolls.
                string wanted=FleetImport.Argument("-extractionRequireSite");
                if(!string.IsNullOrEmpty(wanted))
                {
                    var requiredSites=wanted.Split('|');
                    bool HasSite()
                    {
                        var active=Object.FindObjectsByType<PassPickup>(FindObjectsSortMode.None);
                        return requiredSites.All(site=>active.Any(p=>p.siteId==site));
                    }
                    for(int roll=0;!HasSite() && roll<256;roll++)route.NewGame();
                    Check(HasSite(),"Could not roll requested placement "+wanted);
                }
                yield return EnterGate(route.hubGate,()=>route.Aboard);
                clockAtBoarding=route.RunSeconds;checkTimetable=true;
                var manifest=name=="shortfall" ? new[]{"cameo","intaglio_seal","astronomical_globe"} :
                    new[]{"pocket_watch","crystal_cup","singing_bird_box"};
                foreach(string id in manifest)yield return Collect(id);
                if(name=="station"||name=="shortfall")
                {
                    yield return PlatformExit(name=="shortfall"?5:7);
                    yield return Collect("portrait_snuffbox");yield return Collect("emerald_tiara");
                    yield return ReturnFromStation();
                }
                else
                {
                    foreach(string id in new[]{"intaglio_seal","pearl_aigrette","rose_egg","parachute"})yield return Collect(id);
                    yield return TailJourney(name=="crash");
                    if(name=="crash")
                    {
                        Check(route.Phase==JourneyPhase.Crashed && route.CarriedValue==248000 && route.Health==0,"wreck bypassed death");
                        Debug.Log("EXTRACTION RESULT: a 248000-value rider died in the wreck"); continue;
                    }
                }
                int target=name=="shortfall"?239999:name=="station"?242000:248000;
                yield return SettleAtHome(name,target);
            }
        }

        static IEnumerator SettleAtHome(string name,int target)
        {
            Check(route.CarriedValue==target&&route.InsideCottage&&route.RunSeconds>0,"arrived with incorrect value or time");
            for(int i=0;i<7;i++){Step(Vector3.zero);yield return null;}
            var button=Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Button>("endGame");
            using(var submit=NavigationSubmitEvent.GetPooled()){submit.target=button;button.SendEvent(submit);}
            Check(route.HasSettlement&&route.SettledSuccessfully==(target>=PassJourney.TargetValue),"wrong settlement");
            Debug.Log($"EXTRACTION RESULT: {name}, {target}, remaining {route.RunSeconds:F1}s, success={route.SettledSuccessfully}");
        }
    }
}
