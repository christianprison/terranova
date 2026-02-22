// Minimal URP-compatible shader that renders vertex colors.
// Used for solid terrain blocks (grass, dirt, stone, sand).
// No textures needed – colors come from mesh vertex data.
Shader "Terranova/VertexColorOpaque"
{
    Properties
    {
        // No properties needed – everything comes from vertex colors
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

            // Include URP core functions (object-to-clip transforms, etc.)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;   // Object-space position
                float3 normalOS   : NORMAL;     // Object-space normal
                float4 color      : COLOR;      // Vertex color (our block color)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;  // Clip-space position
                float4 color      : COLOR;         // Passed to fragment shader
                float3 normalWS   : TEXCOORD0;    // World-space normal for lighting
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Simple directional lighting using the main light
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));

                // Combine vertex color with basic lighting (ambient + diffuse)
                float3 ambient = 0.3;
                float3 diffuse = NdotL * mainLight.color.rgb * 0.7;
                float3 finalColor = input.color.rgb * (ambient + diffuse);

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

            // Need both Core and Lighting for shadow bias + light direction
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, GetMainLight().direction));
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
