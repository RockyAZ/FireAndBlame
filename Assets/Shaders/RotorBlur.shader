Shader "FlameAndBlame/RotorBlur"
{
    Properties
    {
        [IntRange] _BladeCount ("Blade Count", Range(1,8)) = 4
        _Speed ("Speed (turns/s, script)", Float) = 6
        _Angle ("Angle (turns, script)", Float) = 0
        _BladeColor ("Blade Color", Color) = (0.12,0.12,0.14,0.9)
        _DiscColor ("Disc Color", Color) = (0.2,0.2,0.22,0.08)
        _BladeWidth ("Blade Width (turns)", Range(0,0.1)) = 0.012
        _Smear ("Smear (turns)", Range(0.01,1)) = 0.2
        _HubRadius ("Hub Radius", Range(0,0.5)) = 0.08
        _TipFade ("Tip Fade", Range(0.01,0.5)) = 0.12
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
                float _BladeCount;
                float _Speed;
                float _Angle;
                half4 _BladeColor;
                half4 _DiscColor;
                float _BladeWidth;
                float _Smear;
                float _HubRadius;
                float _TipFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                if (r >= 1.0) return half4(0, 0, 0, 0);

                float n = max(1.0, round(_BladeCount));
                float t = atan2(p.y, p.x) * 0.15915494;
                // how far (in turns) this pixel trails behind the nearest blade
                float d = frac((_Angle - t) * n) / n;

                float smear = _Smear * (0.5 + 0.5 * saturate(_Speed / 8.0));
                float streak = exp(-max(d - _BladeWidth, 0.0) / max(smear, 0.0001));

                float hub = smoothstep(_HubRadius, _HubRadius + 0.1, r);
                float tip = 1.0 - smoothstep(1.0 - _TipFade, 1.0, r);
                float radial = hub * tip;

                float bladeA = _BladeColor.a * streak * radial;
                float discA = _DiscColor.a * radial;

                float a = bladeA + discA * (1.0 - bladeA);
                half3 rgb = (_BladeColor.rgb * bladeA + _DiscColor.rgb * discA * (1.0 - bladeA)) / max(a, 0.0001);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
