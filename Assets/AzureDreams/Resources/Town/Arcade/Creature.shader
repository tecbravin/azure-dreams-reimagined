Shader "AzureDreams/ArcadeCreature"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
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
