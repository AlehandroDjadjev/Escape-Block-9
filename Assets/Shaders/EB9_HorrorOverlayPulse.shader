Shader "EscapeBlock9/HorrorOverlayPulse"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.16, 0.0, 0.015, 0.62)
        _PulseSpeed ("Pulse Speed", Float) = 0.85
        _NoiseScale ("Noise Scale", Float) = 18
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            float _PulseSpeed;
            float _NoiseScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                world.y += sin((world.x + world.z + _Time.y * 0.35) * 3.1) * 0.012;
                o.vertex = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.uv = v.uv;
                o.world = world;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float n = hash21(floor((i.world.xz + i.uv * 2.0) * _NoiseScale));
                float streak = smoothstep(0.62, 1.0, sin((i.uv.y * 24.0) + n * 8.0));
                float pulse = 0.72 + 0.28 * sin(_Time.y * _PulseSpeed + n * 6.2831);
                fixed4 col = _BaseColor;
                col.rgb *= 0.42 + pulse * 0.72 + streak * 0.22;
                col.a *= saturate(0.35 + n * 0.45 + streak * 0.25);
                return col;
            }
            ENDCG
        }
    }
}
