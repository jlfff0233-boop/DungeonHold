Shader "UI/TitleRisingMist"
{
    Properties { _Clock ("Unscaled time", Float) = 0 }
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
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            float _Clock;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(hash(cell), hash(cell+float2(1,0)), f.x),
                    lerp(hash(cell+float2(0,1)), hash(cell+1), f.x), f.y);
            }
            float cloud(float2 p)
            {
                return noise(p)*.55 + noise(p*2.03+7)*.28 + noise(p*4.07+19)*.17;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv = i.uv;
                // Match the painted violet haze along the library floor and distant arches.
                float2 basin = (uv-float2(.51,.315))/float2(.31,.095);
                float2 farMist = (uv-float2(.29,.36))/float2(.14,.14);
                float mask = max(exp(-dot(basin,basin)*1.5), exp(-dot(farMist,farMist)*2)*.55);
                mask *= smoothstep(.17,.29,uv.x)*(1-smoothstep(.74,.83,uv.x));
                mask *= 1-smoothstep(.46,.56,uv.y);
                float2 p = uv*float2(9,14)-float2(_Clock*.012,_Clock*.026);
                p.x += sin(uv.y*14-_Clock*.23)*.24;
                float density = smoothstep(.30,.77,cloud(p));
                float wisps = cloud(p*1.8+float2(_Clock*.012,0));
                float edge = smoothstep(0,.05,uv.x)*(1-smoothstep(.96,1,uv.x));
                float alpha = mask*density*(.12+wisps*.12)*edge;
                return fixed4(.53,.36,.72,alpha);
            }
            ENDCG
        }
    }
}
