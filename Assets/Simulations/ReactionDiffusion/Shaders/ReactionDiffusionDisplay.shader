Shader "GSP/ReactionDiffusion/Display"
{
    Properties
    {
        _StateTex("State Texture", 2D) = "black" {}
        _DisplayMode("Display Mode", Float) = 0
        _ColorMode("Color Mode", Float) = 1
        _OverlayPulseGlow("Overlay Pulse Glow", Float) = 1.4
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _ReseedColor("Reseed Color", Color) = (1,0.2,0.1,1)
        _HotspotColor("Hotspot Color", Color) = (1,0.7,0.2,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_PULSES 8

            sampler2D _StateTex;
            float4 _StateTex_TexelSize;
            float _DisplayMode;
            float _ColorMode;
            float _OverlayPulseGlow;
            fixed4 _BaseColor;
            fixed4 _ReseedColor;
            fixed4 _HotspotColor;

            float4 _PulseCenters[MAX_PULSES];
            float _PulseAges[MAX_PULSES];
            float _PulseRadii[MAX_PULSES];
            float _PulseStrengths[MAX_PULSES];

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float GetSignal(float2 uv)
            {
                float2 ab = tex2D(_StateTex, uv).xy;
                return (_DisplayMode < 0.5) ? ab.y : abs(ab.x - ab.y);
            }

            float GetPulseOverlay(float2 uv)
            {
                float overlay = 0.0;
                for (int i = 0; i < MAX_PULSES; i++)
                {
                    float age = _PulseAges[i];
                    if (age <= 0.0001)
                    {
                        continue;
                    }

                    float2 center = _PulseCenters[i].xy;
                    float radius = _PulseRadii[i];
                    float strength = _PulseStrengths[i];
                    float dist = distance(uv, center);
                    float local = 1.0 - smoothstep(0.0, radius, dist);
                    local *= age * strength;
                    overlay = max(overlay, local);
                }

                return saturate(overlay);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 t = _StateTex_TexelSize.xy;

                float c = GetSignal(i.uv);
                float l = GetSignal(i.uv + float2(-t.x, 0.0));
                float r = GetSignal(i.uv + float2( t.x, 0.0));
                float d = GetSignal(i.uv + float2(0.0, -t.y));
                float u = GetSignal(i.uv + float2(0.0,  t.y));

                float edge = abs(r - l) + abs(u - d);
                edge = saturate(edge * 8.0);
                edge = pow(edge, 0.85);

                float baseGlow = saturate(c * 1.15);
                baseGlow = pow(baseGlow, 1.6) * 0.35;

                float baseValue = saturate(max(edge, baseGlow));
                float pulse = saturate(GetPulseOverlay(i.uv) * _OverlayPulseGlow);
                float hotspot = saturate(edge * pulse * 1.6);

                fixed3 color = _BaseColor.rgb * baseValue;

                // Make reseeds read as real intrusions, not tiny sparks.
                if (_ColorMode > 0.5)
                {
                    float reseedMask = saturate(pulse * 1.8);
                    color = lerp(color, _ReseedColor.rgb, reseedMask);
                }

                // Use hotspot as a brighter conflict accent.
                if (_ColorMode > 1.5)
                {
                    float hotspotMask = saturate(hotspot * 1.5);
                    color = lerp(color, _HotspotColor.rgb, hotspotMask);
                }

                return fixed4(saturate(color), 1.0);
            }
            ENDCG
        }
    }
}