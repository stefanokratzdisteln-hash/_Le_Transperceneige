Shader "Winterbourne/Snowbound spruce"
{
    Properties { _MainTex("Seeded spruce boughs",2D)="white"{} }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        Cull Off
        Pass {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A {float3 p:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half fog:TEXCOORD1;};
            V vert(A i){V o;o.p=TransformObjectToHClip(i.p);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 frag(V i):SV_Target {
                half4 col=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);clip(col.a-.27);
                Light l=GetMainLight();
                half3 light=clamp(SampleSH(half3(0,.88,.32)),.08,.7)+.08+l.color*(.28+.42*saturate(l.direction.y));
                return half4(MixFog(col.rgb*light,i.fog),1);
            }
            ENDHLSL
        }
        Pass {
            Name "Needle shadows" Tags {"LightMode"="ShadowCaster"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex treeCast
            #pragma fragment treeMask
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "PassCaster.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct TreeV {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct Silhouette {float4 clip:SV_POSITION;float2 uv:TEXCOORD0;};
            Silhouette treeCast(TreeV i) {RouteVertex v;v.p=i.p;v.n=i.n;Silhouette o;o.clip=caster(v);o.uv=i.uv;return o;}
            half4 treeMask(Silhouette i):SV_Target {clip(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a-.27);return 0;}
            ENDHLSL
        }
    }
}
