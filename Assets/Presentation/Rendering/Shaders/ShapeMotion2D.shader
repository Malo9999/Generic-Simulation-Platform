Shader "Presentation/ShapeMotion2D"
{
    Properties
    {
        [MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color("Tint", Color) = (1,1,1,1)
        _BaseColor("Base Color", Color) = (1,1,1,1)
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle] _AlphaClip("Alpha Clip", Float) = 0

        [Header(Motion)]
        _MotionEnabled("Motion Enabled", Float) = 0
        _MotionMode("Motion Mode", Float) = 0
        _MotionTime("Motion Time", Float) = 0
        _MotionPhase("Motion Phase", Float) = 0
        _MotionAmplitude("Motion Amplitude", Float) = 0.01
        _MotionFrequency("Motion Frequency", Float) = 1
        _MotionSecondaryAmplitude("Motion Secondary Amplitude", Float) = 0.005
        _MotionSecondaryFrequency("Motion Secondary Frequency", Float) = 0.5
        _MotionFlowDir("Motion Flow Direction", Vector) = (1,0,0,0)
        _MotionTintStrength("Motion Tint Strength", Range(0,1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 centeredUv : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _BaseColor;
                half4 _EmissionColor;
                float _Cutoff;
                float _AlphaClip;
                float _MotionEnabled;
                float _MotionMode;
                float _MotionTime;
                float _MotionPhase;
                float _MotionAmplitude;
                float _MotionFrequency;
                float _MotionSecondaryAmplitude;
                float _MotionSecondaryFrequency;
                float4 _MotionFlowDir;
                float _MotionTintStrength;
            CBUFFER_END

            float2 Rotate2D(float2 value, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2((value.x * c) - (value.y * s), (value.x * s) + (value.y * c));
            }

            float2 ApplyMotion(float2 uv, float2 centeredUv)
            {
                if (_MotionEnabled < 0.5)
                {
                    return uv;
                }

                float tPrimary = (_MotionTime * max(0.01, _MotionFrequency)) + _MotionPhase;
                float tSecondary = (_MotionTime * max(0.01, _MotionSecondaryFrequency)) + (_MotionPhase * 1.73);
                float amp = _MotionAmplitude;
                float ampSecondary = _MotionSecondaryAmplitude;
                float2 offset = 0.0;

                if (_MotionMode < 1.5)
                {
                    // organic_amoeba
                    float radius = length(centeredUv);
                    float radial = sin((radius * 10.0) - tPrimary) * amp;
                    float breathe = sin(tSecondary * 0.83) * ampSecondary;
                    offset = normalize(centeredUv + 1e-5) * (radial + breathe);
                }
                else if (_MotionMode < 2.5)
                {
                    // organic_metaball
                    float driftA = sin((centeredUv.y * 8.0) + tPrimary) * amp;
                    float driftB = cos((centeredUv.x * 6.0) - tSecondary) * ampSecondary;
                    offset = float2(driftA, driftB);
                }
                else if (_MotionMode < 3.5)
                {
                    // field_blob
                    float ripple = sin((centeredUv.x + centeredUv.y) * 7.0 + tPrimary) * amp;
                    float calm = cos((centeredUv.x - centeredUv.y) * 4.0 - tSecondary) * ampSecondary;
                    offset = float2(ripple, calm) * 0.8;
                }
                else
                {
                    // filament
                    float2 flow = normalize(_MotionFlowDir.xy + float2(1e-5, 1e-5));
                    float along = dot(centeredUv, flow);
                    float across = dot(centeredUv, float2(-flow.y, flow.x));
                    float flowShift = sin((along * 10.0) - tPrimary) * amp;
                    float widthWobble = sin((across * 20.0) + tSecondary) * ampSecondary;
                    offset = (flow * flowShift) + (float2(-flow.y, flow.x) * widthWobble);
                }

                return uv + offset;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.centeredUv = IN.uv - 0.5;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = ApplyMotion(IN.uv, IN.centeredUv);
                half4 sampleColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half4 tinted = sampleColor * IN.color * _Color * _BaseColor;

                if (_AlphaClip > 0.5)
                {
                    clip(tinted.a - _Cutoff);
                }

                half motionPulse = 0.5h + (0.5h * sin((_MotionTime * max(0.1, _MotionFrequency)) + _MotionPhase));
                half3 emission = _EmissionColor.rgb + (_EmissionColor.rgb * motionPulse * _MotionTintStrength * _MotionEnabled);
                return half4(tinted.rgb + emission, tinted.a);
            }
            ENDHLSL
        }
    }
}
