Shader "EscapeBlock9/HorrorVeinGlow"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.7, 0.02, 0.0, 0.55)
        _PulseSpeed ("Pulse Speed", Float) = 1.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
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

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float twitch = sin((_Time.y * 3.0 + world.x * 1.7 + world.z * 2.3)) * 0.008;
                world.xz += twitch;
                o.vertex = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.uv = v.uv;
                o.world = world;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float center = 1.0 - abs(i.uv.y - 0.5) * 2.0;
                float pulse = 0.55 + 0.45 * sin(_Time.y * _PulseSpeed + i.world.x * 0.8 + i.world.z * 0.6);
                fixed4 col = _BaseColor;
                col.rgb *= 0.65 + pulse * 0.9;
                col.a *= saturate(center * center * (0.55 + pulse * 0.45));
                return col;
            }
            ENDCG
        }
    }
}
