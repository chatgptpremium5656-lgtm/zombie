// Stylized gradient sky with a soft sun disc. Sun direction comes from StylizedAtmosphere (_ZSunDir).
Shader "Zombie/SkyGradient"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.24, 0.5, 0.88, 1)
        _HorizonColor ("Horizon", Color) = (0.86, 0.9, 0.93, 1)
        _GroundColor ("Below Horizon", Color) = (0.6, 0.66, 0.62, 1)
        _HorizonExponent ("Horizon Falloff", Range(0.1, 4)) = 0.55
        _SunColor ("Sun", Color) = (1, 0.88, 0.66, 1)
        _SunSize ("Sun Size", Range(0.0005, 0.05)) = 0.006
        _SunGlow ("Sun Glow", Range(0, 4)) = 0.7
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _ZenithColor;
            half4 _HorizonColor;
            half4 _GroundColor;
            half _HorizonExponent;
            half4 _SunColor;
            half _SunSize;
            half _SunGlow;
            float4 _ZSunDir;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 c = d.y >= 0
                    ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(d.y), _HorizonExponent))
                    : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-d.y * 5.0));
                float s = dot(d, normalize(_ZSunDir.xyz + float3(0, 1e-4, 0)));
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, s);
                float glow = pow(saturate(s), 48.0) * _SunGlow + pow(saturate(s), 5.0) * _SunGlow * 0.2;
                c += _SunColor.rgb * (disc * 4.0 + glow);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
