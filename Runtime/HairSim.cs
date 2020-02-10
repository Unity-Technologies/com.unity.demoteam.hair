#define LAYOUT_INTERLEAVED
//#define REDUCE_PRECISION// depends on int16_t being supported in hlsl

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#if REDUCE_PRECISION
using UnityEngine.Experimental.Rendering;
#endif
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.DemoTeam.Attributes;

using Xorshift = Unity.Mathematics.Random;

namespace Unity.DemoTeam.Hair
{
	[ExecuteInEditMode]
	public class HairSim : MonoBehaviour
	{
		#region static set of instances
		public static HashSet<HairSim> instances = new HashSet<HairSim>();
		private void OnEnable() { instances.Add(this); }
		private void OnDisable() { instances.Remove(this); ReleaseBuffers(); ReleaseVolumes(); computeParams = Configuration.none; stepFrame = -1; }
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
		const int MAX_BOUNDARIES = 6;

		const int VOLUME_CELLS = 128;

		public static class UniformIDs
		{
			public static int _LocalToWorld = Shader.PropertyToID("_LocalToWorld");
			public static int _LocalToWorldInvT = Shader.PropertyToID("_LocalToWorldInvT");

			// strand params
			public static int _StrandCount = Shader.PropertyToID("_StrandCount");
			public static int _StrandParticleCount = Shader.PropertyToID("_StrandParticleCount");
			public static int _StrandParticleInterval = Shader.PropertyToID("_StrandParticleInterval");

			// solver params
			public static int _DT = Shader.PropertyToID("_DT");
			public static int _Iterations = Shader.PropertyToID("_Iterations");
			public static int _Inference = Shader.PropertyToID("_Inference");
			public static int _Stiffness = Shader.PropertyToID("_Stiffness");
			public static int _Damping = Shader.PropertyToID("_Damping");
			public static int _Gravity = Shader.PropertyToID("_Gravity");
			public static int _Repulsion = Shader.PropertyToID("_Repulsion");
			public static int _Friction = Shader.PropertyToID("_Friction");

			public static int _BendingCurvature = Shader.PropertyToID("_BendingCurvature");
			public static int _BendingRestRadius = Shader.PropertyToID("_BendingRestRadius");
			public static int _DampingFTL = Shader.PropertyToID("_DampingFTL");

			// particle buffers
			public static int _RootPosition = Shader.PropertyToID("_RootPosition");
			public static int _RootTangent = Shader.PropertyToID("_RootTangent");

			public static int _ParticlePosition = Shader.PropertyToID("_ParticlePosition");
			public static int _ParticlePositionTemp = Shader.PropertyToID("_ParticlePositionTemp");
			public static int _ParticleVelocity = Shader.PropertyToID("_ParticleVelocity");

			public static int _ParticlePositionPrev = Shader.PropertyToID("_ParticlePositionPrev");
			public static int _ParticleVelocityPrev = Shader.PropertyToID("_ParticleVelocityPrev");

			// boundary params
			public static int _BoundaryCapsuleCount = Shader.PropertyToID("_BoundaryCapsuleCount");
			public static int _BoundarySphereCount = Shader.PropertyToID("_BoundarySphereCount");
			public static int _BoundaryTorusCount = Shader.PropertyToID("_BoundaryTorusCount");

			public static int _BoundaryCapsule = Shader.PropertyToID("_BoundaryCapsule");
			public static int _BoundarySphere = Shader.PropertyToID("_BoundarySphere");
			public static int _BoundaryTorus = Shader.PropertyToID("_BoundaryTorus");

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
			public static int _DebugColor = Shader.PropertyToID("_DebugColor");
			public static int _DebugSliceAxis = Shader.PropertyToID("_DebugSliceAxis");
			public static int _DebugSliceOffset = Shader.PropertyToID("_DebugSliceOffset");
			public static int _DebugSliceDivider = Shader.PropertyToID("_DebugSliceDivider");
		}

		[Serializable]
		public struct Configuration : IEquatable<Configuration>
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
			public int strandCount;// must be multiple of THREAD_GROUP_SIZE
			[Range(3, MAX_STRAND_PARTICLES)]
			public int strandParticleCount;
			[Range(1e-2f, 5)]
			public float strandLength;

			public static readonly Configuration none = new Configuration();
			public static readonly Configuration basic = new Configuration()
			{
				style = Style.Curtain,
				strandCount = 128,
				strandParticleCount = 32,
				strandLength = 0.5f,
			};

