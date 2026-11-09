Shader "UI/TitlePortalCloudMotion"
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
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float _Clock;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            float purple(float3 c)
            {
                return smoothstep(.045,.13,c.r-c.g)*smoothstep(.10,.25,c.b-c.g);
            }
            float sky(float3 c)
            {
                return smoothstep(.10,.23,c.b-c.r)*smoothstep(.36,.53,c.b);
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 a=floor(p), f=frac(p);
                // Linear interpolation retains broad planar cloud shapes.
                return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),
                    lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                fixed4 color=tex2D(_MainTex,uv);
                float2 center=float2(.866,.514);
                float2 radius=float2(.063,.151);
                float2 p=(uv-center)/radius;
                float r=length(p);
                float mask=(1-smoothstep(.67,.94,r))*purple(color.rgb);
                if(mask>.001)
                {
                    float angle=_Clock*.31*(1-smoothstep(.15,1,r))+.05*sin(_Clock*1.6-r*9);
                    float s=sin(angle), c=cos(angle);
                    float2 q=float2(c*p.x-s*p.y,s*p.x+c*p.y);
                    fixed4 swirl=tex2D(_MainTex,center+q*radius);
                    // Do not pull the hero's cape, gate or masonry into the portal.
                    color.rgb=lerp(color.rgb,swirl.rgb,mask*purple(swirl.rgb));
                    color.rgb+=float3(.045,.012,.065)*mask*(.5+.5*sin(_Clock*2-r*17));
                }
                float skyArea=smoothstep(.25,.29,uv.x)*(1-smoothstep(.51,.55,uv.x));
                skyArea*=smoothstep(.77,.86,uv.y)*sky(color.rgb);
                float2 cloudUV=float2((uv.x-_Clock*.004)*13,uv.y*20);
                float cloud=smoothstep(.48,.70,noise(cloudUV));
                color.rgb=lerp(color.rgb,float3(.66,.75,.91),cloud*skyArea*.33);
                return color*i.color;
            }
            ENDCG
        }
    }
}
