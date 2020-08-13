#pragma warning disable 0649 // some fields are assigned via reflection

#define LAYOUT_INTERLEAVED
//#define CMDBUFFER_SET_DATA

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

using Xorshift = Unity.Mathematics.Random;

namespace Unity.DemoTeam.Hair
{
	using static HairSimUtility;

	[ExecuteInEditMode]
	public class HairSim : MonoBehaviour
	{
		#region static instances
		public static HashSet<HairSim> instances = new HashSet<HairSim>();
		private void OnEnable() { instances.Add(this); }
		private void OnDisable() { instances.Remove(this); ReleaseBuffers(); ReleaseVolumes(); strandsActive = StrandConfiguration.none; }
		#endregion

		#region static initialization
		static void InitializeStaticFields<T>(Type type, Func<string, T> construct)
		{
			foreach (var field in type.GetFields())
			{
				field.SetValue(null, construct(field.Name));
			}
		}

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#else
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
		static void InitializeHairSim()
		{
			Debug.Log("InitializeHairSim");
			InitializeStaticFields(typeof(MarkersCPU), (string s) => new ProfilerMarker("HairSim." + s.Replace('_', '.')));
			InitializeStaticFields(typeof(MarkersGPU), (string s) => new ProfilingSampler("HairSim." + s.Replace('_', '.')));
			InitializeStaticFields(typeof(UniformIDs), (string s) => Shader.PropertyToID(s));
		}
		#endregion

		#region static profile markers
		static class MarkersCPU
		{
			public static ProfilerMarker GetStrandRoots;
		}

		static class MarkersGPU
		{
			public static ProfilingSampler Init;
			public static ProfilingSampler Step;
			public static ProfilingSampler Volume;
			public static ProfilingSampler Volume_0_Clear;
			public static ProfilingSampler Volume_1_Splat;
			public static ProfilingSampler Volume_1_SplatDensity;
			public static ProfilingSampler Volume_1_SplatVelocityXYZ;
			public static ProfilingSampler Volume_1_SplatByRasterization;
			public static ProfilingSampler Volume_1_SplatByRasterizationNoGS;
			public static ProfilingSampler Volume_2_Resolve;
			public static ProfilingSampler Volume_2_ResolveFromRasterization;
			public static ProfilingSampler Volume_3_Divergence;
			public static ProfilingSampler Volume_4_PressureEOS;
			public static ProfilingSampler Volume_5_PressureSolve;
			public static ProfilingSampler Volume_6_PressureGradient;
			public static ProfilingSampler Draw;
		}
		#endregion

		#region static shader literals
		static class UniformIDs
		{
			// uniform buffers
			//public static int _SolverParams;
			//public static int _VolumeParams;
			//public static int _RenderParams;

			// storage buffers
			//TODO

			// textures
			//TODO

			// strand params
			public static int _StrandCount;
			public static int _StrandParticleCount;
			public static int _StrandParticleInterval;
			public static int _StrandParticleVolume;
			public static int _StrandParticleContrib;

			// strand buffers
			public static int _RootPosition;
			public static int _RootTangent;

			// solver params
			public static int _LocalToWorld;
			public static int _LocalToWorldInvT;

			public static int _DT;
			public static int _Iterations;
			public static int _Stiffness;
			public static int _Relaxation;
			public static int _Damping;
			public static int _Gravity;
			public static int _Repulsion;
			public static int _Friction;
			public static int _BendingCurvature;
			public static int _BendingStiffness;
			public static int _DampingFTL;

			// solver buffers
			public static int _ParticlePosition;
			public static int _ParticleVelocity;
			public static int _ParticlePositionPrev;
			public static int _ParticleVelocityPrev;
			public static int _ParticlePositionCorr;

			// boundary params
			public static int _BoundaryCapsuleCount;
			public static int _BoundarySphereCount;
			public static int _BoundaryTorusCount;

			// boundary buffers
			public static int _BoundaryCapsule;
			public static int _BoundarySphere;
			public static int _BoundaryTorus;
			public static int _BoundaryPack;

			public static int _BoundaryMatrix;
			public static int _BoundaryMatrixInv;
			public static int _BoundaryMatrixW2PrevW;

			// volume params
			public static int _VolumeCells;
			public static int _VolumeWorldMin;
			public static int _VolumeWorldMax;

			// volume buffers
			public static int _AccuDensity;
			public static int _AccuVelocityX;
			public static int _AccuVelocityY;
			public static int _AccuVelocityZ;

			public static int _VolumeDensity;
			public static int _VolumeVelocity;
			public static int _VolumeDivergence;

			public static int _VolumePressure;
			public static int _VolumePressurePrev;
			public static int _VolumePressureGrad;

			// debug params
			public static int _DebugSliceAxis;
			public static int _DebugSliceOffset;
			public static int _DebugSliceDivider;
		}
		#endregion

		const int THREAD_GROUP_SIZE = 64;

		const int MAX_STRANDS = 64000;
		const int MAX_STRAND_PARTICLES = 128;
		const int MAX_BOUNDARIES = 8;

		[Serializable]
		public struct StrandConfiguration
		{
			public enum Style
			{
				Curtain,
				Brush,
				Cap,
				StratifiedCurtain,
			}

			public Style style;
			[Range(64, MAX_STRANDS)]
			public int strandCount;
			[Range(3, MAX_STRAND_PARTICLES)]
			public int strandParticleCount;
			[Range(0.001f, 5.0f), Tooltip("Strand length in meters")]
			public float strandLength;
			[Range(0.070f, 100.0f), Tooltip("Strand diameter in millimeters")]
			public float strandDiameter;
			[Range(0.0f, 9.0f)]
			public float strandParticleContrib;// TODO remove

			public static readonly StrandConfiguration none = new StrandConfiguration();
			public static readonly StrandConfiguration basic = new StrandConfiguration()
			{
				style = Style.Curtain,
				strandCount = 1024,
				strandParticleCount = 32,
				strandLength = 0.5f,
				strandDiameter = 0.125f,
				strandParticleContrib = 1.0f,// TODO remove
			};
		}

		[Serializable]
		public struct SolverConfiguration
		{
			public enum Method : uint
			{
				GaussSeidelReference = 0,
				GaussSeidel = 1,
				Jacobi = 2,
			}

