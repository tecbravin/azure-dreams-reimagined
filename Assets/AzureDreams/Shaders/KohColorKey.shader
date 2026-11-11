Shader "AzureDreams/KohColorKey"
{
 Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
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
   sampler2D _MainTex;
   struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o; }
   fixed4 frag(Output i):SV_Target
   {
    fixed4 color=tex2D(_MainTex,i.uv);
    fixed3 key=tex2D(_MainTex,float2(0.0005,0.0005)).rgb;
    clip(distance(color.rgb,key)-0.002);
    return color * i.color;
   }
   ENDCG
  }
 }
}
