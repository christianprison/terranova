// URP-compatible lit shader for terrain props (rocks, wood, berries, etc.)
// Replaces "Universal Render Pipeline/Lit" which gets stripped from code-only builds.
// Supports: _BaseColor, _Smoothness, _Metallic, _EmissionColor
Shader "Terranova/PropLit"
{
    Properties
    {
        _BaseColor   ("Color", Color) = (0.5, 0.5, 0.5, 1)
        _Smoothness  ("Smoothness", Range(0, 1)) = 0.2
        _Metallic    ("Metallic", Range(0, 1)) = 0.0
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Smoothness;
                float  _Metallic;
                float4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();

                // Diffuse (Lambert)
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = _BaseColor.rgb * NdotL * mainLight.color.rgb;

                // Specular (Blinn-Phong)
                float3 halfDir = normalize(mainLight.direction + input.viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPow = exp2(10.0 * _Smoothness + 1.0);
                float3 specular = pow(NdotH, specPow) * _Smoothness * mainLight.color.rgb;

                // Fresnel metallic tint
                float fresnel = pow(1.0 - saturate(dot(normalWS, input.viewDirWS)), 4.0);
                specular += fresnel * _Metallic * 0.2;

                // Procedural normal variation (fakes surface roughness)
                float noise = frac(sin(dot(input.positionWS.xz, float2(12.9898, 78.233))) * 43758.5453);
                float microDetail = lerp(0.95, 1.05, noise);

                // Ambient
                float3 ambient = _BaseColor.rgb * 0.35;

                float3 finalColor = (ambient + diffuse * 0.65 + specular) * microDetail;
                finalColor += _EmissionColor.rgb;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Terranova/VertexColorOpaque"
}
