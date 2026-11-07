Shader "Winterbourne/Snow and schist"
{
    Properties {
        _Snow("Snow",Color)=(.88,.91,.94,1)
        _Stone("Schist",Color)=(.29,.31,.34,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Forward" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Snow,_Stone;
            CBUFFER_END
            struct In {float3 p:POSITION; float3 n:NORMAL;};
            struct V {float4 clip:SV_POSITION; float3 p:TEXCOORD0;half3 n:TEXCOORD1;half fog:TEXCOORD2;};
            V vert(In i){ V o; o.p=TransformObjectToWorld(i.p);o.clip=TransformWorldToHClip(o.p);
                o.n=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.clip.z);return o;}
            float hash(float2 p){ p=frac(p*float2(.11369,.13787));p+=dot(p,p+19.19);return frac(p.x*p.y*31.37);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            half4 frag(V i):SV_Target{
                float3 p=i.p, n=normalize(i.n);
                float broad=noise(p.xz*.033+p.y*.023);
                float strata=noise(float2(p.x*.16+p.y*.12,p.z*.12-p.y*.10));
                float chip=noise(float2(p.x*.48+p.y*.34,p.z*.45-p.y*.30));
                float slope=length(n.xz);
                float deposit=.69-slope+(broad-.51)*.46+(strata-.54)*.19+.065*n.z;
                float cover=smoothstep(-.15,.21,deposit);
                float ripple=noise(p.xz*float2(.26,.48)+broad);
                float3 snow=_Snow.rgb*(.81+.075*ripple+.10*broad);
                float3 stone=lerp(float3(.17,.19,.205),float3(.38,.37,.36),.59*strata+.41*chip);
                float3 albedo=lerp(stone,snow,cover);
                Light main=GetMainLight(TransformWorldToShadowCoord(p));
                float key=saturate(dot(n,main.direction)) * main.shadowAttenuation;
                float sky=.29+.23*n.y;
                float3 irradiance=clamp(SampleSH(n),.055,.68)+.05+main.color*(.11+key*.68);
                float3 lit=albedo*irradiance;
                lit=lit/(1+.35*lit);
                // Cool shadow in drift hollows, warm sun on the ridgelines.
                lit+=snow*cover*float3(.018,.024,.037)*sky;
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
        Pass {
            Name "Caster" Tags {"LightMode"="ShadowCaster"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex caster
            #pragma fragment empty
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "PassCaster.hlsl"
            ENDHLSL
        }
        Pass {
            Name "Depth" Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex depth
            #pragma fragment empty
            #include "PassCaster.hlsl"
            ENDHLSL
        }
    }
}
