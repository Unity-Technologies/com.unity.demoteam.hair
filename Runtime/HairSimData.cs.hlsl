//
// This file was automatically generated. Please don't edit by hand.
//

#ifndef HAIRSIMDATA_CS_HLSL
#define HAIRSIMDATA_CS_HLSL
//
// Unity.DemoTeam.Hair.HairSim+SolverFeatures:  static fields
//
#define SOLVERFEATURES_BOUNDARY (1)
#define SOLVERFEATURES_BOUNDARY_FRICTION (2)
#define SOLVERFEATURES_DISTANCE (4)
#define SOLVERFEATURES_DISTANCE_LRA (8)
#define SOLVERFEATURES_DISTANCE_FTL (16)
#define SOLVERFEATURES_CURVATURE_EQ (32)
#define SOLVERFEATURES_CURVATURE_GEQ (64)
#define SOLVERFEATURES_CURVATURE_LEQ (128)
#define SOLVERFEATURES_POSE_LOCAL_SHAPE (256)
#define SOLVERFEATURES_POSE_LOCAL_SHAPE_RWD (512)
#define SOLVERFEATURES_POSE_GLOBAL_POSITION (1024)
#define SOLVERFEATURES_POSE_GLOBAL_ROTATION (2048)

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
    uint _StrandParticleOffset;
    uint _StrandParticleStride;
    uint _SolverFeatures;
    uint _SolverStrandCount;
    float _GroupScale;
    float _GroupMaxParticleInterval;
    float _GroupMaxParticleDiameter;
    float _GroupMaxParticleFootprint;
    uint _LODCount;
    uint _LODIndexLo;
    uint _LODIndexHi;
    float _LODBlendFraction;
    uint _StagingSubdivision;
    uint _StagingVertexCount;
    uint _StagingVertexOffset;
    float _DT;
    uint _Substeps;
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
    float _LocalShapeBias;
    float _GlobalPosition;
    float _GlobalPositionInterval;
    float _GlobalRotation;
    float _GlobalFadeOffset;
    float _GlobalFadeExtent;
    float _scbpad1;
    float _scbpad2;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeCBuffer
// PackingRules = Exact
CBUFFER_START(VolumeCBuffer)
    float4 _VolumeCells;
    float4 _VolumeWorldMin;
    float4 _VolumeWorldMax;
    float _AllGroupsDebugWeight;
    float _AllGroupsMaxParticleVolume;
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
    float _vcbpad1;
    float _vcbpad2;
CBUFFER_END


#endif
