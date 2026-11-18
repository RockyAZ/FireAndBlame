Shader "FlameAndBlame/FillEffect"
{
    Properties
    {
        _Fill ("Fill", Range(0,1)) = 0
        _Intensity ("Intensity", Range(0,1)) = 1
        _FillColor ("Fill Color", Color) = (1,0.45,0.05,0.85)
        _EdgeColor ("Edge Color", Color) = (1,0.9,0.2,1)
        _BaseColor ("Base Color (above fill)", Color) = (0,0,0,0)
        _EdgeWidth ("Edge Width", Range(0,0.5)) = 0.08
        _NoiseScale ("Noise Scale", Float) = 6
        _NoiseSpeed ("Noise Speed", Float) = 1.5
        _MinY ("Min Y (object)", Float) = 0
        _MaxY ("Max Y (object)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Fill;
                float _Intensity;
                half4 _FillColor;
                half4 _EdgeColor;
                half4 _BaseColor;
                float _EdgeWidth;
                float _NoiseScale;
                float _NoiseSpeed;
                float _MinY;
                float _MaxY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.posOS = IN.positionOS.xyz;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float y01 = saturate((IN.posOS.y - _MinY) / max(_MaxY - _MinY, 1e-4));
                float t = _Time.y * _NoiseSpeed;
                float2 q = float2(IN.posOS.x + IN.posOS.z, IN.posOS.y);
                float n = vnoise(q * _NoiseScale + float2(0, -t));
                n += 0.5 * vnoise(q * _NoiseScale * 2.1 + float2(t * 0.7, -t * 1.3));
                n = n / 1.5 - 0.5; // about -0.5..0.5

                // Noise only acts near the fill line; faded out at fill 0 and 1 so nothing leaks.
                float edgeFade = saturate(_Fill * 20.0) * saturate((1.0 - _Fill) * 20.0);
                float fillLine = _Fill + n * _EdgeWidth * 2.0 * edgeFade;
                float filled = step(y01, fillLine);
                filled = max(filled, step(0.9999, _Fill));
                filled *= step(1e-4, _Fill);

                float edge = saturate(1.0 - abs(y01 - fillLine) / max(_EdgeWidth, 1e-4)) * filled;
                half4 fillCol = lerp(_FillColor, _EdgeColor, edge);
                half4 col = lerp(_BaseColor, fillCol, filled);
                col.a *= _Intensity;
                return col;
            }
            ENDHLSL
        }
    }
}
