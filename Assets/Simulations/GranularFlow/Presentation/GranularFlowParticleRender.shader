Shader "Simulations/GranularFlow/Particles"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Particle
            {
                float2 position;
                float2 velocity;
                uint colorId;
                float radius;
            };

            StructuredBuffer<Particle> _Particles;
            float4 _Palette[16];
            int _PaletteCount;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                Particle p = _Particles[v.instanceID];
                float2 world = p.position + v.vertex.xy * (p.radius * 2.0);
                o.vertex = UnityObjectToClipPos(float4(world, 0, 1));
                o.uv = v.uv * 2.0 - 1.0;
                uint idx = p.colorId % max(1, _PaletteCount);
                o.color = _Palette[idx];
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = dot(i.uv, i.uv);
                clip(1.0 - d);
                float edge = smoothstep(1.0, 0.75, d);
                return fixed4(i.color.rgb * edge, 1);
            }
            ENDCG
        }
    }
}
