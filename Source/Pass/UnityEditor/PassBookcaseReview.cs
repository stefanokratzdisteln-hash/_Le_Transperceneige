using System;
using System.Collections;
using UnityEngine;
using Winterbourne.Pass;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Winterbourne.Editor
{
    internal static class PassBookcaseReview
    {
        static void Check(bool condition,string message) => PassBuild.Check(condition,"BOOKCASE: "+message);
        static void Pose(PassJourney route,PassPortal gate,Vector3 local,Vector3 look)
        {
            Vector3 feet=gate.transform.TransformPoint(local);
            var direction=gate.transform.TransformPoint(look)-(feet+Vector3.up*route.walker.eyeCamera.transform.localPosition.y);
            float tilt=-Mathf.Asin(direction.normalized.y)*Mathf.Rad2Deg;
            route.walker.Place(null,feet,Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,Vector3.up)),tilt);
            Physics.SyncTransforms();
        }

        public static IEnumerator Sweep(PassJourney route,PassPortal gate,bool photograph)
        {
            var walker=route.walker;
            Transform previousParent=walker.transform.parent;
            Vector3 savedFeet=walker.transform.localPosition;
            Quaternion savedFacing=walker.transform.localRotation;
            float savedPitch=walker.ViewPitch;
            var shelf=gate.cover;
            bool film=photograph && !string.IsNullOrEmpty(FleetImport.Argument("-portalFilm"));
            Check(shelf && shelf.AllowsPassage && shelf.leaf && shelf.panel && shelf.frontPull && shelf.rearPull,
                "missing modeled slide, collider, handles or authored-open pose");
            Vector3 open=shelf.leaf.localPosition;
            float oldStep=Time.captureDeltaTime;
            Time.captureDeltaTime=1f/30;
            // Fix the eye at a reachable front-side operating position for all three
            // photographs. Only the gate's moving component changes pose.
            Pose(route,gate,new Vector3(-.61f,.04f,-2.37f),new Vector3(-.63f,1.34f,0));
            var body=route.PlayerBody;
#if ENABLE_INPUT_SYSTEM
            var keys=InputSystem.AddDevice<Keyboard>();
            var mouse=InputSystem.AddDevice<Mouse>();
            var previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            walker.enabled=true;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
            yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)}.WithButton(MouseButton.Left));
            yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());
            yield return null;
            Check(walker.HasMouse,"test Game view must be focused and accept real controls");
#endif
            Check(shelf.InSight(walker.eyeCamera,walker.interactionDistance,gate.threshold),
                "authored front operating point did not resolve the nearby portal");
            if(photograph) PassPortalReview.Photo("BookcaseOpen");
#if ENABLE_INPUT_SYSTEM
            Check(route.ActionPrompt()==PassText.Get("E   关门"),"front aim prompt disagrees with available input");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E));yield return null;yield return null;
            InputSystem.QueueStateEvent(keys,new KeyboardState());
#else
            walker.useOverride();
#endif
            Check(shelf.TargetClosed && !shelf.AllowsPassage,"E did not start closing the open bookcase");
            if(film)
            {
                for(int n=1;n<=4;n++)
                {
                    while(shelf.ClosedFraction<n*.1f)yield return null;
                    PassPortalReview.Photo("Close"+n);
                }
            }
            for(int i=0;shelf.ClosedFraction<.49f && i<110;i++)yield return null;
            Check(shelf.ClosedFraction>=.49f && shelf.ClosedFraction<.65f &&
                Vector3.Distance(shelf.leaf.localPosition,open)>.40f,"no visible half-way pose");
            if(photograph) PassPortalReview.Photo("BookcaseHalf");
            if(film)
            {
                for(int n=6;n<=9;n++)
                {
                    while(shelf.ClosedFraction<n*.1f)yield return null;
                    PassPortalReview.Photo("Close"+n);
                }
            }
            for(int i=0;shelf.ClosedFraction<.998f && i<110;i++)yield return null;
            Check(shelf.ClosedFraction>=.998f && Vector3.Distance(shelf.leaf.localPosition,open+shelf.closedOffset)<.006f,
                "shelf did not reach its right-hand covering position");
            if(photograph) PassPortalReview.Photo("BookcaseClosed");
            if(film)
            {
#if ENABLE_INPUT_SYSTEM
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E));yield return null;yield return null;
                InputSystem.QueueStateEvent(keys,new KeyboardState());
#else
                walker.useOverride();
