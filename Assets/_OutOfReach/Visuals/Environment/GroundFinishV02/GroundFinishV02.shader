// TERRAIN-V03 (15 Nov 2026): one continuous launch-site ground for the near terrain, the far land and the
// mountains. Colour comes from a single grass palette driven by broad world-space fields (no patchwork);
// the CC0 ambientCG Ground109 photo only adds luminance/normal micro detail near the camera. Steep or high
// ground turns into scrub and warm rock. Distant ground gets an aerial-perspective haze in the scene fog
// colour, only while the scene fog is on. Shader asset GUID and the old property names are kept so the
// existing GroundFinishV02.mat and root's I06GroundFinishAuthoring keep working.
Shader "OutOfReach/Launch Ground V03"
{
    Properties
    {
        _SoilColor("Ground109 photo colour (micro detail)",2D)="white"{}
        _SoilNormal("Ground109 normal",2D)="bump"{}
        _SoilMask("Ground109 AO / height / roughness",2D)="white"{}
        [HideInInspector]_GravelColor("Unused (V02)",2D)="white"{}
        [HideInInspector]_GravelNormal("Unused (V02)",2D)="bump"{}
        [HideInInspector]_GravelMask("Unused (V02)",2D)="white"{}
        [HideInInspector]_MacroMap("Unused (V02 macro)",2D)="white"{}
        _SiteOriginWS("Authored site origin (world)",Vector)=(0,0,0,0)
        _TileMetres("Detail repeat metres (near, far)",Vector)=(3.1,11.7,0,0)
        _NormalStrength("Photo normal strength",Range(0,2))=0.7
        _DatumY("Ground datum, site-local metres",Float)=-36.6
        _GrassDry("Dry grass",Color)=(0.63,0.56,0.36,1)
        _GrassGreen("Olive grass",Color)=(0.47,0.49,0.28,1)
        _GrassLush("Green grass in swales",Color)=(0.37,0.42,0.22,1)
        _Scrub("Scrub on slopes",Color)=(0.33,0.34,0.22,1)
        _Rock("Warm rock",Color)=(0.62,0.52,0.42,1)
        _RockDark("Dark rock",Color)=(0.39,0.33,0.29,1)
        _DetailStrength("Photo luminance detail",Range(0,1.5))=0.75
        _DetailHue("Photo hue share",Range(0,1))=0.22
        _GreenAmount("Green share",Range(0,1))=0.45
        _HazeDensity("Terrain haze density (exp2)",Float)=0.00009
        _HazeColor("Haze away from the sun",Color)=(0.42,0.48,0.58,1)
        _HazeSunColor("Haze towards the sun",Color)=(0.86,0.80,0.70,1)
        _HazeFloor("Haze at height scale (m)",Float)=1400
        _Sheen("Back-lit grass sheen",Range(0,1))=0.25
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
        CBUFFER_START(UnityPerMaterial)
        float4 _SiteOriginWS, _TileMetres;
        half4 _GrassDry, _GrassGreen, _GrassLush, _Scrub, _Rock, _RockDark, _BaseColor, _HazeColor, _HazeSunColor;
        float _DatumY, _HazeDensity, _HazeFloor;
        half _NormalStrength, _DetailStrength, _DetailHue, _GreenAmount, _Sheen, _Cull;
        float4 _BaseMap_ST;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS:POSITION;
            float3 normalOS:NORMAL;
        };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1;
        };
        Varyings GroundVertex(Attributes input)
        {
            Varyings output;
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS=p.positionCS;output.positionWS=p.positionWS;
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
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
        // Rotated octaves avoid axis-aligned artefacts in the broad fields.
        float Fbm(float2 p,int octaves)
        {
            float sum=0,amp=.5,norm=0;
            const float2x2 r=float2x2(.8,-.6,.6,.8);
            for(int i=0;i<octaves;i++){sum+=amp*Noise(p);norm+=amp;p=mul(r,p)*2.03+17.17;amp*=.5;}
            return sum/norm;
        }
        bool SceneFogOn()
        {
            bool on=false;
            #if defined(FOG_LINEAR_KEYWORD_DECLARED)
            if(FOG_LINEAR)on=true;
            #endif
            #if defined(FOG_EXP_KEYWORD_DECLARED)
            if(FOG_EXP)on=true;
            #endif
            #if defined(FOG_EXP2_KEYWORD_DECLARED)
            if(FOG_EXP2)on=true;
            #endif
            return on;
        }
        // Inigo Quilez's "texture repetition" (technique 3): a smooth virtual tile index picks two random
        // offsets per region, so the photo never shows a regular grid. Explicit gradients avoid mip seams.
        void NoTileSetup(float2 uv,out float2 offA,out float2 offB,out float f)
        {
            float k=Noise(uv*.21+.37);
            float l=k*8;float i=floor(l);f=frac(l);
            offA=sin(float2(3,7)*(i+0));offB=sin(float2(3,7)*(i+1));
        }
        void GroundSurface(Varyings input,out half3 albedo,out half3 normalWS,out half roughness,out half ao)
        {
            float2 p=input.positionWS.xz-_SiteOriginWS.xz;
            float h=input.positionWS.y-(_SiteOriginWS.y+_DatumY);
            half3 n=normalize(input.normalWS);
            float slope=saturate(1-n.y);
            float d=distance(GetCameraPositionWS(),input.positionWS);
            // Grassland swells: soften their low-sun shading so metre-high undulations read as meadow, not dunes.
            n=normalize(lerp(n,half3(0,1,0),.4*(1-smoothstep(25,120,h))));

            // Broad, domain-warped fields: hundreds of metres, smooth transitions, no hard edges.
            float2 warp=float2(Fbm(p/1300+3.7,3),Fbm(p/1300+41.3,3))-.5;
            float2 w=p+warp*560;
            float broad=Fbm(w/820,4);
            float mid=Fbm(w/190+5.3,3);
            float fine=Fbm(p/27+9.1,3);
            float swale=saturate((2.5-h)*.22);                          // lower ground keeps more green
            float green=smoothstep(.34,.66,broad+(_GreenAmount-.5)*.5+swale*.10+(mid-.5)*.30);
            half3 grass=lerp(_GrassDry.rgb,_GrassGreen.rgb,green);
            grass=lerp(grass,_GrassLush.rgb,smoothstep(.6,1,green)*saturate(.4+swale)*.6);
            grass*=lerp(.92,1.07,mid)*lerp(.95,1.04,fine);
            // Meadow texture as seen from the deck: olive tufts and lighter straw mottled over five octaves
            // (1-50 m). Each octave fades to its mean once a pixel covers it, so the texture carries smoothly
            // from the apron edge to the horizon without shimmer, bands or a change of tone.
            float footprint=max(length(fwidth(p)),1e-4);
            float mottle=0,mottleW=0;
            const float scales[5]={1.0,2.7,7.3,19.0,52.0};
            const float weights[5]={1.0,.9,.75,.55,.4};
            [unroll]for(int o=0;o<5;o++)
            {
                float vis=1-smoothstep(scales[o]*.16,scales[o]*.5,footprint);
                float nz=Noise(p/scales[o]+float2(31.7+o*7.1,7.3+o*3.7));
                mottle+=weights[o]*lerp(.5,nz,vis);mottleW+=weights[o];
            }
            mottle/=mottleW;
            float tuft=smoothstep(.30,.70,mottle);
            half3 tuftColor=lerp(_GrassGreen.rgb,_GrassLush.rgb,.5)*.86;
            half3 gapColor=lerp(_GrassDry.rgb,grass,.4)*1.05;
            grass=lerp(grass,lerp(gapColor,tuftColor,tuft),.42*(1-smoothstep(1500,4000,d)));
            // Close range: elongated blade streaks that slowly change direction.
            float bladeAngle=broad*12.566+mid*3.0;
            float2 bd=float2(cos(bladeAngle),sin(bladeAngle));
            float2 bq=float2(dot(p,bd),dot(p,float2(-bd.y,bd.x)));
            half blades=Noise(bq*float2(2.2,24))*.6+Noise(bq*float2(4.7,51)+3.1)*.4;
            grass*=lerp(1,lerp(.80,1.14,blades),1-smoothstep(.035,.11,footprint));

            // Foothills and mountains: olive scrub on slopes and mid heights, warm rock on steep or high ground.
            float relief=smoothstep(35,240,h);
            float scrubW=saturate(smoothstep(.05,.26,slope)*.8+relief*.45)*(1-smoothstep(700,950,h)*.5);
            float strata=Fbm(float2(p.x*.0019+p.y*.0011,h*.05),3);
            float rockW=saturate(smoothstep(.24,.55,slope+(strata-.5)*.3)+smoothstep(420,900,h+(broad-.5)*300)*.7);
            half3 rock=lerp(_RockDark.rgb,_Rock.rgb,smoothstep(.2,.8,strata+(fine-.5)*.15));
            half3 scrub=_Scrub.rgb*lerp(.82,1.14,mid);
            albedo=lerp(grass,scrub,scrubW*(1-rockW));
            albedo=lerp(albedo,rock,rockW);

            // Photographic micro detail of Ground109, only near the camera; luminance plus a little hue.
            float2 uvA=p/_TileMetres.x;
            float2 uvB=float2(.8191*p.x-.5736*p.y,.5736*p.x+.8191*p.y)/_TileMetres.y;
            float2 dxA=ddx(uvA),dyA=ddy(uvA),dxB=ddx(uvB),dyB=ddy(uvB);
            float2 oA0,oA1,oB0,oB1;float fA,fB;
            NoTileSetup(uvA,oA0,oA1,fA);NoTileSetup(uvB+13.1,oB0,oB1,fB);
            half3 mean=max(SAMPLE_TEXTURE2D_LOD(_SoilColor,sampler_SoilColor,float2(.5,.5),12).rgb,1e-3);
            half3 a0=SAMPLE_TEXTURE2D_GRAD(_SoilColor,sampler_SoilColor,uvA+oA0,dxA,dyA).rgb;
            half3 a1=SAMPLE_TEXTURE2D_GRAD(_SoilColor,sampler_SoilColor,uvA+oA1,dxA,dyA).rgb;
            half tA=smoothstep(.2,.8,fA-.1*dot(a0-a1,half3(1,1,1)));
            half3 b0=SAMPLE_TEXTURE2D_GRAD(_SoilColor,sampler_SoilColor,uvB+oB0,dxB,dyB).rgb;
            half3 b1=SAMPLE_TEXTURE2D_GRAD(_SoilColor,sampler_SoilColor,uvB+oB1,dxB,dyB).rgb;
            half tB=smoothstep(.2,.8,fB-.1*dot(b0-b1,half3(1,1,1)));
            half3 photoA=lerp(a0,a1,tA)/mean, photoB=lerp(b0,b1,tB)/mean;
            half nearW=1-smoothstep(20,110,d);
            half3 photo=lerp(photoB,photoA*.6+photoB*.4,nearW);
            half detailW=(nearW*.75+.25*(1-smoothstep(110,420,d)))*_DetailStrength*(1-rockW*.6);
            half lum=dot(photo,half3(.3333,.3333,.3334));
            half3 detail=lerp(half3(1,1,1),lerp(lum.xxx,photo,_DetailHue),detailW);
            albedo*=clamp(detail,.5,1.5);

            half4 maskA=lerp(SAMPLE_TEXTURE2D_GRAD(_SoilMask,sampler_SoilMask,uvA+oA0,dxA,dyA),
                SAMPLE_TEXTURE2D_GRAD(_SoilMask,sampler_SoilMask,uvA+oA1,dxA,dyA),tA);
            ao=lerp(1,maskA.r,nearW*.5);
            roughness=lerp(.93,lerp(.86,maskA.b,.5),nearW);

            half3 nA=UnpackNormalScale(lerp(SAMPLE_TEXTURE2D_GRAD(_SoilNormal,sampler_SoilNormal,uvA+oA0,dxA,dyA),
                SAMPLE_TEXTURE2D_GRAD(_SoilNormal,sampler_SoilNormal,uvA+oA1,dxA,dyA),tA),_NormalStrength);
            half3 nB=UnpackNormalScale(lerp(SAMPLE_TEXTURE2D_GRAD(_SoilNormal,sampler_SoilNormal,uvB+oB0,dxB,dyB),
                SAMPLE_TEXTURE2D_GRAD(_SoilNormal,sampler_SoilNormal,uvB+oB1,dxB,dyB),tB),_NormalStrength*.55);
            nB.xy=float2(.8191*nB.x+.5736*nB.y,-.5736*nB.x+.8191*nB.y);
            half3 detailN=normalize(half3(nA.xy*nearW+nB.xy*(1-smoothstep(90,380,d)),1));
            half3 tangent=normalize(half3(1,0,0)-n*n.x);
            half3 bitangent=normalize(half3(0,0,1)-n*n.z);
            normalWS=normalize(n*detailN.z+tangent*detailN.x+bitangent*detailN.y);
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
                surface.albedo=albedo;surface.metallic=0;surface.specular=half3(.03,.03,.03);
                surface.smoothness=1-clamp(roughness,.7,1);surface.occlusion=ao;
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
                // Dry grass glows a little when the low sun is behind it (forward scattering).
                Light sun=GetMainLight(lighting.shadowCoord);
                half back=pow(saturate(dot(-lighting.viewDirectionWS,sun.direction)),5)*saturate(n.y);
                color.rgb+=albedo*sun.color*sun.shadowAttenuation*back*_Sheen*(1-smoothstep(.25,.6,1-n.y));
                if(SceneFogOn())
                {
                    // Aerial perspective: bluish away from the sun, warm and bright towards it (forward scattering);
                    // thinner with altitude so ridge tops keep their colour.
                    float d=distance(GetCameraPositionWS(),input.positionWS);
                    float above=max(0,input.positionWS.y-(_SiteOriginWS.y+_DatumY));
                    float density=_HazeDensity*lerp(1,.55,saturate(above/_HazeFloor));
                    float haze=saturate(exp2(-(d*density)*(d*density)*1.442695));
                    // The far land ends at ~10.2 km (camera far clip 10.5 km): dissolve its rim into the haze.
                    haze*=1-smoothstep(9300,10150,length(input.positionWS.xz-_SiteOriginWS.xz-float2(0,5)));
                    float2 viewH=normalize(-lighting.viewDirectionWS.xz+1e-5);
                    float2 sunH=normalize(sun.direction.xz+1e-5);
                    half toSun=pow(saturate(dot(viewH,sunH)*.5+.5),3);
                    half3 hazeColor=lerp(_HazeColor.rgb,_HazeSunColor.rgb,toSun);
                    hazeColor=lerp(hazeColor,unity_FogColor.rgb,.12);
                    color.rgb=lerp(hazeColor,color.rgb,haze);
                }
                return half4(color.rgb,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GroundVertex
            #pragma fragment DepthFragment
            half DepthFragment(Varyings input):SV_Target{return input.positionCS.z;}
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
                // Geometric normal only: SSAO needs the shape, not the photo micro detail (keeps this pass cheap).
                half3 n=normalize(input.normalWS);
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
