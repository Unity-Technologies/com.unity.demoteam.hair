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
    float4 _WorldGravity;
    float4 _StagingOriginExtent;
    float4 _StagingOriginExtentPrev;
    uint _StrandCount;
    uint _StrandParticleCount;
    uint _SolverStrandCount;
    uint _SolverStrandCountFinal;
    float _StrandMaxParticleInterval;
    float _StrandMaxParticleWeight;
    float _StrandScale;
    float _StrandDiameter;
    uint _LODIndexLo;
    uint _LODIndexHi;
    float _LODBlendFraction;
    uint _StagingVertexCount;
    uint _StagingSubdivision;
    float _DT;
    uint _Iterations;
    float _Stiffness;
    float _SOR;
    float _Damping;
    float _DampingInterval;
    float _AngularDamping;
    float _AngularDampingInterval;
    float _CellPressure;
    float _CellVelocity;
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
    uint _BoundaryCountDiscrete;
    uint _BoundaryCountCapsule;
    uint _BoundaryCountSphere;
    uint _BoundaryCountTorus;
    uint _BoundaryCountCube;
    float _BoundaryWorldEpsilon;
    float _BoundaryWorldMargin;
    uint _StrandCountPhi;
    uint _StrandCountTheta;
    uint _StrandCountSubstep;
    float _StrandCountDiameter;
CBUFFER_END


#endif
