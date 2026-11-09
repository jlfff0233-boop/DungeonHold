Shader "UI/TitleFlameTipMotion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Region ("Crop", Vector) = (0,0,1,1)
        _Clock ("Unscaled time", Float) = 0
        _Sway ("Subtle sway", Float) = 1
        _Twinkle ("Twinkle artwork stars", Float) = 0
        _StarMask ("Painted star masks and phases", 2D) = "black" {}
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
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            sampler2D _StarMask;
            float4 _MainTex_TexelSize;
            float4 _Region;
            float _Clock;
            float _Sway;
            float _Twinkle;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 local = saturate((i.uv - _Region.xy) / _Region.zw);
                fixed4 original = tex2D(_MainTex, i.uv);
                if (_Twinkle > .5)
                {
                    float4 star = tex2D(_StarMask, local);
                    if (star.a > .001)
                    {
                        // Hold a genuinely dark interval, then fade on and off independently.
                        // A slow 4.8–6.7 second cycle, with long holds and gentle fades.
                        float cycle = frac(_Clock * (.15 + star.g * .06) + star.r);
                        float visible = smoothstep(.12, .32, cycle) * (1 - smoothstep(.72, .94, cycle));
                        float2 offset = _MainTex_TexelSize.xy * 14;
                        float3 background = (
                            tex2D(_MainTex, i.uv + float2(offset.x, 0)).rgb
                            + tex2D(_MainTex, i.uv - float2(offset.x, 0)).rgb
                            + tex2D(_MainTex, i.uv + float2(0, offset.y)).rgb
                            + tex2D(_MainTex, i.uv - float2(0, offset.y)).rgb
                            + tex2D(_MainTex, i.uv + offset).rgb
                            + tex2D(_MainTex, i.uv - offset).rgb
                            + tex2D(_MainTex, i.uv + float2(offset.x, -offset.y)).rgb
                            + tex2D(_MainTex, i.uv + float2(-offset.x, offset.y)).rgb) * .125;
                        // Replace the baked-in star with nearby cavern color while switched off.
                        float3 lit = original.rgb + max(original.rgb - background, 0) * .25;
                        original.rgb = lerp(original.rgb, lerp(background, lit, visible), star.a);
                    }
                    return original * i.color;
                }
                // Zero displacement at every boundary keeps the original artwork seamless.
                float envelope = sin(local.x * 3.14159265) * sin(local.y * 3.14159265);
                envelope *= smoothstep(.08, .40, local.y);
                float sway = sin(_Clock * 1.7 + local.y * 2.5) * .030
                    + sin(_Clock * 2.9 - local.y * 4) * .009;
                float2 uv = i.uv;
                // Warm pixels alone move: stone, smoke and torch baskets stay fixed.
                float warm = smoothstep(.12, .4, original.r - original.b) * smoothstep(.4, .8, original.r);
                uv.x += sway * envelope * _Region.z * _Sway * warm;
                return tex2D(_MainTex, uv) * i.color;
            }
            ENDCG
        }
    }
}
