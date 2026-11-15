// LAUNCH-GRASS-V04 (15 Nov 2026, issue #11 R07). Original HLSL, written for OUT OF REACH.
// Tall golden pampas / feather grass drawn with Graphics.RenderMeshPrimitives from one structured buffer
// (PampasGrassField). No textures: stems, arching leaves and feathery seed heads are mesh ribbons; the
// feather barbs and the far "card" LOD are procedural alpha patterns.
// Instance buffer: float4(world x, y, z, seed). Per draw: _ChunkStart, _ChunkCount, _IsCard.
// Globals written by PampasGrassField / LaunchGrassGustPresenter:
//   _OOR_GrassWind      xy wind direction (world xz), z sway angle (rad), w gust-wave angle (rad)
//   _OOR_GrassWindWave  x wave speed (m/s), y wavelength (m), z reduced effects (0/1), w wind time (s)
//   _OOR_GrassLod       x near-mesh distance, y cross-fade band, z max draw distance, w far count fraction
//   _OOR_GrassGust      xyz thruster gust origin (world), w strength 0..1 (0 = off)
//   _OOR_GrassGustWave  x shock front radius (m), y front width (m), z sustained reach (m), w flutter 0..1
//   _OOR_LaunchHaze     aerial haze multiplier (0..1)
Shader "OutOfReach/Launch Pampas Grass V04"
{
    Properties
    {
        _HeightRange("Clump height min, max (m)",Vector)=(1.05,1.7,0,0)
        _Olive("Olive base",Color)=(0.20,0.19,0.08,1)
        _Straw("Straw stems and leaves",Color)=(0.40,0.30,0.15,1)
        _Gold("Golden amber",Color)=(0.50,0.35,0.15,1)
        _Plume("Seed heads (pale amber)",Color)=(0.60,0.46,0.27,1)
        _OliveShare("Olive share",Range(0,1))=0.35
        _Translucency("Back-light through plumes",Range(0,4))=1.15
        _TransTint("Back-light tint",Color)=(1.0,0.66,0.34,1)
        _FarCanopy("Canopy colour at the draw-distance edge (matches the ground shader)",Color)=(0.19,0.15,0.07,1)
        _Ambient("Ambient scale",Range(0,2))=0.9
        _GustSheen("Brightening in gust waves",Range(0,1))=0.3
        _HazeDensity("Aerial haze per metre",Float)=0.0012
        _HazeColor("Haze away from the sun",Color)=(0.62,0.58,0.52,1)
        _HazeSunColor("Haze towards the sun",Color)=(1.0,0.80,0.52,1)
        _HazeSunPower("Sun-ward haze sharpness",Float)=6
        _Cutoff("Feather alpha cutoff",Range(0.05,0.95))=0.42
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" "DisableBatching"="True"}
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _HeightRange;
        half4 _Olive, _Straw, _Gold, _Plume, _TransTint, _HazeColor, _HazeSunColor, _FarCanopy;
        half _OliveShare, _Translucency, _Ambient, _GustSheen, _Cutoff;
        float _HazeDensity, _HazeSunPower;
        float _ChunkStart, _ChunkCount, _IsCard;
        CBUFFER_END
        StructuredBuffer<float4> _OOR_GrassInstances;
        float4 _OOR_GrassWind, _OOR_GrassWindWave, _OOR_GrassLod, _OOR_GrassGust, _OOR_GrassGustWave;
        float _OOR_LaunchHaze;

        struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; uint instanceID:SV_InstanceID; };
        struct Varyings
        {
            float4 positionCS:SV_POSITION;
            float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1;
            float4 uvPart:TEXCOORD2;   // uv, part id, stem random
            float4 shade:TEXCOORD3;    // height along clump, instance seed, gust wave 0..1, lod weight (0 near .. 1 card)
            float  fog:TEXCOORD4;
            float4 tint:TEXCOORD5;     // per-instance stem colour, olive flag (vertex stage: keeps the fragment cheap)
            float4 light:TEXCOORD6;    // back-light, haze amount, sun-ward haze, far blend
            float3 ambient:TEXCOORD7;
        };

        float Hash11(float p){p=frac(p*.1031);p*=p+33.33;p*=p+p;return frac(p);}
        float Hash21(float2 p){p=frac(p*float2(.1031,.11369));p+=dot(p,p.yx+19.19);return frac((p.x+p.y)*p.x);}
        float Noise(float2 p)
        {
            float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(Hash21(c),Hash21(c+float2(1,0)),f.x),lerp(Hash21(c+float2(0,1)),Hash21(c+1),f.x),f.y);
        }

        // Per-instance LOD weight 0 (full mesh) .. 1 (card), and visibility after count thinning.
        float LodWeight(float d){return smoothstep(_OOR_GrassLod.x-_OOR_GrassLod.y,_OOR_GrassLod.x+_OOR_GrassLod.y,d);}
        float KeepFraction(float d)
        {
            float t=saturate((d-_OOR_GrassLod.x)/max(_OOR_GrassLod.z-_OOR_GrassLod.x,1));
            float e=1-t;
            return lerp(1,_OOR_GrassLod.w,1-e*e);
        }

        Varyings GrassVertex(Attributes v)
        {
            Varyings o=(Varyings)0;
            uint index=(uint)_ChunkStart+v.instanceID;
            float4 inst=_OOR_GrassInstances[index];
            float3 root=inst.xyz; float seed=inst.w;
            float r1=Hash11(seed*91.7+1.3), r2=Hash11(seed*53.1+7.7), r3=Hash11(seed*17.3+3.1);
            float3 toCam=root-_WorldSpaceCameraPos;
            float d=length(toCam);

            // Count thinning with distance: instance rank inside its shuffled chunk vs. the kept fraction.
            float rank=(v.instanceID+.5)/max(_ChunkCount,1);
            float keep=KeepFraction(d)*(_OOR_GrassWindWave.z>.5?.55:1);
            float visible=saturate((keep-rank)/.06);
            float widen=clamp(rsqrt(max(keep,.05)),1,2.4);
            float fade=1-smoothstep(_OOR_GrassLod.z*.6,_OOR_GrassLod.z,d);

            // Height: per instance and in broad patches (taller swathes, lower hollows).
            float patch=Noise(root.xz/46+17)*.65+Noise(root.xz/13+3)*.35;
            float H=lerp(_HeightRange.x,_HeightRange.y,saturate(r1*.55+patch*.6-.08));
            float yaw=r2*6.2831853;
            float s,c; sincos(yaw,s,c);
            float3 p=v.positionOS;
            p=float3(p.x*c-p.z*s,p.y,p.x*s+p.z*c);
            float3 n=float3(v.normalOS.x*c-v.normalOS.z*s,v.normalOS.y,v.normalOS.x*s+v.normalOS.z*c);
            float width=(_IsCard>.5?widen*1.22:1)*lerp(.85,1.2,r3);
            p.xz*=H*width; p.y*=H*fade*visible;
            float hNorm=saturate(v.positionOS.y);

            // Wind: slow sway plus travelling gust bands along the wind direction.
            float2 wd=normalize(_OOR_GrassWind.xy+1e-5);
            float along=dot(root.xz,wd), across=dot(root.xz,float2(-wd.y,wd.x));
            float t=_OOR_GrassWindWave.w;
            float reduced=_OOR_GrassWindWave.z;
            float wl=max(_OOR_GrassWindWave.y,1);
            float wave=Noise(float2((along-t*_OOR_GrassWindWave.x)/wl,across/(wl*2.6)))*.75
                      +Noise(float2((along-t*_OOR_GrassWindWave.x*1.3)/(wl*.37),across/(wl*.9)+5))*.25;
            wave=smoothstep(.42,.9,wave);
            float sway=sin(t*1.6+r1*6.28+along*.21)*.5+.5;
            float flick=sin(t*4.3+r3*40+hNorm*2)*.5;
            float windAngle=_OOR_GrassWind.z*(.45+.55*sway)+_OOR_GrassWind.w*wave*(reduced>.5?.4:1);
            float2 bendVec=wd*windAngle+float2(-wd.y,wd.x)*flick*_OOR_GrassWind.z*.35*(1-reduced);

            // Thruster gust: radial shock front plus a sustained push near the pad, flattening the grass outwards.
            if(_OOR_GrassGust.w>1e-4)
            {
                float2 rel=root.xz-_OOR_GrassGust.xz; float dist=length(rel)+1e-3; float2 rdir=rel/dist;
                float frontW=max(_OOR_GrassGustWave.y,1);
                float q=(dist-_OOR_GrassGustWave.x)/frontW; float front=exp(-q*q);
                float inside=pow(saturate(1-dist/max(_OOR_GrassGustWave.z,1)),.7)*saturate((_OOR_GrassGustWave.x-dist)/frontW+1);
                float push=_OOR_GrassGust.w*(front*1.1+inside*.75);
                float shiver=sin(t*23+r3*60+dist*.7)*_OOR_GrassGustWave.w*push*(1-reduced);
                bendVec+=rdir*(push*1.25+shiver*.22);
                wave=max(wave,push*1.6);
            }
            float angle=min(length(bendVec),1.35);
            float2 bendDir=bendVec/max(length(bendVec),1e-4);
            float a=angle*pow(hNorm,1.6)*v.color.r;
            float sa,ca; sincos(a,sa,ca);
            float lift=p.y;
            p.y=lift*ca;
            p.xz+=bendDir*lift*sa;

            float3 posWS=root+p;
            o.positionWS=posWS;
            o.positionCS=TransformWorldToHClip(posWS);
            o.normalWS=normalize(n+float3(bendDir.x,0,bendDir.y)*sa*.5);
            o.uvPart=float4(v.uv,v.color.g,v.color.b);
            o.shade=float4(hNorm,seed,wave,LodWeight(d));
            o.fog=ComputeFogFactor(o.positionCS.z);
            // Per-instance colour and view/sun terms once per vertex: the far cards overdraw heavily.
            float patchN=Noise(root.xz/31+5.3);
            float olive=step(Hash11(seed*7.1+.3),_OliveShare*(.6+.8*patchN));
            o.tint=float4(lerp(_Straw.rgb,_Gold.rgb,saturate(Hash11(seed*3.7)*.7+patchN*.5)),olive);
            float dv=length(posWS-_WorldSpaceCameraPos);
            float toSun=dot((posWS-_WorldSpaceCameraPos)/max(dv,1e-3),_MainLightPosition.xyz);
            o.light=float4(pow(saturate(toSun),5),(1-exp(-dv*_HazeDensity))*saturate(_OOR_LaunchHaze),
                pow(saturate(toSun*.5+.5),_HazeSunPower),smoothstep(_OOR_GrassLod.z*.22,_OOR_GrassLod.z*.9,dv));
            o.ambient=SampleSH(float3(0,1,0))*_Ambient;
            return o;
        }

        float IGN(float2 pixel){return frac(52.9829189*frac(dot(pixel,float2(.06711056,.00583715))));}

        // Fibre coordinate across a plume: fibres fan out towards the tip.
        float PlumeFibre(float2 uv,float rnd){return (uv.x-.5)*(1.7-.7*uv.y)*58+rnd*17;}

        // Near mesh. Leaves and stalks are solid ribbons. Plumes: a soft spindle envelope with a thin dense
        // core and fine fibres of random length fanning towards the tip, lightly dithered at the rim.
        float NearCoverage(Varyings i,float dither)
        {
            float cover=1;
            if(i.uvPart.z>.5)
            {
                float2 uv=i.uvPart.xy;
                float v=uv.y, du=abs(uv.x-.5);
                float env=.5*pow(saturate(sin(3.14159*pow(saturate(v),.7))),.8)*(1-.25*v);
                float rel=du/max(env,1e-3);
                float fib=PlumeFibre(uv,i.uvPart.w);
                float len=Hash21(float2(floor(fib),i.uvPart.w*9.1+3));
                float fibre=smoothstep(.5,.1,abs(frac(fib)-.5))*step(v,lerp(.45,1.02,len))*step(.08*len,v);
                fibre=lerp(fibre,.5,saturate(fwidth(fib)*.9));
                float core=1-smoothstep(.04,.26,rel);
                cover=(max(core,fibre*(1-smoothstep(.55,1,rel)*.8))+(dither-.5)*.35*rel)*step(rel,1);
            }
            return (1-i.shade.w<dither)?0:cover;                       // cross-fade out into cards
        }

        // Far card: 7 plumed stems per quad (plumes take the top ~45%) above a dense leafy base.
        float CardCoverage(Varyings i,float dither)
        {
            float2 uv=i.uvPart.xy;
            float u=uv.x*7; float k=floor(u); float cu=frac(u);
            float rk=Hash21(float2(k,i.uvPart.w*31+i.shade.y*7));
            float top=lerp(.70,1,rk);
            float v=uv.y;
            float plumeV=saturate((v-(top-.46))/.46);
            float env=.44*pow(saturate(sin(3.14159*pow(plumeV,.7))),.8)*step(v,top)*step(top-.46,v);
            float rel=abs(cu-.5+(plumeV-.5)*.12*(rk-.5))/max(env,1e-3);
            float fluff=sin(u*31+i.shade.y*40)*sin(v*53+u*7)*.5+.5;
            float plume=(1-smoothstep(.35,1,rel))*(.7+.55*fluff)*step(1e-3,env);
            float stalk=step(abs(cu-.5),max(.05,fwidth(u)*.8))*step(v,top-.4);
            float base=saturate((.45-v)/.45)*(.6+.6*(sin(uv.x*83+i.shade.y*50)*sin(v*19+uv.x*11)*.5+.5));
            float cover=max(max(plume,stalk),base)+(dither-.5)*.2;
            return (i.shade.w<dither)?0:cover;                         // cross-fade in from meshes
        }

        float Coverage(Varyings i,float2 pixel)
        {
            float dither=IGN(pixel);
            float cover;
            if(_IsCard<.5) cover=NearCoverage(i,dither); else cover=CardCoverage(i,dither);
            return cover;
        }

        half3 Albedo(Varyings i)
        {
            float seed=i.shade.y; float part=i.uvPart.z; float h=i.shade.x;
            half3 stem=lerp(i.tint.rgb,_Olive.rgb*1.25,i.tint.a*.55*(1-h));
            half3 col=lerp(_Olive.rgb,stem,smoothstep(0,.55,h));         // olive, shaded bases
            half3 plume=_Plume.rgb*lerp(.86,1.08,Hash11(seed*11.3+i.uvPart.w));
            if(_IsCard<.5) plume*=lerp(.78,1.12,Hash21(float2(floor(PlumeFibre(i.uvPart.xy,i.uvPart.w)),i.uvPart.w*5.3)));
            float isPlume=_IsCard>.5?smoothstep(.45,.7,i.uvPart.y):step(.5,part)*step(part,.8);
            col=lerp(col,plume,isPlume);
            col*=lerp(.42,1,smoothstep(0,.6,h));                          // self-shadowed thatch at the base
            col*=1+_GustSheen*i.shade.z;
            return lerp(col,_FarCanopy.rgb,i.light.w*.92);
        }
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags {"LightMode"="UniversalForward"}
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            half4 GrassFragment(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                float cover=Coverage(i,i.positionCS.xy);
                clip(cover-_Cutoff);
                half3 albedo=Albedo(i);
                float3 N=normalize(i.normalWS)*(front?1:-1);
                float part=i.uvPart.z;
                N=normalize(lerp(N,float3(0,1,0),part>.5?.92:.45));
                Light light=GetMainLight();
                float shadow=1;
                if(_IsCard<.5) shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.positionWS));
                float ndl=saturate(dot(N,light.direction)*.6+.4);
                float thin=_IsCard>.5?lerp(.45,1,smoothstep(.5,.8,i.uvPart.y)):(part>.5?1:(part>.2?.35:.55));
                float back=i.light.x;
                float rim=0;
                if(_IsCard<.5) rim=pow(1-saturate(abs(dot(normalize(i.normalWS),normalize(_WorldSpaceCameraPos-i.positionWS)))),3)*.35;
                float h=i.shade.x;
                half3 ambient=i.ambient*lerp(.45,1,h);
                half3 color=albedo*(light.color*ndl*shadow+ambient);
                float farT=i.light.w;
                color+=albedo*_TransTint.rgb*light.color*(back*_Translucency+rim*back)*thin*lerp(.3,1,shadow)*smoothstep(.1,.7,h)*(1-.85*farT);

                // Aerial haze: warm and glowing towards the low sun, cooler away from it.
                half3 hazeColor=lerp(_HazeColor.rgb,_HazeSunColor.rgb,i.light.z)*max(light.color,.35);
                color=lerp(color,hazeColor,i.light.y);
                color=MixFog(color,i.fog);
                return half4(color,1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVertex
            #pragma fragment DepthFragment
            half4 DepthFragment(Varyings i):SV_Target
            {
                clip(Coverage(i,i.positionCS.xy)-_Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormals"}
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVertex
            #pragma fragment DepthNormalsFragment
            half4 DepthNormalsFragment(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                clip(Coverage(i,i.positionCS.xy)-_Cutoff);
                float3 N=normalize(lerp(normalize(i.normalWS)*(front?1:-1),float3(0,1,0),.45));
                return half4(N,0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
