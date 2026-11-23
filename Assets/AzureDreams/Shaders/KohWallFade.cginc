#ifndef AZURE_KOH_WALL_FADE_INCLUDED
#define AZURE_KOH_WALL_FADE_INCLUDED
// Coverage transparency keeps the original depth/order of the combined city and
// tower meshes, rather than putting a whole building in the transparent queue.
float4 _KohWallFadeTarget; // feet xyz; w enables the effect
float KohWallCoverage(float3 world)
{
    float3 n = cross(ddx(world), ddy(world));
    if (_KohWallFadeTarget.w < 0.5 || abs(n.y) > length(n) * 0.6) return 1;
    float3 feet = _KohWallFadeTarget.xyz;
    float2 toEye = _WorldSpaceCameraPos.xz - feet.xz;
    float2 eyeAxis = lerp(toEye, UNITY_MATRIX_V[2].xz, unity_OrthoParams.w);
    float2 view = eyeAxis / max(length(eyeAxis), 0.001);
    float eyeDistance = max(dot(toEye, view), 0.001);
    float2 delta = world.xz - feet.xz;
    float front = dot(delta, view);
    float lateral = abs(delta.x * view.y - delta.y * view.x);
    if (front <= 0.02 || front >= eyeDistance || lateral >= 0.9) return 1;
    float t = front / eyeDistance;
    float low = lerp(feet.y + 0.05, _WorldSpaceCameraPos.y, t);
    float high = lerp(feet.y + 1.9, _WorldSpaceCameraPos.y, t);
    // Orthographic rays stay parallel instead of converging on the camera.
    float rise = front * UNITY_MATRIX_V[2].y / max(length(UNITY_MATRIX_V[2].xz), 0.001);
    low = lerp(low, feet.y + 0.05 + rise, unity_OrthoParams.w);
    high = lerp(high, feet.y + 1.9 + rise, unity_OrthoParams.w);
    // Apply to every foreground wall along the camera-to-Koh corridor, regardless of distance from Koh.
    float mask = (1 - smoothstep(0.65, 0.9, lateral)) *
                 smoothstep(low - 0.2, low, world.y) * (1 - smoothstep(high, high + 0.2, world.y));
    return lerp(1, 0.2, mask);
}
float KohBayer2(float2 p) { return 2 * p.x + 3 * p.y - 4 * p.x * p.y; }
void ClipKohWall(float3 world, float2 screenPixel)
{
    float coverage = KohWallCoverage(world);
    float2 p = fmod(floor(screenPixel), 4);
    float threshold = (4 * KohBayer2(fmod(p, 2)) + KohBayer2(floor(p / 2)) + 0.5) / 16;
    clip(coverage - threshold);
}
#endif
