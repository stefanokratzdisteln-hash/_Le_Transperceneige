Shader "Winterbourne/Coursed masonry"
{
    Properties { _Tint("Stone",Color)=(.42,.38,.33,1)  _Course("Bed height",Float)=.35 }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Pass { Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;float _Course;
            CBUFFER_END
            struct A {float3 p:POSITION,n:NORMAL;};
            struct V {float4 clip:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float fog:TEXCOORD2;};
            V vert(A i) {V o;o.world=TransformObjectToWorld(i.p);o.n=TransformObjectToWorldNormal(i.n);o.clip=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.clip.z);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
            half4 frag(V i):SV_Target {
                float3 p=i.world,n=normalize(i.n);float s=-p.z;
                float row=p.y/_Course;
                float2 uv=float2((abs(n.x)>.65?p.z:p.x)/(_Course*2.55)+.5*fmod(floor(row),2),row);
                if(abs(n.y)>.78)uv=float2(p.x,p.z)/(.8+_Course);
                float2 pattern=frac(uv),ed=min(pattern,1-pattern);
                float mortar=smoothstep(.01,.065,min(ed.x*2.5,ed.y));
                float resolve=1-smoothstep(.12,.45,max(fwidth(uv.x),fwidth(uv.y)));
                float shade=(hash(floor(uv))-.5)*.22+(mortar-.72)*.23;
                float3 base=_Tint.rgb*(.97+resolve*shade);
                Light light=GetMainLight(TransformWorldToShadowCoord(p));
                float3 lighting=clamp(SampleSH(n),.06,.72)+.10+light.color*saturate(dot(n,light.direction))*.78*light.shadowAttenuation;
                // De-couple the narrow tunnel's light rhythm from URP's per-mesh punctual-light cap.
                if(abs(p.x)<5.9&&s>480&&s<780){
                    float ds=frac((s-(p.x>0?497:491)+10)/20)*20-10;
                    float glow=exp(-.12*ds*ds-.42*(p.y-2.65)*(p.y-2.65));
                    lighting+=float3(.14,.115,.086)+float3(1,.50,.20)*(.10+glow*1.6);
                }
                return half4(MixFog(base*lighting,i.fog),1);
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
