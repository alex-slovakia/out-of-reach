Shader "OutOfReach/Marine Contact Splash"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; o.color=input.color; return o; }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 q=(input.uv-.5)*float2(2.2,1.8);
                float alpha=1-smoothstep(.10,.95,dot(q,q));
                return half4(input.color.rgb,min(.45h,input.color.a)*alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
