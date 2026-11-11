Shader "Hidden/AzureDreams/BuiltinPostProcess"
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
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _MainTex_TexelSize;
            float4 _AAAndBloom; // FXAA, bloom on, intensity, threshold
            float4 _Grade; // saturation, contrast, vignette, grain
            float4 _Balance; // temperature, tint, chromatic shift, time
            float4 _Depth; // AO strength, focal distance, DOF on, AO on
            float Luma(float3 c) { return dot(c, float3(0.299,0.587,0.114)); }
            float3 SampleColor(float2 uv) { return tex2D(_MainTex, uv).rgb; }
            float EyeDepth(float2 uv)
            {
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
                #endif
                return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));
            }
            float3 Fxaa(float2 uv)
            {
                float2 px = abs(_MainTex_TexelSize.xy);
                float3 center = SampleColor(uv);
                float3 nw = SampleColor(uv + px * float2(-1,-1));
                float3 ne = SampleColor(uv + px * float2(1,-1));
                float3 sw = SampleColor(uv + px * float2(-1,1));
                float3 se = SampleColor(uv + px * float2(1,1));
                float lnw=Luma(nw), lne=Luma(ne), lsw=Luma(sw), lse=Luma(se), lm=Luma(center);
                float low=min(lm,min(min(lnw,lne),min(lsw,lse)));
                float high=max(lm,max(max(lnw,lne),max(lsw,lse)));
                if (high-low < max(0.0312,high*0.125)) return center;
                float2 dir=float2(-((lnw+lne)-(lsw+lse)),(lnw+lsw)-(lne+lse));
                float reduce=max((lnw+lne+lsw+lse)*0.03125,0.0078125);
                dir=clamp(dir/(min(abs(dir.x),abs(dir.y))+reduce),-8,8)*px;
                float3 a=0.5*(SampleColor(uv+dir*(-1.0/6.0))+SampleColor(uv+dir*(1.0/6.0)));
                float3 b=a*0.5+0.25*(SampleColor(uv-dir*0.5)+SampleColor(uv+dir*0.5));
                float lb=Luma(b);
                return lb<low || lb>high ? a : b;
            }
            float4 frag(v2f_img i) : SV_Target
            {
                float2 uv=i.uv;
                float2 px=abs(_MainTex_TexelSize.xy);
                float3 c=_AAAndBloom.x>0.5 ? Fxaa(uv) : SampleColor(uv);
                if (_Balance.z>0)
                {
                    float2 shift=(uv-0.5)*_Balance.z*0.025;
                    c.r=SampleColor(uv+shift).r;
                    c.b=SampleColor(uv-shift).b;
                }
                if (_Depth.z>0.5)
                {
                    float depth=EyeDepth(uv);
                    float blur=saturate(abs(depth-_Depth.y)/max(_Depth.y,0.1))*6;
                    float3 b=(SampleColor(uv+px*float2(blur,0))+SampleColor(uv-px*float2(blur,0))+
                              SampleColor(uv+px*float2(0,blur))+SampleColor(uv-px*float2(0,blur)))*0.25;
                    c=lerp(c,b,saturate(blur));
                }
                if (_AAAndBloom.y>0.5)
                {
                    float3 glow=0;
                    [unroll] for(int y=-1;y<=1;y++)
                    [unroll] for(int x=-1;x<=1;x++)
                    {
                        float3 s=SampleColor(uv+px*float2(x,y)*4);
                        glow+=max(s-_AAAndBloom.w,0);
                    }
                    c+=glow*(_AAAndBloom.z/9.0);
                }
                if (_Depth.w>0.5)
                {
                    // Small depth-only AO, not a claim of URP SSAO/normal reconstruction.
                    float z=EyeDepth(uv), occlusion=0;
                    float radius=4;
                    float dz0=z-EyeDepth(uv+px*float2(radius,0));
                    float dz1=z-EyeDepth(uv-px*float2(radius,0));
                    float dz2=z-EyeDepth(uv+px*float2(0,radius));
                    float dz3=z-EyeDepth(uv-px*float2(0,radius));
                    float4 dz=float4(dz0,dz1,dz2,dz3);
                    float4 valid=step(0.01,dz)*(1-step(max(z*0.1,0.1),dz));
                    occlusion=dot(valid,float4(0.25,0.25,0.25,0.25));
                    c*=1-saturate(occlusion*_Depth.x);
                }
                c*=max(float3(0.1,0.1,0.1),float3(1+_Balance.x*0.12+_Balance.y*0.05,
                                                              1-_Balance.y*0.10,
                                                              1-_Balance.x*0.12+_Balance.y*0.05));
                c=lerp(Luma(c).xxx,c,_Grade.x);
                c=(c-0.5)*_Grade.y+0.5;
                float2 edge=uv*(1-uv);
                float vignette=pow(saturate(edge.x*edge.y*16),0.4);
                c*=lerp(1,vignette,_Grade.z);
                float noise=frac(sin(dot(floor(uv/px),float2(12.9898,78.233))+_Balance.w)*43758.5453)-0.5;
                c+=noise*_Grade.w*0.1;
                return float4(max(c,0),tex2D(_MainTex,uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
