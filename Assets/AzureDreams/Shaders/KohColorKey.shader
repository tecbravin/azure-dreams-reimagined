Shader "AzureDreams/KohColorKey"
{
 Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
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
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST;
   CBUFFER_END
   struct AzVertex { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   struct AzPixel { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
   AzPixel vert(AzVertex v) { AzPixel o; o.vertex=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; o.color=v.color; return o; }
   half4 frag(AzPixel i):SV_Target
   {
    half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
    half3 key=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(0.0005,0.0005)).rgb;
    clip(distance(color.rgb,key)-0.002);
    return color*i.color;
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
