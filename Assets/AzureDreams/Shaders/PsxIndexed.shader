Shader "AzureDreams/PSX Indexed CLUT"
{
    Properties
    {
        [NoScaleOffset] _IndexTex ("Index atlas (linear, byte / 255)", 2D) = "black" {}
        [NoScaleOffset] _ClutTex ("CLUT words (R=low byte, G=high byte)", 2D) = "black" {}
        _PaletteRow ("Palette row", Range(0,15)) = 0
        _Color ("Primitive modulation (128/128 = neutral)", Color) = (1,1,1,1)
        [Toggle] _SemiTransparency ("Primitive semi-transparency enabled", Float) = 0
        [Enum(Half,0,Add,1,Subtract,2,AddQuarter,3)] _AbrMode ("PSX ABR mode", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        [Toggle] _ZWrite ("Opaque texels write depth", Float) = 1
        [HideInInspector] _SrcBlend ("Source blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination blend", Float) = 10
        [HideInInspector] _BlendOp ("Blend operation", Float) = 0
        [HideInInspector] _BlendAlpha ("ABR source weight", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZTest [_ZTest]
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _IndexTex, _ClutTex;
        float4 _IndexTex_TexelSize, _ClutTex_TexelSize;
        float4 _Color;
        float _PaletteRow, _SemiTransparency, _BlendAlpha;
        struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
        v2f vert(appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            o.color = v.color * _Color;
            return o;
        }
        float4 ReadPixel(v2f i, out float blendPixel)
        {
            // Point sample at texel centres. Never interpret sRGB or filtered bytes as indices.
            float2 size = _IndexTex_TexelSize.zw;
            float2 pixel = min(floor(saturate(i.uv) * size), size - 1);
            float index = floor(tex2D(_IndexTex, (pixel + 0.5) / size).r * 255.0 + 0.5);
            clip(15.5 - index);
            float row = clamp(floor(_PaletteRow + 0.5), 0, 15);
            float2 paletteUV = (float2(index, row) + 0.5) * _ClutTex_TexelSize.xy;
            float2 bytes = floor(tex2D(_ClutTex, paletteUV).rg * 255.0 + 0.5);
            float word = bytes.x + bytes.y * 256.0;
            clip(word - 0.5); // ONLY 0x0000 disappears. 0x8000 is real black.
            float stp = floor(word / 32768.0);
            blendPixel = step(0.5, _SemiTransparency) * stp;
            float3 rgb = fmod(floor(word / float3(1.0,32.0,1024.0)),32.0) / 31.0;
            // PS1 colors are display/gamma values. Respect Unity's color-space conversion.
            #ifndef UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
            #endif
            return float4(saturate(rgb * i.color.rgb), 1.0);
        }
        float4 fragOpaque(v2f i) : SV_Target
        {
            float blendPixel;
            float4 c = ReadPixel(i, blendPixel);
            clip(0.5 - blendPixel);
            return c;
        }
        float4 fragBlend(v2f i) : SV_Target
        {
            float blendPixel;
            float4 c = ReadPixel(i, blendPixel);
            clip(blendPixel - 0.5);
            c.a = _BlendAlpha;
            return c;
        }
        ENDCG
        Pass
        {
            Name "OPAQUE_TEXELS"
            ZWrite [_ZWrite]
            Blend Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragOpaque
            ENDCG
        }
        Pass
        {
            Name "STP_TEXELS"
            ZWrite Off
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            BlendOp [_BlendOp], Add
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragBlend
            ENDCG
        }
    }
    Fallback Off
    CustomEditor "AzureDreamsReimagined.Editor.PsxIndexedShaderGUI"
}
