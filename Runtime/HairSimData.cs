using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairSim
	{
		public struct SolverData
		{
			public static SolverExternals<int> s_externalIDs;
			public struct SolverExternals<T>
			{
				public T _RootMeshPosition;				// root mesh vertex buffer w/ positions
				public T _RootMeshTangent;				// root mesh vertex buffer w/ tangents
				public T _RootMeshNormal;				// root mesh vertex buffer w/ normals

				public T _RootResolveDummyRT;			// render target when resolving via vertex stage
			};

			public static SolverBuffers<int> s_bufferIDs;
			public struct SolverBuffers<T>
			{
				public T SolverCBufferRoots;			// constant buffer for root resolve
				public T SolverCBuffer;					// constant buffer

				public T _RootUV;						// xy: root uv
				public T _RootScale;					// xy: root scale (length, diameter normalized to maximum within group), z: tip scale offset, w: tip scale

				public T _RootPositionNext;				// xyz: strand root position, w: -
				public T _RootPositionPrev;				// xyz: ...
				public T _RootPosition;					// xyz: ...
				public T _RootFrameNext;				// quat(xyz,w): strand root material frame where (0,1,0) is tangent to curve
				public T _RootFramePrev;				// quat(xyz,w): ...
				public T _RootFrame;					// quat(xyz,w): ...

				public T _SolverLODStage;				// x: lod index lo, y: lod index hi, z: lod blend fraction, w: lod value/quantity
				public T _SolverLODRange;				// xy: dispatch strand range [begin, end)
				public T _SolverLODDispatch;			// xyz: dispatch args compute, w: dispatch strand count || xyzw: dispatch args draw

				public T _InitialParticleOffset;		// xyz: initial particle offset from strand root, w: initial local accumulated weight (gather)
				public T _InitialParticleFrameDelta;	// quat(xyz,w): initial particle material frame delta
				public T _InitialParticleFrameDelta16;	// xy: compressed initial particle material frame delta

				public T _ParticlePosition;				// xyz: position
				public T _ParticlePositionPrev;			// xyz: ...
				public T _ParticlePositionPrevPrev;		// xyz: ...
				public T _ParticleVelocity;				// xyz: velocity
				public T _ParticleVelocityPrev;			// xyz: ...
				public T _ParticleCorrection;			// xyz: ftl distance correction

				public T _ParticleOptTexCoord;			// xy: optional particle uv
				public T _ParticleOptDiameter;			// x: optional particle diameter

				public T _LODGuideCount;				// x: lod index -> num. guides
				public T _LODGuideIndex;				// x: lod index * strand count + strand index -> guide index
				public T _LODGuideCarry;				// x: lod index * strand count + strand index -> guide carry
				public T _LODGuideReach;				// x: lod index * strand count + strand index -> guide reach (approximate cluster extent)

				public T _StagingVertex;				// xyz: position (uncompressed) || xy: position (compressed)
				public T _StagingVertexPrev;			// xyz: ...
			}

			public static SolverTextures<int> s_textureIDs;
			public struct SolverTextures<T>
			{
				//public T _LODIndexLUT;				// TODO
			}

			public struct SolverKeywords
			{
				public bool LAYOUT_INTERLEAVED;
				public bool LIVE_POSITIONS_3;
				public bool LIVE_POSITIONS_2;
				public bool LIVE_POSITIONS_1;
				public bool LIVE_ROTATIONS_2;
			}

			// data accessible by GPU
			public SolverBuffers<ComputeBuffer> buffers;
			public SolverTextures<Texture> textures;

			// data accessible by GPU + CPU
			public SolverCBufferRoots constantsRoots;
			public SolverCBuffer constants;
			public SolverKeywords keywords;

			// data accessible by CPU, updated via async readback
			public SolverBuffers<HairSimUtility.AsyncReadbackBuffer> buffersReadback;

			// data accessible by CPU
			public HairAsset.MemoryLayout memoryLayout;

			public float initialSumStrandLength;
			public float initialMaxStrandLength;
			public float initialMaxStrandDiameter;
			public float initialAvgStrandDiameter;

			public bool manualBounds;
			public Vector3 manualBoundsMin;
			public Vector3 manualBoundsMax;

			public NativeArray<float> lodThreshold;		// lod index -> relative guide count [0..1]
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true), StructLayout(LayoutKind.Sequential, Pack = 16)]
		public struct SolverCBufferRoots
		{
			// NOTE: explicit end padding to 16 byte boundary required on some platforms, please update counts if modifying

			// 0
			#region Strand Roots (34 floats, 136 bytes)
			public Matrix4x4 _RootMeshMatrix;			// float4x4: root mesh local to world

			// +16
			public Vector4 _RootMeshRotation;			// quat(xyz,w): root mesh rotation
			public Vector4 _RootMeshRotationInv;		// quat(xyz,w): root mesh rotation inverse
			public Vector4 _RootMeshSkinningRotation;	// quat(xyz,w): root mesh skinning rotation

			// +28
			public uint _RootMeshPositionOffset;		// x: position stream offset
			public uint _RootMeshPositionStride;		// x: position stream stride
			public uint _RootMeshTangentOffset;			// x: tangent ...
			public uint _RootMeshTangentStride;			// x: tangent ...
			public uint _RootMeshNormalOffset;			// x: normal ...
			public uint _RootMeshNormalStride;			// x: normal ...

			// +34
			#endregion

			// 34 --> 36 (pad to 16 byte boundary)
			public float _rcbpad1;
			public float _rcbpad2;
			//public float _rcbpad3;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true), StructLayout(LayoutKind.Sequential, Pack = 16)]
		public struct SolverCBuffer
		{
			// NOTE: explicit end padding to 16 byte boundary required on some platforms, please update counts if modifying

			// 0
			#region Strand Geometry (12 floats, 48 bytes)
			public uint _StrandCount;					// number of strands
			public uint _StrandParticleCount;			// number of particles in strand
			public uint _StrandParticleOffset;			// stride in particles between strands
			public uint _StrandParticleStride;			// stride in particles between particles in strand
			public uint _LODCount;						// group lod count

			// +5
			public float _GroupScale;					// group scale
			public float _GroupMaxParticleVolume;		// (already scaled) max particle volume
			public float _GroupMaxParticleInterval;		// (already scaled) max particle interval
			public float _GroupMaxParticleDiameter;		// (already scaled) max particle diameter
			public float _GroupAvgParticleDiameter;		// (already scaled) avg particle diameter
			public float _GroupAvgParticleMargin;		// (already scaled) avg particle margin

			// +10
			public uint _GroupBoundsIndex;				// group bounds index
			public float _GroupBoundsPadding;			// group bounds padding

			// +12
			#endregion

			// 12
			#region Strand Solver (28 floats, 112 bytes)
			public uint _SolverLODMethod;				// solver lod method (for lod selection)
			public float _SolverLODCeiling;				// solver lod ceiling
			public float _SolverLODScale;				// solver lod scale
			public float _SolverLODBias;				// solver lod bias

			// +4
			public uint _SolverFeatures;				// solver feature flags

			// +5
			public float _DT;							// solver time step
			public uint _Substeps;						// solver substeps
			public uint _Iterations;					// constraint iterations
			public float _Stiffness;					// constraint stiffness
			public float _SOR;							// constraint sor factor

			// +10
			public float _LinearDamping;				// linear damping factor (fraction to subtract per interval)
			public float _LinearDampingInterval;		// linear damping interval
			public float _AngularDamping;				// angular damping factor (fraction to subtract per interval)
			public float _AngularDampingInterval;		// angular damping interval
			public float _CellPressure;					// scale factor for cell-sampled pressure impulse [0..1]
			public float _CellVelocity;					// scale factor for cell-sampled velocity impulse [0..1]
			public float _CellExternal;					// scale factor for cell-sampled external impulse [0..1]
			public float _GravityScale;					// scale factor for system gravity [0..1]

			// +18
			public float _BoundaryFriction;				// boundary collision constraint friction
			public float _FTLCorrection;				// follow-the-leader constraint correction
			public float _LocalCurvature;				// local curvature
			public float _LocalShape;					// local shape constraint influence
			public float _LocalShapeBias;				// local shape constraint bias

			// +23
			public float _GlobalPosition;
			public float _GlobalPositionInterval;
			public float _GlobalRotation;
			public float _GlobalFadeOffset;
			public float _GlobalFadeExtent;

			// +28
			#endregion

			// 40
			#region Strand Staging (10 floats, 40 bytes)
			public uint _StagingSubdivision;			// staging subdivision count
			public uint _StagingVertexFormat;			// staging buffer vertex format
			public uint _StagingVertexStride;			// staging buffer vertex stride

			public uint _StagingStrandVertexCount;		// staging strand vertex count
			public uint _StagingStrandVertexOffset;		// staging strand vertex offset

			// +5
			public uint _RenderLODMethod;				// render lod method (for lod selection)
			public float _RenderLODCeiling;				// render lod ceiling
			public float _RenderLODScale;				// render lod scale
			public float _RenderLODBias;				// render lod bias
			public float _RenderLODClipThreshold;       // reclip threshold (min pixel coverage)

			// +10
			#endregion

			// 50 --> 52 (pad to 16 byte boundary)
			public float _scbpad1;
			public float _scbpad2;
			//public float _scbpad3;
		}

		[GenerateHLSL, Flags]
		public enum SolverFeatures
		{
			Boundary			= 1 << 0,
			BoundaryFriction	= 1 << 1,
			Distance			= 1 << 2,
			DistanceLRA			= 1 << 3,
			DistanceFTL			= 1 << 4,
			CurvatureEQ			= 1 << 5,
			CurvatureGEQ		= 1 << 6,
			CurvatureLEQ		= 1 << 7,
			PoseLocalShape		= 1 << 8,
			PoseLocalShapeRWD	= 1 << 9,
			PoseGlobalPosition	= 1 << 10,
			PoseGlobalRotation	= 1 << 11,
		}

		[GenerateHLSL]
		public enum SolverLODStage
		{
			Physics		= 0,
			Rendering	= 1,
			__COUNT
		}

		[GenerateHLSL]
		public enum SolverLODRange
		{
			Solve					= 0,
			Interpolate				= 1,
			InterpolateAdd			= 2,
			InterpolatePromote		= 3,
			Render					= 4,
			__COUNT
		}

		[GenerateHLSL]
		public enum SolverLODDispatch
		{
			Solve					= 0,	// thread group is 64 strands
			SolveGroupParticles		= 1,	// thread group is 16|32|64|128 particles (one group = one strand)
			Interpolate				= 2,	// thread group is 64 strands
			InterpolateAdd			= 3,	// thread group is 64 strands
			InterpolatePromote		= 4,	// thread group is 64 strands
			Staging					= 5,	// thread group is 64 strands
			StagingReentrant		= 6,	// thread group is 64 strands
			Transfer				= 7,	// thread group is 64 particles
			TransferAll				= 8,	// thread group is 64 particles
			RasterPoints			= 9,	// -
			RasterPointsAll			= 10,	// -
			RasterQuads				= 11,	// -
			RasterQuadsAll			= 12,	// -
			__COUNT
		}

		[GenerateHLSL]
		public enum SolverLODSelection
		{
			AutomaticPerGroup	= 0,
			AutomaticPerVolume	= 1,
			Manual				= 2,
		}

		[GenerateHLSL, Flags]
		public enum RenderFeatures
		{
			Tapering			= 1 << 0,
			PerVertexTexCoord	= 1 << 1,
			PerVertexDiameter	= 1 << 2,
		}

		[GenerateHLSL]
		public enum RenderLODSelection
		{
			AutomaticPerSegment	= 0,
			AutomaticPerGroup	= 1,
			AutomaticPerVolume	= 2,
			MatchPhysics		= 3,
			Manual				= 4,
		}

		[GenerateHLSL]
		public enum StagingVertexFormat
		{
			Compressed		= 0,
			Uncompressed	= 1,
		}

		public struct VolumeData
		{
			public static VolumeBuffers<int> s_bufferIDs;
			public struct VolumeBuffers<T>
			{
				public T VolumeCBufferEnvironment;	// constant buffer for environment capture
				public T VolumeCBuffer;				// constant buffer

				public T _LODFrustum;				// array(LODFrustum): observer properties (camera properties and frustum planes)

				public T _BoundaryMatrixNext;		// push per frame, array(float4x4)
				public T _BoundaryMatrixPrevA;		// push per frame, array(float4x4)
				public T _BoundaryMatrixPrevQ;		// push per frame, array(float4)
				public T _BoundaryMatrix;			// compute per step (volume or group), array(float4x4): local to world
				public T _BoundaryMatrixInv;		// compute per step (volume or group), array(float4x4): world to local
				public T _BoundaryMatrixInvStep;	// compute per step (volume or group), array(float4x4): world to previous world
				public T _BoundaryShapeNext;		// push per frame, array(HairBoundary.RuntimeShape.Data)
				public T _BoundaryShapePrevLUT;		// push per frame, array(int)
				public T _BoundaryShapePrev;		// compute per frame, array(HairBoundary.RuntimeShape.Data)
				public T _BoundaryShape;			// compute per step (volume or group), array(HairBoundary.RuntimeShape.Data)

				public T _WindEmitterNext;			// push per frame, array(HairWind.RuntimeEmitter)
				public T _WindEmitterPrevLUT;		// push per frame, array(int)
				public T _WindEmitterPrev;			// compute per frame, array(HairWind.RuntimeEmitter)
				public T _WindEmitter;				// compute per step (volume), array(HairWind.RuntimeEmitter)

				public T _BoundsMinMaxU;			// xyz: bounds min/max (unsigned sortable)
				public T _BoundsPrev;				// array(LODBounds): bounds (center, extent, radius, reach)
				public T _Bounds;					// array(LODBounds): bounds (center, extent, radius, reach)
				public T _BoundsGeometry;			// array(LODGeometry): bounds geometry description (dimensions for coverage)
				public T _BoundsCoverage;			// xy: bounds coverage (unbiased ceiling)

				public T _VolumeLODStage;			// array(VolumeLODGrid): grid properties
				public T _VolumeLODDispatch;		// xyz: num groups, w: num grid cells in one dimension

				public T _AccuWeightBuffer;			// x: fp accumulated weight
				public T _AccuWeight0Buffer;		// x: fp accumulated target weight
				public T _AccuVelocityXBuffer;		// x: fp accumulated x-velocity
				public T _AccuVelocityYBuffer;		// x: ... ... ... .. y-velocity
				public T _AccuVelocityZBuffer;		// x: .. ... ... ... z-velocity
			}

			public static VolumeTextures<int> s_textureIDs;
			public struct VolumeTextures<T>
			{
				public T _AccuWeight;				// x: fp accumulated weight
				public T _AccuWeight0;				// x: fp accumulated target weight
				public T _AccuVelocityX;			// x: fp accumulated x-velocity
				public T _AccuVelocityY;			// x: ... ... ... .. y-velocity
				public T _AccuVelocityZ;			// x: .. ... ... ... z-velocity

				public T _VolumeDensity;			// x: density (cell fraction occupied)
				public T _VolumeDensity0;			// x: density target
				public T _VolumeDensityComp;		// (temp)
				public T _VolumeDensityPreComp;		// (temp)

				public T _VolumeVelocity;			// xyz: velocity, w: accumulated weight
				public T _VolumeDivergence;			// x: velocity divergence + source term
				public T _VolumePressure;			// x: pressure
				public T _VolumePressureNext;		// x: pressure (output of iteration)
				public T _VolumePressureGrad;		// xyz: pressure gradient, w: -

				public T _VolumeScattering;			// xyzw: L1 spherical harmonic
				public T _VolumeImpulse;			// xyz: accumulated external forces, w: -

				public T _BoundarySDF;				// x: signed distance to arbitrary solid
				public T _BoundarySDF_undefined;	// .. (placeholder for when inactive)
			};

			public struct VolumeKeywords
			{
				public bool VOLUME_SPLAT_CLUSTERS;
				public bool VOLUME_SUPPORT_CONTRACTION;
				public bool VOLUME_TARGET_INITIAL_POSE;
				public bool VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES;
			}

			// data accessible by GPU
			public VolumeBuffers<ComputeBuffer> buffers;
			public VolumeTextures<Texture> textures;

			// data accessible by GPU + CPU
			public VolumeCBufferEnvironment constantsEnvironment;
			public VolumeCBuffer constants;
			public VolumeKeywords keywords;

			// data accessible by CPU, updated via async readback
			public VolumeBuffers<HairSimUtility.AsyncReadbackBuffer> buffersReadback;

			// data accessible by CPU
			public NativeArray<int> boundaryPrevHandle;
			public NativeArray<Matrix4x4> boundaryPrevMatrix;
			public int boundaryCount;
			public int boundaryCountDiscard;

			public NativeArray<int> emitterPrevHandle;
			public int emitterCount;
			public int emitterCountDiscard;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true), StructLayout(LayoutKind.Sequential, Pack = 16)]
		public unsafe struct VolumeCBufferEnvironment
		{
			// NOTE: explicit end padding to 16 byte boundary required on some platforms, please update counts if modifying

			// 0
			#region Volume Environment (14 floats, 56 bytes)
			public Vector4 _WorldGravity;

			// +4
			public uint _LODFrustumCount;

			// +5
			public uint _BoundaryDelimDiscrete;
			public uint _BoundaryDelimCapsule;
			public uint _BoundaryDelimSphere;
			public uint _BoundaryDelimTorus;
			public uint _BoundaryDelimCube;
			public float _BoundaryWorldEpsilon;
			public float _BoundaryWorldMargin;

			// +12
			public float _WindEmitterClock;
			public uint _WindEmitterCount;

			// +14
			#endregion

			// 14 --> 16 (pad to 16 byte boundary)
			public float _ecbpad1;
			public float _ecbpad2;
			//public float _ecbpad3;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true), StructLayout(LayoutKind.Sequential, Pack = 16)]
		public struct VolumeCBuffer
		{
			// NOTE: explicit end padding to 16 byte boundary required on some platforms, please update counts if modifying
	
			// 0
			#region Volume Geometry (6 floats, 24 bytes)
			public uint _GridResolution;

			// +1
			public float _AllGroupsMaxParticleVolume;
			public float _AllGroupsMaxParticleInterval;
			public float _AllGroupsMaxParticleDiameter;
			public float _AllGroupsAvgParticleDiameter;
			public float _AllGroupsAvgParticleMargin;

			// +5
			public uint _CombinedBoundsIndex;

			// +6
			#endregion

			// 6
			#region Volume Resolve (13 float, 52 bytes)
			public uint _VolumeFeatures;

			// +1
			public float _TargetDensityScale;
			public float _TargetDensityInfluence;

			// +3
			public float _ScatteringProbeUnitWidth;
			public uint _ScatteringProbeSubsteps;
			public uint _ScatteringProbeSamplesTheta;
			public uint _ScatteringProbeSamplesPhi;
			public float _ScatteringProbeOccluderDensity;
			public float _ScatteringProbeOccluderMargin;

			// +9
			public uint _WindPropagationSubsteps;
			public float _WindPropagationExtinction;
			public float _WindPropagationOccluderDensity;
			public float _WindPropagationOccluderMargin;

			// +13
			#endregion

			// 19 --> 20 (pad to 16 byte boundary)
			public float _vcbpad1;
			//public float _vcbpad2;
			//public float _vcbpad3;
		}

		[GenerateHLSL, Flags]
		public enum VolumeFeatures
		{
			Scattering			= 1 << 0,
			ScatteringFastpath	= 1 << 1,
			Wind				= 1 << 2,
			WindFastpath		= 1 << 3,
		}

		[GenerateHLSL]
		public enum VolumeLODStage
		{
			Resolve			= 0,
			__COUNT
		}

		[GenerateHLSL]
		public enum VolumeLODDispatch
		{
			Resolve			= 0,
			RasterPoints	= 1,
			RasterVectors	= 2,
			__COUNT
		}

		[GenerateHLSL(needAccessors = false)]
		public struct VolumeLODGrid
		{
			public Vector3 volumeWorldMin;
			public Vector3 volumeWorldMax;
			public Vector3 volumeCellCount;
			public float volumeCellRadius;
		}
	}
}
