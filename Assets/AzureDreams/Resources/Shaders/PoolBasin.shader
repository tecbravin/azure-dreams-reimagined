Shader "AzureDreams/PoolBasin"
{
 Properties { _MainTex ("Original pool tiles",2D)="white" {} _TileRect("Tile UV rectangle",Vector)=(.751953,.751953,.121094,.121094) _SurfaceHeight("Water height",Float)=.75 _Depth("Basin depth",Float)=1.2 }
 // URP (HLSL). The Built-in SubShader below stays as the fallback.
 SubShader {
  Tags {"RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite On
  Pass { Tags {"LightMode"="UniversalForward"} HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST; float4 _TileRect; float _SurfaceHeight,_Depth;
   CBUFFER_END
   struct AzVertex {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct AzPixel {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float height:TEXCOORD1;};
   AzPixel vert(AzVertex v){AzPixel o;o.vertex=TransformObjectToHClip(v.vertex.xyz);o.uv=v.uv;o.height=TransformObjectToWorld(v.vertex.xyz).y;return o;}
   half4 frag(AzPixel i):SV_Target {float depth=saturate((_SurfaceHeight-i.height)/max(_Depth,.01));half3 tile=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,_TileRect.xy+frac(i.uv)*_TileRect.zw).rgb;return half4(tile*lerp(float3(.65,.86,.9),float3(.23,.49,.61),depth),1);}
   ENDHLSL }
 }
 SubShader {
  Tags {"RenderType"="Opaque" "Queue"="Geometry"} Cull Off ZWrite On
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _TileRect;float _SurfaceHeight,_Depth;
   struct V {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct P {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float height:TEXCOORD1;};
   P vert(V v){P o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.height=mul(unity_ObjectToWorld,v.vertex).y;return o;}
   fixed4 frag(P i):SV_Target {float depth=saturate((_SurfaceHeight-i.height)/max(_Depth,.01));fixed3 tile=tex2D(_MainTex,_TileRect.xy+frac(i.uv)*_TileRect.zw).rgb;return fixed4(tile*lerp(float3(.65,.86,.9),float3(.23,.49,.61),depth),1);}
   ENDCG }
 }
}
