// ToonOutline: stylized URP shader (hand-written HLSL, no Shader Graph, no geometry shader).
//
// Lighting: main directional light only (with shadows) quantized into _RampSteps bands, softened by
//   _RampSmoothness, tinted with _ShadowColor in the dark bands; ambient comes from spherical harmonics.
//   A faint rim light is optional (_RimStrength 0 disables it).
//
// Wireframe highlight (UV-border method): every face of Unity cube/quad primitives (and of most
//   box-like modelled meshes) has UVs spanning 0..1, so the distance of a pixel to the UV border is the
//   distance to the face edge. Dividing by fwidth(uv) turns it into pixels, so _WireWidth is a constant
//   screen-space width in pixels, no matter how the cube is scaled (12 x 17.5 x 0.5 walls included).
//   The result is a faint line along every hard edge of every box. It uses the raw UV0 (not tiled by
//   _BaseMap_ST). On spheres/capsules it only shows along UV seams, which is harmless.
//
// Outline (inverted hull): second pass, LightMode "SRPDefaultUnlit" (URP draws it with the opaque
//   forward pass), Cull Front. The vertex is pushed in clip space by _OutlineWidth pixels, so the width is
//   roughly constant on screen at any distance. The push direction is the projected normal.
//   Hard-edged cubes have split normals (3 normals per corner), which would tear the hull apart at the
//   corners. Enable _OutlineUsePosition (default on) to push along the object-space direction from the
//   pivot instead (normalize(positionOS)): all copies of a corner move the same way, so the hull stays
//   closed. That works for convex shapes centred on the pivot (cubes, walls, slabs, windows). For smooth
//   curved meshes (rotors, ropes, spheres) set it to 0 to use the mesh normal.
Shader "FlameAndBlame/ToonOutline"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8,0.8,0.8,1)
        [MainTexture] _BaseMap ("Base Map (optional)", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.35,0.38,0.5,1)
        _RampSteps ("Ramp Steps (bands)", Range(2,4)) = 3
        _RampSmoothness ("Ramp Smoothness", Range(0.001,0.5)) = 0.05
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.5
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.15
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _WireColor ("Wire Color", Color) = (1,1,1,1)
        _WireWidth ("Wire Width (pixels)", Range(0.2,4)) = 1.2
        _WireStrength ("Wire Strength", Range(0,1)) = 0.25
        _OutlineColor ("Outline Color", Color) = (0.05,0.05,0.08,1)
        _OutlineWidth ("Outline Width (pixels)", Range(0,8)) = 2
        [Toggle] _OutlineUsePosition ("Outline Along Position (hard-edged meshes)", Float) = 1
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _ShadowColor;
        half _RampSteps;
        half _RampSmoothness;
        half _AmbientStrength;
        half4 _RimColor;
        half _RimStrength;
        half _RimPower;
        half4 _WireColor;
        half _WireWidth;
        half _WireStrength;
        half4 _OutlineColor;
        half _OutlineWidth;
        half _OutlineUsePosition;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(i.uv, _BaseMap)) * _BaseColor;

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);

                // Quantized N.L (shadow attenuation is applied before quantizing so shadow edges follow the bands)
                half ndl = saturate(dot(N, light.direction)) * light.shadowAttenuation;
                half t = ndl * _RampSteps;
                half band = floor(min(t, _RampSteps - 0.001h));
                half f = frac(t);
                half lit = saturate((band + smoothstep(0.5h - _RampSmoothness, 0.5h + _RampSmoothness, f)) / _RampSteps);
                // brightest band must reach 1 when fully lit
                lit = lerp(lit, 1.0h, step(_RampSteps - 0.001h, t));

                half3 toon = lerp(_ShadowColor.rgb, light.color, lit);
                half3 ambient = SampleSH(N) * _AmbientStrength;
                half3 col = albedo.rgb * (toon + ambient);

                // Rim
                half rim = pow(1.0h - saturate(dot(N, V)), _RimPower);
                col += _RimColor.rgb * smoothstep(0.4h, 0.9h, rim) * _RimStrength * lit;

                // Wireframe from UV borders, width in pixels
                float2 fw = max(fwidth(i.uv), 1e-5);
                float2 d = min(i.uv, 1.0 - i.uv) / fw;
                float edge = min(d.x, d.y);
                half wire = 1.0h - smoothstep(_WireWidth - 0.75h, _WireWidth + 0.75h, edge);
                col = lerp(col, _WireColor.rgb, wire * _WireStrength);

                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 dirWS;
                if (_OutlineUsePosition > 0.5h)
                    dirWS = TransformObjectToWorldDir(normalize(i.positionOS.xyz + 1e-6), false);
                else
                    dirWS = TransformObjectToWorldNormal(i.normalOS);
                float4 clip = TransformObjectToHClip(i.positionOS.xyz);
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, dirWS).xy;
                dirCS /= max(length(dirCS), 1e-5);
                // pixels -> clip units: 2 / resolution, times w to stay constant in screen space
                clip.xy += dirCS * (_OutlineWidth * 2.0 / _ScreenParams.xy) * clip.w;
                o.positionCS = clip;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = cs;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
