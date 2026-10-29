Shader "MaskTraversal/CosmicMask"
{
    Properties { _Amount ("Intensity", Range(0,1)) = .8 _Turn ("Gravity pulse", Range(0,1)) = 0 _Aspect ("Aspect", Float) = 1.7778 }
    SubShader
    {
        Tags { "Queue"="Overlay+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Amount, _Turn, _Aspect;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float stars(float2 p, float time)
            {
                float2 cell=floor(p), q=frac(p)-.5;
                float seed=hash(cell); q-=float2(hash(cell+3),hash(cell+9))*.45-.225;
                float radius=length(q);
                float sparkle=exp(-radius*radius*600);
                return sparkle*step(.77,seed)*(.55+.45*sin(time*1.8+seed*62));
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv, p=(uv-.5)*float2(_Aspect,1);
                float edge=smoothstep(.26,.67,length((uv-.5)*float2(1.05,1.15)));
                float t=_Time.y;
                float swirl=_Turn*.22*sin(t*.9+length(p)*7);
                p=mul(float2x2(cos(swirl),-sin(swirl),sin(swirl),cos(swirl)),p);
                float clouds=noise(p*4+float2(t*.023,-t*.017))*.6+noise(p*9+float2(-t*.015,t*.021))*.3;
                float dust=stars(p*32+float2(t*.16,-t*.11),t)+stars(p*57+float2(-t*.2,t*.13),t+5)*.6;
                float ring=pow(saturate(.5+.5*sin(length(p)*45-t*2.2)),16)*_Turn*edge*.3;
                float3 nebula=lerp(float3(.11,.27,.72),float3(.62,.19,.82),clouds);
                float alpha=_Amount*saturate(edge*(.23+clouds*.4)+dust*(.16+edge*.72)+ring);
                float3 color=nebula*(.65+clouds*.65)+dust*float3(.65,.82,1.1)+ring*float3(.24,.6,1);
                return float4(color,alpha);
            }
            ENDCG
        }
    }
}
