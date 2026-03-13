Shader "GSP/ReactionDiffusion/Display"
{
    Properties
    {
        _StateTex("State Texture", 2D) = "black" {}
        _DisplayMode("Display Mode", Float) = 0
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

            sampler2D _StateTex;
            float4 _StateTex_TexelSize;
            float _DisplayMode;

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

                float baseGlow = saturate(c * 1.1);
                baseGlow = pow(baseGlow, 1.8) * 0.18;

                float ridge = saturate(max(edge, baseGlow));

                float3 background = float3(0.98, 0.98, 0.99);
                float3 lineColor = float3(0.12, 0.12, 0.14);

                float3 color = lerp(background, lineColor, ridge);
                return float4(color, 1.0);
            }
            ENDCG
        }
    }
}