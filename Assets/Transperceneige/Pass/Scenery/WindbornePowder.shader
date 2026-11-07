Shader "Winterbourne/Windborne powder"
{
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float3 p:POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};
            V vert(A i){V o;o.p=TransformObjectToHClip(i.p);o.uv=i.uv;o.c=i.c;return o;}
            half4 frag(V i):SV_Target {float2 q=i.uv*2-1;q.x+=q.y*.27;float a=saturate((1-dot(q,q)) * 1.25);
                return half4(i.c.rgb,i.c.a*a*a*.83);}
            ENDHLSL
        }
    }
}
