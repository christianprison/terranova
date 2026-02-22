// URP-compatible lit shader for terrain props (rocks, wood, berries, etc.)
// Replaces "Universal Render Pipeline/Lit" which gets stripped from code-only builds.
// Supports: _BaseColor, _MainTex, _Smoothness, _Metallic, _EmissionColor
// GPU instancing enabled so MaterialPropertyBlock per-instance overrides work.
Shader "Terranova/PropLit"
{
    Properties
    {
        _BaseColor   ("Color", Color) = (1, 1, 1, 1)
        _MainTex     ("Texture", 2D) = "white" {}
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float,  _Smoothness)
                UNITY_DEFINE_INSTANCED_PROP(float,  _Metallic)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
                float smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
                float metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
                float4 emissionColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);

                // Sample texture and combine with base color tint
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 albedo = texColor.rgb * baseColor.rgb;

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();

                // Diffuse (Lambert)
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = albedo * NdotL * mainLight.color.rgb;

                // Specular (Blinn-Phong)
                float3 halfDir = normalize(mainLight.direction + input.viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPow = exp2(10.0 * smoothness + 1.0);
                float3 specular = pow(NdotH, specPow) * smoothness * mainLight.color.rgb;

                // Fresnel metallic tint
                float fresnel = pow(1.0 - saturate(dot(normalWS, input.viewDirWS)), 4.0);
                specular += fresnel * metallic * 0.2;

                // Ambient from spherical harmonics (environment lighting)
                float3 ambient = SampleSH(normalWS) * albedo;

                float3 finalColor = ambient + diffuse + specular;
                finalColor += emissionColor.rgb;

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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, GetMainLight().direction));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Terranova/VertexColorOpaque"
}
