Shader "AzureDreams/PoolBasin"
{
 Properties { _MainTex ("Original pool tiles",2D)="white" {} _TileRect("Tile UV rectangle",Vector)=(.751953,.751953,.121094,.121094) _SurfaceHeight("Water height",Float)=.75 _Depth("Basin depth",Float)=1.2 }
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
