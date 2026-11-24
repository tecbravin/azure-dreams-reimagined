Shader "AzureDreams/TownWater"
{
    Properties
    {
        _MainTex ("Original water atlas", 2D) = "white" {}
        _TileStart ("Tile start in texels", Float) = 160
        _Opacity ("Surface opacity", Range(0, 1)) = 0.78
        _Flow ("Flow in texels per second", Vector) = (0, 6, 0, 0)
        [Toggle] _ZWrite ("Write water surface depth", Float) = 0
        [HideInInspector] _SurfaceDepthBias ("Surface depth bias", Float) = 0
        [HideInInspector] _SmoothFlow ("Smooth water animation", Float) = 0
        [HideInInspector] _FlowTime ("Preview time (-1 uses game time)", Float) = -1
    }
    // URP (HLSL). The Built-in SubShader below stays as the fallback.
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite [_ZWrite]
        Offset 0, [_SurfaceDepthBias]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _MainTex_TexelSize, _Flow;
            float _TileStart, _Opacity, _FlowTime, _SmoothFlow, _ZWrite, _SurfaceDepthBias;
            CBUFFER_END
            struct AzVertex { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct AzPixel { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };
            half4 Tap(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv); }
            AzPixel vert(AzVertex input)
            {
                AzPixel output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv;
                output.world = TransformObjectToWorld(input.position.xyz).xz;
                return output;
            }
            half4 frag(AzPixel input) : SV_Target
            {
                float2 texel = input.uv * _MainTex_TexelSize.zw;
                half4 original = Tap(input.uv);
                // Only the three authored water tiles may move (128..224, 96..128).
                if (texel.x < 128 || texel.x >= 224 || texel.y < 96 || texel.y >= 128)
                    return original;
                float time = _FlowTime < 0 ? _Time.y : _FlowTime;
                float2 tile = floor(texel / 32.0) * 32.0;
                float2 local = frac((texel - tile - 0.5 + _Flow.xy * time) / 32.0) * 32.0;
                float2 sampleUv = (tile + clamp(local, 0.0, 31.0) + 0.5) * _MainTex_TexelSize.xy;
                half4 color = Tap(sampleUv);
                if (_SmoothFlow > 0.5)
                {
                    float2 texel0 = floor(local);
                    float2 texel1 = fmod(texel0 + 1.0, 32.0);
                    float2 weight = frac(local);
                    float2 baseUv = tile + 0.5;
                    half4 a = Tap((baseUv + texel0) * _MainTex_TexelSize.xy);
                    half4 b = Tap((baseUv + float2(texel1.x, texel0.y)) * _MainTex_TexelSize.xy);
                    half4 c = Tap((baseUv + float2(texel0.x, texel1.y)) * _MainTex_TexelSize.xy);
                    half4 d = Tap((baseUv + texel1) * _MainTex_TexelSize.xy);
                    color = lerp(lerp(a, b, weight.x), lerp(c, d, weight.x), weight.y);
                }
                color.a *= _Opacity;
                return color;
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" }
        Cull Off
        ZWrite [_ZWrite]
        Offset 0, [_SurfaceDepthBias]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Flow;
            float _TileStart, _Opacity, _FlowTime, _SmoothFlow;

            struct Vertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Pixel
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            Pixel vert(Vertex input)
            {
                Pixel output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.world = mul(unity_ObjectToWorld, input.position).xz;
                return output;
            }

            fixed4 frag(Pixel input) : SV_Target
            {
                float2 texel = input.uv * _MainTex_TexelSize.zw;
                fixed4 original = tex2D(_MainTex, input.uv);
                // Page 44 shares an atlas with masonry, plants and ornaments.
                // Only the three authored water tiles may move (128..224, 96..128).
                if (texel.x < 128 || texel.x >= 224 || texel.y < 96 || texel.y >= 128)
                    return original;
                float time = _FlowTime < 0 ? _Time.y : _FlowTime;
                float2 tile = floor(texel / 32.0) * 32.0;
                // Wrap locally instead of clamping: clamping freezes whole strips
                // against the atlas edge and can sample the wrong water variant.
                float2 local = frac((texel - tile - 0.5 + _Flow.xy * time) / 32.0) * 32.0;
                float2 sampleUv = (tile + clamp(local, 0.0, 31.0) + 0.5) * _MainTex_TexelSize.xy;
                fixed4 color = tex2D(_MainTex, sampleUv);
                if (_SmoothFlow > 0.5)
                {
                    // Interpolate between texel centers without sampling into
                    // neighboring atlas tiles at the wrap boundary.
                    float2 texel0 = floor(local);
                    float2 texel1 = fmod(texel0 + 1.0, 32.0);
                    float2 weight = frac(local);
                    float2 baseUv = tile + 0.5;
                    fixed4 a = tex2D(_MainTex, (baseUv + texel0) * _MainTex_TexelSize.xy);
                    fixed4 b = tex2D(_MainTex, (baseUv + float2(texel1.x, texel0.y)) * _MainTex_TexelSize.xy);
                    fixed4 c = tex2D(_MainTex, (baseUv + float2(texel0.x, texel1.y)) * _MainTex_TexelSize.xy);
                    fixed4 d = tex2D(_MainTex, (baseUv + texel1) * _MainTex_TexelSize.xy);
                    color = lerp(lerp(a, b, weight.x), lerp(c, d, weight.x), weight.y);
                }
                color.a *= _Opacity;
                return color;
            }
            ENDCG
        }
    }
}
