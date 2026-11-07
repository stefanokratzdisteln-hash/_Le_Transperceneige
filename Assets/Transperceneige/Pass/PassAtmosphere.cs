using UnityEngine;
using UnityEngine.Rendering;

namespace Winterbourne.Pass
{
    /// <summary>Weather and the sound of the wheels follow the observer's chainage, not the locomotive.</summary>
    public sealed class PassAtmosphere : MonoBehaviour
    {
        public Material sky;
        public Light sun;
        public Camera passengerEye;
        public Material flakeMaterial;
        public const float CloudPeriod = 38f;
        public float CloudCover { get; private set; }
        public float TunnelBlend { get; private set; }
        // Low over the compartment-side ridge, between the sleeper's horizontal sash bars.
        static readonly Vector3 MoonDirection = new Vector3(-.9858f,.160f,.050f).normalized;
        const float LunarCloudRow = .4375f;
        static readonly Color Horizon = new Color(.075f,.117f,.176f);
        static readonly Color Moonlight = new Color(.71f,.80f,1f);
        Material weatherSky;
        Texture2D cloudDensity;
        ParticleSystem[] flurries;
        AudioSource runningSound, reflectedSound;
        AudioClip railLoop;
        float lastPosition, observedSpeed;
        bool initialPosition;
        static float ClampRamp(float a,float b,float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
        static float Noise(float a,float b) => Mathf.PerlinNoise(a,b);
        static float Ridge(float v) => 1-Mathf.Abs(2*v-1);

        public static float GroundHeight(float x,float s)
        {
            float d=Mathf.Abs(x);
            float coarse=Noise(x*.016f+73.23f,s*.009f+62.7f);
            float texture=Noise(x*.078f+311.3f,s*.057f+24.8f);
            float formation=-.29f+ClampRamp(2.0f,7.0f,d)*(.12f+coarse*.40f+texture*.22f);
            float station=1-ClampRamp(0,34,Mathf.Max(PassJourney.PlatformStart-s,s-PassJourney.PlatformEnd));
            float shelf=ClampRamp(9,55,d);
            float headland=(x<0 ? -13f : 2.8f)+4*(2*coarse-1)+1.7f*texture;
            float h=formation+shelf*headland*(1-station*(1-ClampRamp(13,48,d)));
            // A basin on the compartment side leaves sightlines between the nearby spruce
            // and granite outcrops to the middle-distance treeline and the far summits.
            float rolling=ClampRamp(78,170,d)*(4.5f+6*coarse+3*Mathf.Sin(s*.009f+x*.017f));
            h+=rolling;
            float ridge1=185+22*Mathf.Sin(s*.0061f+(x<0 ? 0 : 2.3f));
            float band=Mathf.Exp(-Mathf.Pow((d-ridge1)/48,2));
            h+=band*(8+8*coarse);
            float peakDistance=448+40*Mathf.Sin(s*.0037f+(x<0 ? .6f : 2.5f));
            float mid=Mathf.Exp(-Mathf.Pow((d-peakDistance)/96,2));
            float summit=.52f+.48f*Mathf.Sin(s*.017f+1.8f*Mathf.Sin(s*.003f));
            h+=mid*(25+43*summit+15*Ridge(Noise(x*.011f+117,s*.013f+99)));
            float far=Mathf.Exp(-Mathf.Pow((d-810)/135,2));
            h+=far*(60+50*(.5f+.5f*Mathf.Sin(s*.020f+3.2f))+30*Ridge(Noise(x*.009f+68,s*.011f+312)));
            // The bore itself is empty. Cuttings meet the independent watertight portal mesh.
            float tunnel=ClampRamp(433,504,s)*(1-ClampRamp(757,826,s));
            h+=tunnel*ClampRamp(4.4f,20,d)*(17+19*coarse)*Mathf.Exp(-d/87);
            float gorge=ClampRamp(1395,1488,s)*(1-ClampRamp(2152,2211,s));
            float leftBank=243+27*Mathf.Sin(s*.011f), rightBank=192+20*Mathf.Sin(s*.008f);
            float wall=(x<0 ? leftBank : rightBank);
            float bench=ClampRamp(wall*.62f,wall+48,d);
            float floor=-70-7*Noise(x*.019f+17,s*.014f+64)+1.1f*texture;
            float river=RiverX(s);
            floor=Mathf.Lerp(-81.9f,floor,ClampRamp(14,29,Mathf.Abs(x-river)));
            float trough=Mathf.Lerp(floor,h-14+4*coarse,bench);
            float approach=ClampRamp(1170,1350,s)*(1-ClampRamp(1400,1488,s))*ClampRamp(6,32,d);
            h-=approach*(28+12*coarse)*(1-ClampRamp(160,250,d));
            h=Mathf.Lerp(h,trough,gorge*(1-ClampRamp(wall+45,wall+145,d)));
            float tributary=ClampRamp(1100,1200,s)*(1-ClampRamp(1550,1590,s));
            float cut=1-ClampRamp(5.1f,23,Mathf.Abs(x-TributaryX(s)));
            return Mathf.Lerp(h,Mathf.Min(h,TributaryY(s)-.65f),tributary*cut);
        }

        public static float RiverX(float s) => -140 + 29*Mathf.Sin((s-1410)*.010f)+12*Mathf.Sin(s*.018f);
        public static float TributaryX(float s) => 79+12*Mathf.Sin(s*.024f)-173*ClampRamp(1400,1590,s);
        public static float TributaryY(float s) => -6f-21*ClampRamp(1100,1380,s)-54*ClampRamp(1380,1550,s);

        void Awake()
        {
            if (!passengerEye) passengerEye=Camera.main;
            if (sky) { weatherSky=new Material(sky); RenderSettings.skybox=weatherSky; }
            if (passengerEye) { passengerEye.clearFlags=CameraClearFlags.Skybox; passengerEye.farClipPlane=1650; }
            MakeSnow();
            MakeRailSound();
            Apply(passengerEye ? -passengerEye.transform.position.z : 0);
        }

        void LateUpdate()
        {
            if (passengerEye) Apply(-passengerEye.transform.position.z);
        }

        void MakeSnow()
        {
            if (!flakeMaterial) return;
            flurries=new ParticleSystem[2];
            var shelters=FindObjectsByType<PassShelterVolume>(FindObjectsSortMode.None);
            for (int i=0;i<2;i++)
            {
                var go=new GameObject("Windborne snow "+i); go.transform.SetParent(transform,false);
                var ps=go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;
                main.startLifetime=new ParticleSystem.MinMaxCurve(1.9f,3.0f);
                main.startSize=new ParticleSystem.MinMaxCurve(.009f,.030f);
                main.maxParticles=550; main.startSpeed=0; main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startColor=new ParticleSystem.MinMaxGradient(new Color(.33f,.43f,.57f,.32f),new Color(.60f,.67f,.75f,.68f));
                var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(10,5,34);
                var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.World;
                if (shelters.Length>0)
                {
                    var shelter=ps.trigger;
                    shelter.enabled=true;
                    shelter.enter=ParticleSystemOverlapAction.Kill;
                    shelter.inside=ParticleSystemOverlapAction.Kill;
                    for(int j=0;j<shelters.Length;j++)
                        shelter.SetCollider(j,shelters[j].GetComponent<BoxCollider>());
                }
                velocity.x=new ParticleSystem.MinMaxCurve(i==0 ? -.7f : .7f);
                velocity.y=new ParticleSystem.MinMaxCurve(-3.0f);velocity.z=new ParticleSystem.MinMaxCurve(.6f);
                var renderer=go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial=flakeMaterial; renderer.renderMode=ParticleSystemRenderMode.Billboard;
                renderer.alignment=ParticleSystemRenderSpace.View;
                var emission=ps.emission; emission.rateOverTime=0; ps.Play(); flurries[i]=ps;
            }
        }

        public void Apply(float viewerChainage)
        {
            SampleWeather(viewerChainage,(float)(Time.timeSinceLevelLoadAsDouble % CloudPeriod));
        }

        // Both the renderer and the exterior light sample the linear optical-cover
        // texture at the moon's ray. Supply elapsed seconds to reproduce a recorded sky.
        public void SampleWeather(float viewerChainage,float elapsedSeconds)
        {
            float bore=ClampRamp(480,492,viewerChainage)*(1-ClampRamp(768,780,viewerChainage));
            TunnelBlend=bore;
            float drift=Mathf.Repeat(elapsedSeconds/CloudPeriod,1f);
            var activeSky=weatherSky ? weatherSky : sky;
            if (activeSky)
            {
                if (!cloudDensity) cloudDensity=activeSky.GetTexture("_CloudDensity") as Texture2D;
                CloudCover=cloudDensity ? cloudDensity.GetPixelBilinear(drift,LunarCloudRow).r : 0;
                activeSky.SetVector("_CloudSample",new Vector4(drift,LunarCloudRow,0,0));
                activeSky.SetVector("_MoonDirection",MoonDirection);
                activeSky.SetFloat("_MoonTransmission",1f-CloudCover);
            }
            if (sun)
            {
                sun.color=Moonlight;
                sun.intensity=Mathf.Lerp(Mathf.Lerp(.92f,.085f,CloudCover),.038f,bore);
                sun.shadowStrength=Mathf.Lerp(.82f,.28f,CloudCover);
                sun.transform.rotation=Quaternion.LookRotation(-MoonDirection,Vector3.up);
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.24f,.31f,.43f),new Color(.11f,.10f,.10f),bore);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.12f,.17f,.24f),new Color(.09f,.085f,.08f),bore);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.09f,.12f,.17f),new Color(.055f,.053f,.05f),bore);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=Color.Lerp(Horizon,new Color(.075f,.081f,.085f),bore);
            RenderSettings.fogDensity=Mathf.Lerp(.00128f,.012f,bore);
            if (flurries!=null && passengerEye)
            {
                for(int i=0;i<flurries.Length;i++)
                {
                    // Keep all births on the outside of the windows; the van interior stays dry.
                    flurries[i].transform.position=new Vector3((i==0?-1:1)*9.3f,passengerEye.transform.position.y+4,-viewerChainage);
                    var emit=flurries[i].emission;
                    emit.rateOverTime=13f*(1-bore);
                }
            }
            if (runningSound)
            {
                // Sample train distance, not player motion on the platform.
                if (train)
                {
                    float z=train.position.z;
                    if (initialPosition && Time.deltaTime>0)
                        observedSpeed=Mathf.MoveTowards(observedSpeed,Mathf.Min(30,Mathf.Abs(z-lastPosition)/Time.deltaTime),Time.deltaTime*18);
                    initialPosition=true; lastPosition=z;
                }
                runningSound.volume=Mathf.Clamp01(observedSpeed/10)*.19f;
                runningSound.pitch=.72f+Mathf.Clamp(observedSpeed,0,20)*.030f;
                reflectedSound.volume=runningSound.volume*bore*.55f;
                reflectedSound.pitch=runningSound.pitch;
            }
        }

        [Tooltip("World transform of the complete moving consist.")] public Transform train;
        void MakeRailSound()
        {
            const int hz=22050; var samples=new float[hz*3];
            var random=new System.Random(1936); float brown=0;
            for (int i=0;i<samples.Length;i++)
            {
                float t=(float)i/hz;
                float noise=(float)random.NextDouble()*2-1;
                brown=.975f*brown+.025f*noise;
                float phase=t%.75f;
                float tap=(Mathf.Exp(-phase*48)+(phase>.11f ? Mathf.Exp(-(phase-.11f)*52)*.8f:0));
                samples[i]=brown*1.6f+tap*(.24f*noise+.22f*Mathf.Sin(2*Mathf.PI*95*t));
            }
            railLoop=AudioClip.Create("Rail joints • paired axles",samples.Length,1,hz,false);
            railLoop.SetData(samples,0);
            runningSound=gameObject.AddComponent<AudioSource>();runningSound.loop=true;runningSound.playOnAwake=false;
            runningSound.clip=railLoop;runningSound.volume=0;runningSound.Play();
            reflectedSound=gameObject.AddComponent<AudioSource>();reflectedSound.loop=true;reflectedSound.playOnAwake=false;
            reflectedSound.clip=railLoop;reflectedSound.volume=0;reflectedSound.time=.12f;reflectedSound.Play();
        }

        void OnDestroy()
        {
            if (weatherSky) { if (RenderSettings.skybox==weatherSky) RenderSettings.skybox=sky; Destroy(weatherSky); }
            if (railLoop) Destroy(railLoop);
        }
    }
}
