using System;
using System.IO;
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
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount<20||ShaderUtil.anythingCompiling)return;
            SessionState.EraseBool(Flag);
            try { CheckAndPhotograph(); EditorApplication.Exit(0); }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
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
                walker.MoveCharacter(Vector2.ClampMagnitude(horizontal*2.5f,1),false,1f/60);
            }
            throw new InvalidOperationException($"Station route to {x},{expectedY},{z} obstructed at {house.InverseTransformPoint(walker.transform.position):F2}");
        }

        static void CheckAndPhotograph()
        {
            var root=GameObject.Find("Fellbridge Station/Fellbridge station house").transform;
            var journey=Object.FindFirstObjectByType<PassJourney>();
            journey.ManualClock=true;
            var controller=journey.walker;
            controller.enabled=false;
            controller.externalMotion=false;
            var air=Object.FindFirstObjectByType<PassAtmosphere>();
            air.Apply(0,1005);
            CheckSnow(root,air);
            CheckBakedSnow(root);
            // Stage the consist in its actual stopping position for the windows.
            journey.consist.position=new Vector3(0,0,PassJourney.NoseDatum-PassJourney.Stop);
            controller.Place(null,root.TransformPoint(new Vector3(4.8f,.035f,7.5f)),Quaternion.identity,0);
            Physics.SyncTransforms();
            for(int i=0;i<5;i++)controller.MoveCharacter(Vector2.zero,false,1f/60);
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
            Walk(controller,root,4.6f,-7.5f,0);
            Debug.Log("PASS STATION HOUSE: actual player motor walked both platform doors, ground-floor portal approach, ticket counter, duty room, stairs in both directions and luggage room.");
            var camera=controller.eyeCamera;
            var original=camera.transform.localPosition;var rot=camera.transform.localRotation;
            var field=camera.fieldOfView;
            try
            {
                camera.fieldOfView=66;
                Image(camera,root,new Vector3(-1.69f,1.62f,3.27f),new Vector3(-5.40f,1.64f,8.23f),"FellbridgeBookingHall");
                Image(camera,root,new Vector3(-4.86f,5.03f,-3.42f),new Vector3(-1.77f,4.67f,-6.80f),"FellbridgeDutyRoom");
                Image(camera,root,new Vector3(-1.19f,5.03f,-7.49f),new Vector3(8.23f,3.18f,-9.23f),"FellbridgeDutyWindow");
                Image(camera,root,new Vector3(6.53f,1.68f,-13.6f),new Vector3(-3.3f,3.35f,.38f),"FellbridgeStationHouse");
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

        static void Image(Camera eye,Transform root,Vector3 localEye,Vector3 localAim,string name)
        {
            eye.transform.position=root.TransformPoint(localEye);
            eye.transform.rotation=Quaternion.LookRotation(root.TransformPoint(localAim)-eye.transform.position);
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
            }
            finally
            {
                eye.targetTexture=initial;RenderTexture.active=active; RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
