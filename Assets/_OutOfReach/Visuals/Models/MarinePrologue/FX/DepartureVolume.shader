Shader "OutOfReach/Marine/DepartureVolume"
{
    Properties
    {
        _Mode("Volume mode: exhaust0 steam1", Float) = 0
        [HideInInspector] _DstBlend("Legacy blend value (unused)", Float) = 10
        _HotColor("Hot core", Color) = (1.6,1.25,0.8,1)
        _TailColor("Outer exhaust", Color) = (0.7,0.29,0.09,1)
        _SteamColor("Steam", Color) = (0.55,0.60,0.62,1)
        [HideInInspector] _FxParameters("Time intensity motion seed", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Name "DepartureVolume"
            Tags { "LightMode"="SRPDefaultUnlit" }
            // Every owned volume is premultiplied; material/MPB values cannot restore additive stacking.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _HotColor, _TailColor, _SteamColor, _FxParameters;
                float _Mode, _DstBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 local:TEXCOORD2; float2 uv:TEXCOORD3; };
            float Hash31(float3 p) { p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z); }
            float Noise3(float3 p)
            {
                float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash31(i),Hash31(i+float3(1,0,0)),f.x),lerp(Hash31(i+float3(0,1,0)),Hash31(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash31(i+float3(0,0,1)),Hash31(i+float3(1,0,1)),f.x),lerp(Hash31(i+float3(0,1,1)),Hash31(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;float3 p=input.positionOS.xyz;float t=_FxParameters.x*_FxParameters.z;
                if (_Mode<.5)
                {
                    float profile=lerp(.78,.025,pow(saturate(p.z),1.15));
                    profile*=1+.13*sin(p.z*58-t*1.3+p.x*2.7)*smoothstep(.04,.18,p.z);
                    float wander=(Noise3(float3(p.xy*2.6+_FxParameters.w,p.z*8-t*3.5))-.5)*.68*_FxParameters.z;
                    p.xy*=profile*(1+wander);
                    // Two independent, nozzle-anchored wakes; no extra draw or noise octave.
                    float bend=smoothstep(.02,.30,p.z)*(1-p.z)*.13*_FxParameters.z;
                    p.xy+=float2(sin(p.z*17-t*3+_FxParameters.w),cos(p.z*23-t*4+_FxParameters.w))*bend;
                }
                else if(_Mode>1.5&&_Mode<2.5)
                {
                    float spray=lerp(.55,2.5,saturate(p.z));
                    p.xy*=spray*(1+.14*sin(p.z*31-t*13+_FxParameters.w)*_FxParameters.z);
                }
                else p*=1+(Noise3(p*2.3+float3(t*.18,_FxParameters.w,t*.11))-.5)*.30*_FxParameters.z;
                output.positionWS=TransformObjectToWorld(p); output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.local=p;output.uv=input.uv;return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float facing=saturate(dot(normalize(input.normalWS),view));
                float time=_FxParameters.x*_FxParameters.z;
                if (_Mode<.5)
                {
                    float angle=input.uv.x*6.283185;float along=input.uv.y;
                    float noise=Noise3(float3(cos(angle)*2,sin(angle)*2,along*11-time*6));
                    float fine=Noise3(float3(cos(angle)*4+7,sin(angle)*4+3,along*23-time*9));
                    float detailAA=saturate(1-fwidth(along*23)*.8);
                    // Broad compression cells, advected small turbulence, no full-frame strobe.
                    float cells=.5+.5*cos(along*58.0-2.8*noise+sin(angle*3+along*12)*1.4);
                    cells=lerp(.5,cells,saturate(1-fwidth(along*58.0)));
                    float bands=lerp(.38,1.0,pow(cells,3.0));
                    float turbulence=lerp(.72,.28+.90*noise+.15*fine,detailAA);
                    float tail=1-smoothstep(.48+.12*noise,.98,along);
                    float edge=pow(smoothstep(.04,.88,facing),1.5);
                    float flicker=1+.045*sin(time*18.1)+.025*sin(time*27.7);
                    float strength=saturate(_FxParameters.y)*tail*edge*turbulence*flicker;
                    float tongues=pow(saturate(.5+.5*sin(angle*3+along*21+noise*4)),3.0);
                    float hot=saturate(pow(saturate(1-along),2.0)*.65+bands*.25+tongues*.20);
                    float3 color=lerp(_TailColor.rgb,_HotColor.rgb,hot);
                    // Attenuate the bright sky instead of summing two HDR columns over it.
                    float alpha=min(.97,strength*1.90);
                    color=min(color,float3(2.4,2.1,1.6))*lerp(.42,1.0,bands);
                    return half4(color*alpha,alpha);
                }
                if(_Mode>1.5&&_Mode<2.5)
                {
                    float grain=Noise3(float3(input.uv.x*17,input.uv.y*48-time*21,_FxParameters.w));
                    float edge=smoothstep(.02,.75,facing);
                    float fade=(1-smoothstep(.62,1,input.uv.y))*smoothstep(0,.04,input.uv.y);
                    float alpha=saturate(_FxParameters.y)*edge*fade*lerp(.28,1,grain);
                    return half4(_SteamColor.rgb*alpha,alpha);
                }
                float noise=Noise3(input.local*1.7+float3(time*.13,_FxParameters.w,time*.08));
                float fine=Noise3(input.local*6.5+float3(-time*.18,7+_FxParameters.w,time*.12));
                float aa=max(.025,fwidth(noise)*1.5);
                float ridge=smoothstep(.28-aa,.69+aa,noise*.78+fine*.22);
                float density=lerp(.48,1.0,ridge);
                float edge=pow(smoothstep(.015,.68,facing),1.3);
                float alpha=min(.94,saturate(_FxParameters.y))*density*edge;
                // Soft internal shading keeps white steam readable against the bright sky.
                // Artistic self-shadow approximation, not a volumetric light simulation.
                float sun=saturate(dot(normalize(input.normalWS),normalize(float3(-.4,.8,-.3)))*.5+.5);
                // Broad shaded folds with smaller curls: same two samples, no raymarch or extra octave.
                float shade=lerp(.35,1.10,ridge)*lerp(.60,1.0,sun);
                shade*=lerp(.68,1.0,smoothstep(-.65,.45,input.local.y));
                float rim=.035*pow(1-facing,2.0);
                return half4(_SteamColor.rgb*(shade+rim)*alpha,alpha); // premultiplied cloud
            }
            ENDHLSL
        }
    }
    Fallback Off
}
