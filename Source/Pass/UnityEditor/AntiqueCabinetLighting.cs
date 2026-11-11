using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    // Keep specular reflections tied to the moving coach. Each cubemap is captured
    // once from the finished compartment, including its real bulbs and walnut joinery.
    public static class AntiqueCabinetLighting
    {
        const string Folder=PassBuild.Root+"/Valuables/Optics";
        static readonly int[] Coaches={5,9};

        public static void Build(GameObject[] cars)
        {
            foreach(int number in Coaches)
                Fit(cars[number-1].transform,number,true);
        }

        static ReflectionProbe Fit(Transform car,int number,bool requireCapture)
        {
            string label="Collector's lamp "+number;
            var existing=car.Find(label);
            if(existing)Object.DestroyImmediate(existing.gameObject);
            bool shelf=number==5;
            var root=StationFabrication.Child(car,label,shelf?new Vector3(1.48f,2.111f,9.38f):new Vector3(-.94f,2.234f,4.55f)).transform;
            var lamps=new StationFabrication();
            var brass=AssetDatabase.LoadAssetAtPath<Material>(StationFabrication.Folder+"/Ticket brass.mat");
            var opal=AssetDatabase.LoadAssetAtPath<Material>(StationFabrication.Folder+"/Lit station opal.mat");
            var hood=AssetDatabase.LoadAssetAtPath<Material>(StationFabrication.Folder+"/Heritage/Lamp green.mat");
            var basePoint=shelf?new Vector3(.12f,0,-.19f):new Vector3(-.12f,0,-.21f);
            var high=basePoint+Vector3.up*(shelf?.255f:.29f);
            var neckEnd=high+new Vector3(shelf?-.32f:.34f,.068f,shelf?.31f:.102f);
            // Turned foot, swan neck and enamelled brass shade. The bulb is part of the fitting.
            lamps.Ring(brass,basePoint,basePoint+Vector3.up*.009f,.041f,.044f,32);
            lamps.Ring(brass,basePoint+Vector3.up*.009f,basePoint+Vector3.up*.025f,.044f,.013f,32);
            lamps.Ring(brass,basePoint+Vector3.up*.02f,high,.0065f,.0045f,18);
            Vector3 previous=high;
            for(int i=1;i<=12;i++){
                float t=i/12f;
                var next=Vector3.Lerp(high,neckEnd,t)+Vector3.up*(.065f*Mathf.Sin(t*Mathf.PI));
                lamps.Ring(brass,previous,next,.005f,.005f,14);previous=next;
            }
            var lampAxis=(new Vector3(0,shelf?.028f:.071f,0)-neckEnd).normalized;
            var lip=neckEnd+lampAxis*.045f;
            lamps.Ring(brass,neckEnd-lampAxis*.018f,neckEnd,.011f,.014f,24);
            lamps.Ring(hood,neckEnd,lip,.016f,.047f,32);
            lamps.Ring(brass,lip-lampAxis*.002f,lip+lampAxis*.002f,.0475f,.0475f,32);
            lamps.Ring(opal,lip,lip+lampAxis*.008f,.035f,.019f,32);
            lamps.Finish(root,shelf?"Reading rack examination light":"Guard's manifest light");
            var bulb=StationFabrication.Child(root,"Opal reading bulb",lip+lampAxis*.014f);
            var light=bulb.AddComponent<Light>();
            light.type=LightType.Spot;light.intensity=.16f;light.range=1.05f;
            bulb.transform.localRotation=Quaternion.LookRotation(lampAxis,Vector3.up);
            light.spotAngle=69;light.innerSpotAngle=38;
            light.color=new Color(1,.909f,.805f);light.shadows=LightShadows.None;
            var probe=StationFabrication.Child(root,"Coach interior reflection",new Vector3(0,.14f,0)).AddComponent<ReflectionProbe>();
            probe.resolution=128;probe.hdr=true;probe.nearClipPlane=.035f;probe.farClipPlane=31;
            probe.center=new Vector3(-root.localPosition.x,0,0);
            probe.size=new Vector3(4.75f,2.95f,24f);probe.blendDistance=.2f;
            probe.boxProjection=true;probe.importance=8;probe.intensity=.92f;
            probe.mode=ReflectionProbeMode.Custom;
            probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(PathFor(number));
            if(requireCapture)PassBuild.Check(probe.customBakedTexture,"Missing captured coach reflections for "+number);
            return probe;
        }

        static string PathFor(int number)=>Folder+"/Coach "+number+" cabin reflection.asset";
        // Run this method in a graphics-enabled Editor, using the full, stationary
        // saved train. Regular scene generation only needs the authored cubemaps.
        public static void Bake()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            if(!Directory.Exists(Folder)){Directory.CreateDirectory(Folder);AssetDatabase.Refresh();}
            var journey=Object.FindAnyObjectByType<Winterbourne.Pass.PassJourney>();
            // Reflection probes record permanent joinery only. A randomly placed item
            // must not see a duplicate of its own lid in the photographed environment.
            foreach(var item in Object.FindObjectsByType<Winterbourne.Pass.PassPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                foreach(var renderer in item.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(int number in Coaches){
                var probe=Fit(journey.consist.Find("Car"+number.ToString("00")),number,false);
                var rig=new GameObject("Photometer in coach "+number);
                var camera=rig.AddComponent<Camera>();
                rig.transform.position=probe.transform.position;
                camera.backgroundColor=new Color(.043f,.054f,.075f);
                camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;camera.enabled=false;
                camera.nearClipPlane=.035f;camera.farClipPlane=31;
                var source=new RenderTexture(128,128,24,RenderTextureFormat.ARGBHalf){
                    dimension=TextureDimension.Cube,useMipMap=true,autoGenerateMips=false};
                source.Create();
                if(!camera.RenderToCubemap(source))throw new InvalidOperationException("Renderer refused cubemap capture");
                source.GenerateMips();
                var cube=new Cubemap(source.width,TextureFormat.RGBAHalf,true);
                cube.name="Coach "+number+" interior radiance";
                var old=RenderTexture.active;float energy=0;int missingShaderPixels=0;
                for(int mip=0;mip<cube.mipmapCount;mip++){
                    int width=Mathf.Max(1,source.width>>mip);
                    var face=new Texture2D(width,width,TextureFormat.RGBAHalf,false,true);
                    for(int n=0;n<6;n++){
                        var side=(CubemapFace)n;
                        Graphics.SetRenderTarget(source,mip,side);
                        face.ReadPixels(new Rect(0,0,width,width),0,0,false);
                        var colors=face.GetPixels();
                        if(mip==0){
                            energy+=colors.Average(c=>c.maxColorComponent);
                            missingShaderPixels+=colors.Count(c=>c.r>.4f && c.b>.4f && c.g<.05f);
                        }
                        cube.SetPixels(colors,side,mip);
                    }
                    Object.DestroyImmediate(face);
                }
                RenderTexture.active=old;
                if(missingShaderPixels>source.width*source.height)
                    throw new InvalidOperationException("Missing URP shaders in captured coach "+number);
                if(energy<.015f) throw new InvalidOperationException("Empty capture for car "+number+" ("+energy+")");
                cube.Apply(false);
                var last=AssetDatabase.LoadAssetAtPath<Cubemap>(PathFor(number));
                if(last){EditorUtility.CopySerialized(cube,last);Object.DestroyImmediate(cube);EditorUtility.SetDirty(last);}
                else AssetDatabase.CreateAsset(cube,PathFor(number));
                source.Release();Object.DestroyImmediate(source);Object.DestroyImmediate(rig);
                Debug.Log("CABINET RADIANCE coach "+number+" six-face mean sum "+energy);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
