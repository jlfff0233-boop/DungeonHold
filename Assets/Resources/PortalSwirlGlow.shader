Shader "UI/Portal Swirl Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;

            struct Vertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            Varyings Vert(Vertex input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.localPosition = input.position.xy;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float DenseAlpha(float2 uv)
            {
                return smoothstep(0.55, 0.88, tex2D(_MainTex, uv).a);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, input.uv);
                float2 radius = _MainTex_TexelSize.xy * 3.5;
                float nearby = 0.0;
                nearby += DenseAlpha(input.uv + float2(radius.x, 0));
                nearby += DenseAlpha(input.uv - float2(radius.x, 0));
                nearby += DenseAlpha(input.uv + float2(0, radius.y));
                nearby += DenseAlpha(input.uv - float2(0, radius.y));
                nearby += DenseAlpha(input.uv + radius * 0.7071);
                nearby += DenseAlpha(input.uv - radius * 0.7071);
                nearby += DenseAlpha(input.uv + radius * float2(0.7071, -0.7071));
                nearby += DenseAlpha(input.uv + radius * float2(-0.7071, 0.7071));

                float body = sprite.a * smoothstep(0.18, 0.65, sprite.a);
                float glow = nearby * 0.11 * (1.0 - body);
                fixed4 result = fixed4(sprite.rgb * input.color.rgb * body
                    + float3(0.66, 0.34, 1.0) * glow, body + glow);
                result *= input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                result *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
