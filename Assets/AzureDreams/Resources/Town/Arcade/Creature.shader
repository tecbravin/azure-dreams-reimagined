Shader "AzureDreams/ArcadeCreature"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    // URP (HLSL). The Built-in SubShader below stays as the fallback.
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct AzVertex { float4 position : POSITION; float3 normal : NORMAL; };
            struct AzPixel { float4 position : SV_POSITION; half shade : TEXCOORD0; };
            AzPixel vert(AzVertex v)
            {
                AzPixel o;
                o.position = TransformObjectToHClip(v.position.xyz);
                float3 normal = normalize(TransformObjectToWorldNormal(v.normal));
                o.shade = .48 + .52 * saturate(dot(normal, normalize(float3(-.4,1,-.3))));
                return o;
            }
            half4 frag(AzPixel i) : SV_Target { return half4(_Color.rgb * i.shade, _Color.a); }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct Input { float4 position : POSITION; float3 normal : NORMAL; };
            struct Varying { float4 position : SV_POSITION; fixed shade : TEXCOORD0; };
            Varying vert(Input v)
            {
                Varying o;
                o.position = UnityObjectToClipPos(v.position);
                float3 normal = normalize(UnityObjectToWorldNormal(v.normal));
                o.shade = .48 + .52 * saturate(dot(normal, normalize(float3(-.4,1,-.3))));
                return o;
            }
            fixed4 frag(Varying i) : SV_Target { return fixed4(_Color.rgb * i.shade, _Color.a); }
            ENDCG
        }
    }
}
