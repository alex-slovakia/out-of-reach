// Latitude-longitude HDR sky oriented by a full 3D rotation (Unity's built-in Skybox/Panoramic only yaws).
// Sampling, tint, exposure and HDR decode match Skybox/Panoramic, so with _SkyRotation = identity and no Sun or
// Earth the image is identical to the built-in material at _Rotation = 0. Lookup frame: +X = RA 0h, +Y = north,
// +Z = RA 6h. Adds the Sun at its true angular size and Earth as a flux-conserving point (13-17 arcsec, unresolved).
Shader "OutOfReach/Skybox/Celestial Panoramic"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _SkyRotation ("Static sky-from-world quaternion (x, y, z, w)", Vector) = (0, 0, 0, 1)
        [NoScaleOffset] _MainTex ("Spherical (HDR)", 2D) = "grey" {}
        _SunSkyDirection ("Static Sun: sky-frame direction (xyz), angular radius rad (w; 0 = none)", Vector) = (0, 0, 0, 0)
        _SunRadiance ("Sun disc HDR radiance (not scaled by Exposure)", Float) = 60
        _EarthSkyDirection ("Static Earth: sky-frame direction (xyz), map flux in texture units x sr (w; 0 = none)", Vector) = (0, 0, 0, 0)
        _EarthTint ("Earth colour, linear, luminance 1", Vector) = (0.90, 1.0, 1.22, 0)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            half4 _Tint;
            half _Exposure;
            float4 _SkyRotation;
            float4 _SunSkyDirection;
            float _SunRadiance;
            float4 _EarthSkyDirection;
            float4 _EarthTint;
            // Globals from CelestialSkyPresenter; all zeros (never set / presenter disabled) select the static values.
            float4 _OorCelestialSkyRotation;
            float4 _OorCelestialSun;
            float4 _OorCelestialEarth;

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 skyDir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateByQuaternion(float4 q, float3 v)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            // Angle between unit vectors, accurate for tiny angles (asin of the cross product, not acos of the dot).
            float AngleBetween(float3 a, float3 b)
            {
                return dot(a, b) > 0.0 ? asin(min(length(cross(a, b)), 1.0)) : UNITY_PI * 0.5 + asin(min(-dot(a, b), 1.0));
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                float4 q = dot(_OorCelestialSkyRotation, _OorCelestialSkyRotation) > 0.25
                    ? _OorCelestialSkyRotation : _SkyRotation;
                q *= rsqrt(max(dot(q, q), 1e-8));
                o.skyDir = RotateByQuaternion(q, v.vertex.xyz); // linear map, so interpolation stays exact
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.skyDir);
                float longitude = atan2(d.z, d.x);
                float latitude = acos(clamp(d.y, -1.0, 1.0));
                float2 uv = float2(0.5 - longitude * (0.5 / UNITY_PI), 1.0 - latitude / UNITY_PI);
                // Seam-free mip selection: take the u derivative from whichever of u or frac(u + 0.5)
                // is continuous at this pixel, so the atan2 wrap does not sample the smallest mip.
                float uAlt = frac(uv.x + 0.5);
                float2 dx = ddx(uv), dy = ddy(uv);
                float dxAlt = ddx(uAlt), dyAlt = ddy(uAlt);
                if (abs(dxAlt) < abs(dx.x)) dx.x = dxAlt;
                if (abs(dyAlt) < abs(dy.x)) dy.x = dyAlt;
                half4 tex = tex2Dgrad(_MainTex, uv, dx, dy);
                float3 scale = _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                float3 c = DecodeHDR(tex, _MainTex_HDR) * scale;

                // Angular size of this pixel (radians); unit-vector derivatives approximate angle per pixel.
                float pixelAngle = max(max(length(ddx(d)), length(ddy(d))), 1e-6);

                // Earth: unresolved point, drawn one pixel in radius with radiance = flux / solid angle, so its
                // brightness relative to the catalogue stars is preserved at any resolution or field of view.
                float4 earth = _OorCelestialEarth.w > 0.0 ? _OorCelestialEarth : _EarthSkyDirection;
                if (earth.w > 0.0)
                {
                    float rho = pixelAngle;
                    float cover = saturate((rho - AngleBetween(d, normalize(earth.xyz))) / pixelAngle + 0.5);
                    c += _EarthTint.rgb * (earth.w / (UNITY_PI * rho * rho)) * cover * scale;
                }

                // Sun: true angular size (0.33 deg from Mars), linear limb darkening u = 0.6, occludes the stars.
                float4 sun = _OorCelestialSun.w > 0.0 ? _OorCelestialSun : _SunSkyDirection;
                if (sun.w > 0.0)
                {
                    float theta = AngleBetween(d, normalize(sun.xyz));
                    float cover = saturate((sun.w - theta) / pixelAngle + 0.5);
                    float r = saturate(theta / sun.w);
                    float limb = 1.0 - 0.6 * (1.0 - sqrt(1.0 - r * r));
                    c = lerp(c, _SunRadiance * limb, cover);
                }
                return half4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
