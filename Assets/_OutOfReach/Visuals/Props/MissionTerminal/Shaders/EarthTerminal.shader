Shader "OutOfReach/PhysicalEarthTerminal"
{
    Properties
    {
        _Observation ("Unmodified dated EPIC disc", 2D) = "black" {}
        _ObservationAvailable ("Observation available at mission time", Float) = 0
        _GlobeRotation ("Schematic orientation quaternion", Vector) = (0,0,0,1)
        _Pointer ("UV / valid / held", Vector) = (0,0,0,0)
        _Ripple ("Contact UV / age / enabled", Vector) = (0,0,10,0)
        _DiscRadius ("Approximate observed disc radius / image width", Float) = .405
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "TerminalDisplay"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_Observation); SAMPLER(sampler_Observation);
            CBUFFER_START(UnityPerMaterial)
            float4 _GlobeRotation, _Pointer, _Ripple;
            float _ObservationAvailable, _DiscRadius;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv;
                return output;
            }
            float3 Rotate(float3 p,float4 q) { return p+2*cross(q.xyz,cross(q.xyz,p)+q.w*p); }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=input.uv;
                half3 color=half3(.006,.014,.019);
                float rule=1-smoothstep(.001,.002,abs(uv.y-.88));
                color+=half3(.07,.14,.17)*rule;
                float2 disc=(uv-float2(.29,.51))*float2(1.6,1)/.30;
                float radius=length(disc);
                if(radius<1)
                {
                    float3 viewNormal=float3(disc,sqrt(saturate(1-dot(disc,disc))));
                    float3 sourceNormal=Rotate(viewNormal,_GlobeRotation);
                    float longitude=atan2(sourceNormal.x,sourceNormal.z);
                    float latitude=asin(clamp(sourceNormal.y,-1,1));
                    float lines=min(abs(sin(longitude*6)),abs(sin(latitude*6)));
                    float grid=1-smoothstep(.015,.035,lines);
                    color=half3(.022,.040,.048)+grid*half3(.06,.09,.10);
                    // Orthographic projection of the visible EPIC disc, never treated as an equirectangular map.
                    // SourceNormal.z<=0 is an unobserved hemisphere: neutral graticule only.
                    if(sourceNormal.z>.01 && _ObservationAvailable>.5)
                    {
                        float2 observationUv=.5+sourceNormal.xy*_DiscRadius;
                        half3 observed=SAMPLE_TEXTURE2D(_Observation,sampler_Observation,observationUv).rgb;
                        float mask=smoothstep(.01,.06,sourceNormal.z);
                        color=lerp(color,observed*.85,mask);
                    }
                    color*=.65+.35*viewNormal.z;
                }
                color+=half3(.02,.12,.18)*(1-smoothstep(.004,.012,abs(radius-1)));
                // The small square inset always shows the original image with its original framing.
                float2 inset=(uv-float2(.72,.20))/float2(.18/1.6,.18)+.5;
                if(all(inset>=0) && all(inset<=1))
                {
                    color=_ObservationAvailable>.5 ? SAMPLE_TEXTURE2D(_Observation,sampler_Observation,inset).rgb*.85 : half3(.018,.028,.034);
                }
                float2 d=(uv-_Pointer.xy)*float2(1.6,1);
                float pointerRadius=length(d);
                float cursor=(1-smoothstep(.0015,.003,abs(pointerRadius-.012)))*_Pointer.z;
                color+=cursor*half3(.07,.35,.52);
                float age=_Ripple.z;
                float ringRadius=.013+age*.13;
                float ring=(1-smoothstep(.002,.005,abs(length((uv-_Ripple.xy)*float2(1.6,1))-ringRadius)));
                float decay=saturate(1-age/.65)*step(0,age)*_Ripple.w;
                color+=ring*decay*half3(.10,.44,.68);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
