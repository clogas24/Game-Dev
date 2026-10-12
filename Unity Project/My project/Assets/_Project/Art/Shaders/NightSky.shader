// Halloween night sky for RenderSettings.skybox: a three-colour gradient, a few twinkling
// stars and a moon disc with a soft halo. Keep the fog colour close to _HorizonColor so
// distant ground fades into the sky.
Shader "LanternKeeper/NightSky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.015, 0.02, 0.07, 1)
        _HorizonColor ("Horizon", Color) = (0.11, 0.07, 0.2, 1)
        _BottomColor ("Below Horizon", Color) = (0.01, 0.01, 0.025, 1)
        _HorizonPower ("Gradient Curve", Range(0.1, 4)) = 0.6
        _StarDensity ("Star Density", Range(0, 0.2)) = 0.045
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.3
        _MoonDirection ("Moon Direction (xyz)", Vector) = (0.35, 0.55, 0.75, 0)
        [HDR] _MoonColor ("Moon Color", Color) = (1.6, 1.55, 1.3, 1)
        _MoonSize ("Moon Size", Range(0.005, 0.2)) = 0.05
        _MoonHalo ("Moon Halo", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _MoonColor;
                float4 _MoonDirection;
                half _HorizonPower;
                half _StarDensity;
                half _StarBrightness;
                half _MoonSize;
                half _MoonHalo;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);

                half up = pow(saturate(d.y), _HorizonPower);
                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                color = lerp(color, _BottomColor.rgb, saturate(-d.y * 5.0));

                // Stars: one candidate per cell of a grid laid through the sky sphere.
                float3 p = d * 70.0;
                float3 cell = floor(p);
                float3 local = frac(p) - 0.5;
                float h = Hash(cell);
                half star = step(1.0 - _StarDensity, h) * smoothstep(0.22, 0.0, length(local));
                half twinkle = 0.7 + 0.3 * sin(_Time.y * (1.0 + h * 3.0) + h * 40.0);
                color += star * twinkle * _StarBrightness * saturate(d.y * 4.0) * half3(0.85, 0.9, 1.0);

                // towardMoon is the cosine of the angle to the moon, so the disc is a threshold close to 1.
                float towardMoon = dot(d, normalize(_MoonDirection.xyz));
                half disc = smoothstep(1.0 - _MoonSize * 0.02, 1.0 - _MoonSize * 0.016, towardMoon);
                half halo = pow(saturate(towardMoon), 40.0) * _MoonHalo;
                color += _MoonColor.rgb * disc + _MoonColor.rgb * halo * 0.12;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
