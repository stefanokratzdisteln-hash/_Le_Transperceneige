#ifndef PASS_CASTER_INCLUDED
#define PASS_CASTER_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
float3 _LightDirection, _LightPosition;
struct RouteVertex {float4 p:POSITION; float3 n:NORMAL;};
float4 caster(RouteVertex i):SV_POSITION {
    float3 p=TransformObjectToWorld(i.p.xyz);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 dir=normalize(_LightPosition-p);
    #else
        float3 dir=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,TransformObjectToWorldNormal(i.n),dir));
    #if UNITY_REVERSED_Z
        clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #else
        clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #endif
    return clip;
}
float4 depth(RouteVertex i):SV_POSITION {return TransformObjectToHClip(i.p.xyz);}
half4 empty():SV_Target {return 0;}
#endif
