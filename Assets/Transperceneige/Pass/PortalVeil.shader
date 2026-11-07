Shader "Winterbourne/Portal glass"
{
    Properties { _Tint ("Ice-blue light", Color) = (.36,.70,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Input { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Input v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.uv=v.uv;
                return o;
            }
            float grain(float2 v)
            {
                return frac(sin(dot(v,float2(17.13,73.57)))*43758.53);
            }
            float mist(float2 p)
            {
                float2 tile=floor(p), f=frac(p); f=f*f*(3.-2.*f);
                return lerp(lerp(grain(tile),grain(tile+float2(1,0)),f.x),
                            lerp(grain(tile+float2(0,1)),grain(tile+1),f.x),f.y);
            }
            float billows(float2 p)
            {
                float n=.55*mist(p);
                p=mul(float2x2(.82,-.57,.57,.82),p)*2.06+float2(10.7,3.1);
                n+=.29*mist(p);
                p=mul(float2x2(.72,.69,-.69,.72),p)*2.12+float2(4.2,11.);
                return n+.16*mist(p);
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv=input.uv;
                float t=_Time.y*.095;
                float2 p=(uv-.5)*float2(1.42,1.98);
                // Several drifting layers of vapour recede into the case. Broad
                // side-lit folds preserve the depth of the dark central opening.
                float2 bend=float2(billows(float2(p.y*1.65-t*.24,p.x*2.1)),
                                   billows(float2(p.x*2.6+2.4,p.y-t*.32)))-.5;
                float2 w=p+bend*float2(.34,.18);
                float silt=billows(w*float2(4.1,2.05)+float2(2.4,-t*.32));
                float fine=billows(w*float2(11.2,4.6)+float2(silt*2.1,-t*.54));
                float bendX=.045*sin(4.3*p.y-t*.32)+bend.x*.15;
                float left=exp(-6.8*abs(p.x+.61+bendX));
                float right=exp(-7.6*abs(p.x-.60+bendX*.83));
                float edgeMist=left+right*.89;
                float2 oval=w*float2(1.78,.96);
                float angle=atan2(oval.y,oval.x);
                float slowSpiral=.5+.5*cos(angle*1.8-length(oval)*8.1+4.4*silt+t*.38);
                float centreFalloff=smoothstep(.12,.76,length(oval));
                float fold=smoothstep(.39,.85,silt*.53+fine*.30+.17*slowSpiral);
                float floorFog=exp(-uv.y*6.3)*(.4+.6*fine);
                float turn=cos(angle-length(oval)*8.6+1.65*(silt-.5)+t*.18);
                float spiralHaze=pow(saturate(.5+.5*turn),3.)*smoothstep(.09,.31,length(oval))*
                    (1.-smoothstep(.82,1.2,length(oval)));
                float glow=edgeMist*(.27+.70*fold)+floorFog*.40+centreFalloff*fold*.28+.18*spiralHaze;
                float2 motes=uv*float2(48,72)+float2(0,-t*.18);
                float speck=step(.994,grain(floor(motes)))*(1.-smoothstep(.025,.14,length(frac(motes)-.5)));
                float edgeDistance=min(min(uv.x,1.-uv.x),uv.y*.85);
                float rim=exp(-18.*edgeDistance)*(.42+.58*fine);
                half3 base=lerp(half3(.022,.027,.075),half3(.074,.091,.210),silt);
                half3 colour=base+glow*half3(.28,.38,.57)+rim*half3(.16,.23,.36)
                    +fold*half3(.040,.053,.093)+speck*.13*_Tint.rgb;
                return half4(colour,.985);
            }
            ENDHLSL
        }
    }
}
