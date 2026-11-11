Shader "AzureDreams/PSX Color Quantization"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Retro; // Color bits (zero disables), dither, scanline strength, scanline count

            static const float Bayer4x4[16] = {
                0, 8, 2, 10,
                12, 4, 14, 6,
                3, 11, 1, 9,
                15, 7, 13, 5
            };

            float4 frag(v2f_img i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                if (_Retro.x > 0)
                {
                    int2 pixel = (int2)floor(i.uv * _MainTex_TexelSize.zw);
                    int pattern = (pixel.y & 3) * 4 + (pixel.x & 3);
                    float offset = ((Bayer4x4[pattern] + 0.5) / 16.0 - 0.5) * _Retro.y;
                    float levels = exp2(_Retro.x) - 1.0;
                    color.rgb = floor(saturate(color.rgb) * levels + 0.5 + offset) / levels;
                }
                if (_Retro.z > 0)
                {
                    float rowPhase = frac(i.uv.y * _Retro.w);
                    float visibility = saturate(1.0 - fwidth(i.uv.y * _Retro.w));
                    color.rgb *= 1.0 - _Retro.z * visibility * smoothstep(0.2, 0.5, abs(rowPhase - 0.5));
                }
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
