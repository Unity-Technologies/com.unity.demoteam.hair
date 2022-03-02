using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairSim
	{
		public struct SolverData
		{
			public SolverKeywords keywords;

			public SolverCBuffer cbuffer;
			public ComputeBuffer cbufferStorage;		// constant buffer storage

			public ComputeBuffer rootUV;				// xy: strand root uv
			public ComputeBuffer rootScale;				// x: relative strand length [0..1] (to maximum in group)
			public ComputeBuffer rootPosition;			// xyz: strand root position, w: -
			public ComputeBuffer rootPositionPrev;		// ...
			public ComputeBuffer rootDirection;			// xyz: strand root direction, w: -
			public ComputeBuffer rootDirectionPrev;		// ...
			public ComputeBuffer rootFrame;				// quat(xyz,w): strand root material frame where (0,1,0) is tangent
			public ComputeBuffer rootFramePrev;			// ...

			public ComputeBuffer substepRootPosition;	// substep root data (pre-step blend)
			public ComputeBuffer substepRootDirection;	// ...
			public ComputeBuffer substepRootFrame;		// ...

			public ComputeBuffer initialRootFrame;			// quat(xyz,w): initial strand root material frame
			public ComputeBuffer initialParticleOffset;		// xyz: initial particle offset from strand root, w: -
			public ComputeBuffer initialParticleFrameDelta; // quat(xyz,w): initial particle material frame delta

			public float initialMaxParticleInterval;
			public float initialMaxParticleDiameter;
			public float initialTotalLength;

			public ComputeBuffer particlePosition;		// xyz: position, w: initial local accumulated weight (gather)
			public ComputeBuffer particlePositionPrev;		// ...
			public ComputeBuffer particlePositionPrevPrev;	// ...
			public ComputeBuffer particlePositionCorr;	// xyz: ftl correction, w: -
			public ComputeBuffer particleVelocity;		// xyz: velocity, w: splatting weight
			public ComputeBuffer particleVelocityPrev;  // xyz: velocity, w: splatting weight

			public ComputeBuffer lodGuideCount;			// n: lod index -> num. guides
			public ComputeBuffer lodGuideIndex;			// i: lod index * strand count + strand index -> guide index
			public ComputeBuffer lodGuideCarry;			// f: lod index * strand count + strand index -> guide carry

			public NativeArray<int> lodGuideCountCPU;	// n: lod index -> num. guides
			public NativeArray<float> lodThreshold;		// f: lod index -> relative guide count [0..1] (to maximum lod in group)

			public ComputeBuffer stagingPosition;		// xy: encoded position | xyz: position
			public ComputeBuffer stagingPositionPrev;   // ...

			public HairAsset.MemoryLayout memoryLayout;
		}

		public struct SolverKeywords
		{
			public bool LAYOUT_INTERLEAVED;
			public bool APPLY_VOLUME_IMPULSE;
			public bool ENABLE_BOUNDARY;
			public bool ENABLE_BOUNDARY_FRICTION;
			public bool ENABLE_DISTANCE;
			public bool ENABLE_DISTANCE_LRA;
			public bool ENABLE_DISTANCE_FTL;
			public bool ENABLE_CURVATURE_EQ;
			public bool ENABLE_CURVATURE_GEQ;
			public bool ENABLE_CURVATURE_LEQ;
			public bool ENABLE_POSE_LOCAL_BEND_TWIST;
			public bool ENABLE_POSE_GLOBAL_POSITION;
			public bool ENABLE_POSE_GLOBAL_ROTATION;
			public bool STAGING_COMPRESSION;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
		public struct SolverCBuffer
		{
			// 0
			public Matrix4x4 _LocalToWorld;				// root mesh vertex transform
			public Matrix4x4 _LocalToWorldInvT;			// ...

			// 32
			public Vector4 _WorldRotation;				// quat(xyz,w): primary skinning bone rotation
			public Vector4 _WorldGravity;				// xyz: gravity vector, w: -

			public Vector4 _StagingOriginExtent;		// xyz: origin, w: scale
			public Vector4 _StagingOriginExtentPrev;	// ...

			// 48
			public uint _StrandCount;					// number of strands
			public uint _StrandParticleCount;			// number of particles per strand
			public uint _StrandParticleOffset;			// offset in particles to reach the ith strand
			public uint _StrandParticleStride;			// stride in particles between strand particles

			public uint _SolverStrandCount;				// number of strands touched by solver

			public float _GroupScale;                   // group scale
			public float _GroupMaxParticleInterval;     // (scaled) max particle interval
			public float _GroupMaxParticleDiameter;     // (scaled) max particle diameter
			public float _GroupMaxParticleFootprint;    // (scaled) max particle footprint

			public uint _LODIndexLo;					// lod index (lower detail in blend)
			public uint _LODIndexHi;					// lod index (higher detail in blend)
			public float _LODBlendFraction;				// lod blend fraction (lo -> hi)

			public uint _StagingSubdivision;			// staging segment subdivision samples
			public uint _StagingVertexCount;			// staging strand vertex count
			public uint _StagingVertexOffset;			// staging strand vertex offset

			// 63
			public float _DT;
			public uint _Iterations;
			public float _Stiffness;
			public float _SOR;

			public float _Damping;
			public float _DampingInterval;
			public float _AngularDamping;
			public float _AngularDampingInterval;
			public float _CellPressure;
			public float _CellVelocity;

			public float _BoundaryFriction;
			public float _FTLDamping;
			public float _LocalCurvature;
			public float _LocalShape;

			// 77
			public float _GlobalPosition;
			public float _GlobalPositionInterval;
			public float _GlobalRotation;
			public float _GlobalFadeOffset;
			public float _GlobalFadeExtent;

			// 82 --> 84 (16 byte alignment)
			public float _scbpad1;
			public float _scbpad2;

			// NOTE: explicit padding to 16 byte alignment required on some platforms, please update if modifying
		}

		/*
		public static VolumeBuffers<uint> s_ID_VolumeBuffer;
		public struct VolumeBuffers<T>
		{
			//TODO
		}

		public static VolumeTextures<uint> s_ID_VolumeTexture;
		public struct VolumeTextures<T>
		{
			//TODO
		}
		*/

		public struct VolumeData
		{
			public VolumeKeywords keywords;

			public VolumeCBuffer cbuffer;
			public ComputeBuffer cbufferStorage;		// constant buffer storage

			public RenderTexture accuWeight;			// x: fp accumulated weight
			public RenderTexture accuWeight0;			// x: fp accumulated target weight
			public RenderTexture accuVelocityX;			// x: fp accumulated x-velocity
			public RenderTexture accuVelocityY;			// x: ... ... ... .. y-velocity
			public RenderTexture accuVelocityZ;			// x: .. ... ... ... z-velocity

			public ComputeBuffer accuWeightBuffer;		// x: fp accumulated weight
			public ComputeBuffer accuWeight0Buffer;		// x: fp accumulated target weight
			public ComputeBuffer accuVelocityXBuffer;	// x: fp accumulated x-velocity
			public ComputeBuffer accuVelocityYBuffer;	// x: ... ... ... .. y-velocity
			public ComputeBuffer accuVelocityZBuffer;	// x: .. ... ... ... z-velocity

			public RenderTexture volumeDensity;			// x: density (as fraction occupied)
			public RenderTexture volumeDensity0;		// x: density target
			public RenderTexture volumeVelocity;		// xyz: velocity, w: accumulated weight

			public RenderTexture volumeDivergence;		// x: velocity divergence + source term
			public RenderTexture volumePressure;		// x: pressure
			public RenderTexture volumePressureNext;	// x: pressure (output of iteration)
			public RenderTexture volumePressureGrad;	// xyz: pressure gradient, w: -

			public RenderTexture volumeStrandCountProbe;

			public RenderTexture boundarySDF;			// x: signed distance to arbitrary solid
			public RenderTexture boundarySDF_undefined;	// .. (placeholder for when inactive)

			public ComputeBuffer boundaryShape;			// arr(HairBoundary.RuntimeShape.Data)
			public ComputeBuffer boundaryMatrix;		// arr(float4x4): local to world
			public ComputeBuffer boundaryMatrixInv;		// arr(float4x4): world to local
			public ComputeBuffer boundaryMatrixW2PrevW;	// arr(float4x4): world to previous world

			public NativeArray<HairBoundary.RuntimeTransform> boundaryPrevXform;
			public int boundaryPrevCount;
			public int boundaryPrevCountDiscard;
			public int boundaryPrevCountUnknown;
		}

		public struct VolumeKeywords
		{
			public bool VOLUME_SUPPORT_CONTRACTION;
			public bool VOLUME_SPLAT_CLUSTERS;
			public bool VOLUME_TARGET_INITIAL_POSE;
			public bool VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
		public struct VolumeCBuffer
		{
			/*
				_VolumeCells = (3, 3, 3)

				          +---+---+---Q
				     +---+---+---+    |
				+---+---+---+    | ---+
				|   |   |   | ---+    |
				+---+---+---+    | ---+
				|   |   |   | ---+    |
				+---+---+---+    | ---+
				|   |   |   | ---+
				P---+---+---+

				_VolumeWorldMin = P
				_VolumeWorldMax = Q
			*/

			// 0
			public Vector4 _VolumeCells;
			public Vector4 _VolumeWorldMin;
			public Vector4 _VolumeWorldMax;

			// 12
			public float _AllGroupsDebugWeight;
			public float _AllGroupsMaxParticleVolume;

			public float _TargetDensityFactor;

			// 15
			public uint _BoundaryCountDiscrete;
			public uint _BoundaryCountCapsule;
			public uint _BoundaryCountSphere;
			public uint _BoundaryCountTorus;
			public uint _BoundaryCountCube;

			public float _BoundaryWorldEpsilon;
			public float _BoundaryWorldMargin;

			// 22
			public uint _StrandCountPhi;
			public uint _StrandCountTheta;
			public uint _StrandCountSubstep;
			public float _StrandCountDiameter;

			// 26 --> 28 (16 byte alignment)
			public float _vcbpad1;
			public float _vcbpad2;

			// NOTE: explicit padding to 16 byte alignment required on some platforms, please update if modifying
		}
	}
}
