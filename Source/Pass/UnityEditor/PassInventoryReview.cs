using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Winterbourne.Pass;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
using Object=UnityEngine.Object;

namespace Winterbourne.Editor
{
    // Edge cases share the production scene, physical ray tests, suitcase animation
    // and input actions. The temporary fixture is never saved or packaged.
    [InitializeOnLoad]
    public static class PassInventoryReview
    {
        const string Job="Winterbourne.InventoryReview";
        static IEnumerator run;
        static PassJourney journey;
        static Transform fixture;
        static Camera eye;
        static int last=-1;

        static PassInventoryReview() => EditorApplication.update+=Tick;

        // Run against all alternatives before they are randomized, so an unreachable
        // shelf is caught even if none of the four complete runs happens to roll it.
        public static void CheckAuthoredSites()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            var route=Object.FindAnyObjectByType<PassJourney>();
            var house=GameObject.Find("Fellbridge Station/Fellbridge station house").transform;
            var articles=Object.FindObjectsByType<PassPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(p=>PassLootCatalogue.Value(p.itemId)>0).ToArray();
            Check(articles.Length>=36 && articles.Select(p=>p.itemId).Distinct().Count()==21,"incomplete exhibit list");
            var capsule=route.walker.GetComponent<CharacterController>();
            float eyeHeight=route.walker.eyeCamera.transform.localPosition.y;
            Physics.SyncTransforms();
            var failed=new System.Collections.Generic.List<string>();
            int beds=0;
            foreach(var p in articles)
            {
                Transform room=house;
                if(p.transform.IsChildOf(route.consist))
                {
                    room=p.transform;
                    while(room.parent!=route.consist) room=room.parent;
                }
                var container=p.GetComponentInParent<PassLootContainer>(true);
                var shape=p.GetComponentInChildren<Collider>();
                var centre=container && container.TapChest ? container.transform.TransformPoint(container.gripLocal) : shape.bounds.center;
                bool InReach(Vector3 eye, Vector3 aim)
                {
                    var ray=aim-eye;
                    if(!Physics.Raycast(eye,ray.normalized,out var hit,2.25f,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide)) return false;
                    return container ? hit.collider.GetComponentInParent<PassLootContainer>()==container :
                        hit.collider.GetComponentInParent<PassPickup>()==p;
                }
                if(p.bedsideArrival)
                {
                    beds++;
                    if(!InReach(p.bedsideArrival.position+Vector3.up*eyeHeight,centre))
                        failed.Add(p.siteId+" (initial view)");
                }
                var targets=container && !container.TapChest ? container.GetComponentsInChildren<Collider>().Select(c=>c.bounds.center).ToArray() :
                    new[]{centre};
                float floorY=room==house ? house.TransformPoint(Vector3.up*(house.InverseTransformPoint(centre).y>3.42f?3.42f:0)).y :
                    room.TransformPoint(Vector3.up*1.382f).y;
                bool reachable=false;
                for(int ring=1;ring<=6 && !reachable;ring++)
                    for(int j=0;j<32 && !reachable;j++)
                    {
                        var angle=Mathf.PI*2*j/32;
                        var feet=new Vector3(centre.x+ring*.25f*Mathf.Sin(angle),floorY+.04f,centre.z+ring*.25f*Mathf.Cos(angle));
                        if(!Physics.Raycast(feet+Vector3.up*.12f,Vector3.down,out var floor,.3f,
                            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || !floor.collider.transform.IsChildOf(room)) continue;
                        foreach(float height in new[]{capsule.height,1f})
                        {
                            float radius=capsule.radius-.015f;
                            if(Physics.CheckCapsule(feet+Vector3.up*radius,feet+Vector3.up*(height-radius),radius,
                                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                            var eye=feet+Vector3.up*(height==1f?.86f:eyeHeight);
                            if(targets.Any(t=>InReach(eye,t))) {reachable=true; break;}
                        }
                    }
                if(!reachable) failed.Add(p.siteId);
            }
            Check(beds==3 && failed.Count==0,"no view from standing or crouched room floor: "+string.Join(", ",failed));
            PassChestAccessReview.Check(route,house,articles);
            Debug.Log($"PASS SEARCH SITES: {articles.Length} authored alternatives, 21 articles, {beds} initial bedside sightlines");
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            var route=Object.FindAnyObjectByType<PassJourney>();
            var rack=new GameObject("Inventory review desk").transform;
            rack.SetParent(route.consist,false);
            rack.localPosition=new Vector3(12,0,0);
            string[] manifest={"pocket_watch","crystal_cup","singing_bird_box","intaglio_seal","rose_egg",
                "pearl_aigrette","astronomical_globe","scent_flacon","diptych_dial"};
            for(int i=0;i<manifest.Length;i++)
                Item(rack,manifest[i],new Vector3(i*.46f,1.8f,0),"review-"+i);
            var container=GameObject.CreatePrimitive(PrimitiveType.Cube);
            container.name="Review strongbox";container.transform.SetParent(rack,false);
            container.transform.localPosition=new Vector3(5.4f,1.77f,0);
            container.transform.localScale=new Vector3(.4f,.4f,.4f);
            var box=container.AddComponent<PassLootContainer>();
            box.locked=true;
            var lid=new GameObject("Hinged lid").transform;lid.SetParent(box.transform,false);
            box.movingPart=lid;box.openEuler=Vector3.right*-88;
            Item(box.transform,"cameo",new Vector3(-.3f,0,0),"review-strongbox-a");
            Item(box.transform,"silver_nef",new Vector3(.3f,0,0),"review-strongbox-b");
            var station=new GameObject("Review station shelf").transform;
            station.position=new Vector3(100,0,-950);
            Item(station,"portrait_snuffbox",new Vector3(0,1.8f,0),"review-station-a");
            Item(station,"emerald_tiara",new Vector3(.5f,1.8f,0),"review-station-b");
            SessionState.SetBool(Job,true);
            SessionState.SetString(Job+".start",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        static void Item(Transform parent,string id,Vector3 position,string site)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name=site;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
            obj.transform.localScale=Vector3.one*.14f;
            var p=obj.AddComponent<PassPickup>();p.siteId=site;p.itemId=id;
        }

        static void Tick()
        {
            if(!SessionState.GetBool(Job,false)) return;
            try
            {
                Check((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Job+".start","")).ToUniversalTime()).TotalMinutes<4,"timed out");
                if(EditorApplication.isPaused) EditorApplication.isPaused=false;
                EditorApplication.QueuePlayerLoopUpdate();
                if(!EditorApplication.isPlaying || last==Time.frameCount || Time.frameCount<10) return;
                last=Time.frameCount;
                if(run==null) run=Cases();
                if(run.MoveNext()) return;
                SessionState.EraseBool(Job); EditorApplication.Exit(0);
            }
            catch(Exception e) {SessionState.EraseBool(Job);Debug.LogException(e);EditorApplication.Exit(1);}
        }

        static void Check(bool value,string message) => PassBuild.Check(value,"INVENTORY: "+message);

        static void Aim(Transform target)
        {
            Vector3 feet=target.position+new Vector3(0,-.45f,-1.05f);
            var support=target.IsChildOf(journey.consist)?journey.consist:null;
            journey.walker.Place(support,support?support.InverseTransformPoint(feet):feet,Quaternion.identity,0);
            eye.transform.LookAt(target.position);
            Physics.SyncTransforms();
        }

        static PassPickup Parcel(int i)=>fixture.Find("review-"+i).GetComponent<PassPickup>();

        static void Take(PassPickup p)
        {
            Aim(p.transform);
            Check(journey.UseFocused() && !p.gameObject.activeSelf,"world F failed on "+p.itemId);
        }

        static IEnumerator Cases()
        {
            Time.captureFramerate=30;
            journey=Object.FindAnyObjectByType<PassJourney>();
            journey.ManualClock=true;journey.walker.enabled=false;
            eye=journey.walker.eyeCamera;
            fixture=journey.consist.Find("Inventory review desk");
            PassReview.EnterFromRoom(journey);
            var ui=Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            if(Object.FindObjectsByType<PassPickup>(FindObjectsSortMode.None).Any(p=>p.bedsideArrival))
            {
                yield return null;
                PassPortalReview.Photo("FirstViewWatch",eye);
                Check(journey.UseFocused() && journey.Slot(0)=="pocket_watch" && journey.CarriedValue==52000,
                    "bedside arrival did not actually focus the watch in the open case");
                journey.NewGame();
                PassReview.EnterFromRoom(journey);
            }
            for(int i=0;i<6;i++) Take(Parcel(i));
            Check(journey.CarriedValue==248000 && !journey.HasSettlement && !journey.Terminal,"six-piece train manifest");
            journey.SetInventory(true);
            yield return null;
            Check(ui.Q<Label>("runTarget").text.StartsWith("248,000") && ui.Q<Label>("bagValue").text=="248,000" &&
                ui.Q<Label>("inventoryValue").text.Contains("248,000"),"HUD values not synchronized on pickup");
            Check(journey.MoveSlot(0,7) && journey.CarriedValue==248000,"moving a slot changed the value");
            journey.SetInventory(false);
            Take(Parcel(6)); // Seven full slots.
            var box=fixture.Find("Review strongbox").GetComponent<PassLootContainer>();
            Aim(box.transform);
            Check(journey.UseFocused() && journey.PickingLock && journey.SearchCount==0,"locked case exposed contents");
            Check(!journey.UseFocused() && !box.Unlocked,"immediate press opened the lock");
            journey.CloseSearch();
            Check(!box.TargetOpen && !journey.SearchOpen,"cancel failed");
            Aim(box.transform);journey.UseFocused();
            while(journey.LockPosition<.57f || journey.LockPosition>.72f) yield return null;
            Check(journey.UseFocused() && box.Unlocked && journey.SearchCount==2,"timed lock");
            for(int i=0;i<180 && box.Openness<.99f;i++) yield return null;
            Check(box.Openness>.98f && Quaternion.Angle(Quaternion.identity,box.movingPart.localRotation)>75,$"lid at {box.Openness:F2}, rotation {box.movingPart.localRotation.eulerAngles:F1}, target={box.TargetOpen} search={journey.SearchOpen}");
            int before=journey.CarriedValue;
            var held=journey.SearchItem(0);
            Check(journey.TakeAllFocused()==1 && journey.SearchCount==1 && journey.CarriedValue==before+PassLootCatalogue.Value(held.itemId),"H discarded excess or counted twice");
            Check(!journey.PickSelected() && journey.AwaitingSwap,"full bag did not request exchange");
            var incoming=journey.SearchItem(0);var outgoing=Parcel(0);
            Check(journey.ExchangeSlot(7) && journey.Slot(7)==incoming.itemId && journey.SearchItem(0)==outgoing &&
                outgoing.transform.IsChildOf(box.transform) && outgoing.gameObject.activeSelf,"case swap lost physical model");
            journey.CloseSearch();
            Aim(Parcel(8).transform);journey.UseFocused();
            Check(journey.AwaitingSwap,"loose item exchange failed");
            var location=Parcel(8).transform.position;
            Check(journey.ExchangeSlot(7) && journey.Slot(7)=="diptych_dial" &&
                Vector3.Distance(location,incoming.transform.position)<.001f && incoming.gameObject.activeSelf,"loose exchange physical return");
            int checkpointValue=journey.CarriedValue;
            while(journey.Distance<886) journey.Advance(.1f);
            Aim(Parcel(7).transform);journey.UseFocused();journey.ExchangeSlot(7);
            journey.AdvanceExpedition(19);
            float time=journey.RunSeconds;
            journey.Retry();
            Check(journey.CarriedValue==checkpointValue && journey.Slot(7)=="diptych_dial" && Parcel(7).gameObject.activeSelf &&
                !Parcel(8).gameObject.activeSelf && box.Unlocked && !journey.InventoryOpen && Mathf.Approximately(time,journey.RunSeconds),
                "checkpoint changed inventory, world state, lock or clock");
            journey.NewGame();PassReview.EnterFromRoom(journey);
            Check(journey.CarriedValue==0 && !box.Unlocked,"new run failed to reset the first roll");
            while(journey.Phase!=JourneyPhase.Dwell) journey.Advance(.3f);
            var shelf=GameObject.Find("Review station shelf").transform;
            var first=shelf.GetChild(0).GetComponent<PassPickup>();
            Aim(first.transform);journey.ReconcileBoarding();
            Check(!journey.Aboard,"station test still riding");
            Take(first);Take(shelf.GetChild(1).GetComponent<PassPickup>());
            Check(journey.CarriedValue==110000,"station loot omitted");
#if ENABLE_INPUT_SYSTEM
            if(Environment.GetCommandLineArgs().Contains("-lootNativeKeys"))
            {
                var keys=NativeKeys(box);
                while(keys.MoveNext()) yield return keys.Current;
            }
#endif
            Debug.Log("PASS INVENTORY: world and station F, lock/Esc, H capacity, same-frame values, physical swaps, checkpoint and reroll");
        }

#if ENABLE_INPUT_SYSTEM
        static IEnumerator NativeKeys(PassLootContainer box)
        {
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            IEnumerator Press(Key key)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return null;yield return null;
            }
            journey.NewGame();PassReview.EnterFromRoom(journey);
            Aim(Parcel(0).transform);
            yield return null;
            journey.walker.ResumeLook();
            Check(journey.walker.HasMouse,"Game view needs native pointer capture");
            var press=Press(Key.F);while(press.MoveNext())yield return null;
            Check(journey.Slot(0)=="pocket_watch","F did not pick the focused parcel");
            press=Press(Key.Digit4);while(press.MoveNext())yield return null;
            Check(journey.QuickSlot==3,"4 did not select the fourth quick slot");
            press=Press(Key.Tab);while(press.MoveNext())yield return null;
            Check(journey.InventoryOpen,"Tab did not open inventory");
            press=Press(Key.Escape);while(press.MoveNext())yield return null;
            Check(!journey.InventoryOpen && journey.walker.HasMouse,"Esc did not return to exploration");
            Aim(box.transform);
            press=Press(Key.F);while(press.MoveNext())yield return null;
            Check(journey.PickingLock,"F did not start the lock");
            while(journey.LockPosition<.56f || journey.LockPosition>.65f)yield return null;
            press=Press(Key.F);while(press.MoveNext())yield return null;
            Check(journey.SearchCount==2 && !journey.PickingLock,"F did not release the lock");
            press=Press(Key.H);while(press.MoveNext())yield return null;
            Check(journey.SearchCount==0 && journey.CarriedValue==52000+23999+30000,"H did not take both items");
            press=Press(Key.Escape);while(press.MoveNext())yield return null;
            Check(!journey.SearchOpen && !box.TargetOpen,"Esc did not close the searched case");
            Debug.Log("PASS INVENTORY KEYBOARD: F, H, Esc, Tab and 4 via the Input System");
        }
#endif
    }
}
