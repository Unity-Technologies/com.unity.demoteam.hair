#define LAYOUT_INTERLEAVED

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
		#region static set of instances
		public static HashSet<HairSim> instances = new HashSet<HairSim>();
		private void OnEnable() { instances.Add(this); }
		private void OnDisable() { instances.Remove(this); ReleaseBuffers(); ReleaseVolumes(); strandsActive = StrandConfiguration.none; }
		#endregion

		public static class ProfilerMarkers
		{
			public static ProfilerMarker GetStrandRoots = new ProfilerMarker("HairSim.GetStrandRoots");
			public static ProfilerMarker Init = new ProfilerMarker("HairSim.Init");
			public static ProfilerMarker Step = new ProfilerMarker("HairSim.Step");
			public static ProfilerMarker Voxelize = new ProfilerMarker("HairSim.Voxelize");
			public static ProfilerMarker Draw = new ProfilerMarker("HairSim.Draw");
		}

		const int THREAD_GROUP_SIZE = 64;

		const int MAX_STRANDS = 64000;
		const int MAX_STRAND_PARTICLES = 128;
		const int MAX_BOUNDARIES = 8;

		public static class UniformIDs
		{
			// matrices
			public static int _LocalToWorld = Shader.PropertyToID("_LocalToWorld");
			public static int _LocalToWorldInvT = Shader.PropertyToID("_LocalToWorldInvT");

			// strand params
			public static int _StrandCount = Shader.PropertyToID("_StrandCount");
			public static int _StrandParticleCount = Shader.PropertyToID("_StrandParticleCount");
			public static int _StrandParticleInterval = Shader.PropertyToID("_StrandParticleInterval");

			// strand buffers
			public static int _RootPosition = Shader.PropertyToID("_RootPosition");
			public static int _RootTangent = Shader.PropertyToID("_RootTangent");

			// solver params
			public static int _DT = Shader.PropertyToID("_DT");
			public static int _Iterations = Shader.PropertyToID("_Iterations");
			public static int _Stiffness = Shader.PropertyToID("_Stiffness");
			public static int _Inference = Shader.PropertyToID("_Inference");
			public static int _Damping = Shader.PropertyToID("_Damping");
			public static int _Gravity = Shader.PropertyToID("_Gravity");
			public static int _Repulsion = Shader.PropertyToID("_Repulsion");
			public static int _Friction = Shader.PropertyToID("_Friction");

			public static int _BendingCurvature = Shader.PropertyToID("_BendingCurvature");
			public static int _BendingStiffness = Shader.PropertyToID("_BendingStiffness");

			public static int _DampingFTL = Shader.PropertyToID("_DampingFTL");

			// solver buffers
			public static int _ParticlePosition = Shader.PropertyToID("_ParticlePosition");
			public static int _ParticleVelocity = Shader.PropertyToID("_ParticleVelocity");
			public static int _ParticlePositionPrev = Shader.PropertyToID("_ParticlePositionPrev");
			public static int _ParticleVelocityPrev = Shader.PropertyToID("_ParticleVelocityPrev");
			public static int _ParticlePositionCorr = Shader.PropertyToID("_ParticlePositionCorr");

			// boundary params
			public static int _BoundaryCapsuleCount = Shader.PropertyToID("_BoundaryCapsuleCount");
			public static int _BoundarySphereCount = Shader.PropertyToID("_BoundarySphereCount");
			public static int _BoundaryTorusCount = Shader.PropertyToID("_BoundaryTorusCount");

			// boundary buffers
			public static int _BoundaryCapsule = Shader.PropertyToID("_BoundaryCapsule");
			public static int _BoundarySphere = Shader.PropertyToID("_BoundarySphere");
			public static int _BoundaryTorus = Shader.PropertyToID("_BoundaryTorus");
			public static int _BoundaryPack = Shader.PropertyToID("_BoundaryPack");

			// volume params
			public static int _VolumeCells = Shader.PropertyToID("_VolumeCells");
			public static int _VolumeWorldMin = Shader.PropertyToID("_VolumeWorldMin");
			public static int _VolumeWorldMax = Shader.PropertyToID("_VolumeWorldMax");

			// volume buffers
			public static int _VolumeDensity = Shader.PropertyToID("_VolumeDensity");
			public static int _VolumeVelocityX = Shader.PropertyToID("_VolumeVelocityX");
			public static int _VolumeVelocityY = Shader.PropertyToID("_VolumeVelocityY");
			public static int _VolumeVelocityZ = Shader.PropertyToID("_VolumeVelocityZ");
			public static int _VolumeVelocity = Shader.PropertyToID("_VolumeVelocity");
			public static int _VolumeGradient = Shader.PropertyToID("_VolumeGradient");

			// debug params
			public static int _DebugSliceAxis = Shader.PropertyToID("_DebugSliceAxis");
			public static int _DebugSliceOffset = Shader.PropertyToID("_DebugSliceOffset");
			public static int _DebugSliceDivider = Shader.PropertyToID("_DebugSliceDivider");
		}

		private struct StrandRoot
		{
			public Vector3 localPos;
			public Vector3 localTan;
		}

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
			[Range(1e-2f, 5)]
			public float strandLength;

			public static readonly StrandConfiguration none = new StrandConfiguration();
			public static readonly StrandConfiguration basic = new StrandConfiguration()
			{
				style = Style.Curtain,
				strandCount = 1024,
				strandParticleCount = 32,
				strandLength = 0.5f,
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

			public enum SplatFilter : uint
			{
				Trilinear,
			}

			public SplatMethod volumeSplatMethod;
			public SplatFilter volumeSplatFilter;
			[Range(4, 128)]
			public int volumeResolution;
			[Range(-1.0f, 1.0f)]
			public float volumeOffsetX;
			[Range(-1.0f, 1.0f)]
			public float volumeOffsetY;

			public static readonly VolumeConfiguration none = new VolumeConfiguration();
			public static readonly VolumeConfiguration basic = new VolumeConfiguration()
			{
				volumeSplatMethod = SplatMethod.Compute,
				volumeSplatFilter = SplatFilter.Trilinear,
				volumeResolution = 64,
				volumeOffsetX = 0.0f,
				volumeOffsetY = 0.0f,
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
			public float inference;
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
			public float bendingStiffness;
			[Range(0.0f, 1.0f)]
			public float dampingFTL;

			public static readonly SolverConfiguration none = new SolverConfiguration();
			public static readonly SolverConfiguration basic = new SolverConfiguration()
			{
				method = Method.GaussSeidelReference,
				iterations = 3,
				stiffness = 1.0f,
				inference = 1.0f,
				damping = 0.0f,
				gravity = 1.0f,
				repulsion = 0.0f,
				friction = 0.0f,

				bendingCurvature = 0.0f,
				bendingStiffness = 0.0f,
				dampingFTL = 0.8f,
			};
		}

		[Serializable]
		public struct DebugConfiguration
		{
			public bool drawParticles;
			public bool drawStrands;
			public bool drawDensity;
			public bool drawGradient;
			public bool drawSliceX;
			public bool drawSliceY;
			public bool drawSliceZ;

			[Range(0.0f, 1.0f)]
			public float drawSliceOffsetX;
			[Range(0.0f, 1.0f)]
			public float drawSliceOffsetY;
			[Range(0.0f, 1.0f)]
			public float drawSliceOffsetZ;
			[Range(0.0f, 2.0f)]
			public float drawSliceDivider;

			public static readonly DebugConfiguration none = new DebugConfiguration();
			public static readonly DebugConfiguration basic = new DebugConfiguration()
			{
				drawParticles = false,
				drawStrands = true,
				drawDensity = false,
				drawGradient = false,
				drawSliceX = false,
				drawSliceY = false,
				drawSliceZ = false,
				drawSliceOffsetX = 0.4f,
				drawSliceOffsetY = 0.4f,
				drawSliceOffsetZ = 0.4f,
				drawSliceDivider = 0.0f,
			};
		}

		private StrandConfiguration strandsActive = StrandConfiguration.none;
		public StrandConfiguration strands = StrandConfiguration.basic;
		public VolumeConfiguration volume = VolumeConfiguration.basic;
		public DebugConfiguration debug = DebugConfiguration.basic;
		public SolverConfiguration solver = SolverConfiguration.basic;
		public List<HairSimBoundary> boundaries = new List<HairSimBoundary>(MAX_BOUNDARIES);

		[HideInInspector] public ComputeShader compute;
		[HideInInspector] public ComputeShader computeVolume;
		[HideInInspector] public Material computeVolumeRaster;
		private MaterialPropertyBlock computeVolumeRasterPB;

		[HideInInspector] public Material debugMaterial;
		private MaterialPropertyBlock debugMaterialPB;

		private int kernelUpdatePosition;
		private int kernelSolveConstraints_GaussSeidelReference;
		private int kernelSolveConstraints_GaussSeidel;
		private int kernelSolveConstraints_Jacobi_16;
		private int kernelSolveConstraints_Jacobi_32;
		private int kernelSolveConstraints_Jacobi_64;
		private int kernelSolveConstraints_Jacobi_128;
		private int kernelUpdateVelocity;

		private int kernelVolumeClear;
		private int kernelVolumeDensity;
		private int kernelVolumeVelocityX;
		private int kernelVolumeVelocityY;
		private int kernelVolumeVelocityZ;
		private int kernelVolumeVelocity;
		private int kernelVolumeVelocityDensity;
		private int kernelVolumeGradient;

		private ComputeBuffer rootPosition;
		private ComputeBuffer rootTangent;

		private ComputeBuffer particlePosition;
		private ComputeBuffer particlePositionPrev;
		private ComputeBuffer particlePositionCorr;

		private ComputeBuffer particleVelocity;
		private ComputeBuffer particleVelocityPrev;

		private ComputeBuffer boundaryCapsule;
		private ComputeBuffer boundarySphere;
		private ComputeBuffer boundaryTorus;
		private ComputeBuffer boundaryPack;

		private int boundaryCapsuleCount;
		private int boundarySphereCount;
		private int boundaryTorusCount;

		private RenderTexture volumeDensity;
		private RenderTexture volumeVelocityX;
		private RenderTexture volumeVelocityY;
		private RenderTexture volumeVelocityZ;
		private RenderTexture volumeVelocity;
		private RenderTexture volumeGradient;

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

			volume.volumeResolution = Mathf.Max(4, volume.volumeResolution);
		}

		private void OnValidate()
		{
			ValidateConfiguration();
		}

		private void OnDrawGizmos()
		{
			if (debug.drawDensity || debug.drawGradient || debug.drawSliceX || debug.drawSliceY || debug.drawSliceZ)
			{
				Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
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

				changed |= CreateBuffer(ref boundaryCapsule, "BoundaryCapsule", MAX_BOUNDARIES, sizeof(HairSimBoundary.BoundaryCapsule));
				changed |= CreateBuffer(ref boundarySphere, "BoundarySphere", MAX_BOUNDARIES, sizeof(HairSimBoundary.BoundarySphere));
				changed |= CreateBuffer(ref boundaryTorus, "BoundaryTorus", MAX_BOUNDARIES, sizeof(HairSimBoundary.BoundaryTorus));
				changed |= CreateBuffer(ref boundaryPack, "BoundaryPack", MAX_BOUNDARIES, sizeof(HairSimBoundary.BoundaryPack));

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

			ReleaseBuffer(ref boundaryCapsule);
			ReleaseBuffer(ref boundarySphere);
			ReleaseBuffer(ref boundaryTorus);
			ReleaseBuffer(ref boundaryPack);
		}

		private bool CreateVolumes()
		{
			bool changed = false;

			int volumeCells = volume.volumeResolution;

			changed |= CreateVolume(ref volumeDensity, "VolumeDensity", volumeCells, RenderTextureFormat.RInt);//TODO switch to R16_SInt
			changed |= CreateVolume(ref volumeVelocityX, "VolumeVelocityX", volumeCells, RenderTextureFormat.RInt);
			changed |= CreateVolume(ref volumeVelocityY, "VolumeVelocityY", volumeCells, RenderTextureFormat.RInt);
			changed |= CreateVolume(ref volumeVelocityZ, "VolumeVelocityZ", volumeCells, RenderTextureFormat.RInt);
			changed |= CreateVolume(ref volumeVelocity, "VolumeVelocity", volumeCells, RenderTextureFormat.ARGBFloat);
			changed |= CreateVolume(ref volumeGradient, "VolumeGradient", volumeCells, RenderTextureFormat.ARGBFloat);

			return changed;
		}

		private void ReleaseVolumes()
		{
			ReleaseVolume(ref volumeDensity);
			ReleaseVolume(ref volumeVelocityX);
			ReleaseVolume(ref volumeVelocityY);
			ReleaseVolume(ref volumeVelocityZ);
			ReleaseVolume(ref volumeVelocity);
			ReleaseVolume(ref volumeGradient);
		}

		private NativeArray<StrandRoot> GetStrandRoots()
		{
			using (ProfilerMarkers.GetStrandRoots.Auto())
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

		private Vector3 GetVolumeCenter()
		{
			return this.transform.position + volume.volumeOffsetX * Vector3.right + volume.volumeOffsetY * Vector3.up;
		}

		private Vector3 GetVolumeExtent()
		{
			return (0.75f + 1.2f * strands.strandLength) * Vector3.one;
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
			using (ProfilerMarkers.Init.Auto())
			{
				using (NativeArray<StrandRoot> tmpRoots = GetStrandRoots())
				using (NativeArray<Vector4> tmpRootsPos = new NativeArray<Vector4>(strands.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpRootsTan = new NativeArray<Vector4>(strands.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpPosition = new NativeArray<Vector4>(strands.strandCount * strands.strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpVelocity = new NativeArray<Vector4>(strands.strandCount * strands.strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					unsafe
					{
						StrandRoot* ptrRoots = (StrandRoot*)tmpRoots.GetUnsafePtr();
						Vector4* ptrRootsPos = (Vector4*)tmpRootsPos.GetUnsafePtr();
						Vector4* ptrRootsTan = (Vector4*)tmpRootsTan.GetUnsafePtr();
						Vector4* ptrPosition = (Vector4*)tmpPosition.GetUnsafePtr();
						Vector4* ptrVelocity = (Vector4*)tmpVelocity.GetUnsafePtr();

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

						UnsafeUtility.MemClear(ptrVelocity, sizeof(Vector4) * tmpVelocity.Length);
					}

					rootPosition.SetData(tmpRootsPos);
					rootTangent.SetData(tmpRootsTan);

					particlePosition.SetData(tmpPosition);
					particlePositionPrev.SetData(tmpPosition);
					particlePositionCorr.SetData(tmpVelocity);

					particleVelocity.SetData(tmpVelocity);
					particleVelocityPrev.SetData(tmpVelocity);
				}

				kernelUpdatePosition = compute.FindKernel("KUpdatePosition");
				kernelSolveConstraints_GaussSeidelReference = compute.FindKernel("KSolveConstraints_GaussSeidelReference");
				kernelSolveConstraints_GaussSeidel = compute.FindKernel("KSolveConstraints_GaussSeidel");
				kernelSolveConstraints_Jacobi_16 = compute.FindKernel("KSolveConstraints_Jacobi_16");
				kernelSolveConstraints_Jacobi_32 = compute.FindKernel("KSolveConstraints_Jacobi_32");
				kernelSolveConstraints_Jacobi_64 = compute.FindKernel("KSolveConstraints_Jacobi_64");
				kernelSolveConstraints_Jacobi_128 = compute.FindKernel("KSolveConstraints_Jacobi_128");
				kernelUpdateVelocity = compute.FindKernel("KUpdateVelocity");

				kernelVolumeClear = computeVolume.FindKernel("KVolumeClear");
				kernelVolumeDensity = computeVolume.FindKernel("KVolumeDensity");
				kernelVolumeVelocityX = computeVolume.FindKernel("KVolumeVelocityX");
				kernelVolumeVelocityY = computeVolume.FindKernel("KVolumeVelocityY");
				kernelVolumeVelocityZ = computeVolume.FindKernel("KVolumeVelocityZ");
				kernelVolumeVelocity = computeVolume.FindKernel("KVolumeVelocity");
				kernelVolumeVelocityDensity = computeVolume.FindKernel("KVolumeVelocityDensity");
				kernelVolumeGradient = computeVolume.FindKernel("KVolumeGradient");

				strandsActive = strands;
			}
		}

		public void Step(CommandBuffer cmd, float dt)
		{
			if (InitCond())
			{
				//Debug.Log("Step -> Init");
				Init(cmd);
			}

			using (ProfilerMarkers.Step.Auto())
			{
				SwapBuffers(ref particlePosition, ref particlePositionPrev);
				SwapBuffers(ref particleVelocity, ref particleVelocityPrev);

				//-------------------
				// update boundaries

				boundaryCapsuleCount = 0;
				boundarySphereCount = 0;
				boundaryTorusCount = 0;

				using (NativeArray<HairSimBoundary.BoundaryCapsule> tmpCapsule = new NativeArray<HairSimBoundary.BoundaryCapsule>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<HairSimBoundary.BoundarySphere> tmpSphere = new NativeArray<HairSimBoundary.BoundarySphere>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<HairSimBoundary.BoundaryTorus> tmpTorus = new NativeArray<HairSimBoundary.BoundaryTorus>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<HairSimBoundary.BoundaryPack> tmpPack = new NativeArray<HairSimBoundary.BoundaryPack>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					int boundaryPackCount = 0;

					unsafe
					{
						HairSimBoundary.BoundaryCapsule* ptrCapsule = (HairSimBoundary.BoundaryCapsule*)tmpCapsule.GetUnsafePtr();
						HairSimBoundary.BoundarySphere* ptrSphere = (HairSimBoundary.BoundarySphere*)tmpSphere.GetUnsafePtr();
						HairSimBoundary.BoundaryTorus* ptrTorus = (HairSimBoundary.BoundaryTorus*)tmpTorus.GetUnsafePtr();
						HairSimBoundary.BoundaryPack* ptrPack = (HairSimBoundary.BoundaryPack*)tmpPack.GetUnsafePtr();

						foreach (HairSimBoundary boundary in boundaries)
						{
							if (boundary == null || !boundary.isActiveAndEnabled)
								continue;

							switch (boundary.type)
							{
								case HairSimBoundary.Type.Capsule:
									ptrCapsule[boundaryCapsuleCount++] = boundary.GetCapsule();
									break;
								case HairSimBoundary.Type.Sphere:
									ptrSphere[boundarySphereCount++] = boundary.GetSphere();
									break;
								case HairSimBoundary.Type.Torus:
									ptrTorus[boundaryTorusCount++] = boundary.GetTorus();
									break;
							}
						}

						for (int i = 0; i != boundaryCapsuleCount; i++)
							ptrPack[boundaryPackCount++] = HairSimBoundary.Pack(ptrCapsule[i]);
						for (int i = 0; i != boundarySphereCount; i++)
							ptrPack[boundaryPackCount++] = HairSimBoundary.Pack(ptrSphere[i]);
						for (int i = 0; i != boundaryTorusCount; i++)
							ptrPack[boundaryPackCount++] = HairSimBoundary.Pack(ptrTorus[i]);
					}

					boundaryCapsule.SetData(tmpCapsule, 0, 0, boundaryCapsuleCount);
					boundarySphere.SetData(tmpSphere, 0, 0, boundarySphereCount);
					boundaryTorus.SetData(tmpTorus, 0, 0, boundaryTorusCount);
					boundaryPack.SetData(tmpPack, 0, 0, boundaryPackCount);
				}

				SetComputeParams(cmd, compute, dt);

				//int particleThreadGroupsX = particlePosition.count / THREAD_GROUP_SIZE;
				//int particleThreadGroupsY = 1;
				//int particleThreadGroupsZ = 1;

				//SetKernelBufferParams(cmd, compute, kernelUpdatePosition);
				//cmd.DispatchCompute(compute, kernelUpdatePosition,
				//	particleThreadGroupsX,
				//	particleThreadGroupsY,
				//	particleThreadGroupsZ);

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

				SetComputeKernelParams(cmd, compute, kernelSolveConstraints);
				cmd.DispatchCompute(compute, kernelSolveConstraints,
					solveThreadGroupsX,
					solveThreadGroupsY,
					solveThreadGroupsZ);

				//SetKernelBufferParams(cmd, compute, kernelUpdateVelocity);
				//cmd.DispatchCompute(compute, kernelUpdateVelocity,
				//	particleThreadGroupsX,
				//	particleThreadGroupsY,
				//	particleThreadGroupsZ);
			}
		}

		public void Voxelize(CommandBuffer cmd, float dt)
		{
			if (InitCond())
			{
				//Debug.Log("Voxelize -> Init");
				Init(cmd);
			}

			using (ProfilerMarkers.Voxelize.Auto())
			{
				SetComputeParams(cmd, computeVolume, dt);

				int particleThreadCountX = 64;
				int particleThreadGroupsX = particlePosition.count / particleThreadCountX;
				int particleThreadGroupsY = 1;
				int particleThreadGroupsZ = 1;

				int volumeThreadCountXYZ = 4;
				int volumeThreadGroupsX = volume.volumeResolution / volumeThreadCountXYZ;
				int volumeThreadGroupsY = volume.volumeResolution / volumeThreadCountXYZ;
				int volumeThreadGroupsZ = volume.volumeResolution / volumeThreadCountXYZ;

				using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeClear (GPU)"))
				{
					SetComputeKernelParams(cmd, computeVolume, kernelVolumeClear);
					cmd.DispatchCompute(computeVolume, kernelVolumeClear,
						volumeThreadGroupsX,
						volumeThreadGroupsY,
						volumeThreadGroupsZ);
				}

				switch (volume.volumeSplatMethod)
				{
					case VolumeConfiguration.SplatMethod.Compute:
						{
							using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeDensity (GPU)"))
							{
								SetComputeKernelParams(cmd, computeVolume, kernelVolumeDensity);
								cmd.DispatchCompute(computeVolume, kernelVolumeDensity,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);
							}

							using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeVelocityXYZ (GPU)"))
							{
								SetComputeKernelParams(cmd, computeVolume, kernelVolumeVelocityX);
								cmd.DispatchCompute(computeVolume, kernelVolumeVelocityX,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);

								SetComputeKernelParams(cmd, computeVolume, kernelVolumeVelocityY);
								cmd.DispatchCompute(computeVolume, kernelVolumeVelocityY,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);

								SetComputeKernelParams(cmd, computeVolume, kernelVolumeVelocityZ);
								cmd.DispatchCompute(computeVolume, kernelVolumeVelocityZ,
									particleThreadGroupsX,
									particleThreadGroupsY,
									particleThreadGroupsZ);
							}

							using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeVelocity (GPU)"))
							{
								SetComputeKernelParams(cmd, computeVolume, kernelVolumeVelocity);
								cmd.DispatchCompute(computeVolume, kernelVolumeVelocity,
									volumeThreadGroupsX,
									volumeThreadGroupsY,
									volumeThreadGroupsZ);
							}
						}
						break;

					case VolumeConfiguration.SplatMethod.ComputeSplit:
						{
							//TODO
						}
						break;

					case VolumeConfiguration.SplatMethod.Rasterization:
					case VolumeConfiguration.SplatMethod.RasterizationNoGS:
						{
							using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeSplatParticles (GPU)"))
							{
								SetRenderMaterialParams(ref computeVolumeRasterPB);
								CoreUtils.SetRenderTarget(cmd, volumeVelocity, ClearFlag.Color);

								if (volume.volumeSplatMethod == VolumeConfiguration.SplatMethod.Rasterization)
									cmd.DrawProcedural(Matrix4x4.identity, computeVolumeRaster, 0, MeshTopology.Points, strands.strandCount * strands.strandParticleCount, 1, computeVolumeRasterPB);
								else
									cmd.DrawProcedural(Matrix4x4.identity, computeVolumeRaster, 1, MeshTopology.Quads, strands.strandCount * strands.strandParticleCount * 8, 1, computeVolumeRasterPB);
							}

							using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeVelocityDensity (GPU)"))
							{
								SetComputeKernelParams(cmd, computeVolume, kernelVolumeVelocityDensity);
								cmd.DispatchCompute(computeVolume, kernelVolumeVelocityDensity,
									volumeThreadGroupsX,
									volumeThreadGroupsX,
									volumeThreadGroupsX);
							}
						}
						break;
				}

				using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeGradient (GPU)"))
				{
					SetComputeKernelParams(cmd, computeVolume, kernelVolumeGradient);
					cmd.DispatchCompute(computeVolume, kernelVolumeGradient,
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

			using (ProfilerMarkers.Draw.Auto())
			{
				if (!debug.drawParticles &&
					!debug.drawStrands &&
					!debug.drawDensity &&
					!debug.drawGradient &&
					!debug.drawSliceX &&
					!debug.drawSliceY &&
					!debug.drawSliceZ)
					return;

				SetRenderMaterialParams(ref debugMaterialPB);
				CoreUtils.SetRenderTarget(cmd, colorRT, depthStencilRT);

				if (debug.drawStrands)
				{
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 0, MeshTopology.LineStrip, strands.strandParticleCount, strands.strandCount, debugMaterialPB);
				}

				if (debug.drawParticles)
				{
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 0, MeshTopology.Points, strands.strandParticleCount, strands.strandCount, debugMaterialPB);
				}

				if (debug.drawDensity)
				{
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 1, MeshTopology.Points, GetVolumeCellCount(), 1, debugMaterialPB);
				}

				if (debug.drawGradient)
				{
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 2, MeshTopology.Lines, GetVolumeCellCount() * 2, 1, debugMaterialPB);
				}

				if (debug.drawSliceX || debug.drawSliceY || debug.drawSliceZ)
				{
					debugMaterialPB.SetFloat(UniformIDs._DebugSliceDivider, debug.drawSliceDivider);

					if (debug.drawSliceX)
					{
						debugMaterialPB.SetInt(UniformIDs._DebugSliceAxis, 0);
						debugMaterialPB.SetFloat(UniformIDs._DebugSliceOffset, debug.drawSliceOffsetX);
						cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 3, MeshTopology.Quads, 4, 1, debugMaterialPB);
					}
					if (debug.drawSliceY)
					{
						debugMaterialPB.SetInt(UniformIDs._DebugSliceAxis, 1);
						debugMaterialPB.SetFloat(UniformIDs._DebugSliceOffset, debug.drawSliceOffsetY);
						cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 3, MeshTopology.Quads, 4, 1, debugMaterialPB);
					}
					if (debug.drawSliceZ)
					{
						debugMaterialPB.SetInt(UniformIDs._DebugSliceAxis, 2);
						debugMaterialPB.SetFloat(UniformIDs._DebugSliceOffset, debug.drawSliceOffsetZ);
						cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 3, MeshTopology.Quads, 4, 1, debugMaterialPB);
					}
				}

				if (debug.drawStrands)// motion vectors
				{
					CoreUtils.SetRenderTarget(cmd, motionVectorRT, depthStencilRT);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 4, MeshTopology.LineStrip, strands.strandParticleCount, strands.strandCount, debugMaterialPB);
				}
			}
		}

		void SetRenderMaterialParams(ref MaterialPropertyBlock mpb)
		{
			if (mpb == null)
				mpb = new MaterialPropertyBlock();

			mpb.SetInt(UniformIDs._StrandCount, strands.strandCount);
			mpb.SetInt(UniformIDs._StrandParticleCount, strands.strandParticleCount);

			mpb.SetVector(UniformIDs._VolumeCells, volume.volumeResolution * Vector3.one);
			mpb.SetVector(UniformIDs._VolumeWorldMin, GetVolumeCenter() - GetVolumeExtent());
			mpb.SetVector(UniformIDs._VolumeWorldMax, GetVolumeCenter() + GetVolumeExtent());

			mpb.SetBuffer(UniformIDs._ParticlePosition, particlePosition);
			mpb.SetBuffer(UniformIDs._ParticleVelocity, particleVelocity);
			mpb.SetBuffer(UniformIDs._ParticlePositionPrev, particlePositionPrev);
			mpb.SetBuffer(UniformIDs._ParticleVelocityPrev, particleVelocityPrev);

			mpb.SetTexture(UniformIDs._VolumeDensity, volumeDensity);
			mpb.SetTexture(UniformIDs._VolumeVelocityX, volumeVelocityX);
			mpb.SetTexture(UniformIDs._VolumeVelocityY, volumeVelocityY);
			mpb.SetTexture(UniformIDs._VolumeVelocityZ, volumeVelocityZ);
			mpb.SetTexture(UniformIDs._VolumeVelocity, volumeVelocity);
			mpb.SetTexture(UniformIDs._VolumeGradient, volumeGradient);
		}

		void SetComputeParams(CommandBuffer cmd, ComputeShader cs, float dt)
		{
			float strandParticleInterval = strands.strandLength / (strands.strandParticleCount - 1);

			Matrix4x4 localToWorld = Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.localScale);
			Matrix4x4 localToWorldInvT = Matrix4x4.Transpose(Matrix4x4.Inverse(localToWorld));

			cmd.SetComputeMatrixParam(cs, UniformIDs._LocalToWorld, localToWorld);
			cmd.SetComputeMatrixParam(cs, UniformIDs._LocalToWorldInvT, localToWorldInvT);

			cmd.SetComputeIntParam(cs, UniformIDs._StrandCount, strands.strandCount);
			cmd.SetComputeIntParam(cs, UniformIDs._StrandParticleCount, strands.strandParticleCount);
			cmd.SetComputeFloatParam(cs, UniformIDs._StrandParticleInterval, strandParticleInterval);

			cmd.SetComputeFloatParam(cs, UniformIDs._DT, dt);
			cmd.SetComputeIntParam(cs, UniformIDs._Iterations, solver.iterations);
			cmd.SetComputeFloatParam(cs, UniformIDs._Stiffness, solver.stiffness);
			cmd.SetComputeFloatParam(cs, UniformIDs._Inference, solver.inference);
			cmd.SetComputeFloatParam(cs, UniformIDs._Damping, solver.damping);
			cmd.SetComputeFloatParam(cs, UniformIDs._Gravity, solver.gravity * -Vector3.Magnitude(Physics.gravity));
			cmd.SetComputeFloatParam(cs, UniformIDs._Repulsion, solver.repulsion);
			cmd.SetComputeFloatParam(cs, UniformIDs._Friction, solver.friction);

			cmd.SetComputeFloatParam(cs, UniformIDs._BendingCurvature, solver.bendingCurvature * 0.5f * strandParticleInterval);
			cmd.SetComputeFloatParam(cs, UniformIDs._BendingStiffness, solver.bendingStiffness);
			cmd.SetComputeFloatParam(cs, UniformIDs._DampingFTL, solver.dampingFTL);

			cmd.SetComputeIntParam(cs, UniformIDs._BoundaryCapsuleCount, boundaryCapsuleCount);
			cmd.SetComputeIntParam(cs, UniformIDs._BoundarySphereCount, boundarySphereCount);
			cmd.SetComputeIntParam(cs, UniformIDs._BoundaryTorusCount, boundaryTorusCount);

			cmd.SetComputeVectorParam(cs, UniformIDs._VolumeCells, volume.volumeResolution * Vector3.one);
			cmd.SetComputeVectorParam(cs, UniformIDs._VolumeWorldMin, GetVolumeCenter() - GetVolumeExtent());
			cmd.SetComputeVectorParam(cs, UniformIDs._VolumeWorldMax, GetVolumeCenter() + GetVolumeExtent());
		}

		void SetComputeKernelParams(CommandBuffer cmd, ComputeShader cs, int kernel)
		{
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootPosition, rootPosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootTangent, rootTangent);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionPrev, particlePositionPrev);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocityPrev, particleVelocityPrev);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePosition, particlePosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionCorr, particlePositionCorr);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocity, particleVelocity);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryCapsule, boundaryCapsule);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundarySphere, boundarySphere);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryTorus, boundaryTorus);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryPack, boundaryPack);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeDensity, volumeDensity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocityX, volumeVelocityX);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocityY, volumeVelocityY);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocityZ, volumeVelocityZ);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocity, volumeVelocity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeGradient, volumeGradient);
		}
	}
}