Shader "AzureDreams/ActorSpriteDepth"
{
 Properties
 {
  [PerRendererData] _MainTex ("Sprite",2D)="white" {}
  _Color ("Tint",Color)=(1,1,1,1)
  [MaterialToggle] PixelSnap ("Pixel snap",Float)=0
  [HideInInspector] _RendererColor ("Renderer color",Color)=(1,1,1,1)
  [HideInInspector] _Flip ("Flip",Vector)=(1,1,1,1)
  [PerRendererData] _AlphaTex ("External alpha",2D)="white" {}
  [PerRendererData] _EnableExternalAlpha ("Enable external alpha",Float)=0
  _ColorKey ("Use original Koh color key",Float)=0
  [PerRendererData] _ActorDepthAnchor ("Depth anchor",Vector)=(0,0,0,1)
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
  Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "UnitySprites.cginc"
   float4 _ActorDepthAnchor;
   float _ColorKey;
   v2f vert(appdata_t input)
   {
    v2f output=SpriteVert(input);
    float4 world=mul(unity_ObjectToWorld,UnityFlipSprite(input.vertex.xyz,_Flip));
    float4 view=mul(UNITY_MATRIX_V,world);
    // Keep the fully camera-facing silhouette. Only depth follows the
    // upright body represented by that silhouette, instead of a flat card.
    float3 anchorView=mul(UNITY_MATRIX_V,float4(_ActorDepthAnchor.xyz,1)).xyz;
    float slope=UNITY_MATRIX_V[2].y/max(.25,abs(UNITY_MATRIX_V[1].y));
    if(unity_OrthoParams.w>.5)
        view.z=anchorView.z+(view.y-anchorView.y)*slope;
    else
    {
        // Intersect this pixel's camera ray with the common upright plane.
        // Merely shifting vertex depth is inaccurate in perspective: the
        // head can still cross a surface even when the feet are in front.
        float denominator=1-slope*view.y/view.z;
        view.z=(anchorView.z-slope*anchorView.y)/max(.001,denominator);
    }
    float4 depthClip=mul(UNITY_MATRIX_P,view);
    output.vertex.z=depthClip.z/depthClip.w*output.vertex.w;
    return output;
   }
   fixed4 frag(v2f input):SV_Target
   {
    fixed4 color=SampleSpriteTexture(input.texcoord);
    if(_ColorKey>.5)
    {
     fixed3 key=tex2D(_MainTex,float2(.0005,.0005)).rgb;
     clip(distance(color.rgb,key)-.002);
    }
    color*=input.color;
    color.rgb*=color.a;
    return color;
   }
   ENDCG
  }
 }
}
