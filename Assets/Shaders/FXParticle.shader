Shader "FlameAndBlame/FXParticle"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        [KeywordEnum(Soft, Star, Hard)] _Shape ("Shape", Float) = 0
        _Softness ("Softness", Range(0.05,1)) = 0.6
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _SHAPE_SOFT _SHAPE_STAR _SHAPE_HARD
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Softness;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv * 2 - 1; o.col = i.col; return o;
            }
            half4 frag(V i) : SV_Target
            {
                float r = length(i.uv);
                float a;
                #if defined(_SHAPE_STAR)
                    float ang = atan2(i.uv.y, i.uv.x);
                    float edge = lerp(0.35, 0.95, pow(abs(cos(ang * 2.5)), 1.5));
                    a = smoothstep(edge, edge - 0.12, r);
                #elif defined(_SHAPE_HARD)
                    a = smoothstep(1.0, 0.85, r);
                #else
                    a = saturate(1 - r); a = pow(a, 1.0 / max(_Softness, 0.05) * 0.8);
                #endif
                half4 c = _BaseColor * i.col;
                c.a *= a;
                c.rgb *= c.a > 0 ? 1 : 0;
                return c;
            }
            ENDHLSL
        }
    }
}
