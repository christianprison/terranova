// Enhanced water shader with scrolling ripples, depth-based color,
// Fresnel reflections, and vertex wave displacement.
Shader "Terranova/WaterSurface"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.3, 0.65, 0.85, 0.6)
        _DeepColor    ("Deep Color", Color) = (0.08, 0.25, 0.55, 0.85)
        _WaveSpeed    ("Wave Speed", Float) = 0.8
        _WaveHeight   ("Wave Height", Float) = 0.04
        _RippleScale  ("Ripple Scale", Float) = 6.0
        _RippleSpeed  ("Ripple Speed", Float) = 1.2
        _FresnelPower ("Fresnel Power", Float) = 3.0
        _ReflectColor ("Reflect Tint", Color) = (0.7, 0.85, 1.0, 1.0)
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
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _WaveSpeed;
                float  _WaveHeight;
                float  _RippleScale;
                float  _RippleSpeed;
                float  _FresnelPower;
                float4 _ReflectColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;  // Vertex color for edge/depth encoding
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 color      : COLOR;
            };

            // Simple 2D hash
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Wave vertex displacement
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float wave1 = sin(worldPos.x * 1.5 + _Time.y * _WaveSpeed) * _WaveHeight;
                float wave2 = sin(worldPos.z * 2.0 + _Time.y * _WaveSpeed * 0.7) * _WaveHeight * 0.6;
                float wave3 = sin((worldPos.x + worldPos.z) * 0.8 + _Time.y * _WaveSpeed * 1.3) * _WaveHeight * 0.3;
                input.positionOS.y += wave1 + wave2 + wave3;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();

                // Scrolling ripple normals (perturb the surface normal)
                float2 uv1 = input.positionWS.xz * _RippleScale + float2(_Time.y * _RippleSpeed, 0);
                float2 uv2 = input.positionWS.xz * _RippleScale * 0.7 + float2(0, _Time.y * _RippleSpeed * 0.8);
                float ripple1 = noise2D(uv1);
                float ripple2 = noise2D(uv2);
                float ripple = (ripple1 + ripple2) * 0.5;

                // Perturb normal with ripple
                float3 perturbedNormal = normalize(normalWS + float3(
                    (ripple1 - 0.5) * 0.3,
                    0,
                    (ripple2 - 0.5) * 0.3
                ));

                // Depth-based color (use vertex alpha as depth proxy)
                float depth = input.color.a;
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);

                // Fresnel reflection
                float fresnel = pow(1.0 - saturate(dot(perturbedNormal, input.viewDirWS)), _FresnelPower);
                float3 reflectTint = _ReflectColor.rgb * fresnel * 0.5;

                // Specular highlight (sun reflection on water)
                float3 halfDir = normalize(mainLight.direction + input.viewDirWS);
                float specular = pow(saturate(dot(perturbedNormal, halfDir)), 128.0) * 0.8;
                float3 specColor = mainLight.color.rgb * specular;

                // Lighting
                float NdotL = saturate(dot(perturbedNormal, mainLight.direction));
                float3 ambient = waterColor * 0.4;
                float3 diffuse = waterColor * NdotL * mainLight.color.rgb * 0.6;

                // Ripple highlights (bright spots on wave crests)
                float rippleHighlight = smoothstep(0.55, 0.75, ripple) * 0.15;

                float3 finalColor = ambient + diffuse + reflectTint + specColor + rippleHighlight;

                // Alpha: blend shallow edges with terrain, more opaque in center
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, depth);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Terranova/VertexColorTransparent"
}
