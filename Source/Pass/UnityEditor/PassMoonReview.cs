using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Winterbourne.Pass;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    // Run in a GPU editor with -executeMethod Winterbourne.Editor.PassMoonReview.Run
    // -moonOutput <folder>. Add -recordMoon for a full 20 fps camera sequence.
    [InitializeOnLoad]
    public static class PassMoonReview
    {
        const string Job = "Winterbourne.MoonReview";
        const int Width = 1280, Height = 800, FrameRate = 20;
        static int lastFrame = -1;
        static IEnumerator review;
        static Camera eye;
        static PassAtmosphere air;
        static RenderTexture target;
        static Texture2D pixels;
        static string output;

        static PassMoonReview() => EditorApplication.update += Tick;

        public static void Run()
        {
            PassBuild.Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Moon review needs a GPU");
            EditorSceneManager.OpenScene(PassBuild.ScenePath);
            ShaderUtil.allowAsyncCompilation = false;
            SessionState.SetBool(Job, true);
            SessionState.SetString(Job+".Start",DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Job, false)) return;
            if ((DateTime.UtcNow-DateTime.Parse(SessionState.GetString(Job+".Start","")).ToUniversalTime()).TotalMinutes>12)
            {
                Debug.LogError("Moon camera review timed out");
                Exit(1);
                return;
            }
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPlaying || Time.frameCount < 8 || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (review == null) review = Evaluate();
                if (review.MoveNext()) return;
                Debug.Log("PASS MOON: sleeper, roof and station window photographed in open and covered moonlight.");
                Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); Exit(1); }
        }

        static void Exit(int code)
        {
            SessionState.EraseBool(Job);
            Time.captureFramerate = 0;
            if (target) RenderTexture.ReleaseTemporary(target);
            if (pixels) Object.DestroyImmediate(pixels);
            EditorApplication.Exit(code);
        }

        static IEnumerator Evaluate()
        {
            output = FleetImport.Argument("-moonOutput") ?? throw new ArgumentException("Specify -moonOutput");
            Directory.CreateDirectory(output);
            bool record = Environment.GetCommandLineArgs().Contains("-recordMoon");
            var route = Object.FindAnyObjectByType<PassJourney>();
            air = Object.FindAnyObjectByType<PassAtmosphere>();
            route.ManualClock = true;
            PassReview.EnterFromRoom(route);
            route.walker.enabled = false;
            route.enabled = false; // Lock the train and drive the production weather clock at exact film timestamps.
            air.enabled = false;
            eye = route.walker.eyeCamera;
            eye.enabled = true;
            eye.aspect = Width/(float)Height;
            target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            Time.captureFramerate = FrameRate;
            var clear = 0f;
            var overcast = PassAtmosphere.CloudPeriod * .5f;
            var fixture = route.consist.GetComponentsInChildren<Light>().First(l => l.type == LightType.Point && l.enabled);
            var lampIntensity = fixture.intensity;

            // Keep the original 07 bedside eye height, turning the passenger's head towards the glass.
            eye.transform.rotation = Quaternion.LookRotation(new Vector3(-1f,.075f,-.15f));
            air.SampleWeather(-eye.transform.position.z, clear);
            for (int i=0;i<8;i++) { Render(); yield return null; }
            int frames = Mathf.RoundToInt(PassAtmosphere.CloudPeriod * FrameRate);
            if (record) Directory.CreateDirectory(Path.Combine(output,"frames"));
            float clearLight = 0, coveredLight = 0;
            for (int i=0;i<=frames;i++)
            {
                if (!record && i!=0 && i!=frames/2 && i!=frames) continue;
                float t=i/(float)FrameRate;
                air.SampleWeather(-eye.transform.position.z,t);
                yield return null;
                Render();
                if (record) File.WriteAllBytes(Path.Combine(output,"frames",i.ToString("D4")+".jpg"),pixels.EncodeToJPG(94));
                if (i==0) { clearLight=air.sun.intensity; Save("SleeperMoon"); }
                if (i==frames/2) { coveredLight=air.sun.intensity; Save("SleeperVeiled"); }
                if (i==frames) Save("SleeperMoonReturn");
            }
            PassBuild.Check(clearLight>coveredLight*1.35f && Mathf.Abs(fixture.intensity-lampIntensity)<.0001f,
                "Moonlight must dim under cloud while the carriage lamp remains lit");

            var rear=route.consist.Find("Car09").GetComponentInChildren<PassRoofLadder>();
            eye.transform.position=rear.roofLanding.position+Vector3.up*1.54f;
            eye.transform.rotation=Quaternion.LookRotation(new Vector3(-.86f,.08f,-.54f));
            var station=GameObject.Find("Fellbridge Station/Fellbridge station house").transform;
            foreach(string view in new[]{"Roof","DutyWindow"})
            {
                if(view=="DutyWindow")
                {
                    route.consist.position=new Vector3(0,0,route.NoseDatum-PassJourney.Stop);
                    eye.transform.position=station.TransformPoint(new Vector3(-1.25f,5.03f,-7.45f));
                    eye.transform.LookAt(station.TransformPoint(new Vector3(8.23f,3.18f,-9.23f)));
                    Physics.SyncTransforms();
                }
                foreach(float t in new[]{clear,overcast})
                {
                    air.SampleWeather(-eye.transform.position.z,t);
                    for(int i=0;i<8;i++) yield return null;
                    Render(); Save(view+(t==clear?"Moon":"Veiled"));
                }
            }
            // The passenger can stand here on the platform. This angle includes both
            // the warm station lanterns and the snow outside their pools of light.
            eye.transform.position=station.TransformPoint(new Vector3(5.8f,1.6f,-14.5f));
            eye.transform.rotation=Quaternion.LookRotation(new Vector3(-.88f,.135f,.48f));
            if(record) Directory.CreateDirectory(Path.Combine(output,"platform-frames"));
            air.SampleWeather(-eye.transform.position.z,clear);
            for(int i=0;i<8;i++) { Render(); yield return null; }
            for(int i=0;i<=frames;i++)
            {
                if (!record && i!=0 && i!=frames/2) continue;
                air.SampleWeather(-eye.transform.position.z,i/(float)FrameRate);
                yield return null;
                Render();
                if(record) File.WriteAllBytes(Path.Combine(output,"platform-frames",i.ToString("D4")+".jpg"),pixels.EncodeToJPG(94));
                if(i==0) Save("PlatformMoon");
                if(i==frames/2) Save("PlatformVeiled");
            }
        }

        static void Render()
        {
            var saved=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(eye,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);
                pixels.Apply(false);
            }
            finally { RenderTexture.active=saved; }
        }

        static void Save(string name)
        {
            var rgb=pixels.GetPixelData<byte>(0);
            int magenta=0;
            for(int i=0;i<rgb.Length;i+=3*23)
                if(rgb[i]>220 && rgb[i+1]<75 && rgb[i+2]>185) magenta++;
            PassBuild.Check(magenta<15,name+" contains unready material pixels");
            File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
            var disc=eye.WorldToViewportPoint(eye.transform.position-air.sun.transform.forward*1000f);
            Debug.Log($"MOON FRAME {name}: cover={air.CloudCover:F3}, key={air.sun.intensity:F3}, disc viewport={disc:F3}");
        }
    }
}
