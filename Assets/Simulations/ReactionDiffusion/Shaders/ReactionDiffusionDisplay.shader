Shader "GSP/ReactionDiffusion/Display"
{
    Properties
    {
        _StateTex("State Texture", 2D) = "black" {}
        _DisplayMode("Display Mode", Float) = 0
        _TextureSize("Texture Size", Vector) = (256,256,0.00390625,0.00390625)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _StateTex;
            float4 _StateTex_ST;
            float _DisplayMode;
            float4 _TextureSize;

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
                o.uv = TRANSFORM_TEX(v.uv, _StateTex);
                return o;
            }

            float2 SnapUvToTexel(float2 uv)
            {
                float2 texSize = max(_TextureSize.xy, float2(1.0, 1.0));
                float2 pixel = floor(uv * texSize);
                return (pixel + 0.5) / texSize;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 snappedUv = SnapUvToTexel(i.uv);
                float2 ab = tex2D(_StateTex, snappedUv).xy;
                float value = _DisplayMode < 0.5 ? ab.y : saturate(ab.x - ab.y);
                return fixed4(value, value, value, 1.0);
            }
            ENDCG
        }
    }
}