			public bool Equals(Configuration other)
			{
				return	(style == other.style) &&
						(strandCount == other.strandCount) &&
						(strandParticleCount == other.strandParticleCount) &&
						(strandLength == other.strandLength);
			}
		}

		[Serializable, GenerateHLSL]
		public struct VolumeConfiguration
		{
			public enum Method : uint
			{
				Compute,
				RasterGS,
				RasterVS,
			}

			public enum Filtering : uint
			{
				None,
				Trilinear,
			}

			[Range(1, 128)]
			public uint volumeCells;
			public Method volumeMethod;
			public Filtering volumeFiltering;

			public static readonly VolumeConfiguration none = new VolumeConfiguration();
			public static readonly VolumeConfiguration basic = new VolumeConfiguration()
			{
				volumeCells = 64,
				volumeMethod = Method.Compute,
				volumeFiltering = Filtering.Trilinear,
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
			[Range(1.0f, 2.0f)]
			public float inference;
			[Range(0.0f, 1.0f)]
			public float stiffness;
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
			public float bendingRestRadius;
			[Range(0.0f, 1.0f)]
			public float dampingFTL;

			public static readonly SolverConfiguration none = new SolverConfiguration();
			public static readonly SolverConfiguration basic = new SolverConfiguration()
			{
				method = Method.GaussSeidelReference,
				iterations = 3,
				inference = 1.0f,
				stiffness = 0.0f,
				damping = 0.0f,
				gravity = 1.0f,
				repulsion = 0.0f,
				friction = 0.0f,

				bendingCurvature = 0.0f,
				bendingRestRadius = 0.0f,
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
				drawSliceOffsetX = 0.5f,
				drawSliceOffsetY = 0.5f,
				drawSliceOffsetZ = 0.5f,
				drawSliceDivider = 0.5f,
			};
		}

		public Configuration configuration = Configuration.basic;
		public VolumeConfiguration volume;
		public DebugConfiguration debug = DebugConfiguration.basic;
		public SolverConfiguration solver = SolverConfiguration.basic;

		public List<HairSimBoundary> boundaries = new List<HairSimBoundary>(MAX_BOUNDARIES);

		[HideInInspector] public ComputeShader compute;
		[HideInInspector] public ComputeShader computeVolume;
		[HideInInspector] public Material computeVolumeRaster;
		private MaterialPropertyBlock computeVolumeRasterPB;

		[NonSerialized] public Configuration computeParams = Configuration.none;
		[HideInInspector] public Material debugMaterial;
		private MaterialPropertyBlock debugMaterialPB;

		private int kernelUpdatePosition;
		private int kernelSolveConstraints_GaussSeidelReference;
		private int kernelSolveConstraints_GaussSeidel;
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
		private ComputeBuffer particlePositionTemp;
		private ComputeBuffer particleVelocity;

		private ComputeBuffer particlePositionPrev;
		private ComputeBuffer particleVelocityPrev;

		private ComputeBuffer boundaryCapsule;
		private ComputeBuffer boundarySphere;
		private ComputeBuffer boundaryTorus;

		private RenderTexture volumeDensity;
		private RenderTexture volumeVelocityX;
		private RenderTexture volumeVelocityY;
		private RenderTexture volumeVelocityZ;
		private RenderTexture volumeVelocity;
		private RenderTexture volumeGradient;

		private int stepFrame = -1;

		void ValidateConfiguration(SolverConfiguration.Method method)
		{
			switch (method)
			{
				case SolverConfiguration.Method.GaussSeidelReference:
				case SolverConfiguration.Method.GaussSeidel:
					{
						configuration.strandCount = THREAD_GROUP_SIZE * (configuration.strandCount / THREAD_GROUP_SIZE);
						configuration.strandCount = Mathf.Max(THREAD_GROUP_SIZE, configuration.strandCount);
					}
					break;

				case SolverConfiguration.Method.Jacobi:
					{
						if (configuration.strandParticleCount < 48)
							configuration.strandParticleCount = 32;
						else
						{
							if (configuration.strandParticleCount < 96)
								configuration.strandParticleCount = 64;
							else
								configuration.strandParticleCount = 128;
						}
					}
					break;
			}
		}

		private void OnValidate()
		{
			ValidateConfiguration(solver.method);

			if (boundaries.Count > MAX_BOUNDARIES)
			{
				boundaries.RemoveRange(MAX_BOUNDARIES, boundaries.Count - MAX_BOUNDARIES);
				boundaries.TrimExcess();
			}
		}

