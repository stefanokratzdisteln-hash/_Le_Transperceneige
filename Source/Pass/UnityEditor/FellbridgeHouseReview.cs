using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Winterbourne.Demo;
using Winterbourne.Pass;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    // -batchmode -force-d3d11 -executeMethod Winterbourne.Editor.FellbridgeHouseReview.Run
    // Runs the player's capsule through both platform doors and up/down the actual stair.
    [InitializeOnLoad]
    public static class FellbridgeHouseReview
    {
        const string Flag="Fellbridge.HouseReview";
        static FellbridgeHouseReview() { EditorApplication.update+=Tick; }

        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            ShaderUtil.allowAsyncCompilation=false;
            SessionState.SetBool(Flag,true);
            SessionState.SetString(Flag+".Started",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        static IEnumerator inspection;
        static int lastFrame=-1;
        static PassJourney route;
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)) return;
            if ((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Flag+".Started","")).ToUniversalTime()).TotalMinutes>7)
            {
                SessionState.EraseBool(Flag);
                Debug.LogError("Fellbridge station review timed out");
                EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isPaused) EditorApplication.isPaused=false;
            EditorApplication.QueuePlayerLoopUpdate();
            if(!EditorApplication.isPlaying || Time.frameCount<10 || ShaderUtil.anythingCompiling || lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            try {
                if(inspection==null)inspection=CheckAndPhotograph();
                if(inspection.MoveNext())return;
                SessionState.EraseBool(Flag);EditorApplication.Exit(0);
            } catch(Exception e) {SessionState.EraseBool(Flag);Debug.LogException(e);EditorApplication.Exit(1);}
        }

        static void Walk(Car07Walker walker,Transform house,float x,float z,float expectedY)
        {
            var target=house.TransformPoint(new Vector3(x,expectedY,z));
            for(int i=0;i<760;i++)
            {
                var d=walker.transform.InverseTransformDirection(target-walker.transform.position);
                var horizontal=new Vector2(d.x,d.z);
                if(horizontal.magnitude<.09f)
                {
                    var position=house.InverseTransformPoint(walker.transform.position);
                    PassBuild.Check(Mathf.Abs(position.y-expectedY)<.26f,$"Unexpected floor {position:F2} for {x},{expectedY},{z}");
                    return;
                }
                walker.MoveCharacter(Vector2.ClampMagnitude(horizontal*3.5f,1),false,1f/60);
                route.ReconcileBoarding();
                route.Advance(1f/60);
                PassBuild.Check(route.Phase==JourneyPhase.Dwell,"Train departed during the station walking circuit");
            }
            throw new InvalidOperationException($"Station route to {x},{expectedY},{z} obstructed at {house.InverseTransformPoint(walker.transform.position):F2}");
        }

        static IEnumerator CheckAndPhotograph()
        {
            var root=GameObject.Find("Fellbridge Station/Fellbridge station house").transform;
            var journey=Object.FindAnyObjectByType<PassJourney>();
            route=journey; journey.ManualClock=true;
            var controller=journey.walker;
            controller.enabled=false;
            controller.externalMotion=false;
            var air=Object.FindAnyObjectByType<PassAtmosphere>();
            air.Apply(1005);
            CheckSnow(root,air);
            CheckBakedSnow(root);
            PassReview.EnterFromRoom(journey);
            for(int i=0; i<1500 && journey.Phase!=JourneyPhase.Dwell;i++) journey.Advance(.25f);
            PassBuild.Check(journey.Phase==JourneyPhase.Dwell && Mathf.Abs(journey.StationSeconds-120)<.3f,
                "Did not arrive for the full two-minute stop");
            var car=journey.consist.Find("Car07");
            var entry=root.TransformPoint(new Vector3(0,0,7.5f));
            var door=car.GetComponentsInChildren<PassDoor>().Where(d=>d.kind==PassDoorKind.Passenger && d.ExteriorSide<0)
                .OrderBy(d=>Mathf.Abs(d.threshold.position.z-entry.z)).First();
            Vector3 inner=new Vector3(-.92f,1.405f,door.threshold.position.z);
            controller.Place(journey.consist,journey.consist.InverseTransformPoint(inner),Quaternion.identity,0);
            Physics.SyncTransforms();
            PassBuild.Check(journey.TryDoor(door),"07 station door did not open");
            for(int i=0;i<150 && !door.IsOpen;i++){journey.Advance(Time.deltaTime);yield return null;}
            PassBuild.Check(door.IsOpen,"07 door leaf obstructed");
            float doorZ=root.InverseTransformPoint(door.threshold.position).z;
            Walk(controller,root,6.22f,doorZ,0);
            PassBuild.Check(!journey.Aboard,"07 sill did not transfer the player to the platform");
            Walk(controller,root,6.22f,7.5f,0);
            Walk(controller,root,-3.75f,7.5f,0);
            var arch=root.Find("Fellbridge portal arch anchor");
            var arrival=root.Find("Fellbridge portal arrival");
            PassBuild.Check(arch && arrival && Mathf.Abs(arch.localPosition.y)<.01f && arrival.localPosition.y<.1f,
                "Station connection must serve the ground-floor booking hall");
            Walk(controller,root,arrival.localPosition.x,arrival.localPosition.z,0);
            Walk(controller,root,arch.localPosition.x,arch.localPosition.z-.83f,0);
            Walk(controller,root,-5.45f,5.8f,0);
            Walk(controller,root,-5.45f,-3.1f,0);
            Walk(controller,root,-7.88f,-3.0f,0);
            Walk(controller,root,-7.88f,4.64f,3.42f);
            Walk(controller,root,-4.65f,5.66f,3.42f);
            Walk(controller,root,-2.55f,-1.65f,3.42f);
            Walk(controller,root,-2.55f,-4.7f,3.42f);
            Walk(controller,root,-2.55f,-1.65f,3.42f);
            Walk(controller,root,-4.85f,5.30f,3.42f);
            Walk(controller,root,-7.88f,4.64f,3.42f);
            Walk(controller,root,-7.88f,-3.02f,0);
            Walk(controller,root,-1.43f,-3.02f,0);
            Walk(controller,root,-1.43f,-7.5f,0);
            Walk(controller,root,-3.11f,-7.55f,0);
            Walk(controller,root,-1.43f,-7.5f,0);
            Walk(controller,root,6.22f,-7.5f,0);
            Walk(controller,root,6.22f,doorZ,0);
            Walk(controller,root,8.02f,doorZ,0);
            PassBuild.Check(journey.Aboard && journey.StationSeconds>0,
                "Could not return to sleeper 07 while the train was at Fellbridge");
            Debug.Log($"PASS NIGHT ROUND TRIP: exited sleeper 07, visited rooms and came back aboard in {120-journey.StationSeconds:F1}s of real railway timetable");
            Debug.Log("PASS STATION HOUSE: actual player motor walked both platform doors, ground-floor portal approach, ticket counter, duty room, stairs in both directions and luggage room.");
            var camera=controller.eyeCamera;
            var original=camera.transform.localPosition;var rot=camera.transform.localRotation;
            var field=camera.fieldOfView;
            try
            {
                var pictures=new[]{
                    ("FellbridgeBookingHall",new Vector3(-3.88f,1.59f,4.73f),new Vector3(-6.54f,1.60f,6.43f),58f),
                    ("FellbridgeTicketWindow",new Vector3(-5.68f,1.48f,6.02f),new Vector3(-6.42f,1.15f,6.30f),54f),
                    ("FellbridgeCardTicket",new Vector3(-5.83f,1.50f,6.16f),new Vector3(-6.22f,1.04f,6.22f),31f),
                    ("FellbridgeParcelsRoom",new Vector3(-1.69f,1.56f,-6.95f),new Vector3(-4.1f,.94f,-8.78f),65f),
                    ("FellbridgeScaleBeam",new Vector3(-2.30f,1.5f,-7.70f),new Vector3(-2.85f,.94f,-8.80f),41f),
                    ("FellbridgeHearth",new Vector3(-2.4f,1.56f,-.88f),new Vector3(-4.38f,.92f,-3.95f),60f),
                    ("FellbridgeDutyRoom",new Vector3(-3.72f,5.00f,-5.54f),new Vector3(-2.02f,4.39f,-6.24f),60f),
                    ("FellbridgeDutyWindow",new Vector3(-1.19f,5.03f,-7.49f),new Vector3(8.23f,3.18f,-9.23f),64f),
                    ("FellbridgeStationHouse",new Vector3(6.53f,1.68f,-13.6f),new Vector3(-3.3f,3.35f,.38f),66f)};
                camera.enabled=true;
                foreach(var pic in pictures) {
                    camera.fieldOfView=pic.Item4;
                    camera.transform.position=root.TransformPoint(pic.Item2);
                    camera.transform.rotation=Quaternion.LookRotation(root.TransformPoint(pic.Item3)-camera.transform.position);
                    for(int f=0;f<8;f++)yield return null;
                    Image(camera,pic.Item1);
                }
                // A separate observation from the duty room after the round-trip:
                // keep the feet and eye fixed in the station as the train leaves under its own timetable.
                var window=root.TransformPoint(new Vector3(-1.19f,5.03f,-7.49f));
                controller.Place(null,window-Vector3.up*1.54f,Quaternion.identity,0);
                journey.ReconcileBoarding();
                camera.fieldOfView=64;
                camera.transform.position=window;
                camera.transform.LookAt(root.TransformPoint(new Vector3(8.23f,3.18f,-9.23f)));
                for(int i=0;i<8;i++)yield return null;
                var before=Image(camera,"FellbridgeDutyWindow",true);
                journey.Advance(journey.StationSeconds+.25f);
                var tail=journey.consist.Find("Car09");
                for(int i=0;i<300 && -tail.position.z-10 < -window.z+15;i++) journey.Advance(.25f);
                PassBuild.Check(journey.Phase==JourneyPhase.Chase && -tail.position.z-10 > -window.z+14.8f &&
                    Vector3.Distance(camera.transform.position,window)<.01f,"Last vehicle failed to pass the fixed station window");
                for(int i=0;i<10;i++)yield return null;
                var after=Image(camera,"FellbridgeTrainDeparted",true);
                int changed=0;
                // The view through the window occupies the middle two-thirds; the train is below the horizon.
                for(int y=215;y<495;y+=2)for(int x=170;x<985;x+=2)
                {
                    int idx=y*1440+x;
                    if(Math.Abs(before[idx].r-after[idx].r)+Math.Abs(before[idx].g-after[idx].g)+
                        Math.Abs(before[idx].b-after[idx].b)>38) changed++;
                }
                PassBuild.Check(changed>2600,"Window view stayed fixed as the last coach passed: "+changed+" sample pixels");
                Debug.Log($"PASS OFFICE DEPARTURE: stationary observer, departing tail passed at {journey.Distance:F1} m; {changed} changed samples in train window");
            }
            finally { camera.transform.SetLocalPositionAndRotation(original,rot);camera.fieldOfView=field; }
        }

        static void CheckSnow(Transform house,PassAtmosphere weather)
        {
            var snow=weather.GetComponentInChildren<ParticleSystem>();
            PassBuild.Check(snow,"No snow emitter in the station weather scene");
            var emission=snow.emission;
            emission.enabled=false;
            snow.Clear();
            var flake=new ParticleSystem.EmitParams{position=house.TransformPoint(new Vector3(-3.5f,2f,0)),
                velocity=Vector3.zero,startLifetime=3,startSize=.03f};
            snow.Emit(flake,1);
            flake.position=house.TransformPoint(new Vector3(4.5f,2f,0));snow.Emit(flake,1);
            snow.Simulate(.08f,false,false);
            var particles=new ParticleSystem.Particle[4];
            int count=snow.GetParticles(particles);
            PassBuild.Check(count==1 && house.InverseTransformPoint(particles[0].position).x>3,
                "Station roof should exclude interior snowfall and retain the flake outside. Count="+count);
            snow.Clear();emission.enabled=true;snow.Play(false);
        }

        static void CheckBakedSnow(Transform house)
        {
            var bounds=new Bounds(house.TransformPoint(new Vector3(-4.5f,3.34f,0)),new Vector3(8.65f,6.68f,19.64f));
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var render=filter.GetComponent<MeshRenderer>();
                if(!render || !render.bounds.Intersects(bounds)) continue;
                var mesh=filter.sharedMesh;if(!mesh||!mesh.isReadable) continue;
                var materials=render.sharedMaterials;
                Vector3[] positions=null;
                for(int sub=0;sub<Math.Min(mesh.subMeshCount,materials.Length);sub++)
                {
                    var mat=materials[sub];
                    if(!mat || mat.name.IndexOf("snow",StringComparison.OrdinalIgnoreCase)<0) continue;
                    if(positions==null)
                    {
                        positions=mesh.vertices;
                        var matrix=house.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                        for(int p=0;p<positions.Length;p++) positions[p]=matrix.MultiplyPoint3x4(positions[p]);
                    }
                    var faces=mesh.GetTriangles(sub);
                    for(int f=0;f<faces.Length;f+=3)
                    {
                        var a=positions[faces[f]];var b=positions[faces[f+1]];var c=positions[faces[f+2]];
                        if(Mathf.Max(a.y,Mathf.Max(b.y,c.y))<.015f || Mathf.Min(a.y,Mathf.Min(b.y,c.y))>6.68f) continue;
                        float dx=b.x-a.x,dz=b.z-a.z,ex=c.x-a.x,ez=c.z-a.z;
                        float determinant=dx*ez-dz*ex;
                        if(Mathf.Abs(determinant)<.00001f) continue;
                        for(float x=-8.65f;x<-.15f;x+=.5f)
                            for(float z=-9.7f;z<9.8f;z+=.5f)
                            {
                                float u=((x-a.x)*ez-(z-a.z)*ex)/determinant;
                                float v=(dx*(z-a.z)-dz*(x-a.x))/determinant;
                                if(u<0||v<0||u+v>1)continue;
                                float h=a.y+u*(b.y-a.y)+v*(c.y-a.y);
                                PassBuild.Check(h<.015f||h>6.68f,$"Baked {mat.name} in station house at {x},{h},{z} in {filter.name}");
                            }
                    }
                }
            }
            Debug.Log("PASS STATION ROOF: exterior flakes survive, interior flakes culled; no baked snow surface inside either storey.");
        }

        static Color32[] Image(Camera eye,string name,bool sample=false)
        {
            var initial=eye.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                eye.targetTexture=rt;eye.aspect=1.6f;
                // Prime camera-dependent lights and mesh batches after moving to a different room.
                for(int i=0;i<3;i++)
                    RenderPipeline.SubmitRenderRequest(eye,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
                var dir=FleetImport.Argument("-passPhotoDir")??Path.Combine(FleetImport.Checkout,"Previews","Pass");
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir,name+".png"),pixels.EncodeToPNG());
                return sample?pixels.GetPixels32():null;
            }
            finally
            {
                eye.targetTexture=initial;RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
