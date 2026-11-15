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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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
                // Sun-lit billow: a sphere-impostor normal from the camera-facing quad, lit by the scene's actual
                // main light (wrapped) and ambient probe, so the cloud has a lit top/sun side and a shaded belly.
                // Still no per-particle lights, shadows or full-screen distortion.
                float3 camRight=UNITY_MATRIX_V[0].xyz, camUp=UNITY_MATRIX_V[1].xyz, camBack=UNITY_MATRIX_V[2].xyz;
                float bulge=sqrt(saturate(1-radius*.92));
                float3 nrm=normalize(camRight*(p.x+(n-.5)*.35)+camUp*(p.y+(n-.5)*.35)+camBack*bulge);
                Light sun=GetMainLight();
                float wrap=saturate(dot(nrm,sun.direction)*.6+.4);
                // Keep the deluge steam cream: take about half of the sun's tint, all of its intensity.
                float3 sunCol=lerp(dot(sun.color,float3(.299,.587,.114)).xxx,sun.color,.5);
                float3 lit=sunCol*wrap*.80+SampleSH(nrm)*.95;
                float self=.78+.22*n;
                float3 color=i.color.rgb*lit*self;
                float3 delta=i.positionWS-_ContactGlow.xyz;
                float glow=exp(-dot(delta,delta)/max(1,_GlowRadius*_GlowRadius))*_ContactGlow.w;
                color+=float3(2.1,.68,.11)*glow*(.45+.55*density);
                color=MixFog(color,i.fog);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
