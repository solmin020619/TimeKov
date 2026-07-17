#ifndef HS_IMPACTARRAY_INCLUDED
#define HS_IMPACTARRAY_INCLUDED

#define HS_HIT_COUNT 20

float4 _hit[HS_HIT_COUNT]; // xyz = pos, w = start time

void HS_ImpactArray_float(
    float3 worldPos,
    float hitTime,
    float hitRadius,
    out float opacity
)
{
    opacity = 0.0;

    float radius = max(hitRadius, 0.0001);
    float now = _Time.y;

    [unroll]
    for (int i = 0; i < HS_HIT_COUNT; i++)
    {
        float3 hitPos = _hit[i].xyz;
        float startTime = _hit[i].w;

        // skip empty slots if you initialize them with negative time
        if (startTime < 0.0)
            continue;

        float age = now - startTime;

        // active only during hitTime seconds
        if (age < 0.0 || age > hitTime)
            continue;

        float d = distance(worldPos, hitPos);

        // sphere mask: 1 in center -> 0 on edge of hitRadius
        float sphere = saturate(1.0 - (d / radius));

        // fade whole hit over lifetime
        float lifeFade = 1.0 - saturate(age / max(hitTime, 0.0001));

        opacity = max(opacity, sphere * lifeFade);
    }
}

#endif