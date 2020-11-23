using System.Runtime.InteropServices;
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

			public ComputeBuffer length;
			public ComputeBuffer rootPosition;
			public ComputeBuffer rootDirection;

			public ComputeBuffer particlePosition;
			public ComputeBuffer particlePositionPrev;
			public ComputeBuffer particlePositionCorr;
			public ComputeBuffer particlePositionPose;
			public ComputeBuffer particleVelocity;
			public ComputeBuffer particleVelocityPrev;

			public GroomAsset.MemoryLayout memoryLayout;

			public SolverKeywords<bool> keywords;
		}

		public struct VolumeData
		{
			public VolumeCBuffer cbuffer;

			public RenderTexture accuDensity;
			public RenderTexture accuVelocityX;
			public RenderTexture accuVelocityY;
			public RenderTexture accuVelocityZ;

			public RenderTexture volumeDensity;
			public RenderTexture volumeVelocity;
			public RenderTexture volumeDivergence;

			public RenderTexture volumePressure;
			public RenderTexture volumePressureNext;
			public RenderTexture volumePressureGrad;

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

			public uint _StrandCount;
			public uint _StrandParticleCount;
			public float _StrandParticleInterval;
			public float _StrandParticleVolume;
			public float _StrandParticleContrib;//TODO remove

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
			public float _ShapeStiffness;
			public float _ShapeFalloff;
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

			public float _TargetDensityFactor;

			public int _BoundaryCapsuleCount;
			public int _BoundarySphereCount;
			public int _BoundaryTorusCount;
		}
	}
}