		private void CreateBuffers(in Configuration conf)
		{
			unsafe
			{
				int rootCount = conf.strandCount;
				int rootStride = sizeof(Vector4);

				int positionCount = conf.strandCount * conf.strandParticleCount;
				int positionStride = sizeof(Vector4);

				int velocityCount = conf.strandCount * conf.strandParticleCount;
				int velocityStride = sizeof(Vector4);

				CreateBuffer(ref rootPosition, "RootPosition", rootCount, rootStride);
				CreateBuffer(ref rootTangent, "RootTangent", rootCount, rootStride);

				CreateBuffer(ref particlePosition, "ParticlePosition_0", positionCount, positionStride);
				CreateBuffer(ref particlePositionTemp, "ParticlePosition_T", positionCount, positionStride);
				CreateBuffer(ref particleVelocity, "ParticleVelocity_0", velocityCount, velocityStride);

				CreateBuffer(ref particlePositionPrev, "ParticlePosition_1", positionCount, positionStride);
				CreateBuffer(ref particleVelocityPrev, "ParticleVelocity_1", velocityCount, velocityStride);

				int boundaryCapsuleStride = sizeof(HairSimBoundary.BoundaryCapsule);
				int boundarySphereStride = sizeof(HairSimBoundary.BoundarySphere);
				int boundaryTorusStride = sizeof(HairSimBoundary.BoundaryTorus);

				CreateBuffer(ref boundaryCapsule, "BoundaryCapsule", MAX_BOUNDARIES, boundaryCapsuleStride);
				CreateBuffer(ref boundarySphere, "BoundarySphere", MAX_BOUNDARIES, boundarySphereStride);
				CreateBuffer(ref boundaryTorus, "BoundaryTorus", MAX_BOUNDARIES, boundaryTorusStride);
			}
		}

		private static void CreateBuffer(ref ComputeBuffer cb, string name, int count, int stride, ComputeBufferType type = ComputeBufferType.Default)
		{
			if (cb != null && cb.count == count && cb.stride == stride && cb.IsValid())
				return;

			if (cb != null)
				cb.Release();

			cb = new ComputeBuffer(count, stride, type);
			cb.name = name;
		}

		private static void ReleaseBuffer(ref ComputeBuffer cb)
		{
			if (cb != null)
			{
				cb.Release();
				cb = null;
			}
		}

		private void ReleaseBuffers()
		{
			ReleaseBuffer(ref rootPosition);
			ReleaseBuffer(ref rootTangent);

			ReleaseBuffer(ref particlePosition);
			ReleaseBuffer(ref particlePositionTemp);
			ReleaseBuffer(ref particleVelocity);

			ReleaseBuffer(ref particlePositionPrev);
			ReleaseBuffer(ref particleVelocityPrev);

			ReleaseBuffer(ref boundaryCapsule);
			ReleaseBuffer(ref boundarySphere);
			ReleaseBuffer(ref boundaryTorus);
		}

		private static void SwapBuffers(ref ComputeBuffer cbA, ref ComputeBuffer cbB)
		{
			ComputeBuffer tmp = cbA;
			cbA = cbB;
			cbB = tmp;
		}

