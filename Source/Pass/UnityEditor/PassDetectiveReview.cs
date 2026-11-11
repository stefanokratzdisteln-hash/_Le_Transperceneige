using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Winterbourne.Pass;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Winterbourne.Editor
{
    internal static partial class PassExtractionReview
    {
        static int detectiveFilmFrame;
        static string DetectiveOutput=>FleetImport.Argument("-portraitDir");
        static void DetectiveFilm()
        {
            string dir=Path.Combine(DetectiveOutput,"frames");
            Directory.CreateDirectory(dir);
            WriteGameFrame(Path.Combine(dir,(detectiveFilmFrame++).ToString("D4")+".jpg"),false);
        }
        static void Portrait(string name)
        {
            WriteGameFrame(Path.Combine(DetectiveOutput,name+".png"),true);
            Debug.Log("DETECTIVE FRAME "+name);
        }
        static IEnumerator HoldView(int count,bool film=false)
        {
            for(int i=0;i<count;i++){
                if(!walker.enabled)Step(Vector3.zero);
                else {route.AdvanceExpedition(Dt);route.Advance(Dt);}
                yield return null;
                if(film)DetectiveFilm();
            }
        }
#if ENABLE_INPUT_SYSTEM
        static IEnumerator NativeKey(Key key,int frames)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
            yield return HoldView(frames);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return HoldView(2);
        }
        static IEnumerator LookAlong(Mouse mouse,Vector3 direction)
        {
            float yaw=Vector3.SignedAngle(Vector3.ProjectOnPlane(walker.transform.forward,Vector3.up),
                Vector3.ProjectOnPlane(direction,Vector3.up),Vector3.up);
            int count=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(yaw)/7));
            for(int i=0;i<count;i++){
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(yaw/(count*walker.lookSensitivity),0)});
                yield return HoldView(1);
            }
            InputSystem.QueueStateEvent(mouse,new MouseState());
            yield return HoldView(14);
        }
        static IEnumerator OrbitForPortrait(PassDetectivePlayer presentation,Mouse mouse,float target,
            bool forward,bool record)
        {
            var actor=presentation.Avatar;
            float Score(){
                var direction=Vector3.ProjectOnPlane(presentation.DisplayCamera.transform.position-actor.position,Vector3.up).normalized;
                return Vector3.Dot(direction,Vector3.ProjectOnPlane(actor.forward,Vector3.up).normalized);
            }
            // Stop on the actor's actual facing rather than assuming a fixed mouse sensitivity.
            int steps=0;
            for(;steps<260 && (forward?Score()<target:Score()>target);steps++){
                var state=new MouseState{delta=new Vector2(8,0)}.WithButton(MouseButton.Right);
                InputSystem.QueueStateEvent(mouse,state);yield return HoldView(1);
                if(record && steps%2==0)DetectiveFilm();
            }
            var hold=forward?new MouseState().WithButton(MouseButton.Right):new MouseState();
            InputSystem.QueueStateEvent(mouse,hold);yield return HoldView(2);
            Check(forward?Score()>.80f:Score()<-.68f,"Could not orbit to the requested real angle: "+Score());
        }
