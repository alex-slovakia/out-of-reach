Shader "OutOfReach/Ground Finish V02"
{
    Properties
    {
        _SoilColor("Compacted soil color",2D)="white"{}
        _SoilNormal("Compacted soil normal",2D)="bump"{}
        _SoilMask("Soil AO / height / roughness",2D)="white"{}
        _GravelColor("Rocky margin color",2D)="white"{}
        _GravelNormal("Rocky margin normal",2D)="bump"{}
        _GravelMask("Gravel AO / height / roughness",2D)="white"{}
        _MacroMap("Existing landscape color",2D)="white"{}
        _SiteOriginWS("Authored site origin",Vector)=(0,0,0,0)
        _TileMetres("Soil / gravel repeat metres",Vector)=(3.4,2.6,0,0)
        _NormalStrength("Photographic normal strength",Range(0,2))=0.75
        [HideInInspector]_BaseMap("Shadow compatibility",2D)="white"{}
        [HideInInspector]_BaseColor("Shadow compatibility",Color)=(1,1,1,1)
        [HideInInspector]_Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Back ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
        TEXTURE2D(_SoilColor); SAMPLER(sampler_SoilColor);
        TEXTURE2D(_SoilNormal); SAMPLER(sampler_SoilNormal);
        TEXTURE2D(_SoilMask); SAMPLER(sampler_SoilMask);
        TEXTURE2D(_GravelColor); SAMPLER(sampler_GravelColor);
        TEXTURE2D(_GravelNormal); SAMPLER(sampler_GravelNormal);
        TEXTURE2D(_GravelMask); SAMPLER(sampler_GravelMask);
        TEXTURE2D(_MacroMap); SAMPLER(sampler_MacroMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _SiteOriginWS, _TileMetres;
        half _NormalStrength;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
            float2 uv:TEXCOORD0;
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1;
            float2 uv:TEXCOORD2;
            half fog:TEXCOORD3;
        };
        Varyings GroundVertex(Attributes input)
        {
            Varyings output;
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS=p.positionCS;output.positionWS=p.positionWS;
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.uv=input.uv;output.fog=ComputeFogFactor(p.positionCS.z);
            return output;
        }
        float Hash(float2 p)
        {
            p=frac(p*float2(.1031,.11369));p+=dot(p,p.yx+19.19);
            return frac((p.x+p.y)*p.x);
        }
        float Noise(float2 p)
        {
            float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),
                lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
        }
        void GroundSurface(Varyings input,out half3 albedo,out half3 normalWS,out half roughness,out half ao)
        {
            float2 p=(input.positionWS-_SiteOriginWS.xyz).xz;
            float2 soilUV=p/_TileMetres.x;
            // Incommensurate physical sizes and rotation avoid matching repeated features.
            float2 gravelUV=float2(.93358*p.x-.35837*p.y,.35837*p.x+.93358*p.y)/_TileMetres.y;
            half4 soilMask=SAMPLE_TEXTURE2D(_SoilMask,sampler_SoilMask,soilUV);
            half4 gravelMask=SAMPLE_TEXTURE2D(_GravelMask,sampler_GravelMask,gravelUV);
            float broad=Noise(p*.047)+.32*Noise(p*.139+float2(17,39));
            float blend=smoothstep(.49,.79,broad+(gravelMask.g-soilMask.g)*.14);
            float variation=lerp(.88,1.08,Noise(p*.0123+float2(23,41)));
            half3 soil=SAMPLE_TEXTURE2D(_SoilColor,sampler_SoilColor,soilUV).rgb;
            half3 gravel=SAMPLE_TEXTURE2D(_GravelColor,sampler_GravelColor,gravelUV).rgb;
            half3 nearColor=lerp(soil,gravel,blend)*variation;
            half3 macro=SAMPLE_TEXTURE2D(_MacroMap,sampler_MacroMap,input.uv).rgb;
            float nearWeight=1-smoothstep(180,360,length(p));
            albedo=lerp(macro,nearColor,nearWeight);
            roughness=lerp(.88,lerp(soilMask.b,gravelMask.b,blend),nearWeight);
            ao=lerp(1,lerp(soilMask.r,gravelMask.r,blend),nearWeight*.65);
            half3 soilN=UnpackNormalScale(SAMPLE_TEXTURE2D(_SoilNormal,sampler_SoilNormal,soilUV),_NormalStrength);
            half3 gravelN=UnpackNormalScale(SAMPLE_TEXTURE2D(_GravelNormal,sampler_GravelNormal,gravelUV),_NormalStrength);
            // Rotate the sampled tangent slopes back into the original world XZ axes.
            gravelN.xy=float2(.93358*gravelN.x+.35837*gravelN.y,-.35837*gravelN.x+.93358*gravelN.y);
            half3 detail=normalize(lerp(soilN,gravelN,blend));
            half3 n=normalize(input.normalWS);
            half3 tangent=normalize(half3(1,0,0)-n*n.x);
            half3 bitangent=normalize(half3(0,0,1)-n*n.z);
            normalWS=normalize(n*detail.z+(tangent*detail.x+bitangent*detail.y)*nearWeight);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 GroundFragment(Varyings input):SV_Target
            {
                half3 albedo,n;half roughness,ao;
                GroundSurface(input,albedo,n,roughness,ao);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=albedo;surface.metallic=0;surface.specular=half3(.04,.04,.04);
                surface.smoothness=1-clamp(roughness,.65,1);surface.occlusion=ao;
                surface.normalTS=half3(0,0,1);surface.alpha=1;
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;lighting.positionCS=input.positionCS;
                lighting.normalWS=n;lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                lighting.shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                lighting.bakedGI=SampleSH(n);lighting.vertexLighting=VertexLighting(input.positionWS,n);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                half4 color=UniversalFragmentPBR(lighting,surface);
                color.rgb=MixFog(color.rgb,input.fog);
                return half4(color.rgb,1);
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
            #pragma vertex GroundVertex
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
            #pragma vertex GroundVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings input):SV_Target
            {
                half3 a,n;half r,o;GroundSurface(input,a,n,r,o);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 encoded=saturate(PackNormalOctQuadEncode(n)*.5+.5);
                return half4(PackFloat2To888(encoded),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
