Shader "AzureDreams/PoolResident"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable external alpha", Float) = 0
        [PerRendererData] _PoolWaterHeight ("Waterline", Float) = 0
    }
    // URP (HLSL). The Built-in SubShader below stays as the fallback.
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _EnableExternalAlpha;
            float _PoolWaterHeight;
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

            struct AzVertex { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct AzPixel { float4 position : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float height : TEXCOORD1; };
            AzPixel vert(AzVertex input)
            {
                AzPixel output;
                float3 position = AzureFlipSprite(input.vertex.xyz);
                output.position = TransformObjectToHClip(position);
                output.color = input.color * _Color * unity_SpriteColor;
                output.uv = input.texcoord;
                output.height = TransformObjectToWorld(position).y;
                return output;
            }
            half4 frag(AzPixel input) : SV_Target
            {
                clip(input.height - _PoolWaterHeight);
                half4 color = AzSampleSprite(input.uv) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float _PoolWaterHeight;
            struct Pixel
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
            };
            Pixel vert(appdata_t input)
            {
                v2f sprite = SpriteVert(input);
                Pixel output;
                output.position = sprite.vertex;
                output.color = sprite.color;
                output.uv = sprite.texcoord;
                output.height = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex.xyz, _Flip)).y;
                return output;
            }
            fixed4 frag(Pixel input) : SV_Target
            {
                clip(input.height - _PoolWaterHeight);
                fixed4 color = SampleSpriteTexture(input.uv) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
