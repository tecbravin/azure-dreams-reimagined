Shader "AzureDreams/KohForegroundWall"
{
 Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
 // URP (HLSL). The Built-in SubShader below stays as the fallback.
 SubShader
 {
  Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
  Cull Off
  ZWrite On
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
   #include "Assets/AzureDreams/Shaders/KohWallFade.cginc"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST; half4 _Color;
   CBUFFER_END
   struct AzVertex { float4 position:POSITION; float2 uv:TEXCOORD0; };
   struct AzPixel { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
   AzPixel vert(AzVertex input) { AzPixel o; o.position=TransformObjectToHClip(input.position.xyz); o.uv=TRANSFORM_TEX(input.uv,_MainTex); o.world=TransformObjectToWorld(input.position.xyz); return o; }
   half4 frag(AzPixel input):SV_Target
   {
    half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*_Color;
    clip(c.a-0.01);
    ClipKohWall(input.world,input.position.xy);
    return c;
   }
   ENDHLSL
  }
 }
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