#endif
        static IEnumerator DetectivePortraits()
        {
            yield return PrepareNativeGameView();
            Directory.CreateDirectory(DetectiveOutput);
            detectiveFilmFrame=0;
            var view=walker.GetComponent<PassDetectivePlayer>();
            Check(view && view.Avatar && view.DisplayCamera,"The scene must include the playable detective and camera");
            route.NewGame();
            walker.enabled=true;walker.ResumeLook();
#if ENABLE_INPUT_SYSTEM
            Check(keyboard!=null,"The detective review needs -extractionNativeKeys");
            var mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState());
            yield return HoldView(12);
            Check(view.ThirdPersonActive,"The room must start in third person");
            walker.enabled=false;
            var hut=route.hubSpawn.parent;
            yield return Straight(()=>hut.TransformPoint(new Vector3(.54f,.055f,-.34f)));
            walker.enabled=true;walker.ResumeLook();
            yield return LookAlong(mouse,hut.TransformDirection(new Vector3(.95f,0,-.30f)));
            yield return OrbitForPortrait(view,mouse,.94f,true,false);
            yield return HoldView(8);
            Portrait("HolmesCottage");
            yield return HoldView(19,true);
            yield return OrbitForPortrait(view,mouse,-.89f,false,true);

            // The room's own wall shortens the boom. Moving here on foot yields
            // a closer view of eyes, cap and coat without changing the lens.
            walker.enabled=false;
            yield return Straight(()=>hut.TransformPoint(new Vector3(2.50f,.055f,-.60f)));
            walker.enabled=true;walker.ResumeLook();
            yield return LookAlong(mouse,hut.right);
            yield return OrbitForPortrait(view,mouse,.96f,true,false);
            yield return HoldView(7);
            Check(view.ThirdPersonActive,"The wall must leave enough room for a shoulder view");
            Portrait("HolmesFace");yield return HoldView(13,true);
            yield return OrbitForPortrait(view,mouse,-.87f,false,false);
            yield return NativeKey(Key.V,2);
            Check(!view.ThirdPersonActive,"V did not return the player to first person");
            yield return NativeKey(Key.V,2);
            Check(view.ThirdPersonActive,"V did not restore the third-person camera");
            walker.enabled=false;
            yield return Straight(()=>hut.TransformPoint(new Vector3(1.24f,.055f,-2.85f)));
            walker.enabled=true;walker.ResumeLook();
            yield return LookAlong(mouse,hut.forward);
            yield return HoldView(4);
            Check(!view.ThirdPersonActive,"The rear wall must tuck a blocked camera to the player's eye");
            yield return NativeKey(Key.W,21);yield return HoldView(8);
            Check(view.ThirdPersonActive,"Walking away from the wall must restore the camera");
            walker.enabled=false;
            yield return EnterGate(route.hubGate,()=>route.Aboard);
            var car=route.consist.Find("Car05");
            Vector3 start=route.consist.InverseTransformPoint(car.TransformPoint(new Vector3(0,1.405f,6.35f)));
            yield return WalkPoint(route.consist,start,"open saloon centre aisle",.12f);
            Aim(Feet+car.forward*6+Vector3.up*1.45f);
            walker.enabled=true;walker.ResumeLook();
            // All filmed walking and crouching below use real input events in the live Game view.
            yield return LookAlong(mouse,car.forward);
            Vector3 before=car.InverseTransformPoint(Feet);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
            for(int i=0;i<37;i++){yield return HoldView(1);DetectiveFilm();}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Check(Vector3.Distance(before,car.InverseTransformPoint(Feet))>.36f,"Keyboard W did not move the detective through 05");
            yield return HoldView(4);
            Check(view.ThirdPersonActive,"Moving saloon must show the detective");
            Portrait("HolmesSaloon");
            walker.enabled=false;
            var chest=car.GetComponentsInChildren<PassLootContainer>().Single(b=>b.TapChest);
            var article=chest.GetComponentsInChildren<PassPickup>().Single();
            yield return ApproachChestFront(chest,route.consist,1.405f);
            yield return HoldView(8,true);
            int value=route.CarriedValue;
            yield return Press();
            Check(route.WorkingChest && walker.IsCrouched && !view.ThirdPersonActive,"The first F must crouch and show the hands from the eyes");
            yield return HoldView(11,true);
            for(int i=0;i<chest.tapsRequired;i++){
                yield return Press();yield return HoldView(4,true);
                if(i==0)Portrait("HolmesChest");
            }
            float deadline=Time.realtimeSinceStartup+10;
            while(!route.InventoryOpen && Time.realtimeSinceStartup<deadline){yield return HoldView(1,true);}
            Check(route.InventoryOpen && !view.ThirdPersonActive,"The contents list must keep the close interaction view");
            yield return Press();
            Check(route.CarriedValue==value+PassLootCatalogue.Value(article.itemId),"The actual case article was not collected");
            yield return Press(true);
            walker.enabled=true;walker.ResumeLook();
            yield return HoldView(11,true);
            // The player can still be next to the wall. Step back through the actual capsule to give the boom space.
            if(!view.ThirdPersonActive){
                yield return NativeKey(Key.S,20);yield return HoldView(5);
            }
            Check(view.ThirdPersonActive && !chest.HandsVisible && !route.SearchOpen,"Closing the case must return to the third-person preference");
            Portrait("HolmesAfterChest");
            yield return HoldView(17,true);
            InputSystem.RemoveDevice(mouse);
            Debug.Log("DETECTIVE PASS: real room orbit; V both directions; keyboard saloon walk; F crouch and collect; third-person restored.");
#else
            throw new InvalidOperationException("Run the detective review in Unity 6.6 with the Input System");
#endif
        }
    }
}
