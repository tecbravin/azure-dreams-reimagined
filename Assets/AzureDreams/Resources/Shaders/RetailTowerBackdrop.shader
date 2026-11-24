// Retail tower floor rendering (TowerGameController.RetailFloorMesh.cs): texture modulated by the vertex
// colour, colour 0 transparent as on the PS1. Kept under Resources so it is always included in builds.
Shader "AzureDreams/RetailTowerBackdrop"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    // URP (HLSL). The Built-in SubShader below stays as the fallback.
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
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
            struct AzVertex { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct AzPixel { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            AzPixel vert(AzVertex v)
            {
                AzPixel o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                // Far plane, as retail's background ordering table (see Built-in SubShader).
                #if defined(UNITY_REVERSED_Z)
                    o.pos.z = o.pos.w * 0.000001;
                #else
                    o.pos.z = o.pos.w * 0.999999;
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            half4 frag(AzPixel i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                color.rgb *= i.color.rgb;
                color.a = 1;
                return color;
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Retail draws this into the background ordering table. Preserve
                // its perspective without clipping the distant tower to the map camera.
                #if defined(UNITY_REVERSED_Z)
                    o.pos.z = o.pos.w * 0.000001;
                #else
                    o.pos.z = o.pos.w * 0.999999;
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv);
                
                color.rgb *= i.color.rgb;
                color.a = 1;
                return color;
            }
            ENDCG
        }
    }
}
