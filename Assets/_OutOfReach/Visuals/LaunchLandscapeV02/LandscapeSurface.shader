Shader "OutOfReach/Launch Landscape V02"
{
    Properties
    {
        _BaseMap("Existing licensed rocky color",2D)="white"{}
        _BumpMap("Existing rocky normal",2D)="bump"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _TextureWeight("Rock texture contribution",Range(0,1))=0
        _Smoothness("Smoothness",Range(0,1))=0.08
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull [_Cull] ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _TextureWeight, _Smoothness, _Cull;
        CBUFFER_END
        struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;half4 color:COLOR;};
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            half3 normalWS:TEXCOORD1;
            half4 tangentWS:TEXCOORD2;
            float2 uv:TEXCOORD3;
            half4 color:COLOR;
            half fog:TEXCOORD4;
        };
        Varyings LandscapeVertex(Attributes input)
        {
            Varyings output;
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
            output.positionCS=p.positionCS;output.positionWS=p.positionWS;
            output.normalWS=n.normalWS;output.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
            output.uv=TRANSFORM_TEX(input.uv,_BaseMap);output.color=input.color;
            output.fog=ComputeFogFactor(p.positionCS.z);return output;
        }
        half3 SurfaceNormal(Varyings input,half face)
        {
            half3 n=normalize(input.normalWS)*face;
            half3 t=normalize(input.tangentWS.xyz);
            half3 b=normalize(cross(n,t))*input.tangentWS.w;
            half3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_TextureWeight*.4);
            return normalize(n*detail.z+t*detail.x+b*detail.y);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LandscapeVertex
            #pragma fragment LandscapeFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 LandscapeFragment(Varyings input,FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target
            {
                half face=IS_FRONT_VFACE(frontFace,1,-1);
                half3 n=SurfaceNormal(input,face);
                half3 photo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=input.color.rgb*_BaseColor.rgb*lerp(half3(1,1,1),photo*1.65,_TextureWeight);
                surface.metallic=0;surface.specular=half3(.04,.04,.04);surface.smoothness=_Smoothness;
                surface.occlusion=1;surface.normalTS=half3(0,0,1);surface.alpha=1;
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;lighting.positionCS=input.positionCS;lighting.normalWS=n;
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                lighting.shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                lighting.bakedGI=SampleSH(n);lighting.vertexLighting=VertexLighting(input.positionWS,n);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);lighting.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(lighting,surface);
                return half4(MixFog(color.rgb,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LandscapeVertex
            #pragma fragment DepthFragment
            half4 DepthFragment(Varyings input):SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormalsOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LandscapeVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings input,FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 n=SurfaceNormal(input,IS_FRONT_VFACE(frontFace,1,-1));
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 encoded=saturate(PackNormalOctQuadEncode(n)*.5+.5);return half4(PackFloat2To888(encoded),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
