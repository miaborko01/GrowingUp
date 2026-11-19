// Flowing river water for Quest (URP, single-pass instanced stereo safe, no depth texture, no textures).
// - two scrolling procedural ripple layers -> perturbed normal
// - sun specular glints + sky fresnel + shallow/deep colour across the river
// - animated foam along both banks (uv.y = 0 / 1 are the banks)
// - gentle vertex swell, fog
Shader "GrowingUp/RiverWater"
{
    Properties
    {
        _ShallowColor ("Shallow Colour", Color) = (0.28, 0.55, 0.55, 0.65)
        _DeepColor ("Deep Colour", Color) = (0.07, 0.25, 0.32, 0.9)
        _SkyColor ("Sky Reflection Colour", Color) = (0.55, 0.72, 0.86, 1)
        _FoamColor ("Foam Colour", Color) = (0.95, 0.97, 1, 1)
        _Flow ("Flow Direction (xz) & Speed (w)", Vector) = (1, 0, 0, 0.45)
        _RippleScale ("Ripple Scale", Float) = 6
        _RippleStrength ("Ripple Strength", Range(0, 2)) = 0.7
        _Gloss ("Gloss", Range(8, 512)) = 160
        _SpecStrength ("Specular Strength", Range(0, 3)) = 1.4
        _FoamWidth ("Bank Foam Width (0-0.5)", Range(0, 0.5)) = 0.12
        _SwellHeight ("Swell Height", Float) = 0.006
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float4 _Flow;
                float _RippleScale, _RippleStrength, _Gloss, _SpecStrength, _FoamWidth, _SwellHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + 1), u.x), u.y);
            }
            // height field of the water surface (two layers flowing at different speeds/angles)
            float waterHeight(float2 p, float t)
            {
                float2 dir = normalize(_Flow.xy + 1e-4);
                float2 side = float2(-dir.y, dir.x);
                float h = vnoise(p * _RippleScale - dir * t * _Flow.w * _RippleScale);
                h += 0.5 * vnoise(p * _RippleScale * 2.3 + side * 0.37 - (dir * 1.6 + side * 0.25) * t * _Flow.w * _RippleScale);
                h += 0.25 * vnoise(p * _RippleScale * 5.1 - dir * 2.4 * t * _Flow.w * _RippleScale);
                return h / 1.75;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float edge = saturate(min(v.uv.y, 1 - v.uv.y) * 6);           // no swell right at the banks
                ws.y += (waterHeight(ws.xz * 0.35, _Time.y) - 0.5) * _SwellHeight * edge;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y;
                float2 p = i.positionWS.xz;
                float e = 0.02;
                float h  = waterHeight(p, t);
                float hx = waterHeight(p + float2(e, 0), t);
                float hz = waterHeight(p + float2(0, e), t);
                float3 n = normalize(float3((h - hx) * _RippleStrength * 8, 1, (h - hz) * _RippleStrength * 8));

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                float spec = pow(saturate(dot(n, H)), _Gloss) * _SpecStrength;
                float ndl = saturate(dot(n, L.direction)) * 0.5 + 0.5;
                float fres = pow(1 - saturate(dot(n, V)), 4);

                // deeper in the middle of the river
                float across = 1 - abs(i.uv.y * 2 - 1);
                half4 baseCol = lerp(_ShallowColor, _DeepColor, smoothstep(0.1, 0.8, across));
                half3 col = baseCol.rgb * ndl * L.color + SampleSH(n) * baseCol.rgb * 0.35;
                col = lerp(col, _SkyColor.rgb, fres * 0.4);
                col += spec * L.color;

                // bank foam: animated, broken up by noise, flowing downstream
                float bank = min(i.uv.y, 1 - i.uv.y);
                float2 dir = normalize(_Flow.xy + 1e-4);
                float fn = vnoise(p * 9 - dir * t * _Flow.w * 9) * 0.6 + vnoise(p * 23 - dir * t * _Flow.w * 14) * 0.4;
                float foam = smoothstep(_FoamWidth, 0, bank) * smoothstep(0.35, 0.65, fn + (1 - bank / max(_FoamWidth, 1e-3)) * 0.35);
                // sparse drifting foam flecks mid-river
                foam = max(foam, smoothstep(0.88, 0.95, vnoise(p * 4 - dir * t * _Flow.w * 4)) * 0.35);
                col = lerp(col, _FoamColor.rgb * (0.75 + 0.25 * ndl), foam);

                half alpha = saturate(lerp(baseCol.a, 1, max(fres * 0.5, foam)) + spec);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
