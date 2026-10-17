// Full-screen dim with a soft elliptical hole, for the tutorial spotlight. The hole is given in
// the image's UV space (centre xy, radius zw) so it works the same on every platform.
Shader "MergeResto/UI/Spotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (0.2, 0.1, 0.08, 0.5)
        _Hole ("Hole (centre xy, radius zw, UV)", Vector) = (0.5, 0.5, 0.05, 0.05)
        _Softness ("Edge softness (UV)", Float) = 0.01
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _Hole;
            float _Softness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = (i.uv - _Hole.xy) / max(_Hole.zw, 1e-4);
                float dist = length(d);
                // Distance past the rim, in UV, roughly: scale by the smaller radius. A zero-size
                // hole dims everything.
                float edge = (dist - 1.0) * max(min(_Hole.z, _Hole.w), 1e-4);
                float a = smoothstep(0.0, _Softness, edge);
                fixed4 c = i.color;
                c.a *= a;
                return c;
            }
            ENDCG
        }
    }
}
