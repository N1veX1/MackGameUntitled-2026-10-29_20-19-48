Shader "MaskInversion/SoftFirefly"
{
 Properties { _Tint("Tint", Color)=(.5,1,.2,1) _Core("Bright core", Float)=3 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
 struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint; float _Core;
 CBUFFER_END
 V vert(A a) { V o; o.pos=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o; }
 half4 frag(V i):SV_Target { float d=length((i.uv-.5)*2); float halo=pow(saturate(1-d),3);float core=exp(-d*d*120)*_Core; return half4(_Tint.rgb*i.color.rgb*(1+core),halo*_Tint.a*i.color.a); }
 ENDHLSL
 } }
}
