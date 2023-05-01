#ifndef SPECIES_SETTINGS_HLSL
#define SPECIES_SETTINGS_HLSL

struct SpeciesSettings
{
    float moveSpeed;
    float turnSpeed;

    float sensorAngleDegrees;
    float sensorOffsetDst;
    int sensorSize;
    float4 color;
};

#endif
