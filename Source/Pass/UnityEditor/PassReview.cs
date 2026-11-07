// Run with real URP rendering: -batchmode -force-d3d11
// -executeMethod Winterbourne.Editor.PassReview.Run -formationRoot "<checkout>"
// Omit -quit. The job enters Play Mode, exercises the level, captures photos and exits.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Winterbourne.Demo;
using Winterbourne.Pass;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    [InitializeOnLoad]
    public static class PassReview
    {
        const string Flag="Winterbourne.PassReview";
        static IEnumerator sequence;
        static int previous=-1;
        static PassJourney journey;
        static CharacterController body;
        static Camera eye;
        static PassAtmosphere atmosphere;
        static PassDoor rear;
        static PassPickup bag;
        static PassDoor[] gates;
        static Transform[] cars;
        static UIDocument hud;
        static string Output => FleetImport.Argument("-passPhotoDir") ?? Path.Combine(FleetImport.Checkout,"Previews","Pass");

        static PassReview(){ EditorApplication.update+=Tick; }
        public static void Run()
        {
            PassBuild.Check(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset,"URP is required for window photographs");
            PassBuild.Check(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"GPU camera required");
            ShaderUtil.allowAsyncCompilation=false;
            EditorSceneManager.OpenScene(PassBuild.ScenePath,OpenSceneMode.Single);
            SessionState.SetBool(Flag,true);
            SessionState.SetString(Flag+".Utc",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag,false)) return;
            try {
                var start=DateTime.Parse(SessionState.GetString(Flag+".Utc","")).ToUniversalTime();
                PassBuild.Check((DateTime.UtcNow-start).TotalMinutes<12,"Play test timed out");
                if(!EditorApplication.isPlaying || Time.frameCount<5 || previous==Time.frameCount)return;
                previous=Time.frameCount;
                if(sequence==null)sequence=Evaluate();
                if(!sequence.MoveNext()){
                    SessionState.EraseBool(Flag);
                    Debug.Log("PASS COMPLETE: practical movement, dwell and missed boarding, eight-slot rescue and six ending/retry checks passed");
                    EditorApplication.Exit(0);
                }
            } catch(Exception e) {
                SessionState.EraseBool(Flag); Debug.LogException(e); EditorApplication.Exit(1);
            }
        }
        static void At(Vector3 world)
        {
            body.enabled=false;
            body.transform.position=world;
            body.enabled=true;
            Physics.SyncTransforms();
        }
        static void Look(Vector3 at)
        {
            eye.transform.rotation=Quaternion.LookRotation(at-eye.transform.position,Vector3.up);
        }
        static void RideTo(float chainage)
        {
            int steps=0;
            while(journey.Distance<chainage){
                PassBuild.Check(!journey.Terminal && journey.Phase!=JourneyPhase.Dwell && ++steps<1800,"Cannot reach "+chainage+" from "+journey.Phase);
                journey.Advance(Mathf.Clamp((chainage-journey.Distance)/Mathf.Max(1,journey.Speed),.005f,.23f));
            }
            Physics.SyncTransforms();
        }
        static void AdvanceTo(JourneyPhase target,int limit=1200)
        {
            for(int i=0;i<limit && journey.Phase!=target;i++) journey.Advance(.25f);
            PassBuild.Check(journey.Phase==target,"Expected "+target+" but reached "+journey.Phase+" at "+journey.Distance);
        }
        static void Window(int view)
        {
            // Window bays in the actual cabin, occupied saloon and long side corridor.
            int car=view==1?5:7;
            Vector3 p,aim;
            if(view==0){p=new Vector3(-.43f,2.972f,-5.08f);aim=new Vector3(-4.21f,2.98f,-5.65f);}
            else if(view==1){p=new Vector3(-.67f,2.88f,3.74f);aim=new Vector3(-4.0f,3.0f,3.83f);}
            else{p=new Vector3(.72f,2.972f,3.74f);aim=new Vector3(4.0f,2.94f,4.12f);}
            eye.transform.position=cars[car-1].TransformPoint(p);
            Look(cars[car-1].TransformPoint(aim));
            atmosphere.Apply(-eye.transform.position.z);
        }
        static int Photo(string name,bool deliver=true)
        {
            var dir=deliver?Output:Path.Combine(Path.GetTempPath(),"WinterbournePass.WindowQA");
            Directory.CreateDirectory(dir);
            int w=deliver?1440:480,h=deliver?900:300;
            var originalTarget=eye.targetTexture;
            var current=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,1);
            var pixels=new Texture2D(w,h,TextureFormat.RGB24,false);
            try{
                eye.targetTexture=rt;
                eye.aspect=w/(float)h;
                RenderPipeline.SubmitRenderRequest(eye,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,w,h),0,0); pixels.Apply();
                var data=pixels.GetPixelData<byte>(0);
                int lo=255,hi=0,hash=17;
                for(int i=0;i<data.Length;i+=267){int v=data[i];lo=Math.Min(lo,v);hi=Math.Max(hi,v);hash=unchecked(hash*31+v);}
                if(name=="Opening"){
                    var pane=new HashSet<int>();
                    for(int y=h*39/100;y<h*56/100;y+=3)
                        for(int x=w*58/100;x<w*69/100;x+=3){
                            int i=3*(y*w+x);
                            pane.Add((data[i]<<16)|(data[i+1]<<8)|data[i+2]);
                        }
                    Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"WinterbournePass.WindowQA"));
                    File.WriteAllBytes(Path.Combine(Path.GetTempPath(),"WinterbournePass.WindowQA","Opening-raw.png"),pixels.EncodeToPNG());
                    Debug.Log("PASS INITIAL WINDOW RAW: "+pane.Count+" distinct sampled colours; camera="+eye.transform.position+" fog="+RenderSettings.fogDensity+" sun="+RenderSettings.sun.intensity);
                    PassBuild.Check(pane.Count>35,"Initial raw bedroom view at rest is a flat pane ("+pane.Count+" colours)");
                }
                PassBuild.Check(hi-lo>35,name+" has insufficient contrast; check cameras and materials");
                File.WriteAllBytes(Path.Combine(dir,name+".png"),pixels.EncodeToPNG());
                return hash;
            }finally{
                eye.targetTexture=originalTarget; RenderTexture.active=current;
                RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(pixels);
            }
        }
        static void WindowMatrix(string label)
        {
            for(int v=0;v<3;v++){
                var fingerprints=new HashSet<int>();
                Window(v);
                foreach(float seconds in new[]{0f,PassAtmosphere.CloudPeriod*.15f,PassAtmosphere.CloudPeriod*.5f}){
                    atmosphere.SampleWeather(-eye.transform.position.z,seconds);
                    fingerprints.Add(Photo(label+"-window"+v+"-cloud"+seconds,false));
                }
                if(label!="Tunnel") PassBuild.Check(fingerprints.Count==3,label+" moonlight unchanged for window "+v);
            }
            atmosphere.Apply(-eye.transform.position.z);
        }
        static IEnumerator WithInterface(string name)
        {
            const int w=1440, h=900;
            var settings=hud.panelSettings;
            var original=settings.targetTexture;
            var originalClear=settings.clearColor;
            var originalTint=settings.colorClearValue;
            var overlay=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            overlay.Create();
            var background=new Texture2D(w,h,TextureFormat.RGB24,false);
            var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);
            var active=RenderTexture.active;
            try {
                settings.clearColor=true;
                settings.colorClearValue=Color.clear;
                settings.targetTexture=overlay;
                float settle=Time.unscaledTime+.34f;
                while(Time.unscaledTime<settle)yield return null;
                var pane=hud.rootVisualElement;
                PassBuild.Check(pane.Q("quickSlots").childCount==4 && pane.Q("allSlots").childCount==8, "Missing four quick/eight full inventory cells");
                if(journey.InventoryOpen)PassBuild.Check(pane.Q("inventory").resolvedStyle.display!=DisplayStyle.None, "B did not display the full inventory in the rendered UI");
                RenderTexture.active=overlay;
                ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
                RenderTexture.active=active;
                settings.targetTexture=original;
                yield return null;
                Photo(name);
                var file=Path.Combine(Output,name+".png");
                background.LoadImage(File.ReadAllBytes(file));
                var behind=background.GetPixels32();var above=ui.GetPixels32();
                int shown=0;
                for(int i=0;i<behind.Length;i++){
                    var a=above[i];
                    if(a.a==0)continue;
                    shown++;
                    var b=behind[i];
                    float t=a.a/255f;
                    behind[i]=new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(a.r*t+b.r*(1-t)),0,255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a.g*t+b.g*(1-t)),0,255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a.b*t+b.b*(1-t)),0,255),255);
                }
                PassBuild.Check(shown>w*h/700,"UI Toolkit overlay did not actually render for "+name+"; pixel count "+shown);
                background.SetPixels32(behind);background.Apply();
                File.WriteAllBytes(file,background.EncodeToPNG());
                Debug.Log("PASS UI "+name+": "+shown+" nontransparent pixels from the actual UIDocument");
            } finally {
                settings.targetTexture=original;settings.clearColor=originalClear;settings.colorClearValue=originalTint;
                RenderTexture.active=active; overlay.Release();
                Object.DestroyImmediate(overlay);Object.DestroyImmediate(ui);Object.DestroyImmediate(background);
            }
        }
        static void HomeEye()
        {
            eye.transform.localPosition=new Vector3(0,1.54f,0);
            eye.transform.localRotation=Quaternion.identity;
        }
        static void Cross(PassDoor d,bool outbound)
        {
            float z=d.threshold.position.z;
            float platform=PassJourney.PlatformEdge-.49f;
            float start=outbound?-1.06f:platform, end=outbound?platform:-1.06f;
            At(new Vector3(start,1.4f,z));
            journey.ReconcileBoarding();
            for(int i=1;i<=34;i++){
                float want=Mathf.Lerp(start,end,i/34f);
                body.Move(new Vector3(want-body.transform.position.x,-.013f,z-body.transform.position.z));
                journey.ReconcileBoarding();
                PassBuild.Check(Mathf.Abs(body.transform.position.x-want)<.081f && body.transform.position.y>1.26f,
                    $"Passenger blocked in {d.name}, going {(outbound?"out":"in")} at {body.transform.position:F3} expected x {want:F3}");
            }
            PassBuild.Check(journey.Aboard!=outbound,"Boarding frame not transferred across "+d.name);
        }
        static IEnumerator ExerciseDoors()
        {
            foreach(var d in gates){
                At(new Vector3(-1.0f,1.405f,d.threshold.position.z));
                HomeEye(); Look(d.threshold.position+Vector3.up*.12f);
                PassBuild.Check(journey.TryDoor(d),"Could not request passenger door "+d.car.name+"/"+d.name);
            }
            // Do not stand in a moving leaf's arc.
            At(cars[6].TransformPoint(new Vector3(0,1.40f,-7)));
            float until=Time.time+1.3f;
            while(Time.time<until)yield return null;
            foreach(var d in gates){
                PassBuild.Check(d.IsOpen,"Leaf animation stalled on "+d.car.name+"/"+d.name+" at "+d.Openness);
                Cross(d,true); Cross(d,false);
            }
            Debug.Log("PASS STATION: physically crossed the fourteen vestibule thresholds in both directions with the live .22m capsule, including 05 and 07");
        }
        static void ExitToPlatform()
        {
            var d=gates.First(t=>t.car==cars[6] && t.ExteriorSide<0);
            if(!d.TargetOpen){
                At(new Vector3(-.94f,1.4f,d.threshold.position.z)); HomeEye();
                PassBuild.Check(journey.TryDoor(d),"07 exterior locked at the platform");
            }
            d.SetOpen(true,true);
            Cross(d,true);
        }

        static void WalkOpening(PassDoor door, Vector3 axis, float approach, float beyond)
        {
            Vector3 centre = door.threshold.position;
            centre.y = 1.405f;
            foreach (int direction in new[]{1,-1})
            {
                Vector3 from=centre+axis*(direction==1 ? -approach : beyond);
                Vector3 to=centre+axis*(direction==1 ? beyond : -approach);
                At(from);
                for (int i=1;i<=24;i++)
                {
                    Vector3 delta=Vector3.Lerp(from,to,i/24f)-body.transform.position;
                    delta.y=-.007f;
                    body.Move(delta);
                }
                Vector3 error=body.transform.position-to;
                PassBuild.Check(Mathf.Abs(Vector3.Dot(error,axis))<.08f && body.transform.position.y>1.28f,
                    $"Open {door.car.name}/{door.name} passage physically blocked going {direction}: {body.transform.position:F2} for {to:F2}");
            }
        }

        static void ServicePassages(PassDoor[] doors)
        {
            foreach(var door in doors.Where(d=>d.kind==PassDoorKind.Cargo || d.kind==PassDoorKind.Lavatory || d.kind==PassDoorKind.Galley))
            {
                door.SetOpen(true,true); Physics.SyncTransforms();
                if (door.kind==PassDoorKind.Cargo)
                    WalkOpening(door,door.ExteriorSide*door.car.right,.51f,door.ExteriorSide<0?.83f:.13f);
                else if (door.kind==PassDoorKind.Lavatory) WalkOpening(door,-door.car.right,.55f,.55f);
                else WalkOpening(door,door.car.forward,.69f,.69f);
            }
            Debug.Log("PASS PASSAGES: cargo openings on both flanks, 07 lavatory and 06 galley physically crossed in both directions");
        }

        static IEnumerator ProgressiveDoorLock()
        {
            var front=gates.First(t=>t.car==cars[4]);
            var last=gates.Single(t=>t.kind==PassDoorKind.Chase);
            At(new Vector3(-1.1f,1.405f,front.threshold.position.z));
            front.SetOpen(true,true); Physics.SyncTransforms();
            float remaining=PassJourney.PlatformEnd+front.threshold.position.z;
            RideTo(journey.Distance+remaining-.24f);
            At(new Vector3(-1.39f,1.405f,front.threshold.position.z));
            journey.ReconcileBoarding();
            PassBuild.Check(journey.Aboard && front.Occupied(body),"Threshold safety fixture needs occupied moving 05");
            RideTo(journey.Distance+.43f);
            float until=Time.time+1;
            while(Time.time<until)yield return null;
            PassBuild.Check(!journey.DoorAvailable(front) && journey.DoorAvailable(last) && !front.TargetOpen && front.IsOpen,
                "05 must wait on the occupied threshold while 09 still opens");
            At(new Vector3(-.85f,1.405f,front.threshold.position.z));
            until=Time.time+1;
            while(Time.time<until)yield return null;
            PassBuild.Check(front.Openness<.01f && !journey.TryDoor(front),"Cleared 05 should latch and decline E beyond platform end");
            At(new Vector3(-1.01f,1.405f,front.threshold.position.z));
            for (int i=0;i<18;i++) body.Move(Vector3.left*.055f);
            PassBuild.Check(body.transform.position.x>-1.47f,"Closed 05 leaf failed to physically block the capsule");
            Debug.Log("PASS PROGRESSIVE LOCK: 05 waited on its occupied sill, then physically latched; 09 still available at the same instant");
        }
        static void PickBag(int destination)
        {
            At(bag.transform.position+new Vector3(.70f,1.405f-bag.transform.position.y,0));
            HomeEye(); Look(bag.transform.position);
            PassBuild.Check(Physics.Raycast(eye.transform.position,eye.transform.forward,out var hit,2.25f,~0,QueryTriggerInteraction.Collide)
                         && hit.collider.GetComponentInParent<PassPickup>()==bag,"F ray is obstructed before it reaches the pack: "+(hit.collider?hit.collider.name:"no hit"));
            PassBuild.Check(journey.TryPick(bag) && journey.Slot(0)=="parachute" && !bag.gameObject.activeSelf,"Pickup did not enter visible inventory");
            journey.SetInventory(true);
            PassBuild.Check(journey.InventoryOpen && hud.rootVisualElement.panel!=null,"HUD or full inventory unavailable");
            if(destination!=0) PassBuild.Check(journey.MoveSlot(0,destination) && journey.Slot(destination)=="parachute" && journey.Slot(0)==null,"Could not store the pack in slot 8");
            journey.SetInventory(false);
        }
        static IEnumerator JumpFromRear()
        {
            At(journey.escapeLaunch.position-Vector3.forward*1.99f); HomeEye(); Look(rear.threshold.position);
            PassBuild.Check(journey.TryDoor(rear),"Escape door would not unlock with entire formation over the canyon");
            // Open it from inside the guard van and walk onto the plate after it clears.
            float until=Time.time+1.2f;
            while(Time.time<until)yield return null;
            PassBuild.Check(rear.IsOpen,"Rear leaf obstructed at "+rear.Openness);
            for(int i=0;i<41;i++) body.Move(journey.consist.forward*.05f+Vector3.down*.009f);
            PassBuild.Check(Vector3.Distance(body.transform.position,journey.escapeLaunch.position)<.38f,
                "Open tail could not be walked through from the guard van: "+body.transform.position);
            body.transform.rotation=Quaternion.LookRotation(journey.consist.forward);
            HomeEye();
            PassBuild.Check(journey.TryJump() && journey.Phase==JourneyPhase.Jumping,"Space did not launch from the rear footplate");
            PassBuild.Check(!journey.Aboard,"Jump did not leave the moving train frame");
        }
        static IEnumerator CheckDeploymentHelp()
        {
            bool previous=PassText.Chinese;
            var root=hud.rootVisualElement;
            foreach(bool chinese in new[]{true,false}){
                PassText.SetChinese(chinese);
                journey.Tell("已回到检查点");
                float ready=Time.unscaledTime+.18f;
                while(Time.unscaledTime<ready)yield return null;
                PassBuild.Check(root.Q("deployPrompt").resolvedStyle.display!=DisplayStyle.None &&
                    root.Q<Label>("deployKey").text==(chinese?"空格":"SPACE") &&
                    root.Q<Label>("deployAction").text==(chinese?"开伞":"Open parachute"),
                    "Freefall deployment help was hidden by a notification or missing in this language");
            }
            // Clear the unrelated test notification before photographing the key.
            journey.Tell("");
            var falling=WithInterface("FreefallPrompt");while(falling.MoveNext())yield return null;
            PassText.SetChinese(previous);
        }
        internal static void EnterFromRoom(PassJourney route)
        {
            CheckRoom(route);
            var gate=route.hubGate;
            var feet=gate.transform.position+Vector3.up*.045f;
            route.walker.Place(null,feet,gate.transform.rotation,0);
            Physics.SyncTransforms();
            PassBuild.Check(route.TryEnterPortal(gate) && route.Aboard && route.Started &&
                Vector3.Distance(route.walker.transform.position,route.sleeperSpawn.position)<.06f,
                "Hut gate did not place passenger beside the bed in 07");
        }
        static void CheckRoom(PassJourney route)
        {
            PassBuild.Check(route.InHub && !route.Aboard && !route.Started,"Expected a fresh journey in the cottage");
            route.Advance(180);
            PassBuild.Check(route.Distance==0 && !route.Started,"Train moved before the room gate was crossed");
        }
        static IEnumerator PressButton(string name)
        {
            var button=hud.rootVisualElement.Q<Button>(name);
            PassBuild.Check(button!=null && button.panel!=null && button.worldBound.height>0,"Button unavailable: "+name);
            using (var submit=NavigationSubmitEvent.GetPooled()){
                submit.target=button;
                button.SendEvent(submit);
            }
            float until=Time.unscaledTime+.17f;
            while(Time.unscaledTime<until) yield return null;
        }

        static IEnumerator CheckRoomInterface()
        {
            var root=hud.rootVisualElement;
            PassText.SetChinese(false);
            PassBuild.Check(journey.Health==100 && journey.CarriedValue==0 && journey.RunSeconds>715,
                "New run should start in the room with 100 HP, zero value and twelve minutes");
            float initialTime=journey.RunSeconds;
            journey.SetInventory(true);
            journey.AdvanceExpedition(18);
            journey.SetInventory(false);
            PassBuild.Check(Mathf.Abs(initialTime-18-journey.RunSeconds)<.02f && journey.Distance==0,
                "Backpack or cottage changed the independent run timer");
            var ui=PressButton("roomLanguage"); while(ui.MoveNext()) yield return null;
            PassBuild.Check(PassText.Chinese && root.Q<Button>("endGame").text=="结束游戏" &&
                journey.hubGate.plaque.text==PassText.Get("portal.sleeping") && journey.Distance==0,"Room language or sign stayed in English");
            ui=PressButton("endGame");while(ui.MoveNext())yield return null;
            PassBuild.Check(journey.InStartMenu && journey.HasSettlement && !journey.SettledSuccessfully &&
                root.Q("titleScreen").resolvedStyle.display!=DisplayStyle.None,
                "End Game failed to show the title menu");
            ui=PressButton("titleLanguage");while(ui.MoveNext()) yield return null;
            PassBuild.Check(!PassText.Chinese && root.Q<Button>("newGame").text=="New Game" &&
                root.Q<Button>("quitGame").text=="Quit","Title screen language switch failed");
            var journal=WithInterface("FieldJournal");while(journal.MoveNext())yield return null;
            ui=PressButton("newGame");while(ui.MoveNext()) yield return null;
            CheckRoom(journey);
            PassBuild.Check(!PassJourney.EnoughToExtract(PassJourney.TargetValue-1) &&
                PassJourney.EnoughToExtract(PassJourney.TargetValue),"Incorrect settlement boundary");
            journey.AdvanceExpedition(PassJourney.RunDuration);
            journey.EndGame();
            PassBuild.Check(journey.RunExpired && journey.Terminal && !journey.HasSettlement &&
                journey.Health==0,"A late settlement or death at zero seconds was mishandled");
            float wait=Time.unscaledTime+.16f;while(Time.unscaledTime<wait)yield return null;
            ui=PressButton("retry");while(ui.MoveNext())yield return null;
            CheckRoom(journey);
            PassBuild.Check(journey.Health==100 && journey.CarriedValue==0 && journey.RunSeconds==PassJourney.RunDuration,
                "New Game did not reset the run after expiry");
            Debug.Log("PASS EXPEDITION: room and inventory time, localized buttons, value threshold, death at zero and new run");
        }
        static IEnumerator Evaluate()
        {
            journey=Object.FindFirstObjectByType<PassJourney>();
            PassBuild.Check(journey && journey.walker,"Pass journey did not load");
            journey.ManualClock=true;
            body=journey.walker.GetComponent<CharacterController>(); eye=journey.walker.eyeCamera;
            journey.walker.enabled=false;
            atmosphere=Object.FindFirstObjectByType<PassAtmosphere>();
            hud=Object.FindFirstObjectByType<UIDocument>();
            var doors=journey.consist.GetComponentsInChildren<PassDoor>();
            gates=doors.Where(d=>d.ExteriorSide<0 && d.kind!=PassDoorKind.Cargo).ToArray();
            rear=doors.Single(d=>d.kind==PassDoorKind.Escape);
            bag=Array.Find(journey.consist.GetComponentsInChildren<PassPickup>(),p=>p.itemId=="parachute");
            cars=Enumerable.Range(1,9).Select(n=>journey.consist.Find(FleetImport.Id(n))).ToArray();
            PassBuild.Check(cars.All(t=>t) && hud && atmosphere && gates.Length==14 && bag, "Missing gameplay references");
            CheckRoom(journey);
            float ready=Time.unscaledTime+.30f;while(Time.unscaledTime<ready)yield return null;
            var buttons=CheckRoomInterface();while(buttons.MoveNext())yield return null;
            var cottage=WithInterface("Cottage");while(cottage.MoveNext())yield return null;
            EnterFromRoom(journey);
            var ep=eye.transform.localPosition;
            var er=eye.transform.localRotation;
            var start=body.transform.localPosition;
            var sceneCameraOrientation=eye.transform.rotation;
            atmosphere.Apply(-eye.transform.position.z);
            var warmupEnd=Time.realtimeSinceStartup+2.0f;
            while(Time.realtimeSinceStartup<warmupEnd || ShaderUtil.anythingCompiling)yield return null;
            for(int i=0;i<3;i++){Photo("Warmup-"+i,false);yield return null;}
            var opening=WithInterface("Opening");while(opening.MoveNext())yield return null;
            RideTo(162);
            PassBuild.Check((body.transform.localPosition-start).sqrMagnitude<.001f,"Passenger slid along moving carriage");
            WindowMatrix("Mountain");
            Window(0);
            atmosphere.SampleWeather(-eye.transform.position.z,0);
            Photo("MountainMoonlight");
            atmosphere.SampleWeather(-eye.transform.position.z,PassAtmosphere.CloudPeriod*.5f);
            Photo("MountainCloudCover");
            RideTo(696); Window(0);
            PassBuild.Check(atmosphere.TunnelBlend>.9f,"Exterior lighting still enabled in the middle of the bore");
            Photo("Tunnel"); WindowMatrix("Tunnel");
            eye.transform.localPosition=ep; eye.transform.localRotation=er;
            RideTo(PassJourney.Stop);
            PassBuild.Check(journey.Phase==JourneyPhase.Dwell && journey.StationSeconds>119.95f && journey.StationSeconds<=120,"Stop must last two minutes");
            Vector3 stopped=journey.consist.position;
            Window(0); Photo("Fellbridge"); WindowMatrix("Station");
            HomeEye();
            var motion=ExerciseDoors(); while(motion.MoveNext())yield return null;
            ServicePassages(doors);
            PassBuild.Check((stopped-journey.consist.position).sqrMagnitude<.0001f,"Dwell creep");
            ExitToPlatform(); At(new Vector3(-3.5f,1.4f,16-PassFittings.StationHouseChainage));
            eye.transform.localPosition=new Vector3(0,1.54f,0); Look(new Vector3(-8.9f,2.52f,-PassFittings.StationHouseChainage));
            Photo("Platform");
            journey.Advance(105.3f);
            PassBuild.Check(journey.Phase==JourneyPhase.Dwell && journey.StationSeconds<15,"Whistle interval missing");
            var occupiedGate=gates.First(d=>d.car==cars[6]);
            AdvanceTo(JourneyPhase.Chase);
            Vector3 fixedPlatform=body.transform.position;
            PassBuild.Check(journey.DoorAvailable(occupiedGate),"07 door locked while alongside the platform");
            journey.Advance(2);
            PassBuild.Check(Vector3.Distance(fixedPlatform,body.transform.position)<.005f && !journey.Aboard,"Station carried departing player");
            AdvanceTo(JourneyPhase.Freezing);
            PassBuild.Check(!journey.DoorAvailable(occupiedGate) && journey.Distance-journey.TrainLength>=PassJourney.PlatformEnd,"Door unlocked beyond platform");
            AdvanceTo(JourneyPhase.Frozen); journey.Retry();
            PassBuild.Check(journey.Phase==JourneyPhase.Running && journey.Aboard && Mathf.Abs(journey.Distance-880)<2 && bag.gameObject.activeSelf,"Station checkpoint restore failed");
            RideTo(PassJourney.Stop); AdvanceTo(JourneyPhase.Chase);
            var clearance=ProgressiveDoorLock(); while(clearance.MoveNext())yield return null;
            Debug.Log("PASS DWELL: 120s stop, whistle, progressive door locking, missed boarding and station checkpoint verified");
            RideTo(1390);
            At(journey.escapeLaunch.position-Vector3.forward*.9f);HomeEye();
            PassBuild.Check(!journey.TryDoor(rear),"Tail door unlocked before bridge entry");
            RideTo(1555); eye.transform.position=cars[6].TransformPoint(new Vector3(.99f,2.98f,3.75f)); Look(eye.transform.position+new Vector3(3.90f,-1.05f,-2.23f)); Photo("Gorge");
            RideTo(1740); eye.transform.position=cars[6].TransformPoint(new Vector3(-1.18f,3.03f,-5.13f)); Look(eye.transform.position+new Vector3(-2.40f,-1.51f,-3.33f)); Photo("Viaduct");WindowMatrix("Viaduct");HomeEye();
            var jump=JumpFromRear();while(jump.MoveNext())yield return null;
            PassBuild.Check(!journey.TryDeployParachute(),"Opened a canopy without collecting the pack in van 09");
            AdvanceTo(JourneyPhase.Fell);
            PassBuild.Check(journey.Health==0 && journey.LastImpactDamage>=100,"A bare high impact must exhaust all 100 HP");
            journey.Retry(); PassBuild.Check(!journey.HasParachute && bag.gameObject.activeSelf && Mathf.Abs(journey.Distance-1370)<2 && !rear.TargetOpen && !journey.canopy.activeSelf,"Empty-pack bridge checkpoint failed");
            RideTo(2112);
            // The structured route also gets an exterior engineering view of the actual break.
            eye.transform.position=new Vector3(37,17,-2102);
            Look(new Vector3(0,-4.2f,-2130));Photo("BrokenSpan"); HomeEye();
            RideTo(PassJourney.Break);PassBuild.Check(journey.Phase==JourneyPhase.Crashed,"Riding through the gap did not end the game");
            journey.Advance(4); journey.Retry();
            PassBuild.Check(journey.Aboard && journey.Phase==JourneyPhase.Running && Mathf.Abs(journey.Distance-1370)<2,"Wreck checkpoint restore failed");
            // A pack in the inventory provides no protection until the ripcord is pulled.
            PickBag(7);
            RideTo(1830);
            jump=JumpFromRear();while(jump.MoveNext())yield return null;
            journey.Advance(1.35f);
            PassBuild.Check(journey.Phase==JourneyPhase.Jumping && !journey.canopy.activeSelf,
                "Inventory silently deployed a parachute after the jump");
            AdvanceTo(JourneyPhase.Fell);
            PassBuild.Check(journey.Health==0 && journey.LastImpactDamage>=100 && !journey.AtLanding,
                "Undeployed pack prevented fatal impact");
            journey.Retry();
            PickBag(0);
            RideTo(1830);
            jump=JumpFromRear();while(jump.MoveNext())yield return null;
            journey.Advance(1.1f);
            for(int i=0;i<1200 && journey.Phase==JourneyPhase.Jumping;i++){
                if(Physics.Raycast(body.transform.position+Vector3.up*.10f,Vector3.down,out var ground,
                    350f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && ground.distance<1.40f) break;
                journey.Advance(.01f);
            }
            PassBuild.Check(journey.Phase==JourneyPhase.Jumping && journey.TryDeployParachute(),
                "Could not exercise a late ripcord pull immediately above the bank");
            AdvanceTo(JourneyPhase.Fell);
            PassBuild.Check(journey.Health==0 && journey.LastImpactDamage>=100 && !journey.AtLanding,
                "Late canopy deployment erased accumulated downward speed");
            Debug.Log("PASS IMPACT: empty pack, carried-but-unopened pack and late deployment all caused lethal landing damage");
            journey.Retry();
            foreach(var slot in new[]{0,7}){
                PickBag(slot);
                if(slot==7){
                    journey.SetInventory(true);
                    float readyBag=Time.unscaledTime+.15f;while(Time.unscaledTime<readyBag)yield return null;
                    var language=PressButton("bagLanguage");while(language.MoveNext())yield return null;
                    PassBuild.Check(PassText.Chinese && journey.ItemName("parachute")=="帆布跳伞包" &&
                        journey.Slot(7)=="parachute","Backpack language changed the item slot or left the item untranslated");
                    language=PressButton("bagLanguage");while(language.MoveNext())yield return null;
                    PassBuild.Check(!PassText.Chinese && journey.ItemName("parachute")=="Canvas parachute pack","English item name missing");
                    var inventory=WithInterface("Backpack");while(inventory.MoveNext())yield return null;
                    journey.SetInventory(false);
                }
                RideTo(slot==0?1720:2075);
                jump=JumpFromRear();while(jump.MoveNext())yield return null;
                journey.Advance(1.2f);
                PassBuild.Check(journey.Phase==JourneyPhase.Jumping && !journey.canopy.activeSelf,
                    "Carrying a parachute must not automatically pull the ripcord");
                if(slot==0){
                    var falling=CheckDeploymentHelp();while(falling.MoveNext())yield return null;
                }
                PassBuild.Check(journey.TryDeployParachute() && journey.Phase==JourneyPhase.UnderCanopy &&
                    journey.canopy.activeSelf && journey.Slot(slot)=="parachute","Manual canopy failed for slot "+slot);
                if(slot==7){
                    journey.Advance(2.6f); HomeEye();
                    Look(eye.transform.position+new Vector3(-19,5,-45));Photo("Parachute");
                }
                AdvanceTo(JourneyPhase.Won);
                PassBuild.Check(body.transform.position.y < 2f,"Won without descending to valley floor");
                journey.Retry();
                PassBuild.Check(bag.gameObject.activeSelf && !journey.HasParachute && !journey.canopy.activeSelf,"Post-flight checkpoint state retained duplicate pack or canopy");
            }
            // Independent, populated checkpoint regression. Load the authored scene again; this
            // time pick up the real bag before BOTH checkpoints and change its slot between saves.
            EditorSceneManager.LoadSceneInPlayMode(PassBuild.ScenePath,new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null; yield return null;
            journey=Object.FindFirstObjectByType<PassJourney>();journey.ManualClock=true;
            body=journey.walker.GetComponent<CharacterController>();eye=journey.walker.eyeCamera;journey.walker.enabled=false;
            atmosphere=Object.FindFirstObjectByType<PassAtmosphere>();hud=Object.FindFirstObjectByType<UIDocument>();
            bag=Array.Find(journey.consist.GetComponentsInChildren<PassPickup>(),p=>p.itemId=="parachute");gates=journey.consist.GetComponentsInChildren<PassDoor>().Where(d=>d.ExteriorSide<0 && d.kind!=PassDoorKind.Cargo).ToArray();
            cars=Enumerable.Range(1,9).Select(n=>journey.consist.Find(FleetImport.Id(n))).ToArray();
            EnterFromRoom(journey);
            PickBag(0);journey.SetInventory(true);
            float uiReady=Time.unscaledTime+.18f;while(Time.unscaledTime<uiReady)yield return null;
            var cells=hud.rootVisualElement.Q("allSlots");
            using(var select=ClickEvent.GetPooled()){ select.target=cells[0];cells[0].SendEvent(select); }
            using(var move=ClickEvent.GetPooled()){ move.target=cells[7];cells[7].SendEvent(move); }
            PassBuild.Check(journey.Slot(7)=="parachute" && journey.Slot(0)==null,"Actual inventory tile clicks did not move slot 1 to 8");
            journey.SetInventory(false);RideTo(PassJourney.Stop);
            ExitToPlatform();AdvanceTo(JourneyPhase.Frozen);journey.Retry();
            PassBuild.Check(journey.Slot(7)=="parachute" && !bag.gameObject.activeSelf && Mathf.Abs(journey.Distance-880f)<2,"Populated station checkpoint lost/doubled item");
            RideTo(PassJourney.Stop);
            journey.Advance(120.1f);RideTo(1390);
            journey.SetInventory(true);journey.MoveSlot(7,2);journey.SetInventory(false);
            RideTo(PassJourney.Break);journey.Retry();
            PassBuild.Check(journey.Slot(7)=="parachute" && journey.Slot(2)==null && !bag.gameObject.activeSelf && Mathf.Abs(journey.Distance-1370)<2,"Populated bridge checkpoint failed to undo later inventory edits");
            Debug.Log("PASS PACKED CHECKPOINTS: 09 pickup remained consumed and slot eight survived station/bridge rollback; real UI slot click handlers passed");
            Debug.Log("PASS BRIDGE: late tail unlock, unprotected fall, train wreck, manual canopy and actual ground arrival from slots 1 and 8; checkpoints restored");
        }
    }
}
