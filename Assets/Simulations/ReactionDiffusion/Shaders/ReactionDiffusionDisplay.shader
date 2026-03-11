Shader "GSP/ReactionDiffusion/Display"
{
    Properties
    {
        _StateTex("State Texture", 2D) = "black" {}
        _DisplayMode("Display Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
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

            float sampleB(float2 uv)
            {
                float2 ab = tex2D(_StateTex, uv).xy;

                if (_DisplayMode < 0.5)
                    return ab.y;

                return abs(ab.x - ab.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 t = _StateTex_TexelSize.xy;

                float c = sampleB(i.uv);

                float l = sampleB(i.uv + float2(-t.x,0));
                float r = sampleB(i.uv + float2(t.x,0));
                float u = sampleB(i.uv + float2(0,t.y));
                float d = sampleB(i.uv + float2(0,-t.y));

                float edge = abs(r-l) + abs(u-d);

                edge *= 8.0;
                edge = saturate(edge);

                float3 background = float3(0.95,0.97,0.99);
                float3 pattern = float3(0.05,0.08,0.12);

                float3 color = lerp(background, pattern, edge);

                return float4(color,1);
            }

            ENDCG
        }
    }
}