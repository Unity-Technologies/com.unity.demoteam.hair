//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef HAIRSIM_LOD_CS_HLSL
#define HAIRSIM_LOD_CS_HLSL
// Generated from Unity.DemoTeam.Hair.HairSim+LODBounds
// PackingRules = Exact
struct LODBounds
{
    float3 center;
    float3 extent;
    float radius;
    float reach;
};

// Generated from Unity.DemoTeam.Hair.HairSim+LODFrustum
// PackingRules = Exact
struct LODFrustum
{
    float3 cameraPosition;
    float3 cameraForward;
    float cameraNear;
    float unitSpanSubpixelDepth;
    float4 plane0;
    float4 plane1;
    float4 plane2;
    float4 plane3;
    float4 plane4;
    float4 plane5;
};

// Generated from Unity.DemoTeam.Hair.HairSim+LODGeometry
// PackingRules = Exact
struct LODGeometry
{
    float maxParticleDiameter;
    float maxParticleInterval;
};

// Generated from Unity.DemoTeam.Hair.HairSim+LODIndices
// PackingRules = Exact
struct LODIndices
{
    uint lodIndexLo;
    uint lodIndexHi;
    float lodBlendFrac;
    float lodValue;
};


#endif
