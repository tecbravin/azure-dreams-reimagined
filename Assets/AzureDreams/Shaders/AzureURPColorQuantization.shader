Shader "Hidden/AzureDreams/URP/PSX Color Quantization"
{
    // URP version of "AzureDreams/PSX Color Quantization": a full-screen pass for a URP Renderer Feature (Blitter, _BlitTexture).
    // OnRenderImage does not run in URP. It lives in its own shader because Graphics.Blit in the
    // Built-in pipeline does not skip a "UniversalPipeline" SubShader placed first: the Game view went black.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "PSX Color Quantization"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _AzureSourceTexelSize; // 1/w, 1/h, w, h of the source, set by AzurePostProcessFeature

            float4 _Retro; // Color bits (zero disables), dither, scanline strength, scanline count

            static const float Bayer4x4[16] = {
                0, 8, 2, 10,
                12, 4, 14, 6,
                3, 11, 1, 9,
                15, 7, 13, 5
            };

            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                // The source is the low-resolution RT with point filtering, as in the Built-in path.
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                if (_Retro.x > 0)
                {
                    int2 pixel = (int2)floor(uv * _AzureSourceTexelSize.zw);
                    int pattern = (pixel.y & 3) * 4 + (pixel.x & 3);
                    float offset = ((Bayer4x4[pattern] + 0.5) / 16.0 - 0.5) * _Retro.y;
                    float levels = exp2(_Retro.x) - 1.0;
                    color.rgb = floor(saturate(color.rgb) * levels + 0.5 + offset) / levels;
                }
                if (_Retro.z > 0)
                {
                    float rowPhase = frac(uv.y * _Retro.w);
                    float visibility = saturate(1.0 - fwidth(uv.y * _Retro.w));
                    color.rgb *= 1.0 - _Retro.z * visibility * smoothstep(0.2, 0.5, abs(rowPhase - 0.5));
                }
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
