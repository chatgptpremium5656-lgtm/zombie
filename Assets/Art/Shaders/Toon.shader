// Shared stylized lit shader for the whole game (CLAUDE.md, art rule 4).
// Soft ramp light, colour-tinted shadows, rim light, optional soft highlight,
// vertex-colour albedo (sRGB painted in Blender) and shader wind.
// Vertex alpha = wind weight. Globals (_ZWind, _ZAmbient*, _ZGrassFade) come from StylizedAtmosphere.
Shader "Zombie/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _VertexColor ("Use Vertex Color", Range(0, 1)) = 1
        _VertexColorIsSRGB ("Vertex Color Is sRGB", Range(0, 1)) = 1
        _ShadowTint ("Shadow Tint", Color) = (0.72, 0.68, 0.95, 1)
        _RampThreshold ("Ramp Threshold", Range(-1, 1)) = 0.0
        _RampSmooth ("Ramp Smoothness", Range(0.01, 1)) = 0.2
        _RimColor ("Rim Color", Color) = (1, 0.94, 0.82, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.35
        _Specular ("Soft Highlight", Range(0, 1)) = 0
        _WindStrength ("Wind Strength", Range(0, 1)) = 0
        _WindFlutter ("Wind Flutter", Range(0, 1)) = 0
        _GrassMode ("Grass Mode", Range(0, 1)) = 0
        _FogAmount ("Fog Amount", Range(0, 1)) = 1
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _VertexColor;
            half _VertexColorIsSRGB;
            half4 _ShadowTint;
            half _RampThreshold;
            half _RampSmooth;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _Specular;
            half _WindStrength;
            half _WindFlutter;
            half _GrassMode;
            half _FogAmount;
            half4 _EmissionColor;
            half _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Set globally by StylizedAtmosphere.
        float4 _ZWind;        // xy = wind direction (xz plane), z = speed, w = gust strength
        float4 _ZGrassFade;   // x = fade start, y = fade end (metres from camera)
        half4 _ZAmbientSky;
        half4 _ZAmbientGround;

        float3 ApplyWind(float3 positionWS, float weight, float3 pivotWS)
        {
            float amount = (_WindStrength + _WindFlutter) * weight;
            if (amount <= 0.0001)
                return positionWS;

            float t = _Time.y * max(_ZWind.z, 0.01);
            float phase = dot(pivotWS.xz, float2(0.13, 0.09));
            float sway = sin(t + phase) * 0.6 + sin(t * 0.43 + phase * 1.7) * 0.4;
            float gust = (sin(t * 0.21 + pivotWS.x * 0.02) * 0.5 + 0.5) * _ZWind.w;
            float2 dir = normalize(_ZWind.xy + float2(1e-5, 0));
            float bend = (sway * 0.5 + 0.3 + gust) * _WindStrength * weight;
            positionWS.xz += dir * bend;
            positionWS.y -= abs(bend) * 0.12;

            float flutter = sin(t * 3.1 + dot(positionWS, float3(1.7, 2.3, 1.3))) * _WindFlutter * weight * 0.05;
            positionWS += float3(flutter, flutter * 0.5, -flutter);
            return positionWS;
        }

        float3 DisplacedPositionWS(float3 positionOS, float windWeight)
        {
            float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
            float3 positionWS = TransformObjectToWorld(positionOS);
            if (_GrassMode > 0.5)
            {
                float d = distance(pivot.xz, _WorldSpaceCameraPos.xz);
                float s = 1.0 - smoothstep(_ZGrassFade.x, _ZGrassFade.y, d);
                positionWS = pivot + (positionWS - pivot) * s;
            }
            return ApplyWind(positionWS, windWeight, pivot);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half tint : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 positionWS = DisplacedPositionWS(v.positionOS.xyz, v.color.a);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                half3 vc = _VertexColorIsSRGB > 0.5 ? SRGBToLinear(v.color.rgb) : v.color.rgb;
                o.color = half4(lerp(half3(1, 1, 1), vc, _VertexColor), v.color.a);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);

                // Large soft colour patches for grass so the field is not uniform.
                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                o.tint = sin(pivot.x * 0.11 + sin(pivot.z * 0.07) * 2.0) * sin(pivot.z * 0.13) * 0.5 + 0.5;
                return o;
            }

            half4 Frag(Varyings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * i.color.rgb;
                albedo *= lerp(half3(1, 1, 1), lerp(half3(0.82, 0.92, 0.78), half3(1.12, 1.06, 0.82), i.tint), _GrassMode);

                float3 n = normalize(i.normalWS);
                n = isFrontFace ? n : -n;
                n = normalize(lerp(n, float3(0, 1, 0), _GrassMode * 0.75));
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half ndl = dot(n, mainLight.direction);
                half ramp = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, ndl);
                half lit = ramp * mainLight.shadowAttenuation;

                half3 ambient = lerp(_ZAmbientGround.rgb, _ZAmbientSky.rgb, n.y * 0.5 + 0.5);
                half3 lightColor = lerp(ambient * _ShadowTint.rgb, ambient + mainLight.color, lit);

                half3 additional = 0;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    half wrap = saturate(dot(n, light.direction) * 0.6 + 0.4);
                    additional += light.color * (light.distanceAttenuation * light.shadowAttenuation * wrap);
                LIGHT_LOOP_END
                #endif

                half3 color = albedo * (lightColor + additional);

                half fresnel = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                color += _RimColor.rgb * mainLight.color * fresnel * _RimStrength * (0.35 + 0.65 * lit) * lerp(half3(1, 1, 1), albedo * 2.0, 0.4);

                half3 h = normalize(mainLight.direction + viewDir);
                color += mainLight.color * smoothstep(0.93, 0.985, dot(n, h)) * _Specular * lit * 0.6;

                color += _EmissionColor.rgb;
                color = lerp(color, MixFog(color, i.fogFactor), _FogAmount);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = DisplacedPositionWS(v.positionOS.xyz, v.color.a);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformWorldToHClip(DisplacedPositionWS(v.positionOS.xyz, v.color.a));
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(DisplacedPositionWS(v.positionOS.xyz, v.color.a));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 NormalsFrag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
