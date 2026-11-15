Shader "OutOfReach/LaunchV03/Plume"
{
    Properties
    {
        _Sample("Owner sample: seconds, opacity, motion, reserved",Vector)=(0,0,1,0)
        _Core("White core",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
        Pass
        {
            Name "BoundedExhaust"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Sample;
                float _Core;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; float3 normalWS:TEXCOORD2; float fog:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 q=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(q),Hash(q+float2(1,0)),f.x),lerp(Hash(q+float2(0,1)),Hash(q+1),f.x),f.y);
            }
            V Vert(A a)
            {
                UNITY_SETUP_INSTANCE_ID(a);
                V o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t=_Sample.x*_Sample.z;
                float ripple=sin(a.uv.y*61-t*22+sin(a.uv.x*37))*sin(a.uv.y*19-t*9);
                a.positionOS.xy*=1+ripple*.045*a.uv.y*(1-_Core*.7)*_Sample.z;
                VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;
                o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(V i):SV_Target
            {
                float z=i.uv.y, t=_Sample.x*_Sample.z;
                float2 q=float2(i.uv.x*12,z*19-t*5.2);
                float n=.58*Noise(q)+.28*Noise(q*2.07)+.14*Noise(q*4.13);
                float breakup=smoothstep(.20,.72,n+(1-z)*.33);
                float axial=pow(saturate(1-z),1.05)*smoothstep(0,.015,z);
                float band=pow(saturate(.5+.5*cos(z*52-1.2)),7)*(1-z)*.30;
                float facing=abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)));
                float rim=smoothstep(.02,.65,facing);
                float intensity=axial*lerp(.24,1,breakup)*lerp(.3,1,rim)*_Sample.y;
                float3 amber=lerp(float3(1.45,.22,.018),float3(2.7,1.45,.38),saturate((1-z)*.65+n*.3+band));
                float3 hot=lerp(float3(2.4,1.25,.30),float3(4.2,3.8,2.9),saturate((1-z)*1.7+band));
                float3 emission=lerp(amber,hot,_Core)*intensity*(.40+band);
                // Fog attenuates additive light instead of adding fog colour a second time. URP returns 0 from
                // ComputeFogIntensity when fog is off (upper-atmosphere shot), so only apply it with fog keywords.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                emission*=ComputeFogIntensity(i.fog);
                #endif
                return half4(emission,0);
            }
            ENDHLSL
        }
    }
}
