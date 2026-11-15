Shader "OutOfReach/Land Horizon"
{
    Properties
    {
        _SageColor("Muted sage", Color) = (.31,.34,.25,1)
        _SoilColor("Exposed soil", Color) = (.32,.29,.23,1)
        _DryColor("Dry grass", Color) = (.39,.38,.29,1)
        _Smoothness("Low surface smoothness", Range(0,0.25)) = .12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back ZWrite On ZTest LEqual
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _SageColor, _SoilColor, _DryColor;
            half _Smoothness;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 metreUV : TEXCOORD0;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 metreUV : TEXCOORD2;
            half fog : TEXCOORD3;
        };
        Varyings LandVertex(Attributes input)
        {
            Varyings output;
            VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = pos.positionCS;
            output.positionWS = pos.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            // Mesh-local metre coordinates stay stable with SiteRoot translation/yaw.
            output.metreUV = input.metreUV;
            output.fog = ComputeFogFactor(pos.positionCS.z);
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LandVertex
            #pragma fragment LandFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Original small gradient-noise implementation. No repeating texture or time input.
            // Rotated, incommensurate octaves break axis-aligned patch boundaries.
            float2 Gradient(float2 cell)
            {
                uint2 c = (uint2)(int2)cell;
                uint h = c.x*1597334677u ^ c.y*3812015801u;
                h ^= h >> 16; h *= 2246822519u; h ^= h >> 13;
                float sx = (h & 1u) != 0u ? -1 : 1;
                float sy = (h & 2u) != 0u ? -1 : 1;
                if ((h & 4u) == 0u) return float2(sx,sy)*.70710678;
                return (h & 2u) != 0u ? float2(0,sx) : float2(sx,0);
            }
            float LandNoise(float2 p)
            {
                float2 c = floor(p), q = frac(p);
                float2 w = q*q*q*(q*(q*6-15)+10);
                float n00 = dot(Gradient(c), q);
                float n10 = dot(Gradient(c+float2(1,0)), q-float2(1,0));
                float n01 = dot(Gradient(c+float2(0,1)), q-float2(0,1));
                float n11 = dot(Gradient(c+1), q-1);
                return lerp(lerp(n00,n10,w.x),lerp(n01,n11,w.x),w.y);
            }
            float2 RotateDomain(float2 p) { return float2(.8*p.x-.6*p.y,.6*p.x+.8*p.y); }
            float FilteredNoise(float2 p)
            {
                // Fade unresolved frequencies toward their zero mean, including at grazing horizon angles.
                float footprint = max(length(ddx(p)),length(ddy(p)));
                float weight = 1-smoothstep(.18,.65,footprint);
                return LandNoise(p)*weight;
            }
            half4 LandFragment(Varyings input) : SV_Target
            {
                float2 p = input.metreUV;
                float broad = FilteredNoise(RotateDomain(p)*.0127+float2(17.2,-8.6));
                float middle = FilteredNoise(RotateDomain(RotateDomain(p))*.0431+float2(-3.8,21.4));
                float small = FilteredNoise(p*.193+float2(31.6,6.7));
                float grain = FilteredNoise(RotateDomain(p)*5.7+float2(2.8,-13.1));
                float soil = smoothstep(-.24,.27,broad*.85+middle*.24);
                float dry = smoothstep(.06,.42,middle*.75+small*.18)*.38;
                half3 albedo = lerp(_SageColor.rgb,_SoilColor.rgb,soil*.67);
                albedo = lerp(albedo,_DryColor.rgb,dry);
                albedo *= 1+small*.055+grain*.018;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0;
                surface.specular = half3(.04,.04,.04);
                surface.smoothness = clamp(_Smoothness+grain*.045,.04,.20);
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                // No emission: terrain must remain dependent on the root's actual scene lighting.
                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                lighting.bakedGI = SampleSH(lighting.normalWS);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(lighting,surface);
                color.rgb = MixFog(color.rgb,input.fog);
                return half4(color.rgb,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LandVertex
            #pragma fragment DepthFragment
            half4 DepthFragment(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LandVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFragment(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 encoded = saturate(PackNormalOctQuadEncode(n)*.5+.5);
                    return half4(PackFloat2To888(encoded),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
