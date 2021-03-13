using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

namespace Unity.DemoTeam.Hair
{
	public partial class HairSim
	{
		public struct SolverData
		{
			public SolverCBuffer cbuffer;
			public SolverKeywords keywords;

			public ComputeBuffer rootScale;				// x: relative strand length [0..1] (to group maximum)
			public ComputeBuffer rootPosition;			// xyz: strand root position, w: -
			public ComputeBuffer rootDirection;			// xyz: strand root direction, w: -
			public ComputeBuffer rootFrame;				// quat(xyz,w): strand root material frame where (0,1,0) is tangent

			public ComputeBuffer initialRootFrame;			// quat(xyz,w): initial strand root material frame
			public ComputeBuffer initialParticleOffset;		// xyz: initial particle offset from strand root, w: -
			public ComputeBuffer initialParticleFrameDelta;	// quat(xyz,w): initial particle material frame delta

			public ComputeBuffer particlePosition;		// xyz: position, w: initial local accumulated weight (gather)
			public ComputeBuffer particlePositionPrev;	// xyz: position, w: initial local accumulated weight (gather)
			public ComputeBuffer particlePositionCorr;	// xyz: ftl correction, w: -
			public ComputeBuffer particleVelocity;		// xyz: velocity, w: weight
			public ComputeBuffer particleVelocityPrev;	// xyz: velocity, w: weight

			public HairAsset.MemoryLayout memoryLayout;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
		public struct SolverCBuffer
		{
			public Matrix4x4 _LocalToWorld;
			public Matrix4x4 _LocalToWorldInvT;
			public Vector4 _WorldRotation;

			public uint _StrandCount;                   // group strand count
			public uint _StrandParticleCount;           // group strand particle count
			public float _StrandMaxParticleInterval;    // group max particle interval
			public float _StrandMaxParticleWeight;      // group max particle weight (relative to all groups within volume)

			public float _StrandScale;                  // global scale factor

			public float _DT;
			public uint _Iterations;
			public float _Stiffness;
			public float _SOR;

			public float _CellPressure;
			public float _CellVelocity;
			public float _Damping;
			public float _DampingInterval;
			public float _Gravity;

			public float _BoundaryFriction;
			public float _FTLDamping;
			public float _LocalCurvature;
			public float _LocalShape;

			public float _GlobalPosition;
			public float _GlobalPositionInterval;
			public float _GlobalRotation;
			public float _GlobalFadeOffset;
			public float _GlobalFadeExtent;
		}

		public struct SolverKeywords
		{
			public bool LAYOUT_INTERLEAVED;
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
		}

		public struct VolumeData
		{
			public VolumeCBuffer cbuffer;
			public VolumeKeywords keywords;

			public RenderTexture accuWeight;            // x: fp accumulated weight
			public RenderTexture accuWeight0;           // x: fp accumulated target weight
			public RenderTexture accuVelocityX;         // x: fp accumulated x-velocity
			public RenderTexture accuVelocityY;         // x: ... ... ... .. y-velocity
			public RenderTexture accuVelocityZ;         // x: .. ... ... ... z-velocity

			public RenderTexture volumeDensity;         // x: density (as fraction occupied)
			public RenderTexture volumeDensity0;        // x: density target
			public RenderTexture volumeVelocity;        // xyz: velocity, w: accumulated weight
			public RenderTexture volumeDivergence;      // x: velocity divergence + source term

			public RenderTexture volumePressure;        // x: pressure
			public RenderTexture volumePressureNext;    // x: pressure (output of iteration)
			public RenderTexture volumePressureGrad;    // xyz: pressure gradient, w: -

			public float allGroupsMaxParticleInterval;
			//public float allGroupsMaxStrandDiameter;
			//public float allGroupsMaxStrandLength;

			public RenderTexture boundarySDFDummy;      // (placeholder for when inactive)
			public RenderTexture boundarySDF;           // x: signed distance to arbitrary solid

			public ComputeBuffer boundaryShape;         // arr(HairBoundary.RuntimeShape.Data)
			public ComputeBuffer boundaryMatrix;        // arr(float4x4): local to world
			public ComputeBuffer boundaryMatrixInv;     // arr(float4x4): world to local
			public ComputeBuffer boundaryMatrixW2PrevW; // arr(float4x4): world to previous world

			public NativeArray<HairBoundary.RuntimeTransform> boundaryPrevXform;
			public int boundaryPrevCount;
			public int boundaryPrevCountDiscard;
			public int boundaryPrevCountUnknown;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
		public struct VolumeCBuffer
		{
			public Vector3 _VolumeCells;
			public uint __pad1;
			public Vector3 _VolumeWorldMin;
			public uint __pad2;
			public Vector3 _VolumeWorldMax;
			public uint __pad3;
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

			public float _ResolveUnitVolume;
			public float _ResolveUnitDebugWidth;

			public float _TargetDensityFactor;

			public int _BoundaryCountDiscrete;
			public int _BoundaryCountCapsule;
			public int _BoundaryCountSphere;
			public int _BoundaryCountTorus;
			public int _BoundaryCountCube;

			public float _BoundaryWorldEpsilon;
			public float _BoundaryWorldMargin;
		}

		public struct VolumeKeywords
		{
			public bool VOLUME_SUPPORT_CONTRACTION;
			public bool VOLUME_TARGET_INITIAL_POSE;
			public bool VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES;
		}
	}
}
