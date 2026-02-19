// URP foliage shader with alpha cutout and wind vertex displacement.
// Used for tree canopies, bushes, and leafy vegetation.
// Double-sided rendering, procedural leaf pattern, sine-wave wind sway.
Shader "Terranova/WindFoliage"
{
    Properties
    {
        _BaseColor    ("Color", Color) = (0.3, 0.6, 0.2, 1)
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            // Simple hash for procedural patterns
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Procedural leaf pattern — creates organic cutout shape
                float2 cellPos = input.positionWS.xz * 3.0 + input.positionWS.y * 1.5;
                float h = hash(floor(cellPos));
                float leafMask = smoothstep(_Cutoff - 0.1, _Cutoff + 0.1, h);
                clip(leafMask - 0.5);

                // Lighting with translucency
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float NdotL = dot(normalWS, mainLight.direction);

                // Front + back lighting (translucent leaves)
                float frontLight = saturate(NdotL);
                float backLight = saturate(-NdotL) * 0.3; // Subsurface scatter
                float totalLight = frontLight + backLight;

                // Color variation based on position
                float colorVar = hash(floor(input.positionWS.xz * 0.5)) * 0.15;
                float3 leafColor = _BaseColor.rgb + float3(-colorVar, colorVar * 0.5, -colorVar * 0.5);

                float3 ambient = leafColor * 0.35;
                float3 diffuse = leafColor * totalLight * mainLight.color.rgb * 0.65;

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

            float3 _LightDirection;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Cutoff;
                float  _WindStrength;
                float  _WindSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float heightFactor = saturate(input.positionOS.y * 2.0);
                float wind = sin(_Time.y * _WindSpeed + worldPos.x * 0.7 + worldPos.z * 0.5) * _WindStrength * heightFactor;
                input.positionOS.x += wind;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                output.positionWS = posWS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float2 cellPos = input.positionWS.xz * 3.0 + input.positionWS.y * 1.5;
                float h = hash(floor(cellPos));
                clip(h - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Terranova/VertexColorOpaque"
}
