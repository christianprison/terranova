// Fog of War shader with gradient edges and animated noise boundary.
// Vertex colors control fog density (alpha). Edge cells get animated
// noise for a mysterious, alive-feeling boundary.
Shader "Terranova/FogOfWar"
{
    Properties
    {
        _FogColor    ("Fog Color", Color) = (0.03, 0.04, 0.03, 0.85)
        _NoiseScale  ("Noise Scale", Float) = 8.0
        _NoiseSpeed  ("Noise Speed", Float) = 0.3
        _NoiseAmount ("Noise Amount", Range(0, 0.3)) = 0.15
        _EdgeSoftness("Edge Softness", Range(0.01, 0.5)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
        }

        Pass
        {
            Name "FogOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _NoiseAmount;
                float  _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;      // Alpha = fog density from FogOfWar.cs
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float3 positionWS : TEXCOORD0;
            };

            // 2D value noise
            float hash2D(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // Smoothstep

                float a = hash2D(i);
                float b = hash2D(i + float2(1, 0));
                float c = hash2D(i + float2(0, 1));
                float d = hash2D(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                v += 0.5 * valueNoise(p); p *= 2.01;
                v += 0.25 * valueNoise(p); p *= 2.03;
                v += 0.125 * valueNoise(p);
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float baseFogAlpha = input.color.a;

                // Fully explored areas: no fog at all
                if (baseFogAlpha < 0.02)
                    discard;

                // Animated noise at fog boundary (edges where alpha transitions)
                float2 noiseUV = input.positionWS.xz * (1.0 / _NoiseScale);
                noiseUV += float2(_Time.y * _NoiseSpeed * 0.3, _Time.y * _NoiseSpeed * 0.2);
                float noise = fbm(noiseUV);

                // Stronger noise effect at edges (partial alpha), less in solid fog
                float edgeFactor = 1.0 - abs(baseFogAlpha * 2.0 - 1.0); // Peaks at 0.5 alpha
                float noiseOffset = (noise - 0.5) * _NoiseAmount * edgeFactor * 3.0;

                // Soft gradient at fog boundary
                float alpha = baseFogAlpha + noiseOffset;
                alpha = smoothstep(0.0, _EdgeSoftness, alpha);
                alpha = saturate(alpha) * _FogColor.a;

                // Slight color variation in fog (darker patches)
                float3 fogCol = _FogColor.rgb;
                fogCol += (noise - 0.5) * 0.02;

                return half4(fogCol, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
