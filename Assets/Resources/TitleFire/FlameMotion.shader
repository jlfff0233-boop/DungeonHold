Shader "UI/TitleFlameMotion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Flame", 2D) = "white" {}
        _Clock ("Unscaled time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        // Screen compositing keeps the photographed black background invisible.
        Blend One OneMinusSrcColor
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _Clock;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float strength = smoothstep(.24, .90, uv.y);
                uv.x += strength * (sin(uv.y * 15 - _Clock * 3.7) * .006
                    + sin(uv.y * 29 - _Clock * 6.1) * .003);
                uv.y += strength * sin(uv.x * 20 + _Clock * 4.3) * .005;
                fixed4 color = tex2D(_MainTex, uv) * i.color;
                // Fade all crop edges to avoid a moving rectangular outline.
                float edge = smoothstep(.36, .375, uv.x) * (1 - smoothstep(.615, .63, uv.x));
                edge *= smoothstep(.24, .252, uv.y) * (1 - smoothstep(.94, .96, uv.y));
                color.rgb *= color.a * edge;
                return color;
            }
            ENDCG
        }
    }
}
