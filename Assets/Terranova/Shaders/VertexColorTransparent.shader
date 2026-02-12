// Minimal URP-compatible shader for transparent vertex-colored geometry.
// Used for water surfaces. Same as VertexColorOpaque but with alpha blending.
Shader "Terranova/VertexColorTransparent"
{
    Properties
    {
        // No properties – vertex colors provide everything including alpha
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Alpha blending: source color × source alpha + dest color × (1 - source alpha)
            Blend SrcAlpha OneMinusSrcAlpha

            Cull Back
            ZWrite Off       // Transparent objects don't write to depth buffer
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float3 normalWS   : TEXCOORD0;
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
                // Basic lighting for water
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));

                float3 ambient = 0.4;  // Water gets more ambient (brighter reflections)
                float3 diffuse = NdotL * mainLight.color.rgb * 0.6;
                float3 finalColor = input.color.rgb * (ambient + diffuse);

                // Preserve vertex alpha for transparency
                return half4(finalColor, input.color.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
