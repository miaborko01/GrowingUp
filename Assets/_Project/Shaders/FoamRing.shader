// Animated foam ring around a stepping stone (put on a flat quad lying on the water, UV 0..1).
Shader "GrowingUp/FoamRing"
{
    Properties
    {
        _FoamColor ("Foam Colour", Color) = (0.95, 0.97, 1, 0.9)
        _Inner ("Inner Radius (0-0.5)", Range(0, 0.5)) = 0.28
        _Speed ("Swirl Speed", Float) = 1.2
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FoamColor; float _Inner, _Speed, _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + 1), u.x), u.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 c = i.uv - 0.5;
                float r = length(c);
                float a = atan2(c.y, c.x);
                float t = _Time.y * _Speed + _Seed;
                float n = vnoise(float2(a * 3 + t, r * 18 - t * 2)) * 0.6 + vnoise(float2(a * 7 - t * 1.3, r * 40)) * 0.4;
                float ring = smoothstep(_Inner - 0.02, _Inner + 0.03, r) * smoothstep(0.5, 0.33, r);
                float alpha = ring * smoothstep(0.35, 0.7, n + (0.5 - r) * 0.8) * _FoamColor.a;
                half3 col = MixFog(_FoamColor.rgb, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
