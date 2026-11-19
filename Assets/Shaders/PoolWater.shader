// Pool water surface: see-through underwater look.
// Reads the opaque scene behind the surface (_CameraOpaqueTexture) and the scene depth (_CameraDepthTexture),
// blurs and tints it blue more strongly the deeper the object lies below the surface, adds animated ripples
// (refraction offset) and a soft sun highlight. ZWrite Off and queue Transparent-10, so everything transparent
// that is inside the pool (the tank, world-space UI) is drawn after it and stays sharp and readable.
// Needs Opaque Texture and Depth Texture enabled on the URP asset.
Shader "FlameAndBlame/PoolWater"
{
    Properties
    {
        _ShallowColor ("Shallow Tint", Color) = (0.55, 0.85, 1.0, 1)
        _DeepColor ("Deep Color", Color) = (0.04, 0.28, 0.62, 1)
        _DepthFade ("Depth Fade (m)", Float) = 2.5
        _MaxFog ("Max Fog", Range(0, 1)) = 0.7
        _MinBlur ("Blur At Surface (px)", Range(0, 8)) = 1.5
        _BlurRadius ("Blur At Depth (px)", Range(0, 16)) = 7
        _Distortion ("Ripple Distortion", Range(0, 0.05)) = 0.008
        _RippleScale ("Ripple Scale", Float) = 1.5
        _RippleSpeed ("Ripple Speed", Float) = 0.6
        _HighlightColor ("Highlight (alpha = strength)", Color) = (1, 1, 1, 0.35)
        _HighlightSharpness ("Highlight Sharpness", Range(1, 256)) = 120
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "PoolWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthFade;
                half _MaxFog;
                float _MinBlur;
                float _BlurRadius;
                float _Distortion;
                float _RippleScale;
                float _RippleSpeed;
                half4 _HighlightColor;
                float _HighlightSharpness;
                half _FresnelStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            float Ripple (float2 p, float t)
            {
                return sin(p.x * 1.7 + t * 1.3) * cos(p.y * 1.3 - t * 1.1) + 0.5 * sin((p.x + p.y) * 2.9 + t * 2.1);
            }

            float SceneEye (float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float t = _Time.y * _RippleSpeed;
                float2 p = i.positionWS.xz * _RippleScale;
                const float e = 0.05;
                float h = Ripple(p, t);
                float2 grad = float2(Ripple(p + float2(e, 0), t) - h, Ripple(p + float2(0, e), t) - h) / e;

                float surfaceEye = -TransformWorldToView(i.positionWS).z;
                float thick = max(0, SceneEye(uv) - surfaceEye);
                float depth01 = saturate(thick / max(_DepthFade, 1e-3));

                // Refraction: offset the lookup, but never pull in something that is in front of the surface.
                float2 duv = uv + grad * _Distortion * saturate(thick * 4);
                if (SceneEye(duv) < surfaceEye) duv = uv;

                // 13-tap disc blur whose radius grows with depth.
                float2 texel = (_MinBlur + (_BlurRadius - _MinBlur) * depth01) / _ScreenParams.xy;
                static const float2 taps[12] =
                {
                    float2( 0.50,  0.00), float2( 0.25,  0.43), float2(-0.25,  0.43),
                    float2(-0.50,  0.00), float2(-0.25, -0.43), float2( 0.25, -0.43),
                    float2( 0.87,  0.50), float2( 0.00,  1.00), float2(-0.87,  0.50),
                    float2(-0.87, -0.50), float2( 0.00, -1.00), float2( 0.87, -0.50)
                };
                float3 col = SampleSceneColor(duv);
                [unroll] for (int k = 0; k < 12; k++) col += SampleSceneColor(duv + taps[k] * texel);
                col /= 13.0;

                float3 water = lerp(col * _ShallowColor.rgb, _DeepColor.rgb, depth01 * _MaxFog);

                // Surface: ripple normal, sun highlight and a little fresnel.
                float3 n = normalize(float3(-grad.x * 0.15, 1, -grad.y * 0.15));
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 H = normalize(_MainLightPosition.xyz + V);
                float spec = pow(saturate(dot(n, H)), _HighlightSharpness);
                water += _HighlightColor.rgb * _HighlightColor.a * spec;
                float fres = pow(1 - saturate(dot(n, V)), 4);
                water = lerp(water, _DeepColor.rgb, fres * _FresnelStrength);

                return half4(water, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
