// URP foliage shader with alpha cutout and wind vertex displacement.
// Used for tree canopies, bushes, and leafy vegetation.
// Double-sided rendering, texture-based alpha cutout, sine-wave wind sway.
// When _MainTex is set, uses texture alpha for cutout and texture color.
// When _MainTex is white (default), falls back to procedural hash pattern.
Shader "Terranova/WindFoliage"
{
    Properties
    {
        _BaseColor    ("Color", Color) = (0.3, 0.6, 0.2, 1)
        _MainTex      ("Texture", 2D) = "white" {}
        _Cutoff       ("Alpha Cutoff", Range(0, 1)) = 0.35
        _WindStrength ("Wind Strength", Float) = 0.08
        _WindSpeed    ("Wind Speed", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off    // Double-sided for foliage
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // x=1/w, y=1/h, z=w, w=h

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Cutoff;
                float  _WindStrength;
                float  _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Wind displacement — stronger at top of object
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float heightFactor = saturate(input.positionOS.y * 2.0);
                float phase = worldPos.x * 0.7 + worldPos.z * 0.5;
                float wind = sin(_Time.y * _WindSpeed + phase) * _WindStrength * heightFactor;
                float windZ = sin(_Time.y * _WindSpeed * 0.7 + phase * 1.3) * _WindStrength * 0.4 * heightFactor;

                input.positionOS.x += wind;
                input.positionOS.z += windZ;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = worldPos;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample texture
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Use texture alpha for cutout (EXPLORER assets have leaf alpha maps).
                // If no texture is assigned (_MainTex = white, alpha = 1), the clip
                // has no effect and the full mesh renders (solid foliage canopy).
                clip(texColor.a - _Cutoff);

                // Combine texture color with tint
                float3 leafColor = texColor.rgb * _BaseColor.rgb;

                // Lighting with translucency
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float NdotL = dot(normalWS, mainLight.direction);

                // Front + back lighting (translucent leaves)
                float frontLight = saturate(NdotL);
                float backLight = saturate(-NdotL) * 0.3; // Subsurface scatter
                float totalLight = frontLight + backLight;

                // Ambient from spherical harmonics (environment lighting)
                float3 ambient = SampleSH(normalWS) * leafColor;
                float3 diffuse = leafColor * totalLight * mainLight.color.rgb;

                return half4(ambient + diffuse, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster with wind displacement
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Cutoff;
                float  _WindStrength;
                float  _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float heightFactor = saturate(input.positionOS.y * 2.0);
                float wind = sin(_Time.y * _WindSpeed + worldPos.x * 0.7 + worldPos.z * 0.5) * _WindStrength * heightFactor;
                input.positionOS.x += wind;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, GetMainLight().direction));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Terranova/VertexColorOpaque"
}
