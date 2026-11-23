Shader "AzureDreams/KohForegroundWall"
{
 Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
  Cull Off
  ZWrite On
  Pass
  {
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   #include "../../Shaders/KohWallFade.cginc"
   sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
   struct Vertex { float4 position:POSITION; float2 uv:TEXCOORD0; };
   struct Pixel { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
   Pixel vert(Vertex input) { Pixel o; o.position=UnityObjectToClipPos(input.position); o.uv=TRANSFORM_TEX(input.uv,_MainTex); o.world=mul(unity_ObjectToWorld,input.position).xyz; return o; }
   fixed4 frag(Pixel input):SV_Target
   {
    fixed4 c=tex2D(_MainTex,input.uv)*_Color;
    clip(c.a-0.01);
    ClipKohWall(input.world,input.position.xy);
    return c;
   }
   ENDCG
  }
 }
}
