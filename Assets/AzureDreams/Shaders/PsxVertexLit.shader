Shader "AzureDreams/PSX Vertex Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LightDir ("Light Direction", Vector) = (0.3, 0.8, 0.5, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.3
    }
    // URP (HLSL). Lighting comes from the material's _LightDir, not scene lights, as in the
    // Built-in SubShader below (kept as the fallback).
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/AzureDreams/Shaders/AzureURP.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _LightDir;
            float _Ambient;
            CBUFFER_END
            struct AzVertex { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct AzPixel { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 lighting : TEXCOORD1; float w : TEXCOORD2; };
            AzPixel vert(AzVertex v)
            {
                AzPixel o;
                float4 clipPos = AzurePsxSnap(TransformObjectToHClip(v.vertex.xyz), _PSXVertexSnapResolution);
                o.pos = clipPos;
                o.w = clipPos.w;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) * AzureAffineFactor(clipPos.w, _PSXAffineWarpStrength);
                float3 worldNormal = TransformObjectToWorldNormal(v.normal);
                float ndotl = max(0, dot(worldNormal, normalize(_LightDir.xyz)));
                o.lighting = _Ambient + (1.0 - _Ambient) * ndotl;
                return o;
            }
            half4 frag(AzPixel i) : SV_Target
            {
                float2 uv = i.uv / AzureAffineFactor(i.w, _PSXAffineWarpStrength);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                col.rgb *= i.lighting;
                return col * _Color;
            }
            ENDHLSL
        }
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
                float w : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float3 _LightDir;
            float _Ambient;

            // Optional PS1 look, toggled from the graphics menu (see PostProcessingController),
            // applied uniformly to every object using this shader via global shader properties
            // rather than per-material properties. 0 = disabled (default, unaffected look).
            float _PSXVertexSnapResolution;
            float _PSXAffineWarpStrength;

            v2f vert (appdata v)
            {
                v2f o;
                float4 clipPos = UnityObjectToClipPos(v.vertex);

                // Vertex snapping: round screen position to a coarse grid, like the PS1's
                // fixed-point vertex transform. Same technique as PsxVertexSnapAffine.shader.
                if (_PSXVertexSnapResolution > 0)
                {
                    float2 snapRes = float2(_PSXVertexSnapResolution,
                        _PSXVertexSnapResolution * _ScreenParams.y / _ScreenParams.x);
                    clipPos.xy = round(clipPos.xy / clipPos.w * snapRes) / snapRes * clipPos.w;
                }
                o.pos = clipPos;
                o.w = clipPos.w;

                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Affine texture warp: pre-multiply by w so the hardware's perspective-correct
                // interpolation (which divides by w, then multiplies back by w_pixel) cancels
                // down to plain screen-space-linear UVs once frag() divides by the recovered
                // w_pixel below -- the PS1 had no perspective-correct texturing.
                o.uv = uv * lerp(1.0, clipPos.w, saturate(_PSXAffineWarpStrength));

                // PS1 vertex lighting (Gouraud shading)
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float ndotl = max(0, dot(worldNormal, normalize(_LightDir)));
                o.lighting = _Ambient + (1.0 - _Ambient) * ndotl;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv / lerp(1.0, i.w, saturate(_PSXAffineWarpStrength));
                fixed4 col = tex2D(_MainTex, uv);
                col.rgb *= i.lighting;
                col *= _Color;
                return col;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
