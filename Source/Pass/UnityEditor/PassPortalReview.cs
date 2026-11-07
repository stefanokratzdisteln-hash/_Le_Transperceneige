// Play Mode proof using the authored portal thresholds, the physical walker and real terrain.
// -executeMethod Winterbourne.Editor.PassPortalReview.Run (omit -quit).
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Winterbourne.Pass;
using Object=UnityEngine.Object;

namespace Winterbourne.Editor
{
    [InitializeOnLoad]
    public static class PassPortalReview
    {
        const string Job="Winterbourne.KeeperReview";
        static IEnumerator sequence;
        static int previous=-1;
        static PassJourney route;
        static CharacterController capsule;
        static Vector3 Feet => route.walker.transform.position;
        static PassPortalReview(){EditorApplication.update+=Tick;}

        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath,OpenSceneMode.Single);
            ShaderUtil.allowAsyncCompilation=false;
            SessionState.EraseInt(Job+".exit");
            SessionState.SetBool(Job,true);
            SessionState.SetString(Job+"Limit",DateTime.UtcNow.AddMinutes(6).ToString("O"));
            var game=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(game).Focus();
            Application.runInBackground=true;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            int exit=SessionState.GetInt(Job+".exit",-1);
            if(exit>=0)
            {
                if(!EditorApplication.isPlayingOrWillChangePlaymode) {SessionState.EraseInt(Job+".exit");EditorApplication.Exit(exit);}
                return;
            }
            if(!SessionState.GetBool(Job,false)) return;
            try {
                Check(DateTime.UtcNow < DateTime.Parse(SessionState.GetString(Job+"Limit","")).ToUniversalTime(),"test timeout");
                if (EditorApplication.isPlaying && EditorApplication.isPaused) EditorApplication.isPaused=false;
                EditorApplication.QueuePlayerLoopUpdate();
                if(!EditorApplication.isPlaying || Time.frameCount<3 || previous==Time.frameCount) return;
                previous=Time.frameCount;
                if(sequence==null) sequence=Evaluate();
                if(sequence.MoveNext()) return;
                Debug.Log("KEEPER PASS: physical start, station visit and late return, walkable touchdown at three release points, replay and menu");
                Stop(0);
            }
            catch(Exception e) {Debug.LogException(e);Stop(1);}
        }

        static void Stop(int status)
        {
            SessionState.EraseBool(Job);
            SessionState.SetInt(Job+".exit",status);
            if(EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        static void Check(bool condition,string reason)
        {
            if(!condition) throw new InvalidOperationException("KEEPER: "+reason);
        }

        internal static void Photo(string name,Camera camera=null)
        {
            string path=FleetImport.Argument("-portalImages");
            if(string.IsNullOrEmpty(path)) return;
            Directory.CreateDirectory(path);
            var eye=camera ? camera : route.walker.eyeCamera;
            var old=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(eye,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();
                var pixels=texture.GetPixels32();
                int samples=0, errors=0;
                for(int i=0;i<pixels.Length;i+=97)
                {
                    var c=pixels[i];samples++;
                    if(c.r>230 && c.b>230 && c.g<30)errors++;
                }
                Check(errors<(samples/1000)+1,name+" contains Unity error-material magenta");
                File.WriteAllBytes(Path.Combine(path,name+".png"),texture.EncodeToPNG());
            }
            finally {RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
        }

        static IEnumerator WalkThrough(PassPortal gate,Func<bool> arrived)
        {
            Check(!gate.Contains(capsule),"arrival overlaps outgoing threshold on "+gate.name);
            Vector3 delta=gate.transform.position-Feet;
            var facing=Quaternion.LookRotation(Vector3.ProjectOnPlane(delta,Vector3.up));
            var parent=route.walker.transform.parent;
            route.walker.Place(parent,parent?parent.InverseTransformPoint(Feet):Feet,
                parent?Quaternion.Inverse(parent.rotation)*facing:facing,0);
            for(int f=0;f<210 && !arrived();f++)
            {
                route.walker.MoveCharacter(Vector2.up,false,1f/30);
                yield return null;
            }
            Check(arrived(),"physical approach failed to "+gate.name+" at "+Feet+" under "+route.Phase);
        }

        static void StationTime()
        {
            for(int i=0;i<480 && route.Phase!=JourneyPhase.Dwell;i++) route.Advance(.5f);
            Check(route.Phase==JourneyPhase.Dwell && route.StationSeconds>119.48f,"dwell should start at 120s: "+route.StationSeconds);
        }

        static void StationApproach()
        {
            // Start the local walkthrough on the actual hall floor, facing the arch.
            Vector3 floor=route.stationGate.transform.position-route.stationGate.transform.forward*1.42f+Vector3.up*.04f;
            route.walker.Place(null,floor,route.stationGate.transform.rotation,0);
            route.ReconcileBoarding();
            Check(!route.Aboard,"station hall was attached to the consist");
        }

        static void Pick()
        {
            if(route.HasParachute) return;
            var item=Array.Find(route.consist.GetComponentsInChildren<PassPickup>(),p=>p.itemId=="parachute");
            var spot=item.transform.position+new Vector3(.70f,1.405f-item.transform.position.y,0);
            route.walker.Place(route.consist,route.consist.InverseTransformPoint(spot),Quaternion.identity,0);
            route.walker.eyeCamera.transform.LookAt(item.transform.position);
            Check(route.TryPick(item) && route.HasParachute,"test pickup");
        }

        static void Release(float distance)
        {
            StationTime();
            route.Advance(120.05f);
            while((route.Distance<distance || !route.EscapeReady) && !route.Terminal)route.Advance(.08f);
            float releasedAt=route.Distance;
            var rear=Array.Find(route.consist.GetComponentsInChildren<PassDoor>(),d=>d.kind==PassDoorKind.Escape);
            rear.SetOpen(true,true);
            route.walker.Place(route.consist,route.consist.InverseTransformPoint(route.escapeLaunch.position),
                Quaternion.identity,0);
            Check(route.EscapeReady && route.TryJump(),"failed to leap at "+distance);
            route.Advance(.95f);
            Check(!route.canopy.activeSelf && route.Phase==JourneyPhase.Jumping && route.TryDeployParachute(),
                "manual deployment failed after release at "+distance);
            for(int n=0;n<550 && !route.AtLanding && !route.Terminal;n++) route.Advance(.1f);
            Check(route.AtLanding && !route.Terminal && capsule.enabled && !route.walker.externalMotion &&
                  !route.canopy.activeSelf && route.returnGate.gameObject.activeInHierarchy,"successful landing must permit walking");
            float separation=Vector3.Distance(Feet,route.returnGate.transform.position);
            Check(separation>=3.8f && separation<=7.25f,"return gate separation "+separation);
            Debug.Log($"KEEPER LANDING: release {releasedAt:F1}, touchdown {Feet:F1}, gate {separation:F2} m away");
        }

        static IEnumerator Evaluate()
        {
            route=Object.FindFirstObjectByType<PassJourney>();
            capsule=route.walker.GetComponent<CharacterController>();
            route.ManualClock=true;
            route.walker.enabled=false;
            Debug.Log("KEEPER PLAY: walker starts in keeper's room at "+Feet);
            Check(route.InHub && !route.Aboard && !route.Started && !route.InStartMenu,"spawn must be the keeper's room");
            Photo("KeepersRoom");
            route.Advance(12);
            Check(route.Distance==0 && !route.Started,"room advanced idle train");
            var shelves=PassBookcaseReview.Sweep(route,route.hubGate,true);
            while(shelves.MoveNext())yield return null;
            var board=WalkThrough(route.hubGate,()=>route.Aboard);
            while(board.MoveNext())yield return null;
            Check(Vector3.Distance(Feet,route.sleeperSpawn.position)<.13f && route.Started,"wrong sleeper spawn");
            Pick(); StationTime();
            StationApproach();
            shelves=PassBookcaseReview.Sweep(route,route.stationGate,false);
            while(shelves.MoveNext())yield return null;
            var visit=WalkThrough(route.stationGate,()=>route.VisitingStation);
            while(visit.MoveNext())yield return null;
            route.Advance(route.StationSeconds-13);
            Check(route.StationSeconds>12.9f && route.StationSeconds<13.1f && route.HasParachute,"clock or gear lost in hut");
            var platform=WalkThrough(route.hubGate,()=>!route.InHub);
            while(platform.MoveNext())yield return null;
            Check(Vector3.Distance(Feet,route.stationSpawn.position)<.13f && route.HasParachute && !route.Aboard,"did not return to same platform");
            StationApproach(); visit=WalkThrough(route.stationGate,()=>route.VisitingStation);
            while(visit.MoveNext())yield return null;
            route.Advance(19);
            Check(route.Phase==JourneyPhase.Chase && route.VisitingStation,"train did not depart during hut visit");
            platform=WalkThrough(route.hubGate,()=>!route.InHub);
            while(platform.MoveNext())yield return null;
            Check(route.Phase==JourneyPhase.Chase && route.HasParachute && !route.Aboard,
                "recoverable chase must return to the station, not the train");
            StationApproach(); visit=WalkThrough(route.stationGate,()=>route.VisitingStation);
            while(visit.MoveNext())yield return null;
            route.Advance((PassJourney.PlatformEnd+route.TrainLength-route.Distance)/2.75f+5f);
            Check(route.InHub && route.MissedTrainInHub && !route.Terminal && !route.VisitingStation,"missing train in hut should be safe");
            route.EndGame(); Check(route.InStartMenu && route.HasSettlement && !route.SettledSuccessfully,"hut End Game should settle empty value");
            route.NewGame();
            Check(route.InHub && !route.MissedTrainInHub && route.Distance==0 && !route.HasParachute,"New Game reset");
            Debug.Log("KEEPER STATION: station return, live 120-second dwell, departure, safe miss and New Game passed");
            float firstEscape=PassJourney.Bridge+route.TrainLength+4.2f;
            for(float nose=firstEscape;nose<PassJourney.Break;nose+=.75f)
            {
                var deployment=new Vector3(6.9f,1.64f,-nose+route.TrainLength+.30f-2.21f);
                Check(route.TryFindLandingPair(deployment,out var ground,out var gate) &&
                    Vector3.Distance(ground,gate)>=4 && Vector3.Distance(ground,gate)<=7,
                    "No safely reachable arch for chainage "+nose);
            }
            Debug.Log("KEEPER SURVEY: verified a dry, walkable touchdown and gate every .75m throughout the jump window");
            foreach(float chainage in new[]{firstEscape+1,1830f,2116f})
            {
                float beforeBoarding=route.RunSeconds;
                board=WalkThrough(route.hubGate,()=>route.Aboard);
                while(board.MoveNext())yield return null;
                Check(Mathf.Abs(beforeBoarding-route.RunSeconds)<.04f,"boarding another train reset the global clock");
                route.AdvanceExpedition(25);
                float onTrain=route.RunSeconds;
                Pick(); Release(chainage);
                Photo(chainage<firstEscape+5 ? "LandingGateEarly" : chainage<2000f ? "LandingGate" : "LandingGateLate");
                if(chainage==1830f)
                {
                    shelves=PassBookcaseReview.Sweep(route,route.returnGate,false);
                    while(shelves.MoveNext())yield return null;
                }
                var back=WalkThrough(route.returnGate,()=>route.InHub);
                while(back.MoveNext())yield return null;
                Check(!route.Started && route.Distance>=chainage && route.HasParachute &&
                    !Array.Find(route.consist.GetComponentsInChildren<PassPickup>(),p=>p.itemId=="parachute") && !route.returnGate.gameObject.activeSelf,
                    "landing gate lost the completed trip before settlement");
                Check(Mathf.Abs(onTrain-route.RunSeconds)<.04f,"landing return reset global clock");
            }
            route.AdvanceExpedition(route.RunSeconds+.2f);
            Check(route.RunExpired && route.Health==0 && route.Terminal && !capsule.enabled,"global timer must expire at the hut");
            route.Retry();
            Check(route.RunExpired && route.Health==0,"checkpoint retry bypassed expedition expiry");
            route.NewGame();
            Check(route.InHub && route.Health==100 && route.RunSeconds==PassJourney.RunDuration &&
                route.CarriedValue==0 && !route.HasParachute && !route.HasSettlement,
                "only explicit New Game should restore the expedition");
        }
    }
}
