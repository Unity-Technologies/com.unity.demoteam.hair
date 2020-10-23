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
			public SolverParams cbuffer;

			public ComputeBuffer length;
			public ComputeBuffer rootPosition;
			public ComputeBuffer rootDirection;

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
			public VolumeParams cbuffer;

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

			public NativeArray<Matrix4x4> boundaryMatrixPrev;
			public Hash128 boundaryHash;

			public VolumeKeywords<bool> keywords;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
		public struct SolverParams
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
			public float _Relaxation;

			public float _Gravity;
			public float _Damping;
			public float _VolumePressureScale;
			public float _VolumeFrictionScale;

			public float _DampingFTL;
			public float _BoundaryFriction;
			public float _BendingCurvature;
		}

		[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
		public struct VolumeParams
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

			public float _PressureFromVelocity;
			public float _PressureFromDensity;

			public int _BoundaryCapsuleCount;
			public int _BoundarySphereCount;
			public int _BoundaryTorusCount;
		}
	}
}
