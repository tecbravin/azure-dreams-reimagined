// Retail tower floor rendering (TowerGameController.RetailFloorMesh.cs): texture modulated by the vertex
// colour, colour 0 transparent as on the PS1. Kept under Resources so it is always included in builds.
Shader "AzureDreams/RetailTowerTranslucent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    // URP (HLSL). The Built-in SubShader below stays as the fallback.
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
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
            float4 _MainTex_ST;
            CBUFFER_END
            struct AzVertex { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct AzPixel { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float w : TEXCOORD1; float3 world : TEXCOORD2; };
            AzPixel vert(AzVertex v)
            {
                AzPixel o;
                float4 clipPos = AzurePsxSnap(TransformObjectToHClip(v.vertex.xyz), _PSXVertexSnapResolution);
                o.pos = clipPos;
                o.w = clipPos.w;
                o.world = TransformObjectToWorld(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) * AzureAffineFactor(clipPos.w, _PSXAffineWarpStrength);
                o.color = v.color;
                return o;
            }
            half4 frag(AzPixel i) : SV_Target
            {
                float2 uv = i.uv / AzureAffineFactor(i.w, _PSXAffineWarpStrength);
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                clip(color.a - 0.5);
                color.rgb *= i.color.rgb;
                color.a = 0.5;
                ClipKohWall(i.world, i.pos.xy);
                return color;
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../../Shaders/KohWallFade.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            // Optional PS1 look, toggled from the graphics menu (see PostProcessingController),
            // applied via global shader properties so every retail tower surface reacts to a
            // single toggle at once. 0 = disabled (default, unaffected look).
            float _PSXVertexSnapResolution;
            float _PSXAffineWarpStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float w : TEXCOORD1;
                float3 world : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                if (_PSXVertexSnapResolution > 0)
                {
                    float2 snapRes = float2(_PSXVertexSnapResolution,
                        _PSXVertexSnapResolution * _ScreenParams.y / _ScreenParams.x);
                    clipPos.xy = round(clipPos.xy / clipPos.w * snapRes) / snapRes * clipPos.w;
                }
                o.pos = clipPos;
                o.w = clipPos.w;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                // Pre-multiply by w; frag() divides back out by the recovered w_pixel, cancelling
                // the hardware's perspective-correct interpolation down to screen-space-linear UVs.
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) * lerp(1.0, clipPos.w, saturate(_PSXAffineWarpStrength));
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv / lerp(1.0, i.w, saturate(_PSXAffineWarpStrength));
                fixed4 color = tex2D(_MainTex, uv);
                clip(color.a - 0.5);
                color.rgb *= i.color.rgb;
                color.a = 0.5;
                ClipKohWall(i.world, i.pos.xy);
                return color;
            }
            ENDCG
        }
    }
}
