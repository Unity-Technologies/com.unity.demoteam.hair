//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef HAIRSIMDATA_CS_HLSL
#define HAIRSIMDATA_CS_HLSL
//
// Unity.DemoTeam.Hair.HairSim+StagingVertexFormat:  static fields
//
#define STAGINGVERTEXFORMAT_COMPRESSED (0)
#define STAGINGVERTEXFORMAT_UNCOMPRESSED (1)

//
// Unity.DemoTeam.Hair.HairSim+VolumeLODStage:  static fields
//
#define VOLUMELODSTAGE_RESOLVE (0)
#define VOLUMELODSTAGE___COUNT (1)

//
// Unity.DemoTeam.Hair.HairSim+RenderFeatures:  static fields
//
#define RENDERFEATURES_TAPERING (1)
#define RENDERFEATURES_PER_VERTEX_TEX_COORD (2)
#define RENDERFEATURES_PER_VERTEX_DIAMETER (4)

//
// Unity.DemoTeam.Hair.HairSim+SolverLODRange:  static fields
//
#define SOLVERLODRANGE_SOLVE (0)
#define SOLVERLODRANGE_INTERPOLATE (1)
#define SOLVERLODRANGE_INTERPOLATE_ADD (2)
#define SOLVERLODRANGE_INTERPOLATE_PROMOTE (3)
#define SOLVERLODRANGE_RENDER (4)
#define SOLVERLODRANGE___COUNT (5)

//
// Unity.DemoTeam.Hair.HairSim+SolverLODStage:  static fields
//
#define SOLVERLODSTAGE_PHYSICS (0)
#define SOLVERLODSTAGE_RENDERING (1)
#define SOLVERLODSTAGE___COUNT (2)

//
// Unity.DemoTeam.Hair.HairSim+VolumeLODDispatch:  static fields
//
#define VOLUMELODDISPATCH_RESOLVE (0)
#define VOLUMELODDISPATCH_RASTER_POINTS (1)
#define VOLUMELODDISPATCH_RASTER_VECTORS (2)
#define VOLUMELODDISPATCH___COUNT (3)

//
// Unity.DemoTeam.Hair.HairSim+RenderLODSelection:  static fields
//
#define RENDERLODSELECTION_AUTOMATIC_PER_SEGMENT (0)
#define RENDERLODSELECTION_AUTOMATIC_PER_GROUP (1)
#define RENDERLODSELECTION_AUTOMATIC_PER_VOLUME (2)
#define RENDERLODSELECTION_MATCH_PHYSICS (3)
#define RENDERLODSELECTION_MANUAL (4)

//
// Unity.DemoTeam.Hair.HairSim+VolumeFeatures:  static fields
//
#define VOLUMEFEATURES_SCATTERING (1)
#define VOLUMEFEATURES_SCATTERING_FASTPATH (2)
#define VOLUMEFEATURES_WIND (4)
#define VOLUMEFEATURES_WIND_FASTPATH (8)

//
// Unity.DemoTeam.Hair.HairSim+SolverLODSelection:  static fields
//
#define SOLVERLODSELECTION_AUTOMATIC_PER_GROUP (0)
#define SOLVERLODSELECTION_AUTOMATIC_PER_VOLUME (1)
#define SOLVERLODSELECTION_MANUAL (2)

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

//
// Unity.DemoTeam.Hair.HairSim+SolverLODDispatch:  static fields
//
#define SOLVERLODDISPATCH_SOLVE (0)
#define SOLVERLODDISPATCH_SOLVE_GROUP_PARTICLES (1)
#define SOLVERLODDISPATCH_INTERPOLATE (2)
#define SOLVERLODDISPATCH_INTERPOLATE_ADD (3)
#define SOLVERLODDISPATCH_INTERPOLATE_PROMOTE (4)
#define SOLVERLODDISPATCH_STAGING (5)
#define SOLVERLODDISPATCH_STAGING_REENTRANT (6)
#define SOLVERLODDISPATCH_TRANSFER (7)
#define SOLVERLODDISPATCH_TRANSFER_ALL (8)
#define SOLVERLODDISPATCH_RASTER_POINTS (9)
#define SOLVERLODDISPATCH_RASTER_POINTS_ALL (10)
#define SOLVERLODDISPATCH_RASTER_QUADS (11)
#define SOLVERLODDISPATCH_RASTER_QUADS_ALL (12)
#define SOLVERLODDISPATCH___COUNT (13)

