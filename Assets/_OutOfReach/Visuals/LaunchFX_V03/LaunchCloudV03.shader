Shader "OutOfReach/LaunchV03/Cloud"
{
    Properties
    {
        _Sample("Owner sample: seconds, opacity, motion, reserved",Vector)=(0,1,1,0)
        _ContactGlow("Engine contact world position and strength",Vector)=(0,0,0,0)
        _GlowRadius("Local flame illumination radius",Float)=12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "OriginalBillowParticles"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Sample;
                float4 _ContactGlow;
                float _GlowRadius;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float fog:TEXCOORD1; float3 positionWS:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 q=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(q),Hash(q+float2(1,0)),f.x),lerp(Hash(q+float2(0,1)),Hash(q+1),f.x),f.y);
            }
            V Vert(A a)
            {
                UNITY_SETUP_INSTANCE_ID(a);V o;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.color=a.color;o.uv=a.uv;
                o.positionWS=TransformObjectToWorld(a.positionOS.xyz);
                o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float t=_Sample.x*_Sample.z;
                float2 q=p*3.7+float2(t*.13,-t*.11)+i.positionWS.xz*.11;
                float n=.56*Noise(q)+.29*Noise(q*2.04+11)+.15*Noise(q*4.11-7);
                float radius=dot(p,p);
                float silhouette=1-smoothstep(.36+n*.24,.88+n*.12,radius);
                float density=lerp(.38,1,smoothstep(.16,.72,n))*silhouette;
                float edgeFade=saturate((1-radius)*3);
                float alpha=min(.72,density*edgeFade*i.color.a*_Sample.y);
                clip(alpha-.002);
                // Baked-looking internal self shade; no per-particle lights or full-screen distortion.
                float light=.67+.28*saturate(.5+p.y*.55)+n*.22;
                float3 color=i.color.rgb*light;
                float3 delta=i.positionWS-_ContactGlow.xyz;
                float glow=exp(-dot(delta,delta)/max(1,_GlowRadius*_GlowRadius))*_ContactGlow.w;
                color+=float3(1.55,.43,.055)*glow*(.40+.60*density);
                color=MixFog(color,i.fog);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
