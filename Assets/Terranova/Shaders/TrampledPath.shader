// Ground-level decal shader for trampled paths.
// Soft edges that blend gradually into surrounding terrain.
// Center is more visible, edges fade. Worn-dirt appearance.
Shader "Terranova/TrampledPath"
{
    Properties
    {
        _BaseColor    ("Color", Color) = (0.52, 0.40, 0.25, 0.5)
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.3
        _WearAmount   ("Wear Amount", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Name "PathForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1   // Slight depth offset to prevent z-fighting with terrain

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _EdgeSoftness;
                float  _WearAmount;
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

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Radial falloff from quad center (soft edges)
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0; // 0 at center, 1 at edge
                float edgeMask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, dist);

                // Procedural dirt texture variation
                float noise = hash(floor(input.positionWS.xz * 4.0));
                float dirtPattern = lerp(0.85, 1.15, noise);

                // Wear pattern — center is more compacted/lighter
                float centerWear = smoothstep(0.6, 0.0, dist);
                float3 dirtColor = _BaseColor.rgb;
                dirtColor = lerp(dirtColor, dirtColor * 1.2, centerWear * _WearAmount);

                // Lighting
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = dirtColor * 0.4;
                float3 diffuse = dirtColor * NdotL * mainLight.color.rgb * 0.6;

                float3 finalColor = (ambient + diffuse) * dirtPattern;
                float alpha = edgeMask * _BaseColor.a;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
