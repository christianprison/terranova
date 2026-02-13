// Terrain splatting shader: blends 4 terrain textures based on per-vertex weights.
//
// UV0 (TEXCOORD0) = world-space XZ coordinates for texture sampling
// UV1 (TEXCOORD1) = blend weights: x=Grass, y=Dirt, z=Stone, w=Sand
// Vertex colors serve as fallback tint when no textures are assigned.
//
// Story 0.3: Texturierung und Materialien
Shader "Terranova/TerrainSplat"
{
    Properties
    {
        _GrassTex ("Grass Texture", 2D) = "white" {}
        _DirtTex  ("Dirt Texture",  2D) = "white" {}
        _StoneTex ("Stone Texture", 2D) = "white" {}
        _SandTex  ("Sand Texture",  2D) = "white" {}
        _TexScale ("Texture Scale", Float) = 0.25
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

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_DirtTex);  SAMPLER(sampler_DirtTex);
            TEXTURE2D(_StoneTex); SAMPLER(sampler_StoneTex);
            TEXTURE2D(_SandTex);  SAMPLER(sampler_SandTex);

            CBUFFER_START(UnityPerMaterial)
                float _TexScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;       // Fallback vertex color (weighted blend)
                float2 uv0        : TEXCOORD0;   // World-space XZ for texture sampling
                float4 uv1        : TEXCOORD1;   // Blend weights: x=grass, y=dirt, z=stone, w=sand
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 color       : COLOR;
                float3 normalWS    : TEXCOORD0;
                float2 texCoord    : TEXCOORD1;   // Scaled world-space UV
                float4 blendWeight : TEXCOORD2;   // Terrain type blend weights
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS  = TransformObjectToHClip(input.positionOS.xyz);
                output.color       = input.color;
                output.normalWS    = TransformObjectToWorldNormal(input.normalOS);
                output.texCoord    = input.uv0 * _TexScale;
                output.blendWeight = input.uv1;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample all 4 terrain textures at world-space UV
                float2 uv = input.texCoord;
                half3 grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uv).rgb;
                half3 dirt  = SAMPLE_TEXTURE2D(_DirtTex,  sampler_DirtTex,  uv).rgb;
                half3 stone = SAMPLE_TEXTURE2D(_StoneTex, sampler_StoneTex, uv).rgb;
                half3 sand  = SAMPLE_TEXTURE2D(_SandTex,  sampler_SandTex,  uv).rgb;

                // Blend textures using per-vertex weights
                float4 w = input.blendWeight;
                half3 texColor = grass * w.x + dirt * w.y + stone * w.z + sand * w.w;

                // If all weights are zero (e.g. water submesh), use vertex color as fallback
                float totalWeight = w.x + w.y + w.z + w.w;
                half3 baseColor = totalWeight > 0.01 ? texColor : input.color.rgb;

                // Simple directional lighting (matches VertexColorOpaque)
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));

                float3 ambient = 0.3;
                float3 diffuse = NdotL * mainLight.color.rgb * 0.7;
                float3 finalColor = baseColor * (ambient + diffuse);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass so terrain casts shadows
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
