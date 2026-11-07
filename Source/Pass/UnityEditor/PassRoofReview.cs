using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Winterbourne.Pass;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    // -executeMethod Winterbourne.Editor.PassRoofReview.Run [-roofNativeKeys] [-portalImages <folder>]
    [InitializeOnLoad]
    public static class PassRoofReview
    {
        const string Job="Winterbourne.RoofReview";
        const float Dt=1f/30;
        static readonly Stack<IEnumerator> tasks=new Stack<IEnumerator>();
        static PassJourney route;
        static int lastFrame;
        static bool native;
#if ENABLE_INPUT_SYSTEM
        static Keyboard keyboard;
#endif
        static PassRoofReview(){ EditorApplication.update+=Tick; }

        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            SessionState.SetBool(Job,true);
            SessionState.SetString(Job+"start",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        static void Expect(bool ok,string note) => PassBuild.Check(ok,"ROOF: "+note);
        static void Tick()
        {
            if (!SessionState.GetBool(Job,false)) return;
            try
            {
                Expect((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Job+"start","")).ToUniversalTime()).TotalMinutes<6,"timed out");
                if (EditorApplication.isPaused) EditorApplication.isPaused=false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || lastFrame==Time.frameCount || Time.frameCount<6) return;
                lastFrame=Time.frameCount;
                if (tasks.Count==0) tasks.Push(Cases());
                while (tasks.Count>0)
                {
                    var t=tasks.Peek();
                    if (!t.MoveNext()) { tasks.Pop(); continue; }
                    if (t.Current is IEnumerator inner) { tasks.Push(inner); continue; }
                    return;
                }
                SessionState.EraseBool(Job);
                Debug.Log("ROOF PASS: two working hatches, moving roof, body-height tunnel impact, retry and full crouched passage");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { SessionState.EraseBool(Job); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static Vector3 Feet => route.walker.transform.position;
        static void Pose(Vector3 feet,Vector3 facing)
        {
            float yaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;
            route.walker.Place(route.consist,route.consist.InverseTransformPoint(feet),Quaternion.Euler(0,yaw,0),
                -Mathf.Asin(facing.normalized.y)*Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }

        static IEnumerator Press(string button)
        {
#if ENABLE_INPUT_SYSTEM
            if (native)
            {
                var key=button=="E" ? Key.E : button=="LeftCtrl" ? Key.LeftCtrl : Key.RightCtrl;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return null; yield return null;
                yield break;
            }
#endif
            if (button=="E") route.walker.useOverride();
            else route.walker.ToggleCrouch();
            yield return null;
        }

        static IEnumerator Traverse(PassRoofLadder ladder,bool down,bool crouchOnLadder=false)
        {
            var end=down ? ladder.lowerLanding : ladder.roofLanding;
            var target=down ? ladder.upperRung : ladder.lowerRung;
            var facing=target.position-Feet;
            facing.y=down ? -.9f : .12f;
            Pose(Feet,facing);
            if (native) Expect(route.ActionPrompt()==PassText.Get(down ? "roof.down" : "roof.up"),"E prompt does not match target");
            yield return Press("E");
            Expect(route.ClimbingRoofLadder,"E did not mount the ladder "+ladder.transform.parent.parent.name+" feet="+Feet);
            float initialDistance=route.Distance;
            for (int i=0;i<150 && route.ClimbingRoofLadder;i++)
            {
                Vector3 previous=route.walker.transform.localPosition;
                route.Advance(Dt);
                Expect(Vector3.Distance(previous,route.walker.transform.localPosition)<.095f,"jump in climbing position");
                if (crouchOnLadder && i==15)
                {
                    yield return Press("LeftCtrl");
                    Expect(route.walker.CrouchSelected,"could not crouch partway up the ladder");
                }
                if (i%15==0) yield return null;
            }
            Expect(!route.ClimbingRoofLadder && route.Aboard && route.Distance>initialDistance+10 &&
                Vector3.Distance(end.position,Feet)<.23f,$"did not reach {end.name}: expected {end.position:F3}, got {Feet:F3}, local={ladder.transform.InverseTransformPoint(Feet):F3}, notice={route.Notice}");
            Expect(route.OnRoof==!down,"roof/interior support mismatch");
            Debug.Log($"ROOF LADDER: {ladder.transform.parent.parent.name}, {(down ? "down" : "up")}, feet {ladder.transform.InverseTransformPoint(Feet):F2}");
        }

        static void RideUntil(float riderChainage)
        {
            for (int i=0;i<1400 && -Feet.z<riderChainage && !route.Terminal;i++) route.Advance(.12f);
        }

        static IEnumerator CheckLabel(string name,string message)
        {
            var label=Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Label>(name);
            string expected=PassText.Get(message);
            double deadline=EditorApplication.timeSinceStartup+3;
            while (EditorApplication.timeSinceStartup<deadline && (!label.visible || label.text!=expected)) yield return null;
            Expect(label.visible && label.text==expected,name+" HUD expected '"+expected+"', saw '"+label.text+"'");
        }

        static void WalkDeck(PassRoofLadder ladder)
        {
            Pose(Feet,-ladder.transform.forward);
            float initial=ladder.transform.InverseTransformPoint(Feet).z;
            for (int i=0;i<30;i++)
            {
                route.Advance(Dt);
                route.walker.MoveCharacter(Vector2.up,true,Dt);
                route.ReconcileBoarding();
            }
            float distance=initial-ladder.transform.InverseTransformPoint(Feet).z;
            Expect(distance>2 && route.Aboard && route.OnRoof,"roof walk lost support or blocked");
            Pose(Feet,ladder.transform.forward);
            for (int i=0;i<110 && ladder.transform.InverseTransformPoint(Feet).z<initial-.10f;i++)
            {
                route.Advance(Dt);
                route.walker.MoveCharacter(Vector2.up,false,Dt);
                route.ReconcileBoarding();
            }
            Expect(Vector3.Distance(Feet,ladder.roofLanding.position)<.25f,"could not walk back to hatch");
        }

        static void ApproachFromAisle(PassRoofLadder ladder)
        {
            Vector3 target=ladder.lowerLanding.position;
            Vector3 start=target-ladder.transform.right*.70f+ladder.transform.forward*.85f;
            Pose(start,target-start);
            for (int i=0;i<60;i++)
            {
                Vector3 offset=Vector3.ProjectOnPlane(ladder.lowerLanding.position-Feet,Vector3.up);
                if (offset.magnitude<.095f) return;
                Vector3 input=route.walker.transform.InverseTransformDirection(offset.normalized);
                route.Advance(Dt);
                route.walker.MoveCharacter(new Vector2(input.x,input.z),false,Dt);
                route.ReconcileBoarding();
            }
            Expect(false,"aisle-to-ladder route obstructed in "+ladder.transform.parent.parent.name);
        }

        static IEnumerator Photograph(string name)
        {
            if (string.IsNullOrEmpty(FleetImport.Argument("-portalImages"))) yield break;
            var eye=route.walker.eyeCamera;
            eye.enabled=true;
            // GPU Resident Drawer uploads moving renderer transforms during ordinary
            // frames. Let the main gameplay camera settle after accelerated travel.
            for (int i=0;i<6;i++) yield return null;
            PassPortalReview.Photo(name,eye);
            eye.enabled=false;
        }

        static IEnumerator Cases()
        {
            Time.captureDeltaTime=Dt;
            route=Object.FindFirstObjectByType<PassJourney>();
            route.ManualClock=true;
            native=Array.IndexOf(Environment.GetCommandLineArgs(),"-roofNativeKeys")>=0;
#if ENABLE_INPUT_SYSTEM
            if (native)
            {
                EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
                keyboard=InputSystem.AddDevice<Keyboard>();
                var mouse=InputSystem.AddDevice<Mouse>();
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState());
                Expect(route.walker.HasMouse,"native mouse capture");
            }
#else
            Expect(!native,"native run requires Input System");
#endif
            route.walker.enabled=native;
            route.walker.eyeCamera.enabled=false;
            PassReview.EnterFromRoom(route);
            var ladders=route.consist.GetComponentsInChildren<PassRoofLadder>();
            Expect(ladders.Length==2,"expected one ladder in each van");
            foreach (var l in ladders)
            {
                ApproachFromAisle(l);
                yield return Traverse(l,false);
                for (int i=0;i<24;i++) route.walker.MoveCharacter(Vector2.zero,false,Dt);
                Expect(route.PlayerBody.isGrounded,"running board did not support the capsule");
                WalkDeck(l);
                yield return Traverse(l,true);
            }
            var rear=route.consist.Find("Car09").GetComponentInChildren<PassRoofLadder>();
            // Photograph the fitted ladder from the approaching aisle at standing eye height.
            Vector3 approach=rear.lowerLanding.position+rear.transform.forward*1.1f-rear.transform.right*.46f;
            Pose(approach,Vector3.Lerp(rear.lowerRung.position,rear.upperRung.position,.66f)-
                (approach+Vector3.up*route.walker.eyeCamera.transform.localPosition.y));
            yield return Photograph("Van09RoofLadder");
            Pose(rear.lowerLanding.position,rear.lowerRung.position-rear.lowerLanding.position);
            yield return Traverse(rear,false,true);
            RideUntil(457);
            Expect(route.TunnelAhead && route.Distance>480 && !route.InRoofTunnel,"warning used locomotive mileage");
            Pose(Feet,-route.consist.forward+Vector3.up*.01f);
            yield return Photograph("RooftopTunnelApproach");
            RideUntil(598);
            Expect(route.InRoofTunnel && route.Health==100,"crouched entry failed");
            yield return Press("RightCtrl");
            Expect(route.walker.IsCrouched && route.PlayerBody.height<1.1f,"raised head into bore");
            RideUntil(786);
            Expect(!route.Terminal && !route.InRoofTunnel && route.Health==100,"crouched exit failed");
            yield return Press("RightCtrl");
            Expect(!route.walker.IsCrouched,"could not stand after tunnel");
            yield return Traverse(rear,true);
            route.NewGame();PassReview.EnterFromRoom(route);
            var front=route.consist.Find("Car03").GetComponentInChildren<PassRoofLadder>();
            Pose(front.lowerLanding.position,front.lowerRung.position-front.lowerLanding.position);
            yield return Traverse(front,false);
            RideUntil(430);
            yield return CheckLabel("task","roof.warning");
            RideUntil(490);
            Expect(route.Phase==JourneyPhase.TunnelImpact && route.Health==0 &&
                Mathf.Abs(-Feet.z-480)<1.55f,"standing impact occurred at wrong rider position "+Feet+"; phase="+route.Phase);
            Debug.Log($"ROOF IMPACT: rider {-Feet.z:F2}m, locomotive {route.Distance:F2}m");
            yield return CheckLabel("reason","roof.impact");
            route.Retry();
            Expect(!route.Terminal && route.Aboard && route.Health==100 && !route.walker.externalMotion,"impact retry failed");
            RideUntil(790);
            Expect(!route.Terminal && route.Health==100 && !route.OnRoof,"ordinary interior occupant hit tunnel");
        }
    }
}