			public Method method;
			[Range(1, 100)]
			public int iterations;
			[Range(0.0f, 1.0f)]
			public float stiffness;
			[Range(1.0f, 2.0f)]
			public float relaxation;
			[Range(0.0f, 1.0f)]
			public float damping;
			[Range(-1.0f, 1.0f)]
			public float gravity;
			[Range(0.0f, 5.0f)]
			public float repulsion;
			[Range(0.0f, 1.0f)]
			public float friction;

			[Range(0.0f, 1.0f)]
			public float bendingCurvature;
			[Range(0.0f, 1.0f)]
			public float dampingFTL;

			public static readonly SolverConfiguration none = new SolverConfiguration();
			public static readonly SolverConfiguration basic = new SolverConfiguration()
			{
				method = Method.GaussSeidelReference,
				iterations = 3,
				stiffness = 1.0f,
				relaxation = 1.0f,
				damping = 0.0f,
				gravity = 1.0f,
				repulsion = 0.0f,
				friction = 0.0f,

				bendingCurvature = 0.0f,
				dampingFTL = 0.8f,
			};
		}

		[Serializable]
		public struct VolumeConfiguration
		{
			public enum SplatMethod : uint
			{
				None,
				Compute,
				ComputeSplit,
				Rasterization,
				RasterizationNoGS,
			}

			public SplatMethod volumeSplatMethod;
			[Range(8, 160)]
			public int volumeResolution;
			[Range(-1.0f, 1.0f)]
			public float volumeOffsetX;
			[Range(-1.0f, 1.0f)]
			public float volumeOffsetY;
			[Range(-1.0f, 1.0f)]
			public float volumeOffsetZ;
			[Range(0, 100)]
			public int volumePressureIterations;

			public static readonly VolumeConfiguration none = new VolumeConfiguration();
			public static readonly VolumeConfiguration basic = new VolumeConfiguration()
			{
				volumeSplatMethod = SplatMethod.Compute,
				volumeResolution = 64,
				volumeOffsetX = 0.0f,
				volumeOffsetY = 0.0f,
				volumeOffsetZ = 0.0f,
				volumePressureIterations = 0,
			};
		}

		[Serializable]
		public struct DebugConfiguration
		{
			public bool drawParticles;
			public bool drawStrands;
			public bool drawDensity;
			public bool drawSliceX;
			public bool drawSliceY;
			public bool drawSliceZ;

			[Range(0.0f, 1.0f)]
			public float drawSliceOffsetX;
			[Range(0.0f, 1.0f)]
			public float drawSliceOffsetY;
			[Range(0.0f, 1.0f)]
			public float drawSliceOffsetZ;
			[Range(0.0f, 4.0f)]
			public float drawSliceDivider;

			public static readonly DebugConfiguration none = new DebugConfiguration();
			public static readonly DebugConfiguration basic = new DebugConfiguration()
			{
				drawParticles = false,
				drawStrands = true,
				drawDensity = false,
				drawSliceX = false,
				drawSliceY = false,
				drawSliceZ = false,
				drawSliceOffsetX = 0.4f,
				drawSliceOffsetY = 0.4f,
				drawSliceOffsetZ = 0.4f,
				drawSliceDivider = 0.0f,
			};
		}

		public List<HairSimBoundary> boundaries = new List<HairSimBoundary>(MAX_BOUNDARIES);
		private StrandConfiguration strandsActive = StrandConfiguration.none;
		public StrandConfiguration strands = StrandConfiguration.basic;
		public SolverConfiguration solver = SolverConfiguration.basic;
		public VolumeConfiguration volume = VolumeConfiguration.basic;
		public DebugConfiguration debug = DebugConfiguration.basic;

		[HideInInspector] public ComputeShader computeSolver;
		[HideInInspector] public ComputeShader computeVolume;
		[HideInInspector] public Material computeVolumeRaster;
		private MaterialPropertyBlock computeVolumeRasterMBP;

		[HideInInspector] public Material debugDraw;
		private MaterialPropertyBlock debugDrawMPB;

		private int kernelUpdatePosition;
		private int kernelSolveConstraints_GaussSeidelReference;
		private int kernelSolveConstraints_GaussSeidel;
		private int kernelSolveConstraints_Jacobi_16;
		private int kernelSolveConstraints_Jacobi_32;
		private int kernelSolveConstraints_Jacobi_64;
		private int kernelSolveConstraints_Jacobi_128;
		private int kernelUpdateVelocity;

		private int kernelVolumeClear;
		private int kernelVolumeSplat;
		private int kernelVolumeSplatDensity;
		private int kernelVolumeSplatVelocityX;
		private int kernelVolumeSplatVelocityY;
		private int kernelVolumeSplatVelocityZ;
		private int kernelVolumeResolve;
		private int kernelVolumeResolveFromRasterization;
		private int kernelVolumeDivergence;
		private int kernelVolumePressureEOS;
		private int kernelVolumePressureSolve;
		private int kernelVolumePressureGradient;

		private ComputeBuffer rootPosition;
		private ComputeBuffer rootTangent;

		private ComputeBuffer particlePosition;
		private ComputeBuffer particlePositionPrev;
		private ComputeBuffer particlePositionCorr;
		private ComputeBuffer particleVelocity;
		private ComputeBuffer particleVelocityPrev;

		private int boundaryCapsuleCount;
		private int boundarySphereCount;
		private int boundaryTorusCount;

		private ComputeBuffer boundaryPack;
		private ComputeBuffer boundaryMatrix;
		private ComputeBuffer boundaryMatrixInv;
		private ComputeBuffer boundaryMatrixW2PrevW;

		private NativeArray<Matrix4x4> boundaryMatrixPrev;
		private Hash128 boundaryMatrixPrevHash;

		private Vector3 volumeCells;
		private Vector3 volumeWorldMin;
		private Vector3 volumeWorldMax;

		private RenderTexture accuDensity;
		private RenderTexture accuVelocityX;
		private RenderTexture accuVelocityY;
		private RenderTexture accuVelocityZ;

		private RenderTexture volumeDensity;
		private RenderTexture volumeVelocity;
		private RenderTexture volumeDivergence;

		private RenderTexture volumePressure;
		private RenderTexture volumePressurePrev;
		private RenderTexture volumePressureGrad;

