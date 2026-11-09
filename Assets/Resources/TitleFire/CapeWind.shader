Shader "UI/TitleCapeWind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Clock ("Unscaled time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float _Clock;
            // Deliberately inset within the cloth: no silhouette, hair, armor or stone.
            static const float2 CapeOutline[8] = {
                float2(.224,.585), float2(.215,.440), float2(.205,.386),
                float2(.151,.415), float2(.126,.450), float2(.100,.478),
                float2(.166,.521), float2(.195,.565)
            };
            float clothMask(float2 uv)
            {
                float distanceSquared = 1;
                float inside = 0;
                [unroll]
                for (int n = 0; n < 8; n++)
                {
                    float2 a = CapeOutline[n];
                    float2 b = CapeOutline[(n + 1) % 8];
                    float2 edge = b - a;
                    float2 delta = uv - a;
                    float2 nearest = delta - edge * saturate(dot(delta,edge)/dot(edge,edge));
                    distanceSquared = min(distanceSquared,dot(nearest,nearest));
                    if ((a.y > uv.y) != (b.y > uv.y))
                    {
                        float crossing = a.x + (uv.y-a.y)*edge.x/edge.y;
                        if (uv.x < crossing) inside = 1-inside;
                    }
                }
                float3 color = tex2D(_MainTex,uv).rgb;
                float blue = smoothstep(.012,.055,color.b-color.r)
                    * smoothstep(.002,.025,color.b-color.g);
                // Fade motion inside the outline, leaving the silhouette and background fixed.
                return inside * smoothstep(.0025,.008,sqrt(distanceSquared)) * blue;
            }
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv = i.uv;
                if (uv.x < .100 || uv.x > .224 || uv.y < .386 || uv.y > .585)
                    return fixed4(0,0,0,0);
                // Pin the shoulders; animate only folds safely inside the cloth outline.
                float down = saturate((.655-uv.y)/.31);
                float mask = clothMask(uv);
                if (mask <= .001) return fixed4(0,0,0,0);
                float weight = mask*pow(down,1.2);
                // Gentle 6-9 second waves within the fabric.
                float wave = sin(down*5-_Clock*.72)*.007+sin(down*9-_Clock*1.03)*.002;
                uv.x += wave*weight;
                uv.y += sin(down*7-_Clock*.72+uv.x*20)*.003*weight;
                // Never sample stone, skin or armor into the moving cloth.
                float sourceMask = clothMask(uv);
                fixed4 cloth = tex2D(_MainTex,uv);
                cloth.a *= mask*sourceMask;
                return cloth*i.color;
            }
            ENDCG
        }
    }
}
