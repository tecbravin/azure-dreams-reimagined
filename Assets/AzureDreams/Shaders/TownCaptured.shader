Shader "AzureDreams/TownCaptured"
{
 Properties
 {
  _MainTex ("Texture", 2D) = "white" {}
  _KeySheetBackground ("Key sheet background", Float) = 0
 }
 SubShader
 {
  Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
  Cull Off
  Pass
  {
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   #include "KohWallFade.cginc"
   sampler2D _MainTex;
   float4 _MainTex_TexelSize;
   float _KeySheetBackground;
   struct Vertex { float4 position:POSITION; float2 uv:TEXCOORD0; };
   struct Pixel { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
   Pixel vert(Vertex input) { Pixel output; output.position=UnityObjectToClipPos(input.position); output.uv=input.uv; output.world=mul(unity_ObjectToWorld,input.position).xyz; return output; }
   fixed4 frag(Pixel input):SV_Target
   {
    fixed4 color = tex2D(_MainTex, input.uv);
    clip(color.a - 0.01);
    if (_KeySheetBackground > 0.5)
    {
     fixed3 key = tex2D(_MainTex, _MainTex_TexelSize.xy * 0.5).rgb;
     clip(dot(abs(color.rgb - key), float3(1, 1, 1)) - 0.001);
    }
    if (_KeySheetBackground < 0.5) ClipKohWall(input.world, input.position.xy);
    return color;
   }
   ENDCG
  }
 }
}
