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

			public ComputeBuffer rootScale;     // strand length (relative to group maximum)
			public ComputeBuffer rootPosition;  // strand root position
			public ComputeBuffer rootDirection; // strand root direction
			public ComputeBuffer rootFrame;     // strand root material frame

			public ComputeBuffer initialRootFrame;
			public ComputeBuffer initialParticleOffset;
			public ComputeBuffer initialParticleFrameDelta;

			public ComputeBuffer particlePosition;
			public ComputeBuffer particlePositionPrev;
			public ComputeBuffer particlePositionCorr;
			public ComputeBuffer particleVelocity;
			public ComputeBuffer particleVelocityPrev;

			public GroomAsset.MemoryLayout memoryLayout;

			public SolverKeywords<bool> keywords;
		}

		public struct VolumeData
		{
			public VolumeCBuffer cbuffer;

			public RenderTexture accuWeight;
			public RenderTexture accuWeight0;
			public RenderTexture accuVelocityX;
			public RenderTexture accuVelocityY;
			public RenderTexture accuVelocityZ;

			public RenderTexture volumeDensity;
			public RenderTexture volumeDensity0;
			public RenderTexture volumeVelocity;
			public RenderTexture volumeDivergence;

			public RenderTexture volumePressure;
			public RenderTexture volumePressureNext;
			public RenderTexture volumePressureGrad;

			public float allGroupsMaxParticleInterval;

			public ComputeBuffer boundaryPack;
			public ComputeBuffer boundaryMatrix;
			public ComputeBuffer boundaryMatrixInv;
			public ComputeBuffer boundaryMatrixW2PrevW;

			public struct BoundaryInfo
			{
				public int instanceID;
				public Matrix4x4 matrix;
			}

			public NativeArray<BoundaryInfo> boundaryPrev;
			public int boundaryPrevCount;
			public int boundaryPrevCountDiscarded;

			public VolumeKeywords<bool> keywords;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
		public struct SolverCBuffer
		{
			public Matrix4x4 _LocalToWorld;
			public Matrix4x4 _LocalToWorldInvT;
			public Vector4 _WorldRotation;

			public uint _StrandCount;                // group strand count
			public uint _StrandParticleCount;        // group strand particle count
			public float _StrandMaxParticleInterval; // group max particle interval
			public float _StrandMaxParticleWeight;   // group max particle weight (relative to all groups within volume)

			public float _StrandScale;               // global scale factor

			public float _DT;
			public uint _Iterations;
			public float _Stiffness;
			public float _SOR;

			public float _CellPressure;
			public float _CellVelocity;
			public float _Damping;
			public float _Gravity;

			public float _DampingFTL;
			public float _BoundaryFriction;
			public float _BendingCurvature;

			public float _GlobalShape;
			public float _GlobalShapeFalloff;
			public float _LocalShape;
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

			public int _BoundaryCapsuleCount;
			public int _BoundarySphereCount;
			public int _BoundaryTorusCount;
		}
	}
}
