Shader "AzureDreams/TownWater"
{
    Properties
    {
        _MainTex ("Original water atlas", 2D) = "white" {}
        _TileStart ("Tile start in texels", Float) = 160
        _Opacity ("Surface opacity", Range(0, 1)) = 0.78
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _TileStart, _Opacity;

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
                float phase = input.world.x * 1.7 + input.world.y * 1.25 + _Time.y * 1.4;
                float2 ripple = float2(sin(phase), cos(phase * 0.75)) * 1.25;
                float2 tile = float2(_TileStart, 96);
                float2 texel = clamp(input.uv * 256.0 + ripple, tile + 0.5, tile + 31.5);
                fixed4 color = tex2D(_MainTex, texel / 256.0);
                float crest = 0.5 + 0.5 * sin(phase);
                color.rgb *= lerp(float3(0.75, 0.87, 0.96), float3(1.04, 1.06, 1.08), crest);
                color.a *= _Opacity + (crest - 0.5) * 0.1;
                return color;
            }
            ENDCG
        }
    }
}
