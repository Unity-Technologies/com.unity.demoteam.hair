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
	float _DampingPeriod;
    float _Gravity;
    float _FTLDamping;
    float _BoundaryFriction;
    float _BendingCurvature;
	float _GlobalPosition;
	float _GlobalPositionPeriod;
	float _GlobalRotation;
	float _GlobalFadeOffset;
	float _GlobalFadeExtent;
	float _LocalShape;
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
    int _BoundaryCapsuleCount;
    int _BoundarySphereCount;
    int _BoundaryTorusCount;
CBUFFER_END


#endif