		private void ValidateConfiguration()
		{
			switch (solver.method)
			{
				case SolverConfiguration.Method.GaussSeidelReference:
				case SolverConfiguration.Method.GaussSeidel:
					{
						strands.strandCount = THREAD_GROUP_SIZE * (strands.strandCount / THREAD_GROUP_SIZE);
						strands.strandCount = Mathf.Max(THREAD_GROUP_SIZE, strands.strandCount);
					}
					break;

				case SolverConfiguration.Method.Jacobi:
					{
						unsafe
						{
							int* jacobiGroupSizes = stackalloc int[4]
							{
								16,
								32,
								64,
								128,
							};

							int n = Mathf.Clamp(strands.strandParticleCount, jacobiGroupSizes[0], jacobiGroupSizes[3]);
							for (int i = 1; i != 4; i++)
							{
								int min = jacobiGroupSizes[i - 1];
								int max = jacobiGroupSizes[i];

								if (n >= min && n <= max)
								{
									n = ((max - n) < (n - min)) ? max : min;
								}
							}

							strands.strandCount = Mathf.Max(64, strands.strandCount);
							strands.strandParticleCount = n;
						}
					}
					break;
			}

			if (boundaries.Count > MAX_BOUNDARIES)
			{
				boundaries.RemoveRange(MAX_BOUNDARIES, boundaries.Count - MAX_BOUNDARIES);
				boundaries.TrimExcess();
			}

			volume.volumeResolution = (Mathf.Max(8, volume.volumeResolution) / 8) * 8;
		}

		private void OnValidate()
		{
			ValidateConfiguration();
		}

		private void OnDrawGizmos()
		{
			if (debug.drawDensity || debug.drawSliceX || debug.drawSliceY || debug.drawSliceZ)
			{
				Gizmos.color = Color.Lerp(Color.black, Color.clear, 0.5f);
				Gizmos.DrawWireCube(GetVolumeCenter(), 2.0f * GetVolumeExtent());
			}
		}

		private bool CreateBuffers()
		{
			unsafe
			{
				bool changed = false;

				int particleCount = strands.strandCount * strands.strandParticleCount;
				int particleStride = sizeof(Vector4);

				changed |= CreateBuffer(ref rootPosition, "RootPosition", strands.strandCount, particleStride);
				changed |= CreateBuffer(ref rootTangent, "RootTangent", strands.strandCount, particleStride);

				changed |= CreateBuffer(ref particlePosition, "ParticlePosition_0", particleCount, particleStride);
				changed |= CreateBuffer(ref particlePositionPrev, "ParticlePosition_1", particleCount, particleStride);
				changed |= CreateBuffer(ref particlePositionCorr, "ParticlePositionCorr", particleCount, particleStride);

				changed |= CreateBuffer(ref particleVelocity, "ParticleVelocity_0", particleCount, particleStride);
				changed |= CreateBuffer(ref particleVelocityPrev, "ParticleVelocity_1", particleCount, particleStride);

				changed |= CreateBuffer(ref boundaryPack, "BoundaryPack", MAX_BOUNDARIES, sizeof(HairSimBoundary.BoundaryPack));
				changed |= CreateBuffer(ref boundaryMatrix, "BoundaryMatrix", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref boundaryMatrixInv, "BoundaryMatrixInv", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref boundaryMatrixW2PrevW, "BoundaryMatrixW2PW", MAX_BOUNDARIES, sizeof(Matrix4x4));

				if (boundaryMatrixPrev.IsCreated && boundaryMatrixPrev.Length != MAX_BOUNDARIES)
					boundaryMatrixPrev.Dispose();
				if (boundaryMatrixPrev.IsCreated == false)
					boundaryMatrixPrev = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Persistent, NativeArrayOptions.ClearMemory);

				return changed;
			}
		}

		private void ReleaseBuffers()
		{
			ReleaseBuffer(ref rootPosition);
			ReleaseBuffer(ref rootTangent);

			ReleaseBuffer(ref particlePosition);
			ReleaseBuffer(ref particlePositionPrev);
			ReleaseBuffer(ref particlePositionCorr);

			ReleaseBuffer(ref particleVelocity);
			ReleaseBuffer(ref particleVelocityPrev);

			ReleaseBuffer(ref boundaryPack);
			ReleaseBuffer(ref boundaryMatrix);
			ReleaseBuffer(ref boundaryMatrixInv);
			ReleaseBuffer(ref boundaryMatrixW2PrevW);

			if (boundaryMatrixPrev.IsCreated)
				boundaryMatrixPrev.Dispose();
		}

		private bool CreateVolumes()
		{
			bool changed = false;

			int volumeCells = volume.volumeResolution;

			changed |= CreateVolume(ref accuDensity, "AccuDensity", volumeCells, GraphicsFormat.R32_SInt);//TODO switch to R16_SInt
			changed |= CreateVolume(ref accuVelocityX, "AccuVelocityX", volumeCells, GraphicsFormat.R32_SInt);
			changed |= CreateVolume(ref accuVelocityY, "AccuVelocityY", volumeCells, GraphicsFormat.R32_SInt);
			changed |= CreateVolume(ref accuVelocityZ, "AccuVelocityZ", volumeCells, GraphicsFormat.R32_SInt);

			changed |= CreateVolume(ref volumeDensity, "VolumeDensity", volumeCells, RenderTextureFormat.RFloat);
			changed |= CreateVolume(ref volumeVelocity, "VolumeVelocity", volumeCells, RenderTextureFormat.ARGBFloat);
			changed |= CreateVolume(ref volumeDivergence, "VolumeDivergence", volumeCells, RenderTextureFormat.RFloat);

			changed |= CreateVolume(ref volumePressure, "VolumePressure_0", volumeCells, RenderTextureFormat.RFloat);
			changed |= CreateVolume(ref volumePressurePrev, "VolumePressure_1", volumeCells, RenderTextureFormat.RFloat);
			changed |= CreateVolume(ref volumePressureGrad, "VolumePressureGrad", volumeCells, RenderTextureFormat.ARGBFloat);

			return false;// changed;
		}

		private void ReleaseVolumes()
		{
			ReleaseVolume(ref accuDensity);
			ReleaseVolume(ref accuVelocityX);
			ReleaseVolume(ref accuVelocityY);
			ReleaseVolume(ref accuVelocityZ);

			ReleaseVolume(ref volumeDensity);
			ReleaseVolume(ref volumeVelocity);

			ReleaseVolume(ref volumeDivergence);
			ReleaseVolume(ref volumePressure);
			ReleaseVolume(ref volumePressurePrev);
			ReleaseVolume(ref volumePressureGrad);
		}

		private struct StrandRoot
		{
			public Vector3 localPos;
			public Vector3 localTan;
		}