		private void CreateVolumes()
		{
#if REDUCE_PRECISION
			CreateVolume(ref volumeDensity, "VolumeDensity", VOLUME_CELLS, GraphicsFormat.R16_SInt);//TODO switch to R16_SInt
			CreateVolume(ref volumeVelocityX, "VolumeVelocityX", VOLUME_CELLS, GraphicsFormat.R16_SInt);
			CreateVolume(ref volumeVelocityY, "VolumeVelocityY", VOLUME_CELLS, GraphicsFormat.R16_SInt);
			CreateVolume(ref volumeVelocityZ, "VolumeVelocityZ", VOLUME_CELLS, GraphicsFormat.R16_SInt);
			CreateVolume(ref volumeVelocity, "VolumeVelocity", VOLUME_CELLS, GraphicsFormat.R32G32B32A32_SFloat);
			CreateVolume(ref volumeGradient, "VolumeGradient", VOLUME_CELLS, GraphicsFormat.R32G32B32A32_SFloat);
#else
			CreateVolume(ref volumeDensity, "VolumeDensity", VOLUME_CELLS, RenderTextureFormat.RInt);//TODO switch to R16_SInt
			CreateVolume(ref volumeVelocityX, "VolumeVelocityX", VOLUME_CELLS, RenderTextureFormat.RInt);
			CreateVolume(ref volumeVelocityY, "VolumeVelocityY", VOLUME_CELLS, RenderTextureFormat.RInt);
			CreateVolume(ref volumeVelocityZ, "VolumeVelocityZ", VOLUME_CELLS, RenderTextureFormat.RInt);
			CreateVolume(ref volumeVelocity, "VolumeVelocity", VOLUME_CELLS, RenderTextureFormat.ARGBFloat);
			CreateVolume(ref volumeGradient, "VolumeGradient", VOLUME_CELLS, RenderTextureFormat.ARGBFloat);
#endif
		}

#if REDUCE_PRECISION
		private void CreateVolume(ref RenderTexture volume, string name, int cells, GraphicsFormat format = GraphicsFormat.R32G32B32A32_SFloat)
#else
		private void CreateVolume(ref RenderTexture volume, string name, int cells, RenderTextureFormat format = RenderTextureFormat.Default)
#endif
		{
#if REDUCE_PRECISION
			if (volume != null && volume.width == cells && volume.graphicsFormat == format)
#else
			if (volume != null && volume.width == cells && volume.format == format)
#endif
				return;

			if (volume != null)
				volume.Release();

			RenderTextureDescriptor volumeDesc = new RenderTextureDescriptor()
			{
				dimension = TextureDimension.Tex3D,
				width = cells,
				height = cells,
				volumeDepth = cells,
#if REDUCE_PRECISION
				graphicsFormat = format,
#else
				colorFormat = format,
#endif
				enableRandomWrite = true,
				msaaSamples = 1,
			};

			volume = new RenderTexture(volumeDesc);
			volume.hideFlags = HideFlags.HideAndDontSave;
			volume.name = name;
			volume.Create();

			//Debug.Log("volume " + volume.name + " -> enableRandomWrite = " + volume.enableRandomWrite + ", volumeDepth = " + volume.volumeDepth);
		}

