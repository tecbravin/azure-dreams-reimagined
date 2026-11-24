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
  [PerRendererData] _ActorDepthNormal ("Facade depth normal",Vector)=(0,0,0,0)
 }
 // URP (HLSL). The Built-in SubShader below stays as the fallback.
 // Sprite renderer colour/flip come from unity_SpriteColor/unity_SpriteProps in URP.
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off Blend One OneMinusSrcAlpha
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ PIXELSNAP_ON
   #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
   #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;
   float _EnableExternalAlpha;
   float _ColorKey;
   float4 _ActorDepthAnchor;
   float4 _ActorDepthNormal;
   CBUFFER_END
   
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
   half4 AzSampleSprite(float2 uv)
   {
    half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
    #if ETC1_EXTERNAL_ALPHA
    half4 alpha=SAMPLE_TEXTURE2D(_AlphaTex,sampler_AlphaTex,uv);
    color.a=lerp(color.a,alpha.r,_EnableExternalAlpha);
    #endif
    return color;
   }

   struct AzVertex { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; };
   struct AzPixel { float4 vertex:SV_POSITION; half4 color:COLOR; float2 texcoord:TEXCOORD0; };
   AzPixel vert(AzVertex input)
   {
    AzPixel output;
    float3 position=AzureFlipSprite(input.vertex.xyz);
    output.vertex=TransformObjectToHClip(position);
    output.texcoord=input.texcoord;
    output.color=input.color*_Color*unity_SpriteColor;
    #ifdef PIXELSNAP_ON
    float2 halfScreen=_ScreenParams.xy*0.5;
    output.vertex.xy=round(output.vertex.xy/output.vertex.w*halfScreen)/halfScreen*output.vertex.w;
    #endif
    float4 view=mul(UNITY_MATRIX_V,float4(TransformObjectToWorld(position),1));
    // Keep the camera-facing silhouette; only depth follows the upright body.
    float3 anchorView=mul(UNITY_MATRIX_V,float4(_ActorDepthAnchor.xyz,1)).xyz;
    float slope=UNITY_MATRIX_V[2].y/max(.25,abs(UNITY_MATRIX_V[1].y));
    if(dot(_ActorDepthNormal.xyz,_ActorDepthNormal.xyz)>.5)
    {
     float3 normalView=mul((float3x3)UNITY_MATRIX_V,_ActorDepthNormal.xyz);
     float plane=dot(normalView,anchorView);
     if(unity_OrthoParams.w>.5)
     {
      if(abs(normalView.z)>.001)
       view.z=(plane-dot(normalView.xy,view.xy))/normalView.z;
     }
     else
     {
      float denominator=dot(normalView,view.xyz/view.z);
      if(abs(denominator)>.001) view.z=plane/denominator;
     }
    }
    else if(unity_OrthoParams.w>.5)
     view.z=anchorView.z+(view.y-anchorView.y)*slope;
    else
    {
     float denominator=1-slope*view.y/view.z;
     view.z=(anchorView.z-slope*anchorView.y)/max(.001,denominator);
    }
    float4 depthClip=mul(UNITY_MATRIX_P,view);
    output.vertex.z=depthClip.z/depthClip.w*output.vertex.w;
    return output;
   }
   half4 frag(AzPixel input):SV_Target
   {
    half4 color=AzSampleSprite(input.texcoord);
    if(_ColorKey>.5)
    {
     half3 key=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(.0005,.0005)).rgb;
     clip(distance(color.rgb,key)-.002);
    }
    color*=input.color;
    color.rgb*=color.a;
    return color;
   }
   ENDHLSL
  }
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
   float4 _ActorDepthNormal;
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
    if(dot(_ActorDepthNormal.xyz,_ActorDepthNormal.xyz)>.5)
    {
        float3 normalView=mul((float3x3)UNITY_MATRIX_V,_ActorDepthNormal.xyz);
        float plane=dot(normalView,anchorView);
        if(unity_OrthoParams.w>.5)
        {
            if(abs(normalView.z)>.001)
                view.z=(plane-dot(normalView.xy,view.xy))/normalView.z;
        }
        else
        {
            float denominator=dot(normalView,view.xyz/view.z);
            if(abs(denominator)>.001) view.z=plane/denominator;
        }
    }
    else if(unity_OrthoParams.w>.5)
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
