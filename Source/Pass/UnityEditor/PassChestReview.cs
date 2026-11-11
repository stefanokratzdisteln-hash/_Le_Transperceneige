using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Winterbourne.Pass;
using Object=UnityEngine.Object;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Winterbourne.Editor
{
    // An unsaved interaction bench exercises fresh key/mouse edges on the authored
    // meshes. The route, timers, collision, search and pickup code run normally.
    [InitializeOnLoad]
    internal static class PassChestReview
    {
        const string Flag="Winterbourne.ChestWorkQA";
        static readonly Stack<IEnumerator> steps=new Stack<IEnumerator>();
        static int last=-1;
        static PassJourney game;
#if ENABLE_INPUT_SYSTEM
        static Keyboard keys;
        static Mouse mouse;
#endif
        static PassChestReview()=>EditorApplication.update+=Tick;

        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            var route=Object.FindAnyObjectByType<PassJourney>();
            var bench=new GameObject("Parcel mechanism review").transform;
            bench.SetParent(route.consist,false);
            bench.localPosition=new Vector3(0,12,0);
            var floor=bench.gameObject.AddComponent<BoxCollider>();
            floor.center=new Vector3(1,-.05f,-.6f);
            floor.size=new Vector3(4f,.10f,3.6f);
            using(var fittings=new PassLootFixtures())
            {
                foreach(bool gold in new[]{false,true})
                {
                    string style=gold ? "treasuryChest":"parcelChest";
                    var inside=fittings.Case(bench,"Review "+style,new Vector3(gold ? 2 : 0,0,0),0,style);
                    PassLootFixtures.Item(inside,gold?"cameo":"scent_flacon","Review "+style+" articles","",Vector3.zero);
                }
            }
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            SessionState.SetBool(Flag,true);
            SessionState.SetString(Flag+".begin",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false))return;
            try
            {
                if((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Flag+".begin","")).ToUniversalTime()).TotalMinutes>4)
                    throw new TimeoutException("Timed out during chest work review");
                if(EditorApplication.isPaused)EditorApplication.isPaused=false;
                EditorApplication.QueuePlayerLoopUpdate();
                if(!EditorApplication.isPlaying || Time.frameCount<6 || last==Time.frameCount)return;
                last=Time.frameCount;
                if(steps.Count==0)steps.Push(CheckTapChests());
                while(steps.Count>0)
                {
                    var top=steps.Peek();
                    if(!top.MoveNext()){steps.Pop();continue;}
                    if(top.Current is IEnumerator child){steps.Push(child);continue;}
                    return;
                }
                SessionState.EraseBool(Flag);EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.EraseBool(Flag);Debug.LogException(e);EditorApplication.Exit(1);}
        }

        static void Check(bool success,string message)=>PassBuild.Check(success,"CHEST WORK: "+message);

        static IEnumerator CheckTapChests()
        {
#if ENABLE_INPUT_SYSTEM
            keys=InputSystem.AddDevice<Keyboard>();
            mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            game=Object.FindAnyObjectByType<PassJourney>();
            PassReview.EnterFromRoom(game);
            yield return null;yield return null;
            var panel=GameObject.Find("Parcel mechanism review").transform;
            foreach(bool gold in new[]{false,true})
            {
                string style=gold ? "treasuryChest":"parcelChest";
                var box=panel.Find("Review "+style).GetComponent<PassLootContainer>();
                yield return CheckNativeStance(box);
                var feet=box.transform.TransformPoint(new Vector3(0,.003f,gold?-.805f:-.74f));
                game.walker.Place(game.consist,game.consist.InverseTransformPoint(feet),Quaternion.identity,0);
                game.walker.ResumeLook();Physics.SyncTransforms();
                yield return AimLock(box);
                int old=game.CarriedValue;
                yield return Tap(Key.F,false);
                Check(game.WorkingChest && game.ChestStrokes==0 && !game.InventoryOpen && box.handsRoot.activeSelf,"first F did not engage "+style);
                float run=game.RunSeconds;
                yield return Tap(Key.F,true,8);
                Check(game.ChestStrokes==1 && !game.InventoryOpen,"held or simultaneous F/mouse stroke count="+game.ChestStrokes+", working="+game.WorkingChest);
                yield return Tap(Key.Escape,false);
                Check(!game.SearchOpen && !box.Unlocked && !box.handsRoot.activeSelf,"Esc left a live mechanism");
                yield return new WaitForSecondsRealtime(.24f);
                yield return AimLock(box);
                yield return Tap(Key.F,false);
                yield return Tap(Key.None,true);
                Check(game.ChestStrokes==1,"a mouse-only tap must advance after cancellation");
                game.walker.transform.position+=box.transform.right*.35f;
                yield return null;yield return null;
                Check(!game.WorkingChest && !box.Unlocked,"walking away left progress");
                game.walker.transform.position-=box.transform.right*.35f;
                Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(.24f);
                yield return AimLock(box);
                yield return Tap(Key.F,false);
                Check(game.ChestStrokes==0 && game.WorkingChest,"approach must begin again at zero");
                yield return new WaitForSecondsRealtime(.26f);
                Transform lockedPlate=box.lockWork;
                Vector3 lockSeat=lockedPlate.localPosition;
                for(int i=0;i<box.tapsRequired;i++)
                {
                    yield return Tap(i%2==0?Key.F:Key.None,i%2==1);
                    if(i+1<box.tapsRequired)
                        Check(!box.TargetOpen && game.SearchCount==0 && game.ChestStrokes==i+1,"an early stroke opened the case");
                    if(i==1) Screenshot(style+"-working");
                    if(i+1<box.tapsRequired) yield return new WaitForSecondsRealtime(.12f);
                }
                Check(box.TargetOpen && box.Unlocked && !game.InventoryOpen && game.CarriedValue==old,"final tap took the treasure");
                double began=Time.realtimeSinceStartupAsDouble;
                yield return Tap(Key.F,false);
                yield return Tap(Key.H,true);
                Check(game.CarriedValue==old,"reveal accepted a pickup click");
                bool sawReveal=false;
                while(!game.InventoryOpen && Time.realtimeSinceStartupAsDouble-began<4)
                {
                    if(!sawReveal && box.Openness>.995f)
                    {
                        Screenshot(style+"-revealed");sawReveal=true;
                    }
                    yield return null;
                }
                Check(sawReveal,"the entire reveal was obscured by the folio");
                Check(game.InventoryOpen && game.SearchCount==1 && game.CarriedValue==old,"contents reveal did not settle");
                Check(Time.realtimeSinceStartupAsDouble-began>.53,"reveal too short");
                Check(box.Openness>.99f && box.lockWork.localPosition==lockSeat,"hinge/lock pivot drift");
                if(gold) Check(Quaternion.Angle(Quaternion.identity,box.otherDoor.localRotation)>95 &&
                               Quaternion.Angle(Quaternion.identity,box.movingPart.localRotation)>95,"paired leaves did not open");
                else Check(Quaternion.Angle(Quaternion.identity,box.movingPart.localRotation)>100,"rear hinged lid did not open");
                Check(run>game.RunSeconds+.5f,"world clock froze during work");
                yield return Tap(Key.F,false);
                Check(game.CarriedValue>old && game.SearchCount==0,"new F must collect exactly once");
                yield return Tap(Key.Escape,false);
                Check(box.TargetOpen && !game.InventoryOpen,"completed chest closed on dismissal");
                // Aim at the fixed central mounting (or modest wooden front), since its contents were removed.
                yield return AimPoint(box.transform.TransformPoint(gold?box.gripLocal:new Vector3(0,.23f,-.198f)));
                yield return Tap(Key.F,false);
                Check(game.InventoryOpen && !game.WorkingChest && game.SearchCount==0,"previously opened case required new work");
                yield return Tap(Key.Escape,false);
                if(!gold)
                {
                    // Use the real arrival checkpoint and its normal gameplay trigger.
                    // The independent 12-minute clock continues in real time.
                    while(game.Distance<885)game.Advance(.2f);
                }
                Debug.Log("CHEST VERIFIED "+style+": simultaneous and independent native F/LMB, walk, Esc, fresh press, closed/open bounds, timed reveal, one pickup and reopening");
            }
            float beforeRetry=game.RunSeconds;
            game.Retry();
            var wood=panel.Find("Review parcelChest").GetComponent<PassLootContainer>();
            var goldCase=panel.Find("Review treasuryChest").GetComponent<PassLootContainer>();
            Check(wood.Unlocked && wood.TargetOpen && wood.Openness>.99f && !goldCase.Unlocked &&
                  goldCase.Openness==0 && !game.WorkingChest,"checkpoint failed to restore open vs closed chest");
            Check(game.CarriedValue==PassLootCatalogue.Value("scent_flacon") && !game.InventoryOpen &&
                  Mathf.Abs(beforeRetry-game.RunSeconds)<.10f,"checkpoint duplicated an item or rewound the deadline");
            game.NewGame();
            Check(!wood.Unlocked && !goldCase.Unlocked && !wood.TargetOpen && !goldCase.TargetOpen &&
                  game.CarriedValue==0,"new game retained prior chest or item");
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(keys);
            Debug.Log("CHEST WORK PASS");
#else
            throw new InvalidOperationException("Run this native control review with Unity Input System");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static IEnumerator AimLock(PassLootContainer box) => AimPoint(box.transform.TransformPoint(box.gripLocal));
        static IEnumerator AimPoint(Vector3 target)
        {
            var walker=game.walker;
            var camera=walker.eyeCamera.transform;
            Vector3 direction=(target-camera.position).normalized;
            float yaw=Vector3.SignedAngle(walker.transform.forward,Vector3.ProjectOnPlane(direction,Vector3.up),Vector3.up);
            float targetPitch=-Mathf.Asin(direction.y)*Mathf.Rad2Deg;
            var delta=new Vector2(yaw,walker.ViewPitch-targetPitch)/walker.lookSensitivity;
            InputSystem.QueueDeltaStateEvent(mouse.delta,delta);
            yield return new WaitForSecondsRealtime(.32f);
        }

        static IEnumerator CheckNativeStance(PassLootContainer box)
        {
            var walker=game.walker;
            var camera=walker.eyeCamera;
            var collider=walker.GetComponent<CharacterController>();
            float originalView=camera.fieldOfView;
            // Place the live controller on the bench's floor for the approach;
            // the actual character then walks the remaining distance with W.
            walker.enabled=true;
            walker.SetCrouched(false);
            var far=box.transform.TransformPoint(new Vector3(0,.003f,-1.43f));
            walker.Place(game.consist,game.consist.InverseTransformPoint(far),
                Quaternion.Inverse(game.consist.rotation)*box.transform.rotation,0);
            walker.ResumeLook();
            yield return AimLock(box);
            Check(walker.HasMouse,"Game view failed to capture the mechanical review");
            yield return Tap(Key.F,false);
            Check(!game.WorkingChest && !box.HandsVisible,"far F stretched the arms to the case");
            float targetZ=box.otherDoor?-.86f:-.8f;
            double limit=Time.realtimeSinceStartupAsDouble+3;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));
            while(box.transform.InverseTransformPoint(walker.transform.position).z<targetZ &&
                Time.realtimeSinceStartupAsDouble<limit)yield return null;
            InputSystem.QueueStateEvent(keys,new KeyboardState());
            yield return new WaitForSecondsRealtime(.20f);
            yield return AimLock(box);
            Vector3 initialFeet=box.transform.InverseTransformPoint(walker.transform.position);
            float standingEye=camera.transform.localPosition.y;
            bool arrivedCrouched=box.otherDoor!=null;
            if(arrivedCrouched)
            {
                yield return Tap(Key.LeftCtrl,false);
                yield return new WaitForSecondsRealtime(.25f);
                yield return AimLock(box);
                Check(walker.IsCrouched,"Ctrl on the approach did not lower the player");
            }
            yield return Tap(Key.F,false);
            yield return new WaitForSecondsRealtime(.42f);
            Check(game.WorkingChest && game.OpenContainer==box && walker.IsCrouched && walker.CrouchSelected &&
                collider.height<walker.StandingHeight-.60f && camera.transform.localPosition.y<standingEye-.60f,
                "F must put the live player in a full crouch at "+initialFeet.ToString("F3")+" "+game.Notice);
            Check(Mathf.Abs(camera.fieldOfView-originalView)>.2f,"inspection kept the ordinary field of view");
            float lowEye=camera.transform.localPosition.y;
            float halfWidth=box.otherDoor?.56f:.34f;
            var viewportLeft=camera.WorldToViewportPoint(box.transform.TransformPoint(new Vector3(-halfWidth,.37f,-.225f)));
            var viewportRight=camera.WorldToViewportPoint(box.transform.TransformPoint(new Vector3(halfWidth,.37f,-.225f)));
            float span=Mathf.Abs(viewportRight.x-viewportLeft.x);
            float downward=-Mathf.Asin(camera.transform.forward.y)*Mathf.Rad2Deg;
            Debug.Log($"CHEST OPTICS {box.name}: eye {lowEye:F3} m; panel width {span:P1}; pitch {downward:F1}; FOV {camera.fieldOfView:F1}; feet {initialFeet:F3}");
            yield return Tap(Key.F,false);
            yield return new WaitForSecondsRealtime(.21f);
            AssertContact(box,initialFeet,"crouched");
            var crouchedElbow=box.transform.InverseTransformPoint(box.leftSleeve.position-box.leftSleeve.forward*.25f);
            yield return Tap(Key.RightCtrl,false);
            yield return new WaitForSecondsRealtime(.34f);
            Check(!walker.IsCrouched && Mathf.Abs(collider.height-walker.StandingHeight)<.01f &&
                Mathf.Abs(camera.transform.localPosition.y-standingEye)<.02f && game.ChestStrokes==1,
                "standing mid-stroke cancelled the lock or left the eye down");
            AssertContact(box,initialFeet,"standing");
            var standingElbow=box.transform.InverseTransformPoint(box.leftSleeve.position-box.leftSleeve.forward*.25f);
            Check(Vector3.Distance(crouchedElbow,standingElbow)>.012f,"changing stance left the elbow frozen");
            yield return Tap(Key.None,true);
            yield return Tap(Key.LeftCtrl,false);
            yield return new WaitForSecondsRealtime(.34f);
            Check(walker.IsCrouched && game.ChestStrokes==2,"Ctrl lost the second completed stroke");
            AssertContact(box,initialFeet,"crouched again");
            yield return Tap(Key.Escape,false);
            yield return new WaitForSecondsRealtime(.55f);
            Check(!game.WorkingChest && !box.HandsVisible && !box.Unlocked && walker.IsCrouched==arrivedCrouched &&
                Mathf.Abs(camera.fieldOfView-originalView)<.10f,"dismissal did not restore arrival stance and view");
            Debug.Log($"NATIVE CHEST POSTURE {box.name}: W approached {initialFeet:F3}; F crouched; physical eye " +
                $"{standingEye:F2} m standing, {lowEye:F2} m crouched. " +
                $"Left and right Ctrl preserved 1/2 strokes, elbow changed {Vector3.Distance(crouchedElbow,standingElbow):F3} m; Esc restored view.");
            walker.SetCrouched(false);
            yield return new WaitForSecondsRealtime(.25f);
        }

        static void AssertContact(PassLootContainer box,Vector3 feet,string posture)
        {
            Check(game.WorkingChest && box.HandsVisible && !game.InventoryOpen,"hands dropped while "+posture);
            var grip=box.transform.TransformPoint(box.gripLocal);
            float left=Vector3.Distance(box.leftGlove.position,grip);
            float right=Vector3.Distance(box.rightGlove.position,grip);
            Check(left<.30f && right<.30f,"hands missed the latch while "+posture+" ("+left+", "+right+")");
            var projection=game.walker.eyeCamera.WorldToViewportPoint(grip);
            Check(projection.z>0 && projection.x>.28f && projection.x<.72f && projection.y>.16f && projection.y<.81f,
                "latch left the working view while "+posture+": "+projection);
            var at=box.transform.InverseTransformPoint(game.walker.transform.position);
            Check(new Vector2(at.x-feet.x,at.z-feet.z).magnitude<.065f,"inspection moved the player's feet");
        }

        static IEnumerator Tap(Key key,bool left,int frames=2)
        {
            InputSystem.QueueStateEvent(keys,key==Key.None ? new KeyboardState() : new KeyboardState(key));
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left,left));
            for(int i=0;i<frames;i++)yield return null;
            InputSystem.QueueStateEvent(keys,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());
            yield return null;yield return null;
        }
#endif
        static void Screenshot(string name)
        {
            string dir=FleetImport.Argument("-chestReviewPhotos");
            if(!string.IsNullOrEmpty(dir)){Directory.CreateDirectory(dir);ScreenCapture.CaptureScreenshot(Path.Combine(dir,name+".png"));}
        }
    }
}
