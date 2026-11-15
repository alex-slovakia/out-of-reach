// Art-directed Earth for the condensed departure climb (R09, issue #11).
//
// This is a CINEMATIC SHORTCUT, not an ephemeris or an atmosphere simulation. It models the shot as a
// unit-radius sphere seen from a camera whose height h above it grows with _DepartureAltitude. At full
// altitude h = 0.055 R (about 350 km), a plausible low Earth orbit, so the horizon dip, limb curvature and
// thickness of the glowing atmosphere shell all follow from one geometry instead of the former screen-space
// bend. The atmosphere is a thin, sun-lit Rayleigh-blue shell with an amber twilight line at the terminator.
//
// Exposure: the scene grade has ACES tonemapping, +0.45 EV post exposure and a Bloom threshold of 1.25.
// The output passes through a soft shoulder (1 - exp(-x)), so sunlit cloud tops stay near 0.7 before the
// grade and only the narrow ocean glint can brush the bloom threshold. Nothing here is HDR-emissive.
//
// Cost: no texture reads. Every pixel pays a handful of ALU for the analytic limb; only pixels that hit the
// planet run three small 3D value-noise passes (about 50 hash evaluations). Skybox draws once, after opaques.
Shader "OutOfReach/Cinematics/DepartureTransitSky"
{
    Properties
    {
        _DepartureAltitude("Authored atmosphere passage", Range(0, 1)) = 0
        _Exposure("Sky exposure before the grade", Range(0.5, 1.4)) = 1
        // Fallback only when URP has not published a main light: direction TOWARD the sun.
        _SunDirection("Fallback sun direction", Vector) = (0.707, 0.309, -0.636, 0)
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
                float _Exposure;
                float4 _SunDirection;
            CBUFFER_END

            // Global published by URP's light setup before the skybox draws; xyz points toward a directional light.
            float4 _MainLightPosition;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 ray : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.ray = input.positionOS.xyz;
                return output;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash13(i);
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            float Fbm3(float3 p)
            {
                float v = Noise3(p) * 0.5;
                p = p * 2.03 + 17.1;
                v += Noise3(p) * 0.25;
                p = p * 2.01 + 31.7;
                v += Noise3(p) * 0.125;
                return v / 0.875;
            }

            float3 SunDirection()
            {
                float3 light = _MainLightPosition.xyz;
                float3 fallback = _SunDirection.xyz;
                float lightLength = dot(light, light);
                // Both are directions; a missing light leaves the global at zero.
                return lightLength > 1e-4 ? light * rsqrt(lightLength)
                                          : fallback * rsqrt(max(dot(fallback, fallback), 1e-4));
            }

            // Colour of the thin atmosphere shell seen tangentially at height s above the ground.
            // ndlT is the sun's cosine at the tangent point (dayside > 0, terminator = 0, night < 0).
            float3 LimbColor(float s, float ndlT, float sunDot)
            {
                float rim = exp(-s / 0.0030);
                float halo = exp(-s / 0.0105);
                float3 blue = lerp(float3(0.060, 0.200, 0.660), float3(0.480, 0.760, 1.000), rim);
                float dayside = smoothstep(-0.18, 0.30, ndlT);
                float twilight = exp(-ndlT * ndlT / 0.0200);
                float forward = pow(sunDot, 24.0);
                float3 col = blue * (rim * 0.78 + halo * 0.30) * dayside * (1.0 + forward * 0.55);
                // A restrained amber line where day meets night.
                col += float3(1.000, 0.430, 0.130) * twilight * (rim * 0.34 + halo * 0.10);
                // Faint airglow keeps the night limb a readable graphite-cyan rather than black.
                col += float3(0.020, 0.070, 0.110) * rim * (1.0 - dayside) * 0.55;
                return col;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.ray);
                float altitude = saturate(_DepartureAltitude);
                float h = lerp(0.0004, 0.055, pow(altitude, 1.7));
                float3 origin = float3(0, 1 + h, 0);
                float3 sun = SunDirection();
                float sunDot = saturate(dot(d, sun));

                float b = dot(origin, d);
                float disc = b * b - (dot(origin, origin) - 1.0);
                float discWidth = max(fwidth(disc) * 1.5, 1e-6); // outside any branch: needs uniform control flow

                // Closest approach of the ray to the centre: gives the height of the shell it grazes.
                float3 tangent = b < 0 ? origin - d * b : origin;
                float tangentRadius = max(length(tangent), 1e-4);
                float shellHeight = max(tangentRadius - 1.0, 0.0);
                float3 limb = LimbColor(shellHeight, dot(tangent / tangentRadius, sun), sunDot);

                // Sky: a dome while the camera is still inside the air, a black space with a limb above it.
                float toSpace = smoothstep(0.15, 0.62, altitude);
                float horizonY = -sqrt(saturate(1.0 - 1.0 / ((1.0 + h) * (1.0 + h))));
                float above = d.y - horizonY;
                float haze = exp(-max(above, 0.0) * lerp(7.0, 28.0, altitude));
                float3 zenith = lerp(float3(0.110, 0.280, 0.530), float3(0.003, 0.006, 0.016), smoothstep(0.10, 0.80, altitude));
                float3 dome = lerp(zenith, float3(0.630, 0.740, 0.820), haze);
                dome += float3(1.0, 0.62, 0.34) * pow(sunDot, 14.0) * 0.20 * haze;
                float3 space = float3(0.003, 0.006, 0.016) * (1.0 - 0.5 * smoothstep(0.0, 0.5, d.y));
                float3 col = lerp(dome, space + limb, toSpace);

                if (disc > 0.0 && b < 0.0)
                {
                    float3 p = origin + d * (-b - sqrt(disc));
                    float3 n = normalize(p);
                    float ndl = dot(n, sun);
                    float lit = smoothstep(-0.08, 0.55, ndl);
                    float3 warm = lerp(float3(1.00, 0.58, 0.30), float3(1.00, 0.97, 0.92), smoothstep(0.0, 0.35, ndl));

                    // Surface: two broad continents-and-ocean masks; muted, graphite-leaning land.
                    float landNoise = Fbm3(n * 3.1 + 7.3);
                    float land = smoothstep(0.57, 0.63, landNoise);
                    float shallow = smoothstep(0.535, 0.575, landNoise) * (1.0 - land);
                    float3 ocean = lerp(float3(0.020, 0.095, 0.230), float3(0.050, 0.250, 0.330), shallow);
                    float3 ground = lerp(float3(0.150, 0.170, 0.115), float3(0.300, 0.240, 0.150), Noise3(n * 9.0 + 3.1));
                    float3 albedo = lerp(ocean, ground, land);

                    // Clouds: domain-warped fbm on the sphere, scale tied to altitude so the deck stays a deck.
                    float cloudScale = lerp(90.0, 38.0, smoothstep(0.0, 1.0, altitude));
                    float3 warp = n * 7.0 + 11.7;
                    float3 cloudPos = n * cloudScale + (Noise3(warp) - 0.5) * 4.0;
                    float density = Fbm3(cloudPos);
                    float cloud = smoothstep(0.54, 0.80, density);
                    float3 cloudColor = float3(0.86, 0.88, 0.90) * lerp(0.74, 1.0, saturate(density * 1.4));

                    float3 surface = albedo * (0.012 + lit * 1.05) * warm;
                    // Sun glint on open water, hidden under cloud; the only value allowed near the bloom threshold.
                    float glint = pow(saturate(dot(reflect(d, n), sun)), 220.0) * (1.0 - land) * (1.0 - cloud);
                    surface += float3(1.0, 0.90, 0.74) * glint * 0.85 * lit;
                    surface = lerp(surface, cloudColor * warm * (0.035 + lit * 0.90), cloud * 0.88);

                    // Airlight thickening toward the limb joins seamlessly with the sky-side shell.
                    float mu = saturate(dot(-d, n));
                    float airlight = pow(1.0 - mu, 5.0);
                    float3 shell = LimbColor(0.0, ndl, sunDot);
                    surface = surface * (1.0 - airlight * 0.40) + shell * airlight * 0.70;

                    col = lerp(col, surface, saturate(disc / discWidth));
                }

                col *= _Exposure;
                col = 1.0 - exp(-col * 1.35); // soft shoulder; see the exposure note at the top

                // Interleaved-gradient dither prevents banding in the dark gradient of near-black space.
                float2 pixel = input.positionCS.xy;
                float dither = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                col += (dither - 0.5) * (1.0 / 255.0);
                return half4(max(col, 0.0), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
