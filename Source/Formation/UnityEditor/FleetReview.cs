// Capture and traverse the saved formation with the same camera, controller and
// lamp components the player uses. Run with an active URP asset and GPU:
// -batchmode -force-d3d11 -executeMethod Winterbourne.Editor.FleetReview.Run
// -formationRoot "<checkout>"   (omit -quit; this job exits after Play Mode QA).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Winterbourne.Demo;
using Object=UnityEngine.Object;

namespace Winterbourne.Editor
{
    [InitializeOnLoad]
    public static class FleetReview
    {
        const string Flag="Winterbourne.FleetReview";
        static int phase, carNumber, previousFrame;
        static double waitUntil;
        static Camera eye;
        static CharacterController body;
        static FleetImport.Layout[] layouts;
        static float[] offsets;
        static Dictionary<Car07SlidingDoor,Vector3> openPoses;
        static FleetReview() { EditorApplication.update+=Tick; }
        public static void Run()
        {
            FleetImport.Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset,"Select URP in the review project");
            FleetImport.Require(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Interior previews require a hardware graphics device");
            EditorSceneManager.OpenScene(FleetImport.ScenePath,OpenSceneMode.Single);
            SessionState.SetBool(Flag,true);
            SessionState.SetString(Flag+".start",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        static void Exit(int code)
        {
            SessionState.EraseBool(Flag);
            EditorApplication.Exit(code);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)) return;
            try
            {
                DateTime start=DateTime.Parse(SessionState.GetString(Flag+".start",DateTime.UtcNow.ToString("O"))).ToUniversalTime();
                FleetImport.Require((DateTime.UtcNow-start).TotalSeconds<180,"Formation review timed out");
                if(!EditorApplication.isPlaying || Time.frameCount<4 || previousFrame==Time.frameCount) return;
                previousFrame=Time.frameCount;
                if(phase==0)
                {
                    var walker=Object.FindFirstObjectByType<Car07Walker>();
                    FleetImport.Require(walker,"Missing passenger");
                    body=walker.GetComponent<CharacterController>();
                    eye=Camera.main;
                    FleetImport.Require(eye && body,"Missing camera/controller");
                    walker.enabled=false;
                    eye.transform.SetParent(null,true);
                    layouts=Enumerable.Range(1,9).Select(FleetImport.Read).ToArray();
                    offsets=FleetImport.Offsets(layouts);
                    carNumber=1;
                    phase=1;
                }
                if(phase==1)
                {
                    if(carNumber<=9)
                    {
                        Capture(eye,layouts[carNumber-1],offsets[carNumber-1]);
                        if(carNumber==6)
                        {
                            var galley=new FleetImport.Layout {number=6,view=new FleetImport.View {
                                position=new[]{.16f,2.92f,-6.89f}, target=new[]{-.86f,2.42f,-4.70f},lens=18}};
                            Capture(eye,galley,offsets[5],"Galley");
                        }
                        carNumber++;
                        return;
                    }
                    ValidateWalks();
                    Teleport(new Vector3(.92f,1.395f,-6.85f));
                    openPoses=Object.FindObjectsByType<Car07SlidingDoor>(FindObjectsSortMode.None)
                        .ToDictionary(d=>d,d=>d.transform.localPosition);
                    FleetImport.Require(openPoses.Count>=4,"Missing original compartment doors");
                    foreach(var door in openPoses.Keys) door.Toggle(body);
                    waitUntil=Time.timeAsDouble+2.0;
                    phase=2;
                }
                else if(phase==2 && Time.timeAsDouble>=waitUntil)
                {
                    Physics.SyncTransforms();
                    VerifyDoorWindows();
                    foreach(var pair in openPoses)
                    {
                        var leaf=pair.Key;
                        FleetImport.Require(Vector3.Distance(leaf.transform.localPosition,pair.Value+leaf.closedOffset)<.015f,
                            "Door failed to close: "+leaf.name);
                        var box=leaf.GetComponentInChildren<BoxCollider>();
                        var axis=box.bounds.size.x<box.bounds.size.z?Vector3.right:Vector3.forward;
                        float distance=Vector3.Dot(box.bounds.extents,axis)+.075f;
                        bool blocking=Physics.RaycastAll(box.bounds.center+axis*distance,-axis,distance+.025f)
                            .Any(hit=>hit.collider==box);
                        FleetImport.Require(blocking,"Closed leaf has no collision: "+leaf.name);
                        leaf.Toggle(body);
                    }
                    waitUntil=Time.timeAsDouble+2.0;
                    phase=3;
                }
                else if(phase==3 && Time.timeAsDouble>=waitUntil)
                {
                    foreach(var pair in openPoses)
                        FleetImport.Require(Vector3.Distance(pair.Value,pair.Key.transform.localPosition)<.015f,
                            "Door did not reopen "+pair.Key.name);
                    Debug.Log("FLEET GAME VERIFIED: "+openPoses.Count+" physical slider cycles, nine cars, eight coupled crossings, Game-camera URP images");
                    Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Exit(1);}
        }
        public static void Capture(Camera eye, FleetImport.Layout layout,float offset,string shot="Interior")
        {
            Vector3 from=FleetImport.Vector(layout.view.position)+Vector3.forward*offset;
            Vector3 to=FleetImport.Vector(layout.view.target)+Vector3.forward*offset;
            eye.transform.SetPositionAndRotation(from,Quaternion.LookRotation(to-from,Vector3.up));
            const int width=1600,height=960;
            float horizontal=2*Mathf.Atan(18/layout.view.lens)*Mathf.Rad2Deg;
            eye.aspect=width/(float)height;
            eye.fieldOfView=Camera.HorizontalToVerticalFieldOfView(horizontal,eye.aspect);
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,2);
            var previous=RenderTexture.active;
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                eye.targetTexture=target;
                RenderPipeline.SubmitRenderRequest(eye,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);
                pixels.Apply();
                var samples=pixels.GetPixelData<byte>(0);
                int minimum=255, maximum=0, varied=0;
                for(int i=0;i<samples.Length;i+=309)
                {
                    int value=samples[i];
                    minimum=Math.Min(minimum,value);maximum=Math.Max(maximum,value);
                    if(value>8 && value<247) varied++;
                }
                FleetImport.Require(maximum-minimum>35 && varied>samples.Length/309/10,"Empty or clipped camera "+layout.number);
                string folder=Path.Combine(FleetImport.Checkout,"Previews","Formation");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,FleetImport.Id(layout.number)+"_"+shot+".png"),pixels.EncodeToPNG());
                Debug.Log("FLEET CAMERA "+layout.number+" on "+SystemInfo.graphicsDeviceName+" from "+from.ToString("F2"));
            }
            finally
            {
                eye.targetTexture=null;
                RenderTexture.active=previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
            }
        }
        static void VerifyDoorWindows()
        {
            var car=GameObject.Find("Car04");
            var panes=car.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("DoorPane_",StringComparison.Ordinal)).ToArray();
            FleetImport.Require(panes.Length==4,"Expected four compartment-door glazing panels");
            foreach(var pane in panes)
            {
                var leaf=pane.GetComponentInParent<Car07SlidingDoor>();
                FleetImport.Require(leaf && pane.sharedMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") &&
                    Mathf.Abs(pane.sharedMaterial.GetFloat("_ZWrite"))<.01f,"Door glass is opaque or detached on "+pane.name);
                var shape=leaf.gameObject.AddComponent<MeshCollider>();
                try
                {
                    // Inspect the actual opaque door mesh, independently of its
                    // gameplay blocking volume. A clear glass opening transmits
                    // the probe even though the closed door blocks the passenger.
                    shape.sharedMesh=leaf.GetComponent<MeshFilter>().sharedMesh;
                    Physics.SyncTransforms();
                    var bounds=pane.bounds;
                    int clear=0;
                    for(int col=-1;col<=1;col++)
                        for(int row=-1;row<=1;row++)
                        {
                            var aim=bounds.center+new Vector3(.16f,row*.38f*bounds.extents.y,col*.42f*bounds.extents.z);
                            if(!shape.Raycast(new Ray(aim,Vector3.left),out _,.33f)) clear++;
                        }
                    FleetImport.Require(clear>=6,"Solid backing obstructs door window "+pane.name);
                }
                finally {Object.DestroyImmediate(shape);}
            }
            var center=panes.OrderBy(p=>p.name).First().bounds.center-Vector3.forward*offsets[3];
            var proof=new FleetImport.Layout {number=4,view=new FleetImport.View {
                position=new[]{1.19f,center.y+.045f,center.z-.28f},target=new[]{-.30f,center.y-.075f,center.z+.12f},lens=22}};
            Capture(eye,proof,offsets[3],"DoorWindow");
            Debug.Log("FLEET DOOR GLAZING: 4 transparent moving panes, open sightlines through actual wood leaf meshes in closed position");
        }
        static void Teleport(Vector3 p)
        {
            body.enabled=false;
            body.transform.position=p;
            body.enabled=true;
            Physics.SyncTransforms();
        }
        static void Walk(IEnumerable<Vector3> route,string name)
        {
            var points=route.ToArray();
            Teleport(points[0]+Vector3.up*.012f);
            body.Move(Vector3.down*.06f);
            for(int i=1;i<points.Length;i++)
            {
                float distance=Vector3.Distance(points[i-1],points[i]);
                int steps=Math.Max(1,Mathf.CeilToInt(distance/.042f));
                for(int k=1;k<=steps;k++)
                {
                    var expected=Vector3.Lerp(points[i-1],points[i],k/(float)steps);
                    var delta=expected-body.transform.position;
                    body.Move(new Vector3(delta.x,-.024f,delta.z));
                    var actual=body.transform.position;
                    float drift=Vector2.Distance(new Vector2(actual.x,actual.z),new Vector2(expected.x,expected.z));
                    if(drift>=.063f || Mathf.Abs(actual.y-expected.y)>=.15f)
                    {
                        var near=Physics.OverlapCapsule(actual+Vector3.up*.26f,actual+Vector3.up*1.49f,.46f)
                           .Where(c=>c!=body).Select(c=>c.name+"@"+c.bounds.center.ToString("F2"));
                        Debug.Log("FLEET NEAR "+string.Join(" ",near));
                    }
                    FleetImport.Require(drift<.063f && Mathf.Abs(actual.y-expected.y)<.15f,
                        $"CharacterController blocked or falling on {name} at {expected:F3}, reached {actual:F3}");
                }
            }
            Debug.Log("FLEET CONTROLLER "+name);
        }
        static Vector3 World(int n,Vector3 local) => local+Vector3.forward*offsets[n-1];
        static void ValidateWalks()
        {
            for(int n=1;n<=9;n++)
            {
                if(n==7) continue;
                foreach(var route in layouts[n-1].routes)
                    Walk(route.points.Select(p=>World(n,p)),FleetImport.Id(n)+" / "+route.name);
            }
            for(int i=0;i<8;i++)
            {
                float joint=offsets[i]+layouts[i].socketB;
                Walk(new[]{new Vector3(0,1.395f,joint-.46f),new Vector3(0,1.395f,joint+.46f)},"coupling "+(i+1));
            }
            var bypass=World(6,new Vector3(.93f,1.395f,-5.40f));
            Teleport(bypass);
            for(int i=0;i<24;i++) body.Move(new Vector3(-.045f,-.012f,0));
            FleetImport.Require(body.transform.position.x>.60f && body.transform.position.x<.82f,
                "Restaurant guests can cross the kitchen partition");
            Debug.Log("FLEET CONTROLLER: kitchen partition physically separates the service and passenger routes");
            var path=new List<Vector3>{FleetImport.Bedside,new Vector3(.9f,1.395f,-5.21f),new Vector3(.9f,1.395f,-7.55f),
                new Vector3(0,1.395f,-7.55f),new Vector3(0,1.395f,-9.83f)};
            var restaurant=layouts[5].routes.First(r=>r.name.IndexOf("passenger",StringComparison.OrdinalIgnoreCase)>=0);

            // Preserve authored bends and sequence, reversing only when necessary.
            var through=restaurant.points.ToList();
            if(through[0].z<through[through.Count-1].z) through.Reverse();
            path.AddRange(through.Select(p=>World(6,p)));
            path.Add(World(6,new Vector3(0,1.395f,layouts[5].socketA)));
            var saloon=layouts[4].routes[0].points.ToList();
            if(saloon[0].z<saloon[saloon.Count-1].z) saloon.Reverse();
            var seatingBay=layouts[4].routes.Where(r=>r.name.StartsWith("Paired seating bay",StringComparison.Ordinal))
                .OrderByDescending(r=>r.points[0].z).First();
            path.AddRange(saloon.Where(p=>p.z>seatingBay.points[0].z).Select(p=>World(5,p)));
            path.AddRange(seatingBay.points.Select(p=>World(5,p)));
            Walk(path,"continuous Winterbourne bedside / restaurant / occupied paired seating bay");
            var figures=GameObject.Find("Travellers").GetComponentsInChildren<MeshRenderer>();
            Vector3 foot=body.transform.position;
            float clearance=float.PositiveInfinity;
            foreach(var figure in figures)
            {
                var bounds=figure.bounds;
                var axis=foot+Vector3.up*Mathf.Clamp(bounds.center.y-foot.y,FleetImport.Radius,FleetImport.Height-FleetImport.Radius);
                clearance=Mathf.Min(clearance,Mathf.Sqrt(bounds.SqrDistance(axis))-FleetImport.Radius);
            }
            FleetImport.Require(figures.Length==32 && clearance>=.005f,"Arrival capsule overlaps a seated traveller");
            Debug.Log($"FLEET ARRIVAL {seatingBay.name}, Car05 local {(foot-Vector3.forward*offsets[4]):F3}, capsule clearance to nearest passenger bounds {clearance:F3} m");
        }
    }
}
