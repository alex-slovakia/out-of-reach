// LAUNCH-GRASS-V04 (15 Nov 2026, issue #11 R07). Original HLSL, written for OUT OF REACH.
// Tall golden pampas / feather grass drawn with Graphics.RenderMeshPrimitives from one structured buffer
// (PampasGrassField). No textures: stems, arching leaves and feathery seed heads are mesh ribbons; the
// feather barbs and the far "card" LOD are procedural alpha patterns.
// Instance buffer: float4(world x, y, z, seed). Per draw: _ChunkStart, _ChunkCount, _IsCard.
// Globals written by PampasGrassField / LaunchGrassGustPresenter:
//   _OOR_GrassWind      xy wind direction (world xz), z sway angle (rad), w gust-wave angle (rad)
//   _OOR_GrassWindWave  x wave speed (m/s), y wavelength (m), z reduced effects (0/1), w unused
//   _OOR_GrassLod       x near-mesh distance, y cross-fade band, z max draw distance, w far count fraction
//   _OOR_GrassGust      xyz thruster gust origin (world), w strength 0..1 (0 = off)
//   _OOR_GrassGustWave  x shock front radius (m), y front width (m), z sustained reach (m), w flutter 0..1
//   _OOR_LaunchHaze     aerial haze multiplier (0..1)
Shader "OutOfReach/Launch Pampas Grass V04"
{
    Properties
    {
        _HeightRange("Clump height min, max (m)",Vector)=(1.05,1.7,0,0)
        _Olive("Olive base",Color)=(0.30,0.29,0.14,1)
        _Straw("Straw stems and leaves",Color)=(0.66,0.52,0.27,1)
        _Gold("Golden amber",Color)=(0.80,0.58,0.27,1)
        _Plume("Seed heads (pale amber)",Color)=(0.93,0.80,0.58,1)
        _OliveShare("Olive share",Range(0,1))=0.35
        _Translucency("Back-light through plumes",Range(0,4))=2.2
        _TransTint("Back-light tint",Color)=(1.0,0.72,0.38,1)
        _Ambient("Ambient scale",Range(0,2))=0.9
        _GustSheen("Brightening in gust waves",Range(0,1))=0.22
        _HazeDensity("Aerial haze per metre",Float)=0.0032
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
        CBUFFER_START(UnityPerMaterial)
        float4 _HeightRange;
        half4 _Olive, _Straw, _Gold, _Plume, _TransTint, _HazeColor, _HazeSunColor;
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
            return lerp(1,_OOR_GrassLod.w,t*t*(3-2*t));
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
            float fade=1-smoothstep(_OOR_GrassLod.z*.78,_OOR_GrassLod.z,d);

            // Height: per instance and in broad patches (taller swathes, lower hollows).
            float patch=Noise(root.xz/46+17)*.65+Noise(root.xz/13+3)*.35;
            float H=lerp(_HeightRange.x,_HeightRange.y,saturate(r1*.55+patch*.6-.08));
            float yaw=r2*6.2831853;
            float s,c; sincos(yaw,s,c);
            float3 p=v.positionOS;
            p=float3(p.x*c-p.z*s,p.y,p.x*s+p.z*c);
            float3 n=float3(v.normalOS.x*c-v.normalOS.z*s,v.normalOS.y,v.normalOS.x*s+v.normalOS.z*c);
            float width=(_IsCard>.5?widen:1)*lerp(.85,1.2,r3);
            p.xz*=H*width; p.y*=H*fade*visible;
            float hNorm=saturate(v.positionOS.y);

            // Wind: slow sway plus travelling gust bands along the wind direction.
            float2 wd=normalize(_OOR_GrassWind.xy+1e-5);
            float along=dot(root.xz,wd), across=dot(root.xz,float2(-wd.y,wd.x));
            float t=_Time.y;
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
                float inside=saturate(1-dist/max(_OOR_GrassGustWave.z,1))*saturate((_OOR_GrassGustWave.x-dist)/frontW+1);
                float push=_OOR_GrassGust.w*(front*1.1+inside*.75);
                float shiver=sin(t*23+r3*60+dist*.7)*_OOR_GrassGustWave.w*push*(1-reduced);
                bendVec+=rdir*(push*1.25+shiver*.22);
                wave=max(wave,saturate(push));
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
            return o;
        }

        float IGN(float2 pixel){return frac(52.9829189*frac(dot(pixel,float2(.06711056,.00583715))));}

        // Coverage of the procedural parts; returns alpha-test value (clip at _Cutoff).
        float Coverage(Varyings i, float2 pixel)
        {
            float part=i.uvPart.z; float2 uv=i.uvPart.xy;
            float dither=IGN(pixel);
            float lodW=i.shade.w;
            if(_IsCard<.5)
            {
                if(1-lodW<dither) return 0;                          // cross-fade out into cards
                if(part<.5) return 1;                                // leaves and stalks: geometry taper
                // Feather plume: tapering envelope, central rachis and angled barbs.
                float v=uv.y, du=abs(uv.x-.5);
                float env=pow(saturate(sin(3.14159*saturate(v*.9+.06))),.65)*.5*(1-.35*v);
                float s=v*24-du*7+i.uvPart.w*13;
                float strand=1-abs(frac(s)-.5)*2;
                float aa=saturate(fwidth(s)*1.2);
                strand=lerp(strand,.62,aa);
                float edge=saturate(1-du/max(env,1e-3));
                float rachis=saturate(1-du/.035);
                return max((.25+.75*strand)*pow(edge,.45),rachis)*step(du,env);
            }
            if(lodW<dither) return 0;                                // cross-fade in from meshes
            // Far card: 5 plumed stems per quad and a dense leafy base.
            float cols=5; float u=uv.x*cols; float k=floor(u); float cu=frac(u);
            float rk=Hash21(float2(k,i.uvPart.w*31+i.shade.y*7));
            float top=lerp(.72,1,rk);
            float v=uv.y;
            float plumeV=saturate((v-(top-.36))/.36);
            float env=pow(saturate(sin(3.14159*plumeV)),.6)*.42*step(v,top)*step(top-.36,v);
            float plume=saturate(1-abs(cu-.5)/max(env,1e-3))*step(1e-3,env);
            float stalkW=max(.05,fwidth(u)*.8);
            float stalk=step(abs(cu-.5+(v-.5)*.08*(rk-.5)),stalkW)*step(v,top-.3);
            float baseN=Noise(float2(uv.x*38,v*9)+i.shade.y*13);
            float base=saturate((.42-v)/.42)*(.55+.6*baseN);
            return max(max(plume*1.15,stalk),base);
        }

        half3 Albedo(Varyings i)
        {
            float seed=i.shade.y; float part=i.uvPart.z; float h=i.shade.x;
            float patchN=Noise(i.positionWS.xz/31+5.3);
            float olive=step(Hash11(seed*7.1+.3),_OliveShare*(.6+.8*patchN));
            half3 stem=lerp(_Straw.rgb,_Gold.rgb,saturate(Hash11(seed*3.7)*.7+patchN*.5));
            stem=lerp(stem,_Olive.rgb*1.25,olive*.55*(1-h));
            half3 col=lerp(_Olive.rgb,stem,smoothstep(0,.55,h));         // olive, shaded bases
            half3 plume=_Plume.rgb*lerp(.88,1.06,Hash11(seed*11.3+i.uvPart.w));
            float isPlume=_IsCard>.5?smoothstep(.55,.75,i.uvPart.y):step(.5,part)*step(part,.8);
            col=lerp(col,plume,isPlume);
            col*=lerp(.42,1,smoothstep(0,.6,h));                          // self-shadowed thatch at the base
            return col*(1+_GustSheen*i.shade.z);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 GrassFragment(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                float cover=Coverage(i,i.positionCS.xy);
                clip(cover-_Cutoff);
                half3 albedo=Albedo(i);
                float3 N=normalize(i.normalWS)*(front?1:-1);
                N=normalize(lerp(N,float3(0,1,0),.45));
                float3 V=normalize(_WorldSpaceCameraPos-i.positionWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float shadow=light.shadowAttenuation;
                float ndl=saturate(dot(N,light.direction)*.6+.4);
                float part=i.uvPart.z;
                float thin=_IsCard>.5?lerp(.45,1,smoothstep(.5,.8,i.uvPart.y)):(part>.5?1:(part>.2?.35:.55));
                float back=pow(saturate(dot(-V,light.direction)),5);
                float rim=pow(1-saturate(abs(dot(normalize(i.normalWS),V))),3)*.35;
                float h=i.shade.x;
                half3 ambient=SampleSH(float3(0,1,0))*_Ambient*lerp(.45,1,h);
                half3 color=albedo*(light.color*ndl*shadow+ambient);
                color+=albedo*_TransTint.rgb*light.color*(back*_Translucency+rim*back)*thin*lerp(.3,1,shadow)*smoothstep(.1,.7,h);

                // Aerial haze: warm and glowing towards the low sun, cooler away from it.
                float d=length(i.positionWS-_WorldSpaceCameraPos);
                float haze=(1-exp(-d*_HazeDensity))*saturate(_OOR_LaunchHaze);
                float sunward=pow(saturate(dot(-V,light.direction)*.5+.5),_HazeSunPower);
                half3 hazeColor=lerp(_HazeColor.rgb,_HazeSunColor.rgb,sunward)*max(light.color,.35);
                color=lerp(color,hazeColor,haze);
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