		private void ReleaseVolume(ref RenderTexture volume)
		{
			if (volume != null)
			{
				volume.Release();
				volume = null;
			}
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

		private struct StrandRoot
		{
			public Vector3 localPos;
			public Vector3 localTan;
		}

		private NativeArray<StrandRoot> GetStrandRoots(in Configuration conf)
		{
			using (ProfilerMarkers.GetStrandRoots.Auto())
			{
				NativeArray<StrandRoot> strandRoots = new NativeArray<StrandRoot>(conf.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

				if (conf.style == Configuration.Style.Curtain)
				{
					float strandSpan = 1.0f;
					float strandInterval = strandSpan / (conf.strandCount - 1);

					unsafe
					{
						var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
						var strandRoot = new StrandRoot()
						{
							localPos = (-0.5f * strandSpan) * Vector3.right,
							localTan = Vector3.down,
						};
						for (int j = 0; j != conf.strandCount; j++)
						{
							strandRootsPtr[j] = strandRoot;
							strandRoot.localPos += strandInterval * Vector3.right;
						}
					}
				}
				else if (conf.style == Configuration.Style.StratifiedCurtain)
				{
					var xorshift = new Unity.Mathematics.Random(257);

					float strandSpan = 1.0f;
					float strandInterval = strandSpan / (conf.strandCount - 2);

					unsafe
					{
						var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
						var strandRoot = new StrandRoot()
						{
							localPos = ( + 0.5f * strandInterval) * Vector3.right,
							localTan = Vector3.down,
						};
						for (int j = 0; j != conf.strandCount; j++)
						{
							var localPos = (0.5f + j + xorshift.NextFloat(-0.5f, 0.5f)) * strandInterval - 0.5f * strandSpan;
							strandRootsPtr[j] = strandRoot;
							strandRoot.localPos = new Vector3(localPos, 0.0f, 0.0f);
						}
					}
				}
				else if (conf.style == Configuration.Style.Brush)
				{
					var localExt = 0.5f * Vector3.one;
					var xorshift = new Unity.Mathematics.Random(257);

					Vector2 localMin = new Vector2(-localExt.x, -localExt.z);
					Vector2 localMax = new Vector2( localExt.x,  localExt.z);

					unsafe
					{
						var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
						var strandRoot = new StrandRoot()
						{
							localTan = Vector3.down
						};
						for (int j = 0; j != conf.strandCount; j++)
						{
							var localPos = xorshift.NextFloat2(localMin, localMax);
							strandRoot.localPos = new Vector3(localPos.x, 0.0f, localPos.y);
							strandRootsPtr[j] = strandRoot;
						}
					}
				}
				else if (conf.style == Configuration.Style.Cap)
				{
					var localExt = 0.5f * Vector3.one;
					var xorshift = new Unity.Mathematics.Random(257);

					unsafe
					{
						var strandRootsPtr = (StrandRoot*)strandRoots.GetUnsafePtr();
						for (int j = 0; j != conf.strandCount; j++)
						{
							var localDir = xorshift.NextFloat3Direction();
							if (localDir.y < 0.0f)
								localDir.y = -localDir.y;

							strandRootsPtr[j].localPos = localDir * localExt;
							strandRootsPtr[j].localTan = localDir;
						}
					}
				}

				return strandRoots;
			}
		}

		private int GetKernel(in SolverConfiguration conf)
		{
			switch (conf.method)
			{
				case SolverConfiguration.Method.GaussSeidelReference:
					return kernelSolveConstraints_GaussSeidelReference;
				case SolverConfiguration.Method.GaussSeidel:
					return kernelSolveConstraints_GaussSeidel;
				case SolverConfiguration.Method.Jacobi:
					if (computeParams.strandParticleCount == 32)
						return kernelSolveConstraints_Jacobi_32;
					else if (computeParams.strandParticleCount == 64)
						return kernelSolveConstraints_Jacobi_64;
					else
						return kernelSolveConstraints_Jacobi_128;
			}

			return -1;
		}

		[Range(-1.0f, 1.0f)] public float offsetX;//TODO remove
		[Range(-1.0f, 1.0f)] public float offsetY;//TODO remove
		public bool volumeSplatCompute = true;
		public bool volumeSplatRasterGeom = true;

		private Vector3 GetVolumeCenter()
		{
			return this.transform.position + offsetX * Vector3.right + offsetY * Vector3.up;
		}

		private Vector3 GetVolumeExtent()
		{
			return (0.75f + 1.2f * computeParams.strandLength) * Vector3.one;
		}

		public void Init(CommandBuffer cmd, in Configuration conf)
		{
			using (ProfilerMarkers.Init.Auto())
			{
				//Debug.Log("Init");
				CreateBuffers(conf);
				CreateVolumes();

				using (NativeArray<StrandRoot> tmpRoots = GetStrandRoots(conf))
				using (NativeArray<Vector4> tmpRootsPos = new NativeArray<Vector4>(conf.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpRootsTan = new NativeArray<Vector4>(conf.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpPosition = new NativeArray<Vector4>(conf.strandCount * conf.strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpVelocity = new NativeArray<Vector4>(conf.strandCount * conf.strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
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

						float strandParticleInterval = conf.strandLength / (conf.strandParticleCount - 1);
						for (int j = 0; j != conf.strandCount; j++)
						{
							ptrRootsPos[j] = ptrRoots[j].localPos;
							ptrRootsTan[j] = ptrRoots[j].localTan;

							Vector3 strandDirection = localToWorldInvT.MultiplyVector(ptrRoots[j].localTan);
							Vector3 strandParticlePosition = localToWorld.MultiplyPoint3x4(ptrRoots[j].localPos);

#if LAYOUT_INTERLEAVED
							int strandParticleBegin = j;
							int strandParticleStride = conf.strandCount;
#else
							int strandParticleBegin = j * conf.strandParticleCount;
							int strandParticleStride = 1;
#endif
							int strandParticleEnd = strandParticleBegin + strandParticleStride * conf.strandParticleCount;
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
					particlePositionTemp.SetData(tmpVelocity);
					particleVelocity.SetData(tmpVelocity);
				}

				kernelUpdatePosition = compute.FindKernel("KUpdatePosition");
				kernelSolveConstraints_GaussSeidelReference = compute.FindKernel("KSolveConstraints_GaussSeidelReference");
				kernelSolveConstraints_GaussSeidel = compute.FindKernel("KSolveConstraints_GaussSeidel");
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

				computeParams = conf;

				stepFrame = -1;
			}
		}

		public void Step(CommandBuffer cmd, float dt)
		{
			using (ProfilerMarkers.Step.Auto())
			{
				if (!computeParams.Equals(configuration))
					Init(cmd, configuration);
				if (!computeParams.Equals(configuration))
					return;

				if (stepFrame != Time.frameCount)
					stepFrame = Time.frameCount;
				else
					return;

				SwapBuffers(ref particlePosition, ref particlePositionPrev);
				SwapBuffers(ref particleVelocity, ref particleVelocityPrev);

				//-------------------
				// update boundaries

				//TODO test moving these to cbuffer for speed
				int boundaryCapsuleCount = 0;
				int boundarySphereCount = 0;
				int boundaryTorusCount = 0;

				using (NativeArray<HairSimBoundary.BoundaryCapsule> tmpCapsule = new NativeArray<HairSimBoundary.BoundaryCapsule>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<HairSimBoundary.BoundarySphere> tmpSphere = new NativeArray<HairSimBoundary.BoundarySphere>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (NativeArray<HairSimBoundary.BoundaryTorus> tmpTorus = new NativeArray<HairSimBoundary.BoundaryTorus>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						HairSimBoundary.BoundaryCapsule* ptrCapsule = (HairSimBoundary.BoundaryCapsule*)tmpCapsule.GetUnsafePtr();
						HairSimBoundary.BoundarySphere* ptrSphere = (HairSimBoundary.BoundarySphere*)tmpSphere.GetUnsafePtr();
						HairSimBoundary.BoundaryTorus* ptrTorus = (HairSimBoundary.BoundaryTorus*)tmpTorus.GetUnsafePtr();

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
					}

					boundaryCapsule.SetData(tmpCapsule, 0, 0, boundaryCapsuleCount);
					boundarySphere.SetData(tmpSphere, 0, 0, boundarySphereCount);
					boundaryTorus.SetData(tmpTorus, 0, 0, boundaryTorusCount);
				}

				//------------------
				// set and dispatch

				Matrix4x4 localToWorld = Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.localScale);
				Matrix4x4 localToWorldInvT = Matrix4x4.Transpose(Matrix4x4.Inverse(localToWorld));

				cmd.SetComputeMatrixParam(compute, UniformIDs._LocalToWorld, localToWorld);
				cmd.SetComputeMatrixParam(compute, UniformIDs._LocalToWorldInvT, localToWorldInvT);

				cmd.SetComputeIntParam(compute, UniformIDs._StrandCount, computeParams.strandCount);
				cmd.SetComputeIntParam(compute, UniformIDs._StrandParticleCount, computeParams.strandParticleCount);
				cmd.SetComputeFloatParam(compute, UniformIDs._StrandParticleInterval, computeParams.strandLength / (computeParams.strandParticleCount - 1));

				cmd.SetComputeFloatParam(compute, UniformIDs._DT, dt);
				cmd.SetComputeIntParam(compute, UniformIDs._Iterations, solver.iterations);
				cmd.SetComputeFloatParam(compute, UniformIDs._Inference, solver.inference);
				cmd.SetComputeFloatParam(compute, UniformIDs._Stiffness, solver.stiffness);
				cmd.SetComputeFloatParam(compute, UniformIDs._Damping, solver.damping);
				cmd.SetComputeFloatParam(compute, UniformIDs._Gravity, solver.gravity * -Vector3.Magnitude(Physics.gravity));
				cmd.SetComputeFloatParam(compute, UniformIDs._Repulsion, solver.repulsion);
				cmd.SetComputeFloatParam(compute, UniformIDs._Friction, solver.friction);

				cmd.SetComputeFloatParam(compute, UniformIDs._BendingCurvature, solver.bendingCurvature);
				cmd.SetComputeFloatParam(compute, UniformIDs._BendingRestRadius, solver.bendingRestRadius);
				cmd.SetComputeFloatParam(compute, UniformIDs._DampingFTL, solver.dampingFTL);

				cmd.SetComputeIntParam(compute, UniformIDs._BoundaryCapsuleCount, boundaryCapsuleCount);
				cmd.SetComputeIntParam(compute, UniformIDs._BoundarySphereCount, boundarySphereCount);
				cmd.SetComputeIntParam(compute, UniformIDs._BoundaryTorusCount, boundaryTorusCount);

				Vector3 volumeWorldMin = GetVolumeCenter() - GetVolumeExtent();
				Vector3 volumeWorldMax = GetVolumeCenter() + GetVolumeExtent();

				cmd.SetComputeVectorParam(compute, UniformIDs._VolumeCells, VOLUME_CELLS * Vector3.one);
				cmd.SetComputeVectorParam(compute, UniformIDs._VolumeWorldMin, volumeWorldMin);
				cmd.SetComputeVectorParam(compute, UniformIDs._VolumeWorldMax, volumeWorldMax);

				//int particleThreadGroupsX = particlePosition.count / THREAD_GROUP_SIZE;
				//int particleThreadGroupsY = 1;
				//int particleThreadGroupsZ = 1;

				//SetKernelBufferParams(cmd, compute, kernelUpdatePosition);
				//cmd.DispatchCompute(compute, kernelUpdatePosition,
				//	particleThreadGroupsX,
				//	particleThreadGroupsY,
				//	particleThreadGroupsZ);

				int strandThreadGroupsX = computeParams.strandCount / THREAD_GROUP_SIZE;
				int strandThreadGroupsY = 1;
				int strandThreadGroupsZ = 1;

				int kernelSolveConstraints = GetKernel(solver);
				if (kernelSolveConstraints == kernelSolveConstraints_Jacobi_32 ||
					kernelSolveConstraints == kernelSolveConstraints_Jacobi_64 ||
					kernelSolveConstraints == kernelSolveConstraints_Jacobi_128)
				{
					strandThreadGroupsX = computeParams.strandCount;
				}

				SetComputeKernelParams(cmd, compute, kernelSolveConstraints);
				cmd.DispatchCompute(compute, kernelSolveConstraints,
					strandThreadGroupsX,
					strandThreadGroupsY,
					strandThreadGroupsZ);

				//SetKernelBufferParams(cmd, compute, kernelUpdateVelocity);
				//cmd.DispatchCompute(compute, kernelUpdateVelocity,
				//	particleThreadGroupsX,
				//	particleThreadGroupsY,
				//	particleThreadGroupsZ);
			}
		}

		public void Voxelize(CommandBuffer cmd, HDCamera hdCamera)
		{
			using (ProfilerMarkers.Voxelize.Auto())
			{
				if (!computeParams.Equals(configuration))
					Init(cmd, configuration);
				if (!computeParams.Equals(configuration))
					return;

				Vector3 volumeWorldMin = GetVolumeCenter() - GetVolumeExtent();
				Vector3 volumeWorldMax = GetVolumeCenter() + GetVolumeExtent();

				cmd.SetComputeVectorParam(computeVolume, UniformIDs._VolumeCells, VOLUME_CELLS * Vector3.one);
				cmd.SetComputeVectorParam(computeVolume, UniformIDs._VolumeWorldMin, volumeWorldMin);
				cmd.SetComputeVectorParam(computeVolume, UniformIDs._VolumeWorldMax, volumeWorldMax);

				int particleThreadCountX = 64;
				int particleThreadGroupsX = particlePosition.count / particleThreadCountX;
				int particleThreadGroupsY = 1;
				int particleThreadGroupsZ = 1;

				int volumeThreadCountXYZ = 4;
				int volumeThreadGroupsX = VOLUME_CELLS / volumeThreadCountXYZ;
				int volumeThreadGroupsY = VOLUME_CELLS / volumeThreadCountXYZ;
				int volumeThreadGroupsZ = VOLUME_CELLS / volumeThreadCountXYZ;

				using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeClear (GPU)"))
				{
					SetComputeKernelParams(cmd, computeVolume, kernelVolumeClear);
					cmd.DispatchCompute(computeVolume, kernelVolumeClear,
						volumeThreadGroupsX,
						volumeThreadGroupsY,
						volumeThreadGroupsZ);
				}

				if (volumeSplatCompute)
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
				else
				{
					using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeSplatParticles (GPU)"))
					{
						SetRenderMaterialParams(ref computeVolumeRasterPB);
						CoreUtils.SetRenderTarget(cmd, volumeVelocity, ClearFlag.Color);

						if (volumeSplatRasterGeom)
							cmd.DrawProcedural(Matrix4x4.identity, computeVolumeRaster, 0, MeshTopology.Points, computeParams.strandCount * computeParams.strandParticleCount, 1, computeVolumeRasterPB);
						else
							cmd.DrawProcedural(Matrix4x4.identity, computeVolumeRaster, 1, MeshTopology.Quads, 8 * computeParams.strandCount * computeParams.strandParticleCount, 1, computeVolumeRasterPB);
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
			using (ProfilerMarkers.Draw.Auto())
			{
				if (!computeParams.Equals(configuration))
					Init(cmd, configuration);
				if (!computeParams.Equals(configuration))
					return;

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
					debugMaterialPB.SetColor(UniformIDs._DebugColor, Color.red);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 0, MeshTopology.LineStrip, computeParams.strandParticleCount, computeParams.strandCount, debugMaterialPB);
				}

				if (debug.drawParticles)
				{
					debugMaterialPB.SetColor(UniformIDs._DebugColor, Color.green);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 0, MeshTopology.Points, computeParams.strandParticleCount, computeParams.strandCount, debugMaterialPB);
				}

				if (debug.drawDensity)
				{
					debugMaterialPB.SetColor(UniformIDs._DebugColor, Color.green);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 1, MeshTopology.Points, 1, VOLUME_CELLS * VOLUME_CELLS * VOLUME_CELLS, debugMaterialPB);
				}

				if (debug.drawGradient)
				{
					debugMaterialPB.SetColor(UniformIDs._DebugColor, Color.cyan);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 2, MeshTopology.Lines, 2, VOLUME_CELLS * VOLUME_CELLS * VOLUME_CELLS, debugMaterialPB);
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
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 4, MeshTopology.LineStrip, computeParams.strandParticleCount, computeParams.strandCount, debugMaterialPB);
				}
			}
		}

		static void SetConstantBufferData<T>(ComputeBuffer cbuf, ref T value) where T : struct
		{
			using (NativeArray<T> data = new NativeArray<T>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				unsafe
				{
					UnsafeUtility.CopyStructureToPtr(ref value, data.GetUnsafePtr());
					cbuf.SetData(data);
				}
			}
		}

		void SetRenderMaterialParams(ref MaterialPropertyBlock mpb)
		{
			if (mpb == null)
				mpb = new MaterialPropertyBlock();

			mpb.SetInt(UniformIDs._StrandCount, computeParams.strandCount);
			mpb.SetInt(UniformIDs._StrandParticleCount, computeParams.strandParticleCount);

			Vector3 volumeWorldMin = GetVolumeCenter() - GetVolumeExtent();
			Vector3 volumeWorldMax = GetVolumeCenter() + GetVolumeExtent();

			mpb.SetVector(UniformIDs._VolumeCells, VOLUME_CELLS * Vector3.one);
			mpb.SetVector(UniformIDs._VolumeWorldMin, volumeWorldMin);
			mpb.SetVector(UniformIDs._VolumeWorldMax, volumeWorldMax);

			mpb.SetBuffer(UniformIDs._ParticlePosition, particlePosition);
			mpb.SetBuffer(UniformIDs._ParticlePositionPrev, particlePositionPrev);

			mpb.SetBuffer(UniformIDs._ParticleVelocity, particleVelocity);
			mpb.SetBuffer(UniformIDs._ParticleVelocityPrev, particleVelocityPrev);

			mpb.SetTexture(UniformIDs._VolumeDensity, volumeDensity);
			mpb.SetTexture(UniformIDs._VolumeVelocityX, volumeVelocityX);
			mpb.SetTexture(UniformIDs._VolumeVelocityY, volumeVelocityY);
			mpb.SetTexture(UniformIDs._VolumeVelocityZ, volumeVelocityZ);
			mpb.SetTexture(UniformIDs._VolumeVelocity, volumeVelocity);
			mpb.SetTexture(UniformIDs._VolumeGradient, volumeGradient);

			mpb.SetFloat("_VolumeSplatCompute", volumeSplatCompute ? 1.0f : 0.0f);
		}

		void SetComputeKernelParams(CommandBuffer cmd, ComputeShader cs, int kernel)
		{
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootPosition, rootPosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootTangent, rootTangent);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePosition, particlePosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionTemp, particlePositionTemp);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocity, particleVelocity);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionPrev, particlePositionPrev);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocityPrev, particleVelocityPrev);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryCapsule, boundaryCapsule);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundarySphere, boundarySphere);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryTorus, boundaryTorus);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeDensity, volumeDensity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocityX, volumeVelocityX);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocityY, volumeVelocityY);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocityZ, volumeVelocityZ);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocity, volumeVelocity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeGradient, volumeGradient);
		}

		private void OnDrawGizmos()
		{
			if (debug.drawDensity || debug.drawGradient || debug.drawSliceX || debug.drawSliceY || debug.drawSliceZ)
			{
				Gizmos.color = Color.Lerp(Color.black, Color.clear, 0.5f);
				Gizmos.DrawWireCube(GetVolumeCenter(), 2.0f * GetVolumeExtent());
			}
		}
	}
}