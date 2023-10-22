//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef HAIRSIMDATA_CS_HLSL
#define HAIRSIMDATA_CS_HLSL
//
// Unity.DemoTeam.Hair.HairSim+VolumeFeatures:  static fields
//
#define VOLUMEFEATURES_SCATTERING (1)
#define VOLUMEFEATURES_SCATTERING_FASTPATH (2)
#define VOLUMEFEATURES_WIND (4)
#define VOLUMEFEATURES_WIND_FASTPATH (8)

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
    float4x4 _RootTransform;
    float4 _RootRotation;
    float4 _RootRotationInv;
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
    uint _StagingBufferFormat;
    uint _StagingBufferStride;
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
    float _CellForces;
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
    float _scbpad3;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeCBuffer
// PackingRules = Exact
CBUFFER_START(VolumeCBuffer)
    float4 _VolumeCells;
    float4 _VolumeWorldMin;
    float4 _VolumeWorldMax;
    uint _VolumeFeatures;
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
    float _ScatteringProbeUnitWidth;
    uint _ScatteringProbeSubsteps;
    uint _ScatteringProbeSamplesTheta;
    uint _ScatteringProbeSamplesPhi;
    float _ScatteringProbeOccluderDensity;
    float _ScatteringProbeOccluderMargin;
    float _WindEmitterClock;
    uint _WindEmitterCount;
    uint _WindPropagationSubsteps;
    float _WindPropagationExtinction;
    float _WindPropagationOccluderDensity;
    float _WindPropagationOccluderMargin;
    float _vcbpad1;
CBUFFER_END


#endif
