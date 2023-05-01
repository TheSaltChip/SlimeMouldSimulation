#ifndef RANDOM_HLSL
#define RANDOM_HLSL

static const float TAU = 6.28318530718f;

uint RandomState;
// https://www.pcg-random.org/ & https://www.shadertoy.com/view/XlGcRh
float Random()
{
    RandomState = RandomState * 747796405u + 2891336453u;
    uint result = (RandomState >> (RandomState >> 28u) + 4u ^ RandomState) * 277803737u;
    result = result >> 22u ^ result;
    return result / 4294967295.0;
}

// mean = 0 & sd=1
float RandomNormalDistribution()
{
    // https://stackoverflow.com/a/6178290
    const float theta = TAU * Random();
    const float rho = sqrt(-2 * log(Random()));
    return rho * cos(theta);
}


float2 RandomUnitSquare()
{
    return float2(Random(), Random());
}

float3 RandomUnitCube()
{
    return float3(Random(), Random(), Random());
}

float3 RandomUnitSphere()
{
    return float3(RandomNormalDistribution(), RandomNormalDistribution(), RandomNormalDistribution());
}

float3 RandomDirection()
{
    return normalize(RandomUnitSphere());
}

float3 RandomHemisphereDirection(const float3 normal)
{
    const float3 dir = RandomDirection();
    return dir * sign(dot(normal, dir));
}

float2 RandomPointInCircle()
{
    const float angle = Random() * TAU;
    return float2(cos(angle), sin(angle)) * sqrt(Random());
}

float3 RandomCosineDirection()
{
    const float r1 = Random();
    const float r2 = Random();
    const float z = sqrt(1 - r2);

    const float phi = TAU * r1;

    return float3(cos(phi) * sqrt(r2), sin(phi) * sqrt(r2), z);
}

void SetSeed(const uint value)
{
    RandomState = value;
    Random();
}

#endif
