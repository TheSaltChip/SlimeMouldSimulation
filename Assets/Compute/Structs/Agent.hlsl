#ifndef AGENT_HLSL
#define AGENT_HLSL

struct Agent
{
    float2 position;
    float angle;
    int4 speciesMask;
    int speciesIndex;
};

#endif