#endif
                Check(!shelf.TargetClosed,"same operator could not slide the cover back to the left");
                for(int n=1;n<=10;n++)
                {
                    float at=1f-n*.1f+.001f;
                    for(int f=0;f<110 && shelf.ClosedFraction>at;f++)yield return null;
                    Check(shelf.ClosedFraction<=at,"reopening stalled in film");
                    PassPortalReview.Photo("Reveal"+n);
                }
#if ENABLE_INPUT_SYSTEM
                InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E));yield return null;yield return null;
                InputSystem.QueueStateEvent(keys,new KeyboardState());
#else
                walker.useOverride();
#endif
                for(int i=0;i<110 && shelf.ClosedFraction<.998f;i++)yield return null;
                Check(shelf.ClosedFraction>=.998f,"second close from same operator pose");
            }
            // Press against the closed bookcase from the room. Its solid collision
            // must stop us at the front plane, with the portal target still refusing entry.
            Pose(route,gate,new Vector3(0,.04f,-1.0f),new Vector3(0,1.54f,0));
            for(int i=0;i<45;i++) walker.MoveCharacter(Vector2.up,false,1f/60);
            Check(gate.transform.InverseTransformPoint(walker.transform.position).z<-.48f &&
                !route.TryEnterPortal(gate),"closed moving slab was passable from the front");
            // The rear portal volume can be physically reached before the front
            // slab. It too must reject transit while covered.
            Pose(route,gate,new Vector3(0,.04f,.29f),new Vector3(0,1.54f,0));
            Check(!gate.Contains(body) && !route.TryEnterPortal(gate),"closed gate accepted rear entry");
            Pose(route,gate,new Vector3(.27f,.04f,.84f),new Vector3(.16f,1.50f,-.18f));
            Check(shelf.InSight(walker.eyeCamera,walker.interactionDistance,gate.threshold),
                "closed panel or rear pull cannot be reached from the rear");
#if ENABLE_INPUT_SYSTEM
            Check(route.ActionPrompt()==PassText.Get("E   开门"),"rear aim has no open prompt");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E));yield return null;yield return null;
            InputSystem.QueueStateEvent(keys,new KeyboardState());
#else
            walker.useOverride();
#endif
            Check(!shelf.TargetClosed,"rear E failed to reopen the same cover");
            for(int i=0;!shelf.AllowsPassage && i<110;i++)yield return null;
            Check(shelf.AllowsPassage,"cover did not slide fully back to the left");
            if(photograph)
            {
                // The open gate must not teleport a threshold obstruction while the shelf is closing.
                shelf.Toggle();
                Pose(route,gate,new Vector3(-.35f,.04f,-.37f),new Vector3(-.35f,1.54f,0));
                for(int i=0;i<140;i++)shelf.MoveCover(body,1f/60);
                float blocked=shelf.ClosedFraction;
                Check(blocked>.005f && blocked<.95f && !gate.Contains(body),"closing anti-crush did not hold");
                Pose(route,gate,new Vector3(0,.04f,-1.64f),new Vector3(0,1.54f,0));
                for(int i=0;i<140;i++)shelf.MoveCover(body,1f/60);
                Check(shelf.ClosedFraction>.999f,"cover did not resume after closing obstruction left");
                shelf.Toggle();
                Pose(route,gate,new Vector3(-1.42f,.04f,-.37f),new Vector3(-1.42f,1.54f,0));
                for(int i=0;i<140;i++)shelf.MoveCover(body,1f/60);
                Check(shelf.ClosedFraction>.07f && shelf.ClosedFraction<.995f,"opening cover crushed the parked-side obstruction");
                Pose(route,gate,new Vector3(0,.04f,-1.64f),new Vector3(0,1.54f,0));
                for(int i=0;i<140;i++)shelf.MoveCover(body,1f/60);
            }
            Check(!shelf.TargetClosed && shelf.AllowsPassage,"test left a blocked gate");
#if ENABLE_INPUT_SYSTEM
            walker.enabled=false;
            InputSystem.RemoveDevice(keys);InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior=previousBackground;
#endif
            Time.captureDeltaTime=oldStep;
            walker.Place(previousParent,savedFeet,savedFacing,savedPitch);
            Physics.SyncTransforms();
            Debug.Log($"BOOKCASE PASS {gate.destination}: front E closed the solid sliding leaf, both sides denied travel, rear E reopened it");
        }
    }
}
