//
// This file was automatically generated. Please don't edit by hand.
//

#ifndef HAIRSIMDATA_CS_HLSL
#define HAIRSIMDATA_CS_HLSL
// Generated from Unity.DemoTeam.Hair.HairSim+SolverCBuffer
// PackingRules = Exact
CBUFFER_START(SolverCBuffer)
    float4x4 _LocalToWorld;
    float4x4 _LocalToWorldInvT;
    float4 _WorldRotation;
    uint _StrandCount;
    uint _StrandParticleCount;
    float _StrandMaxParticleInterval;
    float _StrandMaxParticleWeight;
    float _StrandScale;
    float _DT;
    uint _Iterations;
    float _Stiffness;
    float _SOR;
    float _CellPressure;
    float _CellVelocity;
    float _Damping;
    float _DampingInterval;
    float _Gravity;
    float _BoundaryFriction;
    float _FTLDamping;
    float _LocalCurvature;
    float _LocalShape;
    float _GlobalPosition;
    float _GlobalPositionInterval;
    float _GlobalRotation;
    float _GlobalFadeOffset;
    float _GlobalFadeExtent;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeCBuffer
// PackingRules = Exact
CBUFFER_START(VolumeCBuffer)
    float3 _VolumeCells;
    uint __pad1;
    float3 _VolumeWorldMin;
    uint __pad2;
    float3 _VolumeWorldMax;
    uint __pad3;
    float _ResolveUnitVolume;
    float _ResolveUnitDebugWidth;
    float _TargetDensityFactor;
    int _BoundaryCountDiscrete;
    int _BoundaryCountCapsule;
    int _BoundaryCountSphere;
    int _BoundaryCountTorus;
    float _BoundaryWorldEpsilon;
    float _BoundaryWorldMargin;
CBUFFER_END


#endif
