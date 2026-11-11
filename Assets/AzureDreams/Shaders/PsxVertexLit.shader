Shader "AzureDreams/PSX Vertex Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LightDir ("Light Direction", Vector) = (0.3, 0.8, 0.5, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 lighting : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float3 _LightDir;
            float _Ambient;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // PS1 vertex lighting (Gouraud shading)
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float ndotl = max(0, dot(worldNormal, normalize(_LightDir)));
                o.lighting = _Ambient + (1.0 - _Ambient) * ndotl;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                col.rgb *= i.lighting;
                col *= _Color;
                return col;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
