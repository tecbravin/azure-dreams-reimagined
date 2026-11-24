Shader "AzureDreams/AuraBikeDust"
{
 // URP (HLSL). The Built-in SubShader below stays as the fallback.
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
   struct AzVertex { float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
   struct AzPixel { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
   AzPixel vert(AzVertex v){AzPixel o;o.vertex=TransformObjectToHClip(v.vertex.xyz);o.uv=v.uv;o.color=v.color;return o;}
   half4 frag(AzPixel i):SV_Target
   {
    float2 p=i.uv*2-1;
    float a=atan2(p.y,p.x);
    float radius=.79+.08*sin(a*5)+.045*cos(a*3);
    float density=1-smoothstep(radius*.4,radius,length(p));
    half4 c=i.color;c.a*=density;
    clip(c.a-.002);return c;
   }
   ENDHLSL
  }
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct V { float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   struct F { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   F vert(V v){F o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(F i):SV_Target
   {
    float2 p=i.uv*2-1;
    float a=atan2(p.y,p.x);
    float radius=.79+.08*sin(a*5)+.045*cos(a*3);
    float density=1-smoothstep(radius*.4,radius,length(p));
    fixed4 c=i.color;c.a*=density;
    clip(c.a-.002);return c;
   }
   ENDCG
  }
 }
}
