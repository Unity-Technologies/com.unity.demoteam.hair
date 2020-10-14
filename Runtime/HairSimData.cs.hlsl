//
// This file was automatically generated. Please don't edit by hand.
//

#ifndef HAIRSIMDATA_CS_HLSL
#define HAIRSIMDATA_CS_HLSL
// Generated from Unity.DemoTeam.Hair.HairSim+SolverParams
// PackingRules = Exact
CBUFFER_START(SolverParams)
    float4x4 _LocalToWorld;
    float4x4 _LocalToWorldInvT;
    uint _StrandCount;
    uint _StrandParticleCount;
    float _StrandParticleInterval;
    float _StrandParticleVolume;
    float _StrandParticleContrib;
    float _DT;
    uint _Iterations;
    float _Stiffness;
    float _Relaxation;
    float _Damping;
    float _Gravity;
    float _Repulsion;
    float _Friction;
    float _BendingCurvature;
    float _DampingFTL;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeParams
// PackingRules = Exact
CBUFFER_START(VolumeParams)
    float3 _VolumeCells;
    float _pad1;
    float3 _VolumeWorldMin;
    float _pad2;
    float3 _VolumeWorldMax;
    float _pad3;
    int _BoundaryCapsuleCount;
    int _BoundarySphereCount;
    int _BoundaryTorusCount;
CBUFFER_END


#endif
