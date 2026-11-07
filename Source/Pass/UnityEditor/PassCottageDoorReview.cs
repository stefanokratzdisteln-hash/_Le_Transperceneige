using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    using Winterbourne.Pass;

    // Run with -executeMethod Winterbourne.Editor.PassCottageDoorReview.Run; omit -quit.
    [InitializeOnLoad]
    public static class PassCottageDoorReview
    {
        const string Flag="Winterbourne.CottageDoorReview";
        static IEnumerator cases;
        static PassJourney journey;
        static PassDoor door;
        static Transform hut;
        static int frame=-1;
        static PassCottageDoorReview(){ EditorApplication.update+=Tick; }

        public static void Run()
        {
            ShaderUtil.allowAsyncCompilation=false;
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            SessionState.SetBool(Flag,true);
            SessionState.SetString(Flag+"Started",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false))return;
            try
            {
                Expect((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Flag+"Started","")).ToUniversalTime()).TotalMinutes<7,"timed out");
                if(!EditorApplication.isPlaying || Time.frameCount<7 || frame==Time.frameCount || ShaderUtil.anythingCompiling)return;
                frame=Time.frameCount;
                if(cases==null)cases=Evaluate();
                if(cases.MoveNext())return;
                SessionState.EraseBool(Flag);
                Debug.Log("COTTAGE PASS: enter, exit, closed slab, both E sightlines, continuous post-glide snow approach and settlement verified");
                EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.EraseBool(Flag);Debug.LogException(e);EditorApplication.Exit(1);}
        }

        static void Expect(bool value,string why){if(!value)throw new InvalidOperationException("COTTAGE: "+why);}
        static Vector3 Feet=>journey.walker.transform.position;
        static float StepZ=>hut.InverseTransformPoint(door.threshold.position).z;
        static Vector3 Site(float x,float z)=>hut.TransformPoint(new Vector3(x,0,z));

        static IEnumerator Walk(Vector3 target)
        {
            Vector3 initial=Feet;
            int frames=Mathf.CeilToInt(Vector3.ProjectOnPlane(target-Feet,Vector3.up).magnitude*30)+180;
            for(int n=0;n<frames;n++)
            {
                Vector3 delta=Vector3.ProjectOnPlane(target-Feet,Vector3.up);
                if(delta.magnitude<.09f)yield break;
                Vector3 axes=journey.walker.transform.InverseTransformDirection(delta.normalized);
                journey.walker.MoveCharacter(new Vector2(axes.x,axes.z),true,1f/60);
                if(n%4==0)yield return null;
            }
            throw new InvalidOperationException($"COTTAGE: walk {initial:F2} to {target:F2} stuck at {Feet:F2}");
        }

        static void Face(Vector3 spot)
        {
            Vector3 v=spot-journey.walker.eyeCamera.transform.position;
            var yaw=Quaternion.LookRotation(Vector3.ProjectOnPlane(v,Vector3.up),Vector3.up);
            journey.walker.Place(null,Feet,yaw,-Mathf.Asin(v.normalized.y)*Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }

        static void Operate(bool open)
        {
            var aim=door.TargetOpen?door.threshold.position+hut.forward*.635f:door.threshold.position;
            Face(aim);
            Expect(PassDoorTarget.Resolve(journey.walker.eyeCamera,journey.walker.interactionDistance,out var seen,out _) && seen==door,
                $"entrance not on crosshair from {hut.InverseTransformPoint(Feet):F2} aiming at {hut.InverseTransformPoint(aim):F2}");
            journey.walker.useOverride();
            Expect(door.TargetOpen==open,"E interaction at the cottage refused");
        }

        static IEnumerator WaitForDoor(bool open)
        {
            float end=Time.time+1.75f;
            while(Time.time<end && (open?!door.IsOpen:door.Openness>.005f))yield return null;
            Expect(open?door.IsOpen:door.Openness<.005f,"Door swing stopped");
        }

        static void FlyFromVan()
        {
            PassReview.EnterFromRoom(journey);
            var bag=Array.Find(journey.consist.GetComponentsInChildren<PassPickup>(),p=>p.itemId=="parachute");
            Vector3 pos=bag.transform.position+new Vector3(.70f,1.405f-bag.transform.position.y,0);
            journey.walker.Place(journey.consist,journey.consist.InverseTransformPoint(pos),Quaternion.identity,0);
            journey.walker.eyeCamera.transform.LookAt(bag.transform.position);
            Expect(journey.TryPick(bag),"could not collect the actual parachute");
            for(int i=0;i<800 && journey.Phase!=JourneyPhase.Dwell;i++)journey.Advance(.4f);
            Expect(journey.Phase==JourneyPhase.Dwell,"station not reached");
            journey.Advance(journey.StationSeconds+.05f);
            for(int i=0;i<3500 && journey.Distance<1980;i++)journey.Advance(.1f);
            var rear=Array.Find(journey.consist.GetComponentsInChildren<PassDoor>(),d=>d.kind==PassDoorKind.Escape);
            rear.SetOpen(true,true);
            journey.walker.Place(journey.consist,journey.consist.InverseTransformPoint(journey.escapeLaunch.position),Quaternion.identity,0);
            Expect(journey.TryJump(),"rear exit failed");
            journey.Advance(.98f);Expect(journey.TryDeployParachute(),"manual canopy would not open");
            for(int i=0;i<1000 && !journey.Terminal && !journey.AtLanding;i++)journey.Advance(.08f);
            Expect(journey.AtLanding && !journey.InHub,"no live landing");
        }

        static IEnumerator Evaluate()
        {
            journey=Object.FindFirstObjectByType<PassJourney>();journey.ManualClock=true;journey.walker.enabled=false;
            door=journey.CottageDoor;Expect(door && door.kind==PassDoorKind.Cottage,"door missing in saved scene");
            hut=door.car;
            Expect(journey.InsideCottage && journey.InHub && !door.TargetOpen,"incorrect fresh cottage state");
            var walk=Walk(Site(2.50f,StepZ));while(walk.MoveNext())yield return null;
            Operate(true);var movement=WaitForDoor(true);while(movement.MoveNext())yield return null;
            walk=Walk(Site(5.73f,StepZ));while(walk.MoveNext())yield return null;
            Expect(!journey.InsideCottage,"outside still counted as a room");
            journey.EndGame();Expect(!journey.InStartMenu,"able to settle from snow before boarding");
            Operate(false);movement=WaitForDoor(false);while(movement.MoveNext())yield return null;
            var c=journey.walker.GetComponent<CharacterController>();
            for(int i=0;i<44;i++)c.Move(-hut.right*.051f+Vector3.down*.009f);
            Expect(hut.InverseTransformPoint(Feet).x>4.19f,"closed panel did not physically stop the player");
            walk=Walk(Site(5.77f,StepZ));while(walk.MoveNext())yield return null;
            Operate(true);movement=WaitForDoor(true);while(movement.MoveNext())yield return null;
            walk=Walk(Site(2.50f,StepZ));while(walk.MoveNext())yield return null;
            Expect(journey.InsideCottage && !journey.Started && journey.Distance==0,"porch walk improperly advanced the first train");
            journey.NewGame();FlyFromVan();
            // The landing test start comes from the running train. All movement from
            // the actual canopy contact to the cottage is now continuous on foot.
            Vector3 touchdown=Feet;
            float remaining=journey.RunSeconds;
            int health=journey.Health;
            Vector3 landed=hut.InverseTransformPoint(Feet);
            Vector3 arch=hut.InverseTransformPoint(journey.returnGate.transform.position);
            float side=landed.z>=arch.z?1:-1;
            float legZ=arch.z+side*Mathf.Max(8.5f,Mathf.Abs(landed.z-arch.z));
            walk=Walk(Site(landed.x,legZ));while(walk.MoveNext())yield return null;
            // Leave the independent bookcase escape available. The player takes a
            // wide route around it instead of walking back through its trigger.
            walk=Walk(Site(9.25f,legZ));while(walk.MoveNext())yield return null;
            walk=Walk(Site(9.25f,StepZ));while(walk.MoveNext())yield return null;
            Face(door.threshold.position+hut.forward*.66f);
            Photo("CottageEntrance");
            walk=Walk(Site(5.75f,StepZ));while(walk.MoveNext())yield return null;
            Expect(journey.AtLanding,"test wrongly entered the magic gate or settled before opening door");
            Operate(true);movement=WaitForDoor(true);while(movement.MoveNext())yield return null;
            walk=Walk(Site(3.35f,StepZ));while(walk.MoveNext())yield return null;
            Expect(journey.InHub && journey.InsideCottage && !journey.AtLanding && !journey.returnGate.gameObject.activeSelf,
                "walking in through the open front door did not return to cottage");
            Expect(Vector3.Distance(Feet,journey.hubSpawn.position)>2 && journey.RunSeconds==remaining &&
                journey.Health==health && journey.HasParachute,"physical arrival changed feet, time, health or baggage");
            Debug.Log($"COTTAGE WALK: landed at {touchdown:F2}; walked beside the sash, crossed the threshold at {Feet:F2}, original kit and clock retained");
            journey.EndGame();Expect(journey.InStartMenu && journey.HasSettlement,"inside physical arrival could not settle");
        }

        static void Photo(string name)
        {
            string path=FleetImport.Argument("-cottagePhotos");
            if(string.IsNullOrEmpty(path))return;
            Directory.CreateDirectory(path);
            var camera=journey.walker.eyeCamera;
            var original=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try{
                for(int i=0;i<3;i++)
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(path,name+".png"),texture.EncodeToPNG());
            }finally{RenderTexture.active=original;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);}
        }
    }
}
