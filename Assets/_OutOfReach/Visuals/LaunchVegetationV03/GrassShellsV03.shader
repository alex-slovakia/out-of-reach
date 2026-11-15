// TERRAIN-V03 dense meadow (15 Nov 2026, Alex's golden/olive grass reference, issue #7 6895376530).
// Shell grass: the generated shell meshes repeat the near ground 24 times, each copy raised by its layer
// height (UV0.x = layer 0..1). Per pixel a grass height field decides which layers exist, so the field has
// real parallax, self-shadowed bases, wind-bent golden tips and warm back-lighting.
// - Far mode (beyond ~30 m, every deck view): a continuous clumpy canopy whose fine octaves fade by pixel
//   footprint, stable without MSAA/TAA (the game camera uses FXAA only).
// - Near mode (ground close-ups): individual tapering blades, dithered into far mode across 18-32 m.
// Roads, the plume corridor and the apron shoulder are excluded in the height field itself.
Shader "OutOfReach/Launch Grass Shells V03"
{
    Properties
    {
        _SiteOriginWS("Authored site origin (world)",Vector)=(0,0,0,0)
        _DatumY("Ground datum, site-local metres",Float)=-36.6
        _ShellHeight("Height of the top shell (m)",Float)=0.95
        _BladeBase("Blade base (shadowed thatch)",Color)=(0.25,0.21,0.12,1)
        _BladeMid("Blade middle",Color)=(0.56,0.46,0.25,1)
        _BladeTip("Blade tips (golden)",Color)=(0.83,0.68,0.40,1)
        _BladeOlive("Olive clumps",Color)=(0.47,0.47,0.25,1)
        _OliveAmount("Olive share",Range(0,1))=0.35
        _Translucency("Back-lit tips",Range(0,3))=1.2
        _WindDir("Wind direction (site xz)",Vector)=(0.82,0.57,0,0)
        _WindStrength("Wind bend (m at the tips)",Float)=0.16
        _HazeDensity("Haze density (exp2)",Float)=0.00009
        _HazeFloor("Haze at height scale (m)",Float)=1400
        _HazeColor("Haze away from the sun",Color)=(0.42,0.48,0.58,1)
        _HazeSunColor("Haze towards the sun",Color)=(0.85,0.80,0.72,1)
        _OuterFade("Shell fade radius start, end (m)",Vector)=(330,430,0,0)
        _ShellRange("Real blades up to this camera distance (m)",Float)=46
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _SiteOriginWS, _WindDir, _OuterFade;
        half4 _BladeBase, _BladeMid, _BladeTip, _BladeOlive, _HazeColor, _HazeSunColor;
        float _DatumY, _ShellHeight, _WindStrength, _HazeDensity, _HazeFloor, _ShellRange;
        half _OliveAmount, _Translucency;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float layer:TEXCOORD2; };
        Varyings ShellVertex(Attributes input)
        {
            Varyings o;
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            o.positionCS=p.positionCS;o.positionWS=p.positionWS;
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.layer=input.uv.x;
            return o;
        }
        float Hash(float2 p){p=frac(p*float2(.1031,.11369));p+=dot(p,p.yx+19.19);return frac((p.x+p.y)*p.x);}
        float2 Hash2(float2 p){return float2(Hash(p),Hash(p+47.31));}
        float Noise(float2 p)
        {
            float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
        }
        float Box(float2 p,float2 lo,float2 hi,float ramp)
        {
            // 1 inside the rectangle, smooth to 0 over `ramp` metres outside it.
            float2 d=max(max(lo-p,p-hi),0);
            return 1-smoothstep(0,ramp,length(d));
        }
        // Grass height (metres) at site-local p, before blades. Roads and safety areas stay clear.
        float ZoneHeight(float2 p)
        {
            float r=length(p-float2(0,5));
            float h=lerp(.68,1,smoothstep(90,230,r));                                   // shorter near the facility
            h*=1-Box(p,float2(-24,-915),float2(24,-55),12);                              // crawlerway corridor
            h*=1-Box(p,float2(-30,62),float2(30,335),15);                                // flame trench and plume corridor
            float shoulder=Box(p,float2(-72,-62),float2(72,72),14);                      // apron edge: mown shoulder
            h*=lerp(1,.22,shoulder);
            h*=1-smoothstep(_OuterFade.x,_OuterFade.y,r);
            return h*_ShellHeight;
        }
        // Clump field 0..1 with footprint-faded fine octaves (no shimmer at range).
        float Clumps(float2 p,float footprint)
        {
            float c=.40*Noise(p/3.6+11.3)+.28*Noise(p/1.25+3.1);
            float v1=1-smoothstep(.10,.32,footprint);
            float v2=1-smoothstep(.04,.13,footprint);
            c+=.20*lerp(.5,Noise(p/.52+7.7),v1)+.12*lerp(.5,Noise(p/.21+1.9),v2);
            return c;
        }
        float2 WindBend(float2 p,float layer,out float gust)
        {
            float2 w=normalize(_WindDir.xy);
            float t=_Time.y;
            gust=.5+.5*sin(dot(p,w)*.085-t*1.7+Noise(p/23+t*.11)*3.0);
            float sway=.65+.35*sin(t*2.3+dot(p,float2(.37,.61)))*.5+gust*.55;
            return w*(_WindStrength*sway*layer*layer);
        }
        // Coverage test shared by all passes. Returns 0 (clip) or 1 and the relative depth inside the canopy.
        float Shell(Varyings input,out float rel,out float clump,out float gust)
        {
            rel=0;clump=0;gust=0;
            // Cost guard: the shells only exist within 46 m of the camera, so everything farther (every deck view)
            // leaves after one distance test instead of evaluating the field 24 times per pixel.
            float camDistance=distance(GetCameraPositionWS(),input.positionWS);
            [branch]if(camDistance>_ShellRange)return 0;
            float2 p=input.positionWS.xz-_SiteOriginWS.xz;
            float layer=input.layer;
            float2 bent=p-WindBend(p,layer,gust);
            float footprint=max(length(fwidth(p)),1e-4);
            float zone=ZoneHeight(bent);
            clump=Clumps(bent,footprint);
            float canopy=zone*(.40+.62*clump);                                           // canopy top (m)
            float z=layer*_ShellHeight;                                                  // this shell (m)
            rel=saturate(z/max(canopy,1e-3));
            float far=step(z,canopy);
            // Near blades: two rotated, fully jittered cell grids (no rows), thin ribbon cross-sections with a
            // random facing, random heights, tapering to the tip.
            float near=0;
            [unroll]for(int g=0;g<2;g++)
            {
                float cell=g==0?.065:.083;
                float2 rp=g==0?bent:float2(.799*bent.x-.602*bent.y,.602*bent.x+.799*bent.y)+3.7;
                float2 q=rp/cell, id=floor(q), f=frac(q)-.5;
                float2 j=(Hash2(id+g*17.3)-.5)*.7;
                float bladeH=canopy*(.55+.55*Hash(id+3.7+g));
                float a=Hash(id+9.1+g)*6.2832;
                float2 o=f-j;
                float2 ribbon=float2(dot(o,float2(cos(a),sin(a))),dot(o,float2(-sin(a),cos(a))));
                float taper=pow(saturate(1-z/max(bladeH,1e-3)),.6);
                float inside=step(length(ribbon/float2(.36,.11)),taper);
                near=max(near,inside*step(z,bladeH));
            }
            // Level of detail: real blades only near the camera (ground-level shots); from the deck (>= 70 m away)
            // the ground shader alone carries the meadow, so the shells cost nothing there and never look like a sheet.
            float d=distance(GetCameraPositionWS(),input.positionWS);
            float dither=Hash(floor(input.positionCS.xy)+frac(layer*7.13)*31.0);
            float nearW=1-smoothstep(14,26,d);
            float keep=lerp(far,near,step(dither,nearW));
            keep*=step(dither,1-smoothstep(_ShellRange*.65,_ShellRange,d));
            return (zone>.005&&layer>.001)?keep:0;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShellVertex
            #pragma fragment ShellFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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
            half4 ShellFragment(Varyings input):SV_Target
            {
                float rel,clump,gust;
                clip(Shell(input,rel,clump,gust)-.5);
                float2 p=input.positionWS.xz-_SiteOriginWS.xz;
                half3 n=normalize(input.normalWS);
                // Colour: dark thatch at the base, golden tips; olive clumps; the gust lifts paler blade sides.
                half3 albedo=lerp(_BladeBase.rgb,_BladeMid.rgb,smoothstep(0,.55,rel));
                albedo=lerp(albedo,_BladeTip.rgb,smoothstep(.45,1,rel));
                half olive=smoothstep(.55,.85,Noise(p/9.5+5.1)*.7+Noise(p/31+2.3)*.3+(_OliveAmount-.5)*.6);
                albedo=lerp(albedo,_BladeOlive.rgb*lerp(.6,1.05,rel),olive*.75);
                albedo*=lerp(.92,1.08,clump)*(1+.07*(gust-.5)*rel);
                float3 v=GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow=sun.shadowAttenuation;
                half ndl=saturate(dot(n,sun.direction)*.75+.25);
                half ao=lerp(.22,1,pow(rel,.9));
                half back=pow(saturate(dot(-v,sun.direction)),2.5)*smoothstep(.3,1,rel)*_Translucency;
                // Beyond the blade zone the shells are a canopy surface: blend its glow and grazing tone towards the
                // ground shader's, so the edge of the real-blade zone does not read as a bright band.
                half canopyShare=smoothstep(14,30,distance(GetCameraPositionWS(),input.positionWS));
                back*=lerp(1,.3,canopyShare);
                albedo*=lerp(1,lerp(.60,1,smoothstep(.02,.35,saturate(dot(n,v)))),canopyShare);
                half3 warm=half3(1,.78,.46);
                half3 color=albedo*sun.color*shadow*(ndl*ao+back*warm);
                color+=albedo*SampleSH(half3(0,1,0))*ao*.8;
                if(SceneFogOn())
                {
                    float d=distance(GetCameraPositionWS(),input.positionWS);
                    float above=max(0,input.positionWS.y-(_SiteOriginWS.y+_DatumY));
                    float density=_HazeDensity*lerp(1,.55,saturate(above/_HazeFloor));
                    float haze=saturate(exp2(-(d*density)*(d*density)*1.442695));
                    float2 viewH=normalize(-v.xz+1e-5),sunH=normalize(sun.direction.xz+1e-5);
                    half toSun=pow(saturate(dot(viewH,sunH)*.5+.5),3);
                    half3 hazeColor=lerp(lerp(_HazeColor.rgb,_HazeSunColor.rgb,toSun),unity_FogColor.rgb,.12);
                    color=lerp(hazeColor,color,haze);
                }
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShellVertex
            #pragma fragment DepthFragment
            half DepthFragment(Varyings input):SV_Target
            {
                float rel,clump,gust;clip(Shell(input,rel,clump,gust)-.5);
                return input.positionCS.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormalsOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShellVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 NormalFragment(Varyings input):SV_Target
            {
                float rel,clump,gust;clip(Shell(input,rel,clump,gust)-.5);
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
