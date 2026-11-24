#ifndef AZURE_ACTOR_FEET_DEPTH_INCLUDED
#define AZURE_ACTOR_FEET_DEPTH_INCLUDED
// Plain HLSL shared by the Built-in and URP SubShaders of ActorSpriteDepth.
//
// ActorSpriteDepth gives each sprite the depth of an upright plane through the actor. A unit standing
// at the front edge of a raised tile hangs a little below its feet on screen, and that overhang lies
// behind the tile's own front face, so the face cut a band across the unit's legs. Fragments whose
// camera ray meets the actor's feet level before reaching the upright plane take the depth of that
// meeting point instead: the overhang then sits on the floor in front of the actor. A face or wall
// rising above the feet between camera and actor is still nearer and still hides the unit.

float4 _ActorDepthFeet; // x: world height of the actor's feet; y: 1 when enabled (set by ActorSpriteDepth)

// rayView: the fragment's billboard position in view space (its camera ray).
// planeView: the same fragment on the actor's upright depth plane.
float ActorSpriteFragmentDepth(float3 rayView, float3 planeView)
{
    float3 target = planeView;
    if (_ActorDepthFeet.y > 0.5)
    {
        float3 up = mul((float3x3)UNITY_MATRIX_V, float3(0, 1, 0));
        float feet = dot(up, mul(UNITY_MATRIX_V, float4(0, _ActorDepthFeet.x, 0, 1)).xyz);
        float3 hit = planeView;
        bool found = false;
        if (unity_OrthoParams.w > 0.5)
        {
            // Parallel rays along view +z through (x, y).
            if (abs(up.z) > 1e-4)
            {
                hit = float3(rayView.xy, (feet - up.x * rayView.x - up.y * rayView.y) / up.z);
                found = true;
            }
        }
        else
        {
            float along = dot(up, rayView);
            if (abs(along) > 1e-5)
            {
                float s = feet / along;
                hit = rayView * s;
                found = s > 0;
            }
        }
        // View space looks down -z: a larger z is nearer the camera. Small bias so the overhang
        // does not z-fight with the floor it now lies on.
        if (found && hit.z > planeView.z) target = hit + float3(0, 0, 0.02);
    }
    float4 clip = mul(UNITY_MATRIX_P, float4(target, 1));
    return clip.z / clip.w;
}
#endif
