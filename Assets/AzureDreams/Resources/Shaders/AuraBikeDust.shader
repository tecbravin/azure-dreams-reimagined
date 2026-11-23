Shader "AzureDreams/AuraBikeDust"
{
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
