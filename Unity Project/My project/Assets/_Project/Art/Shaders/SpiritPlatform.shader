// Spirit platforms: an unlit, see-through cyan slab with a bright rim, so the edge you can
// stand on is the brightest thing on screen and the bloom picks it up.
// SpiritPlatform.cs fades the platform by writing _BaseColor (alpha) through a
// MaterialPropertyBlock, so that property name and its alpha must keep meaning "how visible".
// The rim is measured in metres from the nearest edge of the cube face, so platforms of
// different sizes all get the same rim width.
Shader "LanternKeeper/SpiritPlatform"
{
    Properties
    {
        _BaseColor ("Base Color (alpha fades the platform)", Color) = (0.3, 0.95, 1, 0.85)
        _FillAlpha ("Fill Opacity", Range(0, 1)) = 0.5
        [HDR] _FillGlow ("Fill Glow", Color) = (0.03, 0.22, 0.28, 1)
        [HDR] _EdgeColor ("Edge Glow", Color) = (0.7, 2.6, 3.0, 1)
        _EdgeWidth ("Edge Width (m)", Range(0.01, 0.5)) = 0.1
        _PulseSpeed ("Pulse Speed", Float) = 0.7
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SpiritPlatform"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FillGlow;
                half4 _EdgeColor;
                half _FillAlpha;
                half _EdgeWidth;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float3 scale : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                // World size of one object-space unit along each axis.
                float4x4 m = GetObjectToWorldMatrix();
                output.scale = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Distance in metres to the nearest edge of this face of the unit cube. The axis
                // the face looks along is pushed out of the way.
                float3 toEdge = (0.5 - abs(input.positionOS)) * input.scale;
                toEdge += abs(input.normalOS) * 1000.0;
                float edgeDistance = min(toEdge.x, min(toEdge.y, toEdge.z));
                half edge = 1.0 - smoothstep(0.0, _EdgeWidth, edgeDistance);

                half pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2832 + (input.positionWS.x + input.positionWS.z) * 0.8);
                half3 color = _BaseColor.rgb * 0.55 + _FillGlow.rgb + _EdgeColor.rgb * edge * pulse;
                half alpha = _BaseColor.a * saturate(_FillAlpha + edge);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
