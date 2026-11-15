Shader "OutOfReach/Cinematics/DepartureTransitSky"
{
    Properties
    {
        _DepartureAltitude("Authored atmosphere passage", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Pass
        {
            Name "Departure sky"
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DepartureAltitude;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 ray : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.ray = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Art-directed curvature and cloud bands for the condensed transit. The ray is direction only.
                float3 ray = normalize(input.ray);
                float altitude = saturate(_DepartureAltitude);
                float horizon = lerp(-.035, -.30, altitude) - .13 * ray.x * ray.x * altitude;
                float above = ray.y - horizon;
                float haze = exp(-max(above, 0) * lerp(7, 28, altitude));
                float3 zenith = lerp(float3(.11, .28, .53), float3(.004, .010, .027), altitude);
                float3 limb = lerp(float3(.63, .74, .82), float3(.13, .37, .70), altitude);
                float3 sky = lerp(zenith, limb, haze);

                // Map low-frequency detail onto the visible cloud deck with stable world directions.
                float2 cloudUV = ray.xz * 19 + float2(ray.y * 27 + 15, ray.y * 56 + 21);
                float shape = Noise(cloudUV * .38) * .54 + Noise(cloudUV) * .32 + Noise(cloudUV * 2.6) * .14;
                float cloud = smoothstep(.40, .74, shape);
                float3 sea = lerp(float3(.39, .48, .56), float3(.035, .128, .235), altitude);
                float3 cloudColor = lerp(float3(.78, .79, .76), float3(.46, .61, .73), altitude);
                float3 below = lerp(sea, cloudColor, cloud * .85);
                below = lerp(below, limb, exp(min(above, 0) * 21) * .62);
                return half4(lerp(below, sky, smoothstep(-.014, .008, above)), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
