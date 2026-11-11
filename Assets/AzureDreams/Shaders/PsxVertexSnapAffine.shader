Shader "AzureDreams/PSX Vertex Snap Affine"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SnapResolution ("Snap Resolution", Float) = 320
        _AffineWarp ("Affine Warp Strength", Range(0, 1)) = 1.0
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float w : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _SnapResolution;
            float _AffineWarp;
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                // Transform to clip space
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                
                // Vertex snapping: round to screen grid (PS1 fixed-point precision)
                float2 snapRes = float2(_SnapResolution, _SnapResolution * _ScreenParams.y / _ScreenParams.x);
                clipPos.xy = round(clipPos.xy / clipPos.w * snapRes) / snapRes * clipPos.w;
                
                o.pos = clipPos;
                
                // Affine texture mapping: drop perspective correction
                // Store 1/w for perspective-correct interpolation, then use it to warp UVs
                o.w = clipPos.w;
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Affine warp: interpolate UVs in screen space without perspective division
                if (_AffineWarp > 0)
                {
                    // PS1 affine: UVs are interpolated linearly in screen space
                    // This causes texture warping on oblique surfaces
                    o.uv = uv * lerp(1.0, 1.0 / clipPos.w, _AffineWarp);
                }
                else
                {
                    o.uv = uv;
                }
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Affine correction: divide by w to get proper UV (but with warping artifacts)
                float2 uv = i.uv;
                if (_AffineWarp > 0)
                {
                    uv = uv / lerp(1.0, 1.0 / i.w, _AffineWarp);
                }
                
                fixed4 col = tex2D(_MainTex, uv);
                return col * _Color;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
