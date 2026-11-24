#ifndef AZURE_DREAMS_URP_INCLUDED
#define AZURE_DREAMS_URP_INCLUDED
// Shared by the "UniversalPipeline" SubShaders of the AzureDreams shaders. Each shader keeps its
// Built-in SubShader (UnityCG) below the URP one, so the project renders the same in both pipelines.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Optional PS1 look, set as globals by PostProcessingController (0 = disabled).
float _PSXVertexSnapResolution;
float _PSXAffineWarpStrength;

// Vertex snapping to a coarse screen grid, like the PS1 fixed-point transform.
float4 AzurePsxSnap(float4 clipPos, float resolution)
{
    if (resolution > 0)
    {
        float2 snapRes = float2(resolution, resolution * _ScreenParams.y / _ScreenParams.x);
        clipPos.xy = round(clipPos.xy / clipPos.w * snapRes) / snapRes * clipPos.w;
    }
    return clipPos;
}

// Affine texture warp: pre-multiply by w in the vertex stage, divide by the interpolated w here.
float AzureAffineFactor(float w, float strength) { return lerp(1.0, w, saturate(strength)); }

// Same polynomial as UnityCG GammaToLinearSpace, so both pipelines produce identical colours.
float3 AzureGammaToLinear(float3 c) { return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878); }

// SpriteRenderer data (URP UnityPerDraw): renderer colour and flip.
float3 AzureFlipSprite(float3 position) { return float3(position.xy * unity_SpriteProps.xy, position.z); }
#endif
