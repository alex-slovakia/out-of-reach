// LAUNCH-GRASS-V04 haze dome (original). A camera-centred inverted hemisphere, drawn after opaques and the
// sky with depth test only: everything nearer than its radius is untouched (the grass hazes itself), the far
// land and the lower sky receive a warm horizon band that glows towards the sun azimuth, which reads as low,
// hazy, back-lit air without a depth texture or a renderer feature. Intensity follows _OOR_LaunchHaze.
Shader "OutOfReach/Launch Haze Dome V04"
{
    Properties
    {
        _HazeColor("Haze away from the sun",Color)=(0.66,0.62,0.56,1)
        _GlowColor("Glow towards the sun",Color)=(1.0,0.78,0.48,1)
        _Density("Opacity at the horizon",Range(0,1))=0.55
        _BandSharpness("Band sharpness above the horizon",Float)=9
        _GlowPower("Sun glow sharpness",Float)=5
        _GlowStrength("Sun glow strength",Range(0,2))=0.9
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-60" "IgnoreProjector"="True"}
        Pass
        {
            Name "HazeDome"
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _HazeColor, _GlowColor;
            half _Density, _GlowStrength;
            float _BandSharpness, _GlowPower;
            CBUFFER_END
            float _OOR_LaunchHaze;
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
                float band=exp(-max(dir.y+.015,0)*_BandSharpness);
                float2 sunH=normalize(light.direction.xz+1e-4), viewH=normalize(dir.xz+1e-4);
                float azimuth=saturate(dot(viewH,sunH)*.5+.5);
                float glow=pow(azimuth,_GlowPower)*band+pow(saturate(dot(dir,light.direction)),24)*.6;
                half3 col=lerp(_HazeColor.rgb,_GlowColor.rgb,saturate(glow))*max(light.color,.35);
                col+=_GlowColor.rgb*glow*_GlowStrength*.35;
                float alpha=saturate(_Density*band*saturate(_OOR_LaunchHaze)+glow*_GlowStrength*.12*saturate(_OOR_LaunchHaze));
                return half4(col,alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
