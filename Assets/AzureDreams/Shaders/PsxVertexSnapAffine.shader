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
                
                // Affine warp: interpolate UVs in screen space without perspective division.
                // Hardware perspective-correct interpolation always computes
                // w_pixel * lerp_screenspace(attribute / w_vertex). Feeding it uv * w (not uv / w)
                // makes the w_vertex division cancel the pre-multiplication, leaving
                // w_pixel * lerp_screenspace(uv) -- i.e. plain screen-space-linear UVs once the
                // fragment shader divides back out by the recovered w_pixel. Dividing by w here
                // (as a previous version of this shader did) fed the hardware the wrong quantity
                // and produced warping unrelated to the intended PS1 affine look.
                if (_AffineWarp > 0)
                {
                    o.uv = uv * lerp(1.0, clipPos.w, _AffineWarp);
                }
                else
                {
                    o.uv = uv;
                }
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Recover the screen-space-linear UV by dividing back out the w the vertex
                // stage multiplied in (see vert() above).
                float2 uv = i.uv;
                if (_AffineWarp > 0)
                {
                    uv = uv / lerp(1.0, i.w, _AffineWarp);
                }
                
                fixed4 col = tex2D(_MainTex, uv);
                return col * _Color;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
