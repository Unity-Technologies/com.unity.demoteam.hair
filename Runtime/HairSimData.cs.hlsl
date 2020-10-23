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
    float _Gravity;
    float _Damping;
    float _VolumePressureScale;
    float _VolumeFrictionScale;
    float _DampingFTL;
    float _BoundaryFriction;
    float _BendingCurvature;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeParams
// PackingRules = Exact
CBUFFER_START(VolumeParams)
    float3 _VolumeCells;
    uint __pad1;
    float3 _VolumeWorldMin;
    uint __pad2;
    float3 _VolumeWorldMax;
    uint __pad3;
    float _PressureFromVelocity;
    float _PressureFromDensity;
    int _BoundaryCapsuleCount;
    int _BoundarySphereCount;
    int _BoundaryTorusCount;
CBUFFER_END


#endif
