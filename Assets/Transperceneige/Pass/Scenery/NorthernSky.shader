Shader "Winterbourne/Pass sky"
{
    Properties {
        _Horizon("Horizon",Color)=(.66,.66,.72,1)
        _Zenith("Zenith",Color)=(.18,.29,.46,1)
        _Glow("Light",Color)=(1,.75,.52,1)
        _SunVector("Sunward",Vector)=(-.75,.35,.33,0)
        _Night("Moon",Range(0,1))=0
        _Storm("Cloud",Range(0,1))=.27
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
            struct V {float4 position:SV_POSITION; float3 ray:TEXCOORD0;};
            V vert(float4 position:POSITION) { V o; o.position=TransformObjectToHClip(position.xyz); o.ray=position.xyz; return o; }
            float4 _Horizon,_Zenith,_Glow,_SunVector; float _Night,_Storm;
            float hash(float2 p) {p=frac(p*float2(.3183,.3679)); p+=dot(p,p+19.19); return frac(p.x*p.y);}
            float noise(float2 p) {float2 i=floor(p), f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            float cloud(float2 p) {
                return .53*noise(p)+.27*noise(p*2.08+4.6)+.13*noise(p*4.31+8.19)+.07*noise(p*9.0+2.7);
            }
            half4 frag(V i):SV_Target {
                float3 d=normalize(i.ray);
                float h=max(0,d.y);
                float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(h),.52));
                float disc=saturate(dot(d,normalize(_SunVector.xyz)));
                sky+=_Glow.rgb*(.34*pow(disc,18)+.23*pow(disc,120));
                float2 p=d.xz/(.17+max(h,.014))*float2(1.1,4.7)+float2(.007*_Time.y,8.9);
                float cover=smoothstep(.64-_Storm*.32,.82-_Storm*.22,cloud(p))*smoothstep(.004,.10,h);
                float3 vapour=lerp(_Horizon.rgb*.82,_Zenith.rgb*.83,h)+_Glow.rgb*.19*pow(disc,5);
                sky=lerp(sky,vapour,cover*.83);
                sky+=_Glow.rgb*smoothstep(.9993,.9999,disc)*(1-.78*cover)*lerp(1.5,.89,_Night);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
