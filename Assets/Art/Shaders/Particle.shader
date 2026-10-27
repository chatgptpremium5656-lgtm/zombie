// Simple particle shader: texture * vertex colour * tint. Blend mode is set per material
// (additive for fire and embers, alpha for smoke and leaves).
Shader "Zombie/Particle"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        _FogAmount ("Fog Amount", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _SrcBlend;
                half _DstBlend;
                half _FogAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.color * _Tint;
                // Additive materials fade to black in fog, alpha ones to the fog colour.
                half additive = _DstBlend == 1 ? 1 : 0;
                half3 fogged = additive > 0.5 ? c.rgb * ComputeFogIntensity(i.fog) : MixFog(c.rgb, i.fog);
                c.rgb = lerp(c.rgb, fogged, _FogAmount);
                // Additive (One One) ignores alpha, so fold the texture alpha into the colour.
                c.rgb *= additive > 0.5 ? c.a : 1.0;
                return c;
            }
            ENDHLSL
        }
    }
}
