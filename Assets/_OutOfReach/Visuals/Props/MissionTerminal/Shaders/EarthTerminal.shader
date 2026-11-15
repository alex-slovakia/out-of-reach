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
        _StaticImage ("R33 static flat image mode", Float) = 0
        _StaticRect ("Static image panel UV min.xy / max.xy", Vector) = (.025,.245,.515625,.865)
        _Inset ("R33 dated Slovakia inset (MODIS Terra)", 2D) = "black" {}
        _InsetAvailable ("-1 none: EPIC original inset / 0 pending / 1 shown", Float) = -1
        _InsetRect ("Slovakia inset UV min.xy / max.xy (1280x1012 aspect)", Vector) = (.545,.10,.725,.3277)
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
            TEXTURE2D(_Inset); SAMPLER(sampler_Inset);
            CBUFFER_START(UnityPerMaterial)
            float4 _GlobeRotation, _Pointer, _Ripple, _StaticRect, _InsetRect;
            float _ObservationAvailable, _DiscRadius, _StaticImage, _InsetAvailable;
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
                half3 color=half3(.014,.026,.033); // R33: lifted graphite so the idle screen reads in the cockpit.
                float rule=1-smoothstep(.001,.002,abs(uv.y-.88));
                color+=half3(.07,.14,.17)*rule;
                if(_StaticImage>.5)
                {
                    // R33 static mode: the whole archived image, unmodified pixels, no projection, pointer or ripple.
                    // The panel rect matches the image's pixel aspect, so nothing is cropped or stretched.
                    float2 panel=(uv-_StaticRect.xy)/(_StaticRect.zw-_StaticRect.xy);
                    float2 edge=min(panel,1-panel)*(_StaticRect.zw-_StaticRect.xy)*float2(1.6,1);
                    float border=min(edge.x,edge.y);
                    if(all(panel>=0) && all(panel<=1))
                    {
                        color=_ObservationAvailable>.5
                            ? SAMPLE_TEXTURE2D(_Observation,sampler_Observation,panel).rgb*.92
                            : half3(.018,.028,.034);
                    }
                    color+=half3(.02,.12,.18)*(1-smoothstep(.0015,.004,abs(border)));
                    return half4(color,1);
                }
                // R33: larger globe; presenter IsOverGlobe uses the same centre (.285,.525) and radius .32.
                float2 disc=(uv-float2(.285,.525))*float2(1.6,1)/.32;
                float radius=length(disc);
                if(radius<1)
                {
                    float3 viewNormal=float3(disc,sqrt(saturate(1-dot(disc,disc))));
                    float3 sourceNormal=Rotate(viewNormal,_GlobeRotation);
                    float longitude=atan2(sourceNormal.x,sourceNormal.z);
                    float latitude=asin(clamp(sourceNormal.y,-1,1));
                    float lines=min(abs(sin(longitude*6)),abs(sin(latitude*6)));
                    float grid=1-smoothstep(.015,.035,lines);
                    color=half3(.030,.052,.062)+grid*half3(.07,.11,.12);
                    // Orthographic projection of the visible EPIC disc, never treated as an equirectangular map.
                    // SourceNormal.z<=0 is an unobserved hemisphere: neutral graticule only.
                    if(sourceNormal.z>.01 && _ObservationAvailable>.5)
                    {
                        float2 observationUv=.5+sourceNormal.xy*_DiscRadius;
                        half3 observed=SAMPLE_TEXTURE2D(_Observation,sampler_Observation,observationUv).rgb;
                        float mask=smoothstep(.01,.06,sourceNormal.z);
                        color=lerp(color,observed,mask);
                    }
                    color*=.78+.22*viewNormal.z;
                }
                color+=half3(.02,.12,.18)*(1-smoothstep(.004,.012,abs(radius-1)));
                if(_InsetAvailable<-.5)
                {
                    // Without the Slovakia image the small square inset shows the original EPIC disc, original framing.
                    float2 inset=(uv-float2(.72,.20))/float2(.18/1.6,.18)+.5;
                    if(all(inset>=0) && all(inset<=1))
                        color=_ObservationAvailable>.5 ? SAMPLE_TEXTURE2D(_Observation,sampler_Observation,inset).rgb : half3(.018,.028,.034);
                }
                else
                {
                    // Dated MODIS Terra Slovakia image, whole frame, unmodified pixels, its own aspect; dark until its pass ends.
                    float2 inset=(uv-_InsetRect.xy)/(_InsetRect.zw-_InsetRect.xy);
                    float2 edge=min(inset,1-inset)*(_InsetRect.zw-_InsetRect.xy)*float2(1.6,1);
                    if(all(inset>=0) && all(inset<=1))
                        color=_InsetAvailable>.5 ? SAMPLE_TEXTURE2D(_Inset,sampler_Inset,inset).rgb : half3(.018,.028,.034);
                    color+=half3(.02,.12,.18)*(1-smoothstep(.0012,.003,abs(min(edge.x,edge.y))));
                }
                float2 d=(uv-_Pointer.xy)*float2(1.6,1);
                float pointerRadius=length(d);
                // Cursor-as-hand: open ring while hovering, filled grab disc while held.
                float cursor=(1-smoothstep(.0015,.003,abs(pointerRadius-.014)))*_Pointer.z;
                float grab=(1-smoothstep(.007,.009,pointerRadius))*_Pointer.w*_Pointer.z;
                color+=cursor*half3(.10,.45,.62)+grab*half3(.05,.22,.30);
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
