Shader "OutOfReach/Recovery/IllustrativeMars"
{
    Properties { _BaseMap("NASA/JPL Viking global color map", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        ZWrite On ZTest LEqual Cull Back
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
        CBUFFER_END
        struct A { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
        struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; };
        V Vert(A a)
        {
            V o; o.positionCS=TransformObjectToHClip(a.vertex.xyz);
            o.normalWS=TransformObjectToWorldNormal(a.normal);
            o.uv=TRANSFORM_TEX(a.uv,_BaseMap); return o;
        }
        ENDHLSL
        Pass
        {
            Name "VikingMappedMars"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 Frag(V i):SV_Target
            {
                // Unmodified NASA/JPL/Caltech release. No noise or fabricated elevation.
                // Source imagery already contains some topographic shading and mosaic/color processing.
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                Light sun=GetMainLight();
                float illumination=max(0,dot(normalize(i.normalWS),sun.direction));
                // Existing scene light defines the terminator. The tiny 0.4% display fill is authored,
                // not atmospheric scattering or a calibrated physical illumination claim.
                return half4(albedo*(sun.color*illumination+.004),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma target 3.0
            half4 DepthFrag(V i):SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
