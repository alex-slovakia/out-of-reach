Shader "OutOfReach/Marine Ocean"
{
    Properties
    {
        _DeepColor ("Water body", Color) = (.045,.20,.27,1)
        _SkyColor ("Marine daylight sky reflection", Color) = (.30,.43,.56,1)
        _HorizonColor ("Match marine sky horizon", Color) = (.66,.68,.60,1)
        _FoamColor ("Foam", Color) = (.55,.63,.64,1)
        _Roughness ("Reflection roughness", Range(.15,.8)) = .30
        _Amplitude ("Maximum total visual wave height, metres", Range(0,.25)) = .075
        _NearRadius ("Near mesh fade radius", Float) = 90
        _DistanceFadeStart ("Horizon blend start, metres", Float) = 180
        _DistanceFadeEnd ("Horizon blend end, metres", Float) = 1800
        [HideInInspector] _OceanPhase ("Phase", Vector) = (0,0,0,0)
        [HideInInspector] _WaveStrength ("Motion strength", Float) = 1
        [HideInInspector] _WaterOrigin ("Water origin", Vector) = (0,0,0,0)
        [HideInInspector] _ContactCount ("Contact count", Integer) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "MarineWater"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor, _SkyColor, _HorizonColor, _FoamColor;
                float4 _OceanPhase, _WaterOrigin;
                float _Roughness, _Amplitude, _NearRadius, _DistanceFadeStart, _DistanceFadeEnd, _WaveStrength;
                int _ContactCount;
                float4 _ContactPoints[16]; // world XZ, contact radius, weight
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            float NearWeight(float2 position)
            { return 1-smoothstep(_NearRadius*.52, _NearRadius, max(abs(position.x),abs(position.y))); }
            void Wave(float2 worldXZ, out float height, out float2 gradient)
            {
                float2 q = worldXZ-_WaterOrigin.xz;
                float3 a = float3(dot(q,float2(.52,.19)) + _OceanPhase.x,
                                  dot(q,float2(-.26,.74)) + _OceanPhase.y,
                                  dot(q,float2(.83,-.62)) + _OceanPhase.z);
                float weight = NearWeight(q)*_Amplitude*_WaveStrength;
                float3 slope = cos(a)*float3(.55,.29,.16)*weight;
                height = dot(sin(a),float3(.55,.29,.16))*weight;
                gradient = slope.x*float2(.52,.19)+slope.y*float2(-.26,.74)+slope.z*float2(.83,-.62);
            }
            float FoamCell(float2 cell)
            {
                // A bounded random cell value; smooth interpolation below correlates adjacent samples.
                return frac(sin(dot(cell,float2(127.13,311.71)))*43758.713);
            }
            float FoamNoise(float2 samplePosition)
            {
                float2 cell=floor(samplePosition), f=frac(samplePosition);
                float2 blend=f*f*f*(f*(f*6-15)+10);
                return lerp(lerp(FoamCell(cell),FoamCell(cell+float2(1,0)),blend.x),
                            lerp(FoamCell(cell+float2(0,1)),FoamCell(cell+float2(1,1)),blend.x),blend.y);
            }
            float FoamPatches(float2 delta,float pixelMetres,float seed)
            {
                // Closed phase motion remains continuous when the presenter's phases wrap at 2*pi.
                // Spatial warping and differently rotated octaves remove crossing periodic checker flecks.
                float2 drift=float2(sin(_OceanPhase.y),cos(_OceanPhase.z))*.26;
                float2 rotated=float2(.8*delta.x-.6*delta.y,.6*delta.x+.8*delta.y);
                float warp=FoamNoise(rotated*.85+drift*.4+float2(seed,3.17));
                float2 uv=rotated*4+float2(warp-.5,.5-warp)*.55+drift+float2(2.83,seed);
                float2 detail=float2(.36*uv.x-.93295*uv.y,.93295*uv.x+.36*uv.y)*2.13+float2(5.31,1.79);
                // Unresolved octaves tend to their mean, preventing subpixel foam from crawling.
                float coarseVisibility=1-smoothstep(.25,.8,pixelMetres*4);
                float detailVisibility=1-smoothstep(.25,.8,pixelMetres*8.52);
                float coarse=lerp(.5,FoamNoise(uv),coarseVisibility);
                float fine=lerp(.5,FoamNoise(detail),detailVisibility);
                float softness=.10+min(.16,pixelMetres*3);
                return smoothstep(.47-softness,.58+softness,coarse*.72+fine*.28);
            }
            float WaterCell(int2 cell)
            {
                // Integer avalanche: stationary, irregular cell values without a large-coordinate sin hash.
                uint2 q=asuint(cell);
                uint h=(q.x*1597334677u)^(q.y*3812015801u);
                h^=h>>16; h*=2246822519u; h^=h>>13; h*=3266489917u; h^=h>>16;
                return (h&0x00ffffffu)*(1.0/16777216.0);
            }
            float2 WaterGradient(float2 uv)
            {
                int2 cell=(int2)floor(uv); float2 f=frac(uv);
                float2 blend=f*f*f*(f*(f*6-15)+10);
                float2 derivative=30*f*f*(f-1)*(f-1);
                float a=WaterCell(cell), b=WaterCell(cell+int2(1,0));
                float c=WaterCell(cell+int2(0,1)), d=WaterCell(cell+int2(1,1));
                float2 gradient=derivative*float2(lerp(b-a,d-c,blend.y),lerp(c-a,d-b,blend.x));
                // Filter each scale before it becomes unresolved. Coarse water variation remains
                // when fine detail disappears; this is not a distance switch to a flat normal.
                float2 footprint=fwidth(uv);
                return gradient*(1-smoothstep(.12,.45,max(footprint.x,footprint.y)));
            }
            float2 WaterRippleSlope(float2 metres)
            {
                // Three independent rotated metre scales; no pair of infinite crossing sine stripes.
                // Only existing wrapped phases move this normal detail, so pause/reduced motion stay exact.
                float2 drift=float2(sin(_OceanPhase.x),cos(_OceanPhase.y))*.24;
                float2 a=float2(.8*metres.x-.6*metres.y,.6*metres.x+.8*metres.y)*.17+drift*.6+float2(17.7,-9.1);
                float2 b=float2(.28*metres.x-.96*metres.y,.96*metres.x+.28*metres.y)*.43+drift+float2(53.4,20.8);
                float2 c=float2(.9396926*metres.x+.3420201*metres.y,-.3420201*metres.x+.9396926*metres.y)*1.13
                    +float2(sin(_OceanPhase.z),cos(_OceanPhase.x))*.20+float2(-31.2,47.3);
                float2 ga=WaterGradient(a), gb=WaterGradient(b), gc=WaterGradient(c);
                // Rotate gradients back into water XZ. Weights are dimensionless normal slopes,
                // not extra mesh displacement; original 75mm bounded geometric waves remain unchanged.
                return float2(.8*ga.x+.6*ga.y,-.6*ga.x+.8*ga.y)*.022
                    +float2(.28*gb.x+.96*gb.y,-.96*gb.x+.28*gb.y)*.014
                    +float2(.9396926*gc.x-.3420201*gc.y,.3420201*gc.x+.9396926*gc.y)*.008;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p=TransformObjectToWorld(input.positionOS.xyz);
                float h; float2 slope; Wave(p.xz,h,slope); p.y+=h;
                output.positionWS=p; output.positionCS=TransformWorldToHClip(p); return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float h; float2 slope; Wave(input.positionWS.xz,h,slope);
                float2 p=input.positionWS.xz-_WaterOrigin.xz;
                slope+=WaterRippleSlope(p);
                half3 n=normalize(float3(-slope.x,1,-slope.y));
                half3 v=GetWorldSpaceNormalizeViewDir(input.positionWS);
                half nv=saturate(dot(n,v));
                half fresnel=.02h+.98h*Pow4(1-nv)*(1-nv);
                float3 reflected=reflect(-v,n);
                // Dedicated marine sky field: runtime sky replacement must not reuse a stale
                // ship reflection probe. An authored daylight hemisphere, not a captured reflection.
                half3 environment=lerp(_HorizonColor.rgb,_SkyColor.rgb,saturate(reflected.y));
                Light main=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half nl=saturate(dot(n,main.direction));
                half3 body=_DeepColor.rgb*(.5h+main.color*nl*main.shadowAttenuation*.5h);
                half3 colour=lerp(body,environment,.10h+fresnel*.80h);
                half3 halfVector=SafeNormalize(main.direction+v);
                half highlight=pow(saturate(dot(n,halfVector)),lerp(160,24,_Roughness))*.22h;
                colour+=main.color*highlight*main.shadowAttenuation;
                float foam=0;
                // Derivatives are evaluated before the per-contact divergent branches.
                float foamPixelMetres=length(fwidth(input.positionWS.xz));
                float foamEdgeFilter=min(.035,foamPixelMetres*.65);
                [loop] for(int i=0;i<min(_ContactCount,16);++i)
                {
                    float4 contact=_ContactPoints[i];
                    if(contact.w<=0) continue;
                    float2 delta=input.positionWS.xz-contact.xy;
                    if(dot(delta,delta)>(contact.z+.30)*(contact.z+.30)) continue;
                    float distanceToPoint=length(delta);
                    float angle=atan2(delta.y,delta.x);
                    float brokenEdge=sin(angle*7+_OceanPhase.y)*.045+sin(angle*11-_OceanPhase.z)*.025;
                    float ring=1-smoothstep(max(0,.035-foamEdgeFilter),.19+foamEdgeFilter,abs(distanceToPoint-contact.z+brokenEdge));
                    float flecks=FoamPatches(delta,foamPixelMetres,i*4.37);
                    float pulse=.65+.25*sin(_OceanPhase.x+i*1.71);
                    foam=max(foam,ring*flecks*pulse*contact.w);
                }
                colour=lerp(colour,_FoamColor.rgb,saturate(foam)*.72h);
                float distanceToCamera=distance(input.positionWS.xz,_WorldSpaceCameraPos.xz);
                float haze=smoothstep(_DistanceFadeStart,max(_DistanceFadeStart+1,_DistanceFadeEnd),distanceToCamera);
                // Blend to the marine sky's low-angle tint before the finite camera far plane.
                // Use the flat sea/view angle, not wave normals, so distant haze does not shimmer.
                float grazingHaze=1-smoothstep(.015,.12,abs(v.y));
                colour=lerp(colour,_HorizonColor.rgb,max(haze,grazingHaze));
                return half4(max(colour,0),1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