		private NativeArray<StrandRoot> GetStrandRoots()
		{
			using (MarkersCPU.GetStrandRoots.Auto())
			{
				NativeArray<StrandRoot> strandRoots = new NativeArray<StrandRoot>(strands.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

				switch (strands.style)
				{
					case StrandConfiguration.Style.Curtain:
						{
							float strandSpan = 1.0f;
							float strandInterval = strandSpan / (strands.strandCount - 1);

							unsafe
							{
								var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
								var strandRoot = new StrandRoot()
								{
									localPos = (-0.5f * strandSpan) * Vector3.right,
									localTan = Vector3.down,
								};
								for (int j = 0; j != strands.strandCount; j++)
								{
									strandRootsPtr[j] = strandRoot;
									strandRoot.localPos += strandInterval * Vector3.right;
								}
							}
						}
						break;

					case StrandConfiguration.Style.StratifiedCurtain:
						{
							float strandSpan = 1.0f;
							float strandInterval = strandSpan / strands.strandCount;
							float strandIntervalNoise = 0.5f;

							var xorshift = new Unity.Mathematics.Random(257);

							unsafe
							{
								var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
								var strandRoot = new StrandRoot()
								{
									localTan = Vector3.down,
								};
								for (int j = 0; j != strands.strandCount; j++)
								{
									var localNoise = strandIntervalNoise * xorshift.NextFloat2(-1.0f, 1.0f);
									var localPos = -0.5f * strandSpan + (j + 0.5f + 0.5f * localNoise.x) * strandInterval;
									strandRoot.localPos = new Vector3(localPos, 0.0f, 0.5f * localNoise.y * strandInterval);
									strandRootsPtr[j] = strandRoot;
								}
							}
						}
						break;

					case StrandConfiguration.Style.Brush:
						{
							var localExt = 0.5f * Vector3.one;
							var xorshift = new Unity.Mathematics.Random(257);

							Vector2 localMin = new Vector2(-localExt.x, -localExt.z);
							Vector2 localMax = new Vector2(localExt.x, localExt.z);

							unsafe
							{
								var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
								var strandRoot = new StrandRoot()
								{
									localTan = Vector3.down
								};
								for (int j = 0; j != strands.strandCount; j++)
								{
									var localPos = xorshift.NextFloat2(localMin, localMax);
									strandRoot.localPos = new Vector3(localPos.x, 0.0f, localPos.y);
									strandRootsPtr[j] = strandRoot;
								}
							}
						}
						break;

					case StrandConfiguration.Style.Cap:
						{
							var localExt = 0.5f * Vector3.one;
							var xorshift = new Unity.Mathematics.Random(257);

							unsafe
							{
								var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
								for (int j = 0; j != strands.strandCount; j++)
								{
									var localDir = xorshift.NextFloat3Direction();
									if (localDir.y < 0.0f)
										localDir.y = -localDir.y;

									strandRootsPtr[j].localPos = localDir * localExt;
									strandRootsPtr[j].localTan = localDir;
								}
							}
						}
						break;
				}

				return strandRoots;
			}
		}

		private int GetSolveKernel()
		{
			switch (solver.method)
			{
				case SolverConfiguration.Method.GaussSeidelReference:
					return kernelSolveConstraints_GaussSeidelReference;

				case SolverConfiguration.Method.GaussSeidel:
					return kernelSolveConstraints_GaussSeidel;

				case SolverConfiguration.Method.Jacobi:
					switch (strands.strandParticleCount)
					{
						case 16: return kernelSolveConstraints_Jacobi_16;
						case 32: return kernelSolveConstraints_Jacobi_32;
						case 64: return kernelSolveConstraints_Jacobi_64;
						case 128: return kernelSolveConstraints_Jacobi_128;
					}
					break;
			}
			return -1;
		}

		public Vector3 GetCellSize()
		{
			Vector3 cellSize = new Vector3(
				(volumeWorldMax.x - volumeWorldMin.x) / volumeCells.x,
				(volumeWorldMax.y - volumeWorldMin.y) / volumeCells.y,
				(volumeWorldMax.z - volumeWorldMin.z) / volumeCells.z);
			return cellSize;
		}

		public float GetCellVolume()
		{
			return GetCellSize().x * GetCellSize().y * GetCellSize().z;
		}

		private Vector3 GetVolumeCenter()
		{
			Vector3 cellSize = GetCellSize();
			Vector3 cellOffset = new Vector3(
				cellSize.x * volume.volumeOffsetX,
				cellSize.y * volume.volumeOffsetY,
				cellSize.z * volume.volumeOffsetZ);
			return (/*this.transform.position + */cellOffset);
		}

		private Vector3 GetVolumeExtent()
		{
			//return Vector3.one;
			return Vector3.one * (1.0f + 1.5f * strands.strandLength);
		}

		private int GetVolumeCellCount()
		{
			return volume.volumeResolution * volume.volumeResolution * volume.volumeResolution;
		}

		private bool InitCond()
		{
			ValidateConfiguration();

			bool initRequired = false;

			initRequired |= CreateBuffers();
			initRequired |= CreateVolumes();

			initRequired |= (strandsActive.style != strands.style);
			initRequired |= (strandsActive.strandCount != strands.strandCount);
			initRequired |= (strandsActive.strandParticleCount != strands.strandParticleCount);
			initRequired |= (strandsActive.strandLength != strands.strandLength);

			return initRequired;
		}

		private void Init(CommandBuffer cmd)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Init))
			{
				using (NativeArray<StrandRoot> tmpRoots = GetStrandRoots())
				using (NativeArray<Vector4> tmpRootsPos = new NativeArray<Vector4>(strands.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpRootsTan = new NativeArray<Vector4>(strands.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpPosition = new NativeArray<Vector4>(strands.strandCount * strands.strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpZeroInit = new NativeArray<Vector4>(strands.strandCount * strands.strandParticleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						StrandRoot* ptrRoots = (StrandRoot*)tmpRoots.GetUnsafePtr();
						Vector4* ptrRootsPos = (Vector4*)tmpRootsPos.GetUnsafePtr();
						Vector4* ptrRootsTan = (Vector4*)tmpRootsTan.GetUnsafePtr();
						Vector4* ptrPosition = (Vector4*)tmpPosition.GetUnsafePtr();

						Matrix4x4 localToWorld = Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.localScale);
						Matrix4x4 localToWorldInvT = Matrix4x4.Transpose(Matrix4x4.Inverse(localToWorld));

						float strandParticleInterval = strands.strandLength / (strands.strandParticleCount - 1);
						for (int j = 0; j != strands.strandCount; j++)
						{
							ptrRootsPos[j] = ptrRoots[j].localPos;
							ptrRootsTan[j] = ptrRoots[j].localTan;

							Vector3 strandDirection = localToWorldInvT.MultiplyVector(ptrRoots[j].localTan);
							Vector3 strandParticlePosition = localToWorld.MultiplyPoint3x4(ptrRoots[j].localPos);

#if LAYOUT_INTERLEAVED
							int strandParticleBegin = j;
							int strandParticleStride = strands.strandCount;
#else
							int strandParticleBegin = j * strands.strandParticleCount;
							int strandParticleStride = 1;
#endif
							int strandParticleEnd = strandParticleBegin + strandParticleStride * strands.strandParticleCount;
							for (int i = strandParticleBegin; i != strandParticleEnd; i += strandParticleStride)
							{
								ptrPosition[i] = strandParticlePosition;
								strandParticlePosition += strandParticleInterval * strandDirection;
							}
						}
					}

#if CMDBUFFER_SET_DATA
					cmd.SetComputeBufferData(rootPosition, tmpRootsPos);
					cmd.SetComputeBufferData(rootTangent, tmpRootsTan);

					cmd.SetComputeBufferData(particlePosition, tmpPosition);
					cmd.SetComputeBufferData(particlePositionPrev, tmpPosition);
					cmd.SetComputeBufferData(particlePositionCorr, tmpZeroInit);

					cmd.SetComputeBufferData(particleVelocity, tmpZeroInit);
					cmd.SetComputeBufferData(particleVelocityPrev, tmpZeroInit);
#else
					rootPosition.SetData(tmpRootsPos);
					rootTangent.SetData(tmpRootsTan);

					particlePosition.SetData(tmpPosition);
					particlePositionPrev.SetData(tmpPosition);
					particlePositionCorr.SetData(tmpZeroInit);

					particleVelocity.SetData(tmpZeroInit);
					particleVelocityPrev.SetData(tmpZeroInit);
#endif
				}

				kernelUpdatePosition = computeSolver.FindKernel("KUpdatePosition");
				kernelSolveConstraints_GaussSeidelReference = computeSolver.FindKernel("KSolveConstraints_GaussSeidelReference");
				kernelSolveConstraints_GaussSeidel = computeSolver.FindKernel("KSolveConstraints_GaussSeidel");
				kernelSolveConstraints_Jacobi_16 = computeSolver.FindKernel("KSolveConstraints_Jacobi_16");
				kernelSolveConstraints_Jacobi_32 = computeSolver.FindKernel("KSolveConstraints_Jacobi_32");
				kernelSolveConstraints_Jacobi_64 = computeSolver.FindKernel("KSolveConstraints_Jacobi_64");
				kernelSolveConstraints_Jacobi_128 = computeSolver.FindKernel("KSolveConstraints_Jacobi_128");
				kernelUpdateVelocity = computeSolver.FindKernel("KUpdateVelocity");

				kernelVolumeClear = computeVolume.FindKernel("KVolumeClear");
				kernelVolumeSplat = computeVolume.FindKernel("KVolumeSplat");
				kernelVolumeSplatDensity = computeVolume.FindKernel("KVolumeSplatDensity");
				kernelVolumeSplatVelocityX = computeVolume.FindKernel("KVolumeSplatVelocityX");
				kernelVolumeSplatVelocityY = computeVolume.FindKernel("KVolumeSplatVelocityY");
				kernelVolumeSplatVelocityZ = computeVolume.FindKernel("KVolumeSplatVelocityZ");
				kernelVolumeResolve = computeVolume.FindKernel("KVolumeResolve");
				kernelVolumeResolveFromRasterization = computeVolume.FindKernel("KVolumeResolveFromRasterization");
				kernelVolumeDivergence = computeVolume.FindKernel("KVolumeDivergence");
				kernelVolumePressureEOS = computeVolume.FindKernel("KVolumePressureEOS");
				kernelVolumePressureSolve = computeVolume.FindKernel("KVolumePressureSolve");
				kernelVolumePressureGradient = computeVolume.FindKernel("KVolumePressureGradient");

				strandsActive = strands;
			}

			// pre-step volume for the first solver step
			Volume(cmd, 1.0f);
		}

		public void Step(CommandBuffer cmd, float dt)
		{
			if (InitCond())
			{
				//Debug.Log("Step -> Init");
				Init(cmd);
			}

			if (volumeCells != volume.volumeResolution * Vector3.one)
			{
				Volume(cmd, 1.0f);
			}

			using (new ProfilingScope(cmd, MarkersGPU.Step))
			{
				SwapBuffers(ref particlePosition, ref particlePositionPrev);
				SwapBuffers(ref particleVelocity, ref particleVelocityPrev);

				//-------------------
				// update boundaries

				boundaryCapsuleCount = 0;
				boundarySphereCount = 0;
				boundaryTorusCount = 0;

				using (NativeArray<HairSimBoundary.BoundaryPack> tmpPack = new NativeArray<HairSimBoundary.BoundaryPack>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<Matrix4x4> tmpMatrix = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<Matrix4x4> tmpMatrixInv = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<Matrix4x4> tmpMatrixW2PrevW = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						var ptrPack = (HairSimBoundary.BoundaryPack*)tmpPack.GetUnsafePtr();
						var ptrMatrix = (Matrix4x4*)tmpMatrix.GetUnsafePtr();
						var ptrMatrixInv = (Matrix4x4*)tmpMatrixInv.GetUnsafePtr();
						var ptrMatrixW2PrevW = (Matrix4x4*)tmpMatrixW2PrevW.GetUnsafePtr();

						var ptrMatrixPrev = (Matrix4x4*)boundaryMatrixPrev.GetUnsafePtr();

						foreach (HairSimBoundary boundary in boundaries)
						{
							if (boundary == null || !boundary.isActiveAndEnabled)
								continue;

							switch (boundary.type)
							{
								case HairSimBoundary.Type.Capsule:
									boundaryCapsuleCount++;
									break;
								case HairSimBoundary.Type.Sphere:
									boundarySphereCount++;
									break;
								case HairSimBoundary.Type.Torus:
									boundaryTorusCount++;
									break;
							}
						}

						int boundaryCapsuleIndex = 0;
						int boundarySphereIndex = boundaryCapsuleIndex + boundaryCapsuleCount;
						int boundaryTorusIndex = boundarySphereIndex + boundarySphereCount;

						int boundaryCount = boundaryCapsuleCount + boundarySphereCount + boundaryTorusIndex;
						var boundaryHash = new Hash128(7, 13);

						foreach (HairSimBoundary boundary in boundaries)
						{
							if (boundary == null || !boundary.isActiveAndEnabled)
								continue;

							switch (boundary.type)
							{
								case HairSimBoundary.Type.Capsule:
									ptrMatrix[boundaryCapsuleIndex] = boundary.transform.localToWorldMatrix;
									ptrPack[boundaryCapsuleIndex++] = HairSimBoundary.Pack(boundary.GetCapsule());
									break;
								case HairSimBoundary.Type.Sphere:
									ptrMatrix[boundarySphereIndex] = boundary.transform.localToWorldMatrix;
									ptrPack[boundarySphereIndex++] = HairSimBoundary.Pack(boundary.GetSphere());
									break;
								case HairSimBoundary.Type.Torus:
									ptrMatrix[boundaryTorusIndex] = boundary.transform.localToWorldMatrix;
									ptrPack[boundaryTorusIndex++] = HairSimBoundary.Pack(boundary.GetTorus());
									break;
							}

							int instanceID = boundary.GetInstanceID();

							HashUtilities.ComputeHash128(ref instanceID, ref boundaryHash);
						}

						if (boundaryHash != boundaryMatrixPrevHash)
						{
							Debug.Log("boundary hash changed: " + boundaryHash);
							boundaryMatrixPrevHash = boundaryHash;

							for (int i = 0; i != boundaryCount; i++)
							{
								ptrMatrixPrev[i] = ptrMatrix[i];
							}
						}

						for (int i = 0; i != boundaryCount; i++)
						{
							ptrMatrixInv[i] = Matrix4x4.Inverse(ptrMatrix[i]);
							ptrMatrixW2PrevW[i] = ptrMatrixPrev[i] * ptrMatrixInv[i];
						}

#if CMDBUFFER_SET_DATA
						cmd.SetComputeBufferData(boundaryPack, tmpPack, 0, 0, boundaryCount);
						cmd.SetComputeBufferData(boundaryMatrix, tmpMatrix, 0, 0, boundaryCount);
						cmd.SetComputeBufferData(boundaryMatrixInv, tmpMatrixInv, 0, 0, boundaryCount);
						cmd.SetComputeBufferData(boundaryMatrixW2PrevW, tmpMatrixW2PrevW, 0, 0, boundaryCount);
#else
						boundaryPack.SetData(tmpPack, 0, 0, boundaryCount);
						boundaryMatrix.SetData(tmpMatrix, 0, 0, boundaryCount);
						boundaryMatrixInv.SetData(tmpMatrixInv, 0, 0, boundaryCount);
						boundaryMatrixW2PrevW.SetData(tmpMatrixW2PrevW, 0, 0, boundaryCount);
#endif

						boundaryMatrixPrev.CopyFrom(tmpMatrix);
					}
				}

				PushComputeParams(cmd, computeSolver, dt);

				int solveThreadGroupsX = strands.strandCount / THREAD_GROUP_SIZE;
				int solveThreadGroupsY = 1;
				int solveThreadGroupsZ = 1;

				int kernelSolveConstraints = GetSolveKernel();
				if (kernelSolveConstraints == kernelSolveConstraints_Jacobi_16 ||
					kernelSolveConstraints == kernelSolveConstraints_Jacobi_32 ||
					kernelSolveConstraints == kernelSolveConstraints_Jacobi_64 ||
					kernelSolveConstraints == kernelSolveConstraints_Jacobi_128)
				{
					solveThreadGroupsX = strands.strandCount;
				}

				PushComputeKernelParams(cmd, computeSolver, kernelSolveConstraints);
				cmd.DispatchCompute(computeSolver, kernelSolveConstraints,
					solveThreadGroupsX,
					solveThreadGroupsY,
					solveThreadGroupsZ);
			}
		}

		public void Volume(CommandBuffer cmd, float dt)
		{
			if (InitCond())
			{
				//Debug.Log("Volume -> Init");
				Init(cmd);
			}

			using (new ProfilingScope(cmd, MarkersGPU.Volume))
			{
				volumeCells = volume.volumeResolution * Vector3.one;
				volumeWorldMin = GetVolumeCenter() - GetVolumeExtent();
				volumeWorldMax = GetVolumeCenter() + GetVolumeExtent();

				PushComputeParams(cmd, computeVolume, dt);

				int particleThreadCountX = 64;
				int particleThreadGroupsX = particlePosition.count / particleThreadCountX;
				int particleThreadGroupsY = 1;
				int particleThreadGroupsZ = 1;

				int volumeThreadCountX = 8;
				int volumeThreadCountY = 8;
				int volumeThreadCountZ = 1;
				int volumeThreadGroupsX = volume.volumeResolution / volumeThreadCountX;
				int volumeThreadGroupsY = volume.volumeResolution / volumeThreadCountY;
				int volumeThreadGroupsZ = volume.volumeResolution / volumeThreadCountZ;

				// clear
				using (new ProfilingScope(cmd, MarkersGPU.Volume_0_Clear))
				{
					PushComputeKernelParams(cmd, computeVolume, kernelVolumeClear);
					cmd.DispatchCompute(computeVolume, kernelVolumeClear,
						volumeThreadGroupsX,
						volumeThreadGroupsY,
						volumeThreadGroupsZ);
				}

				// splat
				switch (volume.volumeSplatMethod)
				{
					case VolumeConfiguration.SplatMethod.Compute:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat))
							{
								PushComputeKernelParams(cmd, computeVolume, kernelVolumeSplat);
								cmd.DispatchCompute(computeVolume, kernelVolumeSplat,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);
							}
						}
						break;

					case VolumeConfiguration.SplatMethod.ComputeSplit:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatDensity))
							{
								PushComputeKernelParams(cmd, computeVolume, kernelVolumeSplatDensity);
								cmd.DispatchCompute(computeVolume, kernelVolumeSplatDensity,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);
							}

							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatVelocityXYZ))
							{
								PushComputeKernelParams(cmd, computeVolume, kernelVolumeSplatVelocityX);
								cmd.DispatchCompute(computeVolume, kernelVolumeSplatVelocityX,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);

								PushComputeKernelParams(cmd, computeVolume, kernelVolumeSplatVelocityY);
								cmd.DispatchCompute(computeVolume, kernelVolumeSplatVelocityY,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);

								PushComputeKernelParams(cmd, computeVolume, kernelVolumeSplatVelocityZ);
								cmd.DispatchCompute(computeVolume, kernelVolumeSplatVelocityZ,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);
							}

						}
						break;

					case VolumeConfiguration.SplatMethod.Rasterization:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatByRasterization))
							{
								SetRenderMaterialParams(ref computeVolumeRasterMBP);
								CoreUtils.SetRenderTarget(cmd, volumeVelocity, ClearFlag.Color);
								cmd.DrawProcedural(Matrix4x4.identity, computeVolumeRaster, 0, MeshTopology.Points, strands.strandCount * strands.strandParticleCount, 1, computeVolumeRasterMBP);
							}
						}
						break;

					case VolumeConfiguration.SplatMethod.RasterizationNoGS:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatByRasterizationNoGS))
							{
								SetRenderMaterialParams(ref computeVolumeRasterMBP);
								CoreUtils.SetRenderTarget(cmd, volumeVelocity, ClearFlag.Color);
								cmd.DrawProcedural(Matrix4x4.identity, computeVolumeRaster, 1, MeshTopology.Quads, strands.strandCount * strands.strandParticleCount * 8, 1, computeVolumeRasterMBP);
							}
						}
						break;
				}

				// resolve
				switch (volume.volumeSplatMethod)
				{
					case VolumeConfiguration.SplatMethod.Compute:
					case VolumeConfiguration.SplatMethod.ComputeSplit:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_2_Resolve))
							{
								PushComputeKernelParams(cmd, computeVolume, kernelVolumeResolve);
								cmd.DispatchCompute(computeVolume, kernelVolumeResolve,
									volumeThreadGroupsX,
									volumeThreadGroupsY,
									volumeThreadGroupsZ);
							}
						}
						break;

					case VolumeConfiguration.SplatMethod.Rasterization:
					case VolumeConfiguration.SplatMethod.RasterizationNoGS:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_2_ResolveFromRasterization))
							{
								PushComputeKernelParams(cmd, computeVolume, kernelVolumeResolveFromRasterization);
								cmd.DispatchCompute(computeVolume, kernelVolumeResolveFromRasterization,
									volumeThreadGroupsX,
									volumeThreadGroupsX,
									volumeThreadGroupsX);
							}
						}
						break;
				}

				// divergence
				using (new ProfilingScope(cmd, MarkersGPU.Volume_3_Divergence))
				{
					PushComputeKernelParams(cmd, computeVolume, kernelVolumeDivergence);
					cmd.DispatchCompute(computeVolume, kernelVolumeDivergence,
						volumeThreadGroupsX,
						volumeThreadGroupsY,
						volumeThreadGroupsZ);
				}

				// pressure eos (initial guess)
				using (new ProfilingScope(cmd, MarkersGPU.Volume_4_PressureEOS))
				{
					PushComputeKernelParams(cmd, computeVolume, kernelVolumePressureEOS);
					cmd.DispatchCompute(computeVolume, kernelVolumePressureEOS,
						volumeThreadGroupsX,
						volumeThreadGroupsY,
						volumeThreadGroupsZ);
				}

				// pressure solve
				using (new ProfilingScope(cmd, MarkersGPU.Volume_5_PressureSolve))
				{
					PushComputeKernelParams(cmd, computeVolume, kernelVolumePressureSolve);
					for (int i = 0; i != volume.volumePressureIterations; i++)
					{
						SwapVolumes(ref volumePressure, ref volumePressurePrev);
						cmd.SetComputeTextureParam(computeVolume, kernelVolumePressureSolve, UniformIDs._VolumePressure, volumePressure);
						cmd.SetComputeTextureParam(computeVolume, kernelVolumePressureSolve, UniformIDs._VolumePressurePrev, volumePressurePrev);

						cmd.DispatchCompute(computeVolume, kernelVolumePressureSolve,
							volumeThreadGroupsX,
							volumeThreadGroupsY,
							volumeThreadGroupsZ);

					}
				}

				// pressure gradient
				using (new ProfilingScope(cmd, MarkersGPU.Volume_6_PressureGradient))
				{
					PushComputeKernelParams(cmd, computeVolume, kernelVolumePressureGradient);
					cmd.DispatchCompute(computeVolume, kernelVolumePressureGradient,
						volumeThreadGroupsX,
						volumeThreadGroupsY,
						volumeThreadGroupsZ);
				}
			}
		}

		public void Draw(CommandBuffer cmd, RTHandle colorRT, RTHandle depthStencilRT, RTHandle motionVectorRT)
		{
			if (InitCond())
			{
				//Debug.Log("Draw -> Init");
				Init(cmd);
			}

			using (new ProfilingScope(cmd, MarkersGPU.Draw))
			{
				if (!debug.drawParticles &&
					!debug.drawStrands &&
					!debug.drawDensity &&
					!debug.drawSliceX &&
					!debug.drawSliceY &&
					!debug.drawSliceZ)
					return;

				SetRenderMaterialParams(ref debugDrawMPB);
				CoreUtils.SetRenderTarget(cmd, colorRT, depthStencilRT);

				if (debug.drawStrands)
				{
					cmd.DrawProcedural(Matrix4x4.identity, debugDraw, 0, MeshTopology.LineStrip, strands.strandParticleCount, strands.strandCount, debugDrawMPB);
				}

				if (debug.drawParticles)
				{
					cmd.DrawProcedural(Matrix4x4.identity, debugDraw, 0, MeshTopology.Points, strands.strandParticleCount, strands.strandCount, debugDrawMPB);
				}

				if (debug.drawDensity)
				{
					cmd.DrawProcedural(Matrix4x4.identity, debugDraw, 1, MeshTopology.Points, GetVolumeCellCount(), 1, debugDrawMPB);
				}

				if (debug.drawSliceX || debug.drawSliceY || debug.drawSliceZ)
				{
					debugDrawMPB.SetFloat(UniformIDs._DebugSliceDivider, debug.drawSliceDivider);

					if (debug.drawSliceX)
					{
						debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 0);
						debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debug.drawSliceOffsetX);
						cmd.DrawProcedural(Matrix4x4.identity, debugDraw, 3, MeshTopology.Quads, 4, 1, debugDrawMPB);
					}
					if (debug.drawSliceY)
					{
						debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 1);
						debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debug.drawSliceOffsetY);
						cmd.DrawProcedural(Matrix4x4.identity, debugDraw, 3, MeshTopology.Quads, 4, 1, debugDrawMPB);
					}
					if (debug.drawSliceZ)
					{
						debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 2);
						debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debug.drawSliceOffsetZ);
						cmd.DrawProcedural(Matrix4x4.identity, debugDraw, 3, MeshTopology.Quads, 4, 1, debugDrawMPB);
					}
				}

				if (debug.drawStrands)// motion vectors
				{
					CoreUtils.SetRenderTarget(cmd, motionVectorRT, depthStencilRT);
					cmd.DrawProcedural(Matrix4x4.identity, debugDraw, 4, MeshTopology.LineStrip, strands.strandParticleCount, strands.strandCount, debugDrawMPB);
				}
			}
		}

		void PushComputeParams(CommandBuffer cmd, ComputeShader cs, float dt)
		{
			float strandCrossSectionArea = 0.25f * Mathf.PI * strands.strandDiameter * strands.strandDiameter;
			float strandParticleInterval = strands.strandLength / (strands.strandParticleCount - 1);
			float strandParticleVolume = (1000.0f * strandParticleInterval) * strandCrossSectionArea;

			Matrix4x4 localToWorld = Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.localScale);
			Matrix4x4 localToWorldInvT = Matrix4x4.Transpose(Matrix4x4.Inverse(localToWorld));

			cmd.SetComputeMatrixParam(cs, UniformIDs._LocalToWorld, localToWorld);
			cmd.SetComputeMatrixParam(cs, UniformIDs._LocalToWorldInvT, localToWorldInvT);

			cmd.SetComputeIntParam(cs, UniformIDs._StrandCount, strands.strandCount);
			cmd.SetComputeIntParam(cs, UniformIDs._StrandParticleCount, strands.strandParticleCount);
			cmd.SetComputeFloatParam(cs, UniformIDs._StrandParticleInterval, strandParticleInterval);
			cmd.SetComputeFloatParam(cs, UniformIDs._StrandParticleVolume, strandParticleVolume);
			cmd.SetComputeFloatParam(cs, UniformIDs._StrandParticleContrib, strands.strandParticleContrib);// TODO remove

			cmd.SetComputeFloatParam(cs, UniformIDs._DT, dt);
			cmd.SetComputeIntParam(cs, UniformIDs._Iterations, solver.iterations);
			cmd.SetComputeFloatParam(cs, UniformIDs._Stiffness, solver.stiffness);
			cmd.SetComputeFloatParam(cs, UniformIDs._Relaxation, solver.relaxation);
			cmd.SetComputeFloatParam(cs, UniformIDs._Damping, solver.damping);
			cmd.SetComputeFloatParam(cs, UniformIDs._Gravity, solver.gravity * -Vector3.Magnitude(Physics.gravity));
			cmd.SetComputeFloatParam(cs, UniformIDs._Repulsion, solver.repulsion);
			cmd.SetComputeFloatParam(cs, UniformIDs._Friction, solver.friction);

			cmd.SetComputeFloatParam(cs, UniformIDs._BendingCurvature, solver.bendingCurvature * 0.5f * strandParticleInterval);
			cmd.SetComputeFloatParam(cs, UniformIDs._DampingFTL, solver.dampingFTL);

			cmd.SetComputeIntParam(cs, UniformIDs._BoundaryCapsuleCount, boundaryCapsuleCount);
			cmd.SetComputeIntParam(cs, UniformIDs._BoundarySphereCount, boundarySphereCount);
			cmd.SetComputeIntParam(cs, UniformIDs._BoundaryTorusCount, boundaryTorusCount);

			cmd.SetComputeVectorParam(cs, UniformIDs._VolumeCells, volumeCells);
			cmd.SetComputeVectorParam(cs, UniformIDs._VolumeWorldMin, volumeWorldMin);
			cmd.SetComputeVectorParam(cs, UniformIDs._VolumeWorldMax, volumeWorldMax);
		}

		void PushComputeKernelParams(CommandBuffer cmd, ComputeShader cs, int kernel)
		{
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootPosition, rootPosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootTangent, rootTangent);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionPrev, particlePositionPrev);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocityPrev, particleVelocityPrev);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePosition, particlePosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionCorr, particlePositionCorr);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocity, particleVelocity);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryPack, boundaryPack);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryMatrix, boundaryMatrix);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryMatrixInv, boundaryMatrixInv);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryMatrixW2PrevW, boundaryMatrixW2PrevW);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuDensity, accuDensity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuVelocityX, accuVelocityX);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuVelocityY, accuVelocityY);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuVelocityZ, accuVelocityZ);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeDensity, volumeDensity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocity, volumeVelocity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeDivergence, volumeDivergence);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumePressure, volumePressure);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumePressurePrev, volumePressurePrev);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumePressureGrad, volumePressureGrad);
		}

		void SetRenderMaterialParams(ref MaterialPropertyBlock mpb)
		{
			if (mpb == null)
				mpb = new MaterialPropertyBlock();

			mpb.SetInt(UniformIDs._StrandCount, strands.strandCount);
			mpb.SetInt(UniformIDs._StrandParticleCount, strands.strandParticleCount);

			mpb.SetVector(UniformIDs._VolumeCells, volumeCells);
			mpb.SetVector(UniformIDs._VolumeWorldMin, volumeWorldMin);
			mpb.SetVector(UniformIDs._VolumeWorldMax, volumeWorldMax);

			mpb.SetBuffer(UniformIDs._ParticlePosition, particlePosition);
			mpb.SetBuffer(UniformIDs._ParticlePositionPrev, particlePositionPrev);
			mpb.SetBuffer(UniformIDs._ParticleVelocity, particleVelocity);
			mpb.SetBuffer(UniformIDs._ParticleVelocityPrev, particleVelocityPrev);

			mpb.SetTexture(UniformIDs._VolumeDensity, volumeDensity);
			mpb.SetTexture(UniformIDs._VolumeVelocity, volumeVelocity);
			mpb.SetTexture(UniformIDs._VolumeDivergence, volumeDivergence);

			mpb.SetTexture(UniformIDs._VolumePressure, volumePressure);
			mpb.SetTexture(UniformIDs._VolumePressurePrev, volumePressurePrev);
			mpb.SetTexture(UniformIDs._VolumePressureGrad, volumePressureGrad);
		}
	}
}