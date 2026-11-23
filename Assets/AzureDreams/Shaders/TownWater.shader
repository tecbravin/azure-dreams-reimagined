Shader "AzureDreams/TownWater"
{
    Properties
    {
        _MainTex ("Original water atlas", 2D) = "white" {}
        _TileStart ("Tile start in texels", Float) = 160
        _Opacity ("Surface opacity", Range(0, 1)) = 0.78
        _Flow ("Flow in texels per second", Vector) = (0, 6, 0, 0)
        [Toggle] _ZWrite ("Write water surface depth", Float) = 0
        [HideInInspector] _FlowTime ("Preview time (-1 uses game time)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" }
        Cull Off
        ZWrite [_ZWrite]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Flow;
            float _TileStart, _Opacity, _FlowTime;

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
                color.a *= _Opacity;
                return color;
            }
            ENDCG
        }
    }
}