// Generated from Unity.DemoTeam.Hair.HairSim+SolverCBufferRoots
// PackingRules = Exact
CBUFFER_START(SolverCBufferRoots)
    float4x4 _RootMeshMatrix;
    float4 _RootMeshRotation;
    float4 _RootMeshRotationInv;
    float4 _RootMeshSkinningRotation;
    uint _RootMeshPositionOffset;
    uint _RootMeshPositionStride;
    uint _RootMeshTangentOffset;
    uint _RootMeshTangentStride;
    uint _RootMeshNormalOffset;
    uint _RootMeshNormalStride;
    float _rcbpad1;
    float _rcbpad2;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeLODGrid
// PackingRules = Exact
struct VolumeLODGrid
{
    float3 volumeWorldMin;
    float3 volumeWorldMax;
    float3 volumeCellCount;
    float volumeCellRadius;
};

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeCBufferEnvironment
// PackingRules = Exact
CBUFFER_START(VolumeCBufferEnvironment)
    float4 _WorldGravity;
    uint _LODFrustumCount;
    uint _BoundaryDelimDiscrete;
    uint _BoundaryDelimCapsule;
    uint _BoundaryDelimSphere;
    uint _BoundaryDelimTorus;
    uint _BoundaryDelimCube;
    float _BoundaryWorldEpsilon;
    float _BoundaryWorldMargin;
    float _WindEmitterClock;
    uint _WindEmitterCount;
    float _ecbpad1;
    float _ecbpad2;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+VolumeCBuffer
// PackingRules = Exact
CBUFFER_START(VolumeCBuffer)
    uint _GridResolution;
    float _AllGroupsMaxParticleVolume;
    float _AllGroupsMaxParticleInterval;
    float _AllGroupsMaxParticleDiameter;
    float _AllGroupsAvgParticleDiameter;
    float _AllGroupsAvgParticleMargin;
    uint _CombinedBoundsIndex;
    uint _VolumeFeatures;
    float _TargetDensityScale;
    float _TargetDensityInfluence;
    float _ScatteringProbeUnitWidth;
    uint _ScatteringProbeSubsteps;
    uint _ScatteringProbeSamplesTheta;
    uint _ScatteringProbeSamplesPhi;
    float _ScatteringProbeOccluderDensity;
    float _ScatteringProbeOccluderMargin;
    uint _WindPropagationSubsteps;
    float _WindPropagationExtinction;
    float _WindPropagationOccluderDensity;
    float _WindPropagationOccluderMargin;
    float _vcbpad1;
CBUFFER_END

// Generated from Unity.DemoTeam.Hair.HairSim+SolverCBuffer
// PackingRules = Exact
CBUFFER_START(SolverCBuffer)
    uint _StrandCount;
    uint _StrandParticleCount;
    uint _StrandParticleOffset;
    uint _StrandParticleStride;
    uint _LODCount;
    float _GroupScale;
    float _GroupMaxParticleVolume;
    float _GroupMaxParticleInterval;
    float _GroupMaxParticleDiameter;
    float _GroupAvgParticleDiameter;
    float _GroupAvgParticleMargin;
    uint _GroupBoundsIndex;
    float _GroupBoundsPadding;
    uint _SolverLODMethod;
    float _SolverLODCeiling;
    float _SolverLODScale;
    float _SolverLODBias;
    uint _SolverFeatures;
    float _DT;
    uint _Substeps;
    uint _Iterations;
    float _Stiffness;
    float _SOR;
    float _LinearDamping;
    float _LinearDampingInterval;
    float _AngularDamping;
    float _AngularDampingInterval;
    float _CellPressure;
    float _CellVelocity;
    float _CellExternal;
    float _GravityScale;
    float _BoundaryFriction;
    float _FTLCorrection;
    float _LocalCurvature;
    float _LocalShape;
    float _LocalShapeBias;
    float _GlobalPosition;
    float _GlobalPositionInterval;
    float _GlobalRotation;
    float _GlobalFadeOffset;
    float _GlobalFadeExtent;
    uint _StagingSubdivision;
    uint _StagingVertexFormat;
    uint _StagingVertexStride;
    uint _StagingStrandVertexCount;
    uint _StagingStrandVertexOffset;
    uint _RenderLODMethod;
    float _RenderLODCeiling;
    float _RenderLODScale;
    float _RenderLODBias;
    float _RenderLODClipThreshold;
    float _scbpad1;
    float _scbpad2;
CBUFFER_END


#endif
