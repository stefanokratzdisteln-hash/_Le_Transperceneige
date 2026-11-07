Shader "Winterbourne/Pass sky"
{
    Properties {
        [NoScaleOffset] _CloudDensity("Cloud optical cover",2D)="black"{}
        _CloudSample("Cloud coordinates at the moon",Vector)=(0,.4375,0,0)
        _MoonDirection("Moon direction",Vector)=(-.9858,.160,.050,0)
        _MoonTransmission("Direct moonlight",Range(0,1))=1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct SkyRay { float4 position:SV_POSITION; float3 ray:TEXCOORD0; };
            SkyRay vert(float4 position:POSITION)
            {
                SkyRay o; o.position=TransformObjectToHClip(position.xyz); o.ray=position.xyz; return o;
            }

            TEXTURE2D(_CloudDensity); SAMPLER(sampler_CloudDensity);
            float4 _CloudSample,_MoonDirection;
            float _MoonTransmission;

            // Perspective foreshortening of a broad, low sheet at the horizon.
            float2 cloudPlane(float3 direction)
            {
                return direction.xz / (.19 + max(direction.y,.015)) * .43;
            }
            float hash(float2 p)
            {
                p=frac(p*float2(.3183,.3679)); p+=dot(p,p+19.19); return frac(p.x*p.y);
            }
            float stars(float3 ray)
            {
                float2 grid = float2(atan2(ray.z,ray.x) * (640/6.2831853), ray.y * 110);
                float2 cell = floor(grid);
                cell.x = cell.x - 640 * floor(cell.x/640);
                float seed = hash(cell);
                float2 offset = float2(hash(cell + 13.7),hash(cell + 85.2)) * .65 + .175;
                float radius = length(frac(grid) - offset);
                return step(.974,seed) * (1-smoothstep(.008,.077,radius)) * (.35+.65*hash(cell + 4.1));
            }
            float darkSea(float2 p,float2 center,float2 extent)
            {
                return 1-smoothstep(.56,1.08,length((p-center)/extent));
            }
            half4 frag(SkyRay i):SV_Target
            {
                float3 d=normalize(i.ray), moon=normalize(_MoonDirection.xyz);
                float h=saturate(d.y), alignment=saturate(dot(d,moon));
                float2 uv=cloudPlane(d)-cloudPlane(moon)+_CloudSample.xy;
                float raw=SAMPLE_TEXTURE2D(_CloudDensity,sampler_CloudDensity,uv).r;
                float veil=raw*smoothstep(.008,.105,h);
                float through=1-veil;
                float3 zenith=float3(.005,.010,.026);
                float3 sky=lerp(unity_FogColor.rgb,zenith,sqrt(h));
                sky+=float3(.27,.33,.43)*stars(d)*smoothstep(.045,.23,h)*through;
                float broadHalo=pow(alignment,36), innerHalo=pow(alignment,480);
                // Only the thin bank facing the moon catches a silver edge.
                float3 cloud=lerp(unity_FogColor.rgb*.84,float3(.010,.017,.031),sqrt(h));
                float silver=4*veil*through*(.11+broadHalo*.89);
                sky=lerp(sky,cloud,veil) + float3(.052,.066,.092)*silver;
                sky+=float3(.18,.23,.34)*(.16*broadHalo+.53*innerHalo)*_MoonTransmission*through;

                float2 lunar;
                float3 right=normalize(cross(float3(0,1,0),moon));
                lunar=float2(dot(d,right),dot(d,cross(moon,right)))/.0115;
                float radius=length(lunar);
                float edge=max(fwidth(radius),.02);
                float disc=(1-smoothstep(1-edge,1+edge,radius))*step(.99,alignment);
                // Dark plains across the northern hemisphere against the bright southern highlands.
                float2 face=lunar+.047*sin(lunar.yx*17+.3);
                float maria=max(darkSea(face,float2(-.34,.22),float2(.43,.47)),
                                darkSea(face,float2(.26,.20),float2(.29,.30)));
                maria=max(maria,darkSea(face,float2(.45,-.05),float2(.29,.27)));
                maria=max(maria,darkSea(face,float2(.76,.30),float2(.14,.12)));
                float shading=(.97-.37*maria)*lerp(1,.80,smoothstep(.58,1,radius));
                sky+=float3(.79,.86,.93)*disc*shading*through*through;
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
