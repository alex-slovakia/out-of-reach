// LAUNCH-GRASS-V04 launch haze (original). One transparent pass drawn on a camera-centred mesh after the
// opaques and the sky.
// _DEPTH_FOG (used when the camera has a depth texture): real distance fog with an exponential height falloff.
//   The ray from the camera to the scene depth is integrated through a haze layer that is densest at the site
//   datum and thins with altitude, so the field, the far land and the mountains fade progressively into a warm
//   horizon, the mountain tops stay clearer than their feet, and the sky gets a low band that thins upwards.
//   Nothing closer than _StartDistance is touched (cockpit, tower, deck).
// Fallback (no depth texture): a far dome with a horizon band only; the grass applies its own aerial haze.
// Globals: _OOR_LaunchFog (0..1 multiplier), _OOR_LaunchFogGround (x = site datum, world Y).
Shader "OutOfReach/Launch Haze Dome V04"
{
    Properties
    {
        _HazeColor("Haze away from the sun",Color)=(0.80,0.79,0.76,1)
        _GlowColor("Haze towards the sun",Color)=(1.0,0.86,0.66,1)
        _Brightness("Haze brightness",Range(0,3))=1.2
        _SkyOpacity("Opacity over the sky",Range(0,1))=0.6
        _Density("Density at the site datum (per km)",Float)=0.16
        _HeightFalloff("Height falloff (m)",Float)=500
        _StartDistance("Clear distance from the camera (m)",Float)=70
        _MaxOpacity("Maximum opacity",Range(0,1))=0.88
        _GlowPower("Sun-ward glow sharpness",Float)=4
        _SkyDistance("Ray length used for the sky (m)",Float)=30000
        _FallbackDensity("Fallback: opacity at the horizon",Range(0,1))=0.5
        _BandSharpness("Fallback: band sharpness above the horizon",Float)=9
        [Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("Depth test (Always with depth fog)",Float)=4
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-60" "IgnoreProjector"="True"}
        Pass
        {
            Name "Haze"
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest [_ZTest] Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _DEPTH_FOG
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _HazeColor, _GlowColor;
            half _Brightness, _MaxOpacity, _FallbackDensity, _SkyOpacity;
            float _Density, _HeightFalloff, _StartDistance, _GlowPower, _SkyDistance, _BandSharpness, _ZTest;
            CBUFFER_END
            float _OOR_LaunchFog;
            float4 _OOR_LaunchFogGround;
            struct A { float3 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            V Vert(A a)
            {
                V o; o.positionWS=TransformObjectToWorld(a.positionOS); o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                float3 dir=normalize(i.positionWS-_WorldSpaceCameraPos);
                Light light=GetMainLight();
                float amount=saturate(_OOR_LaunchFog);
                float sunward=pow(saturate(dot(dir,light.direction)*.5+.5),_GlowPower);
                half3 tint=light.color/max(max(light.color.r,light.color.g),max(light.color.b,1e-3));
                half3 col=lerp(_HazeColor.rgb,_GlowColor.rgb,sunward)*lerp(half3(1,1,1),tint,.5)*_Brightness*lerp(.9,1.2,sunward);
            #if defined(_DEPTH_FOG)
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float raw=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                bool sky=raw<1e-7;
                #else
                bool sky=raw>.9999999;
                #endif
                float3 forward=-UNITY_MATRIX_V[2].xyz;
                float dist=sky?_SkyDistance:LinearEyeDepth(raw,_ZBufferParams)/max(dot(dir,forward),1e-3);
                float d=max(dist-_StartDistance,0);
                float b=1/max(_HeightFalloff,1);
                float y0=max(_WorldSpaceCameraPos.y+dir.y*_StartDistance-_OOR_LaunchFogGround.x,0);
                float k=dir.y*b;
                float t=abs(k)>1e-5?(1-exp(-clamp(d*k,-30,60)))/k:d; // optical path through the layer
                float fog=_Density*.001*exp(-y0*b)*max(t,0);
                float alpha=(1-exp(-fog))*_MaxOpacity*amount*(sky?_SkyOpacity:1);
            #else
                float band=exp(-max(dir.y+.015,0)*_BandSharpness);
                float alpha=saturate(_FallbackDensity*band*(.75+.5*sunward))*amount;
            #endif
                return half4(col,alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
