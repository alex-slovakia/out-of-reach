Shader "OutOfReach/Marine/DepartureVolume"
{
    Properties
    {
        _Mode("Volume mode: exhaust0 steam1", Float) = 0
        [HideInInspector] _DstBlend("Destination blend", Float) = 1
        _HotColor("Hot core", Color) = (1.6,1.25,0.8,1)
        _TailColor("Outer exhaust", Color) = (0.7,0.29,0.09,1)
        _SteamColor("Steam", Color) = (0.55,0.60,0.62,1)
        [HideInInspector] _FxParameters("Time intensity motion seed", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Name "DepartureVolume"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One [_DstBlend]
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
                    float profile=lerp(1.0,.10,p.z)*(.95+.14*sin(p.z*3.14159));
                    float wander=(Noise3(float3(p.xy*1.6,p.z*6-t*3.5))-.5)*.30*_FxParameters.z;
                    p.xy*=profile*(1+wander);
                }
                else p*=1+(Noise3(p*2.3+float3(t*.18,_FxParameters.w,t*.11))-.5)*.12*_FxParameters.z;
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
                    float turbulence=lerp(.64,.30+.60*noise+.20*fine,detailAA);
                    float tail=1-smoothstep(.62,1,along);
                    float edge=smoothstep(.08,.85,facing);
                    float heat=lerp(.65,1,pow(saturate(1-along),2))*turbulence;
                    float strength=saturate(_FxParameters.y)*tail*edge*heat;
                    float3 color=lerp(_HotColor.rgb,_TailColor.rgb,smoothstep(.015,.60,along));
                    return half4(color*strength,0); // restrained additive volume, never an opaque screen veil
                }
                float noise=Noise3(input.local*2.6+float3(time*.13,_FxParameters.w,time*.08));
                float aa=max(.025,fwidth(noise)*1.5);
                float density=smoothstep(.27-aa,.72+aa,noise);
                float edge=smoothstep(.02,.6,facing);
                float alpha=min(.47,saturate(_FxParameters.y))*density*edge;
                return half4(_SteamColor.rgb*alpha,alpha); // premultiplied ground cloud
            }
            ENDHLSL
        }
    }
    Fallback Off
}
