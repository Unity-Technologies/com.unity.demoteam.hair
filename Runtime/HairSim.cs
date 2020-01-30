#define LAYOUT_INTERLEAVED

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
		const int MAX_BOUNDARIES = 8;

		const int VOLUME_SIZE = 16;

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
			public static int _VolumeSize = Shader.PropertyToID("_VolumeSize");
			public static int _VolumeWorldMin = Shader.PropertyToID("_VolumeWorldMin");
			public static int _VolumeWorldMax = Shader.PropertyToID("_VolumeWorldMax");

			// volume buffers
			public static int _VolumeDensity = Shader.PropertyToID("_VolumeDensity");
			public static int _VolumeGradient = Shader.PropertyToID("_VolumeGradient");

			// debug params
			public static int _DebugColor = Shader.PropertyToID("_DebugColor");
		}

		[Serializable]
		public struct Configuration : IEquatable<Configuration>
		{
			public enum Style
			{
				Curtain,
				Brush,
				Cap,
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

			public static readonly DebugConfiguration none = new DebugConfiguration();
			public static readonly DebugConfiguration basic = new DebugConfiguration()
			{
				drawParticles = true,
				drawStrands = true,
				drawDensity = false,
			};
		}

		public Configuration configuration = Configuration.basic;
		public DebugConfiguration debug = DebugConfiguration.basic;
		public SolverConfiguration solver = SolverConfiguration.basic;
		public List<HairSimBoundary> boundaries = new List<HairSimBoundary>(MAX_BOUNDARIES);

		[HideInInspector] public ComputeShader compute;
		[HideInInspector] public ComputeShader computeVolume;

		[NonSerialized] public Configuration computeParams = Configuration.none;
		[HideInInspector] public Material debugMaterial;

		private int kernelUpdatePosition;
		private int kernelSolveConstraints_GaussSeidelReference;
		private int kernelSolveConstraints_GaussSeidel;
		private int kernelSolveConstraints_Jacobi_32;
		private int kernelSolveConstraints_Jacobi_64;
		private int kernelSolveConstraints_Jacobi_128;
		private int kernelUpdateVelocity;

		private int kernelVolumeClear;
		private int kernelVolumeDensity;
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
			CreateVolume(ref volumeDensity, "VolumeDensity", VOLUME_SIZE, RenderTextureFormat.RInt);//TODO replace with graphicsformat.r8_uint etc.
			CreateVolume(ref volumeGradient, "VolumeGradient", VOLUME_SIZE, RenderTextureFormat.ARGBFloat);
		}

		private void CreateVolume(ref RenderTexture volume, string name, int width, RenderTextureFormat format = RenderTextureFormat.Default)
		{
			if (volume != null && volume.width == width)
				return;

			if (volume != null)
				volume.Release();

			RenderTextureDescriptor volumeDesc = new RenderTextureDescriptor()
			{
				dimension = TextureDimension.Tex3D,
				width = width,
				height = width,
				volumeDepth = width,
				colorFormat = format,
				enableRandomWrite = true,
				msaaSamples = 1,
			};

			volume = new RenderTexture(volumeDesc);
			volume.hideFlags = HideFlags.HideAndDontSave;
			volume.name = name;

			Debug.Log("volume " + volume.name + " -> enableRandomWrite = " + volume.enableRandomWrite + ", volumeDepth = " + volume.volumeDepth);
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

				//---------------------
				// update strand roots

				/*
				//TODO just pass transform to shader instead
				using (NativeArray<StrandRoot> tmpRoots = GetStrandRoots(computeParams))
				using (NativeArray<Vector4> tmpRootsPos = new NativeArray<Vector4>(computeParams.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (NativeArray<Vector4> tmpRootsTan = new NativeArray<Vector4>(computeParams.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					unsafe
					{
						StrandRoot* srcRoots = (StrandRoot*)tmpRoots.GetUnsafePtr();
						Vector3* srcRootsPos = &srcRoots[0].localPos;
						Vector3* srcRootsTan = &srcRoots[0].localTan;

						Vector4* ptrRootsPos = (Vector4*)tmpRootsPos.GetUnsafePtr();
						Vector4* ptrRootsTan = (Vector4*)tmpRootsTan.GetUnsafePtr();

						int dstStride = sizeof(Vector4);
						int srcStride = sizeof(StrandRoot);

						UnsafeUtility.MemCpyStride(ptrRootsPos, dstStride, srcRootsPos, srcStride, sizeof(Vector3), computeParams.strandCount);
						UnsafeUtility.MemCpyStride(ptrRootsTan, dstStride, srcRootsTan, srcStride, sizeof(Vector3), computeParams.strandCount);
					}

					rootPosition.SetData(tmpRootsPos);
					rootTangent.SetData(tmpRootsTan);
				}
				*/

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

				cmd.SetComputeFloatParam(compute, UniformIDs._BendingCurvature, solver.bendingCurvature);
				cmd.SetComputeFloatParam(compute, UniformIDs._BendingRestRadius, solver.bendingRestRadius);
				cmd.SetComputeFloatParam(compute, UniformIDs._DampingFTL, solver.dampingFTL);

				cmd.SetComputeIntParam(compute, UniformIDs._BoundaryCapsuleCount, boundaryCapsuleCount);
				cmd.SetComputeIntParam(compute, UniformIDs._BoundarySphereCount, boundarySphereCount);
				cmd.SetComputeIntParam(compute, UniformIDs._BoundaryTorusCount, boundaryTorusCount);

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

				SetKernelBufferParams(cmd, compute, kernelSolveConstraints);
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

		public void Voxelize(CommandBuffer cmd)
		{
			using (ProfilerMarkers.Voxelize.Auto())
			{
				if (!computeParams.Equals(configuration))
					Init(cmd, configuration);
				if (!computeParams.Equals(configuration))
					return;

				Vector3 volumeWorldMin = this.transform.position - 1.0f * computeParams.strandLength * Vector3.one;
				Vector3 volumeWorldMax = this.transform.position + 1.0f * computeParams.strandLength * Vector3.one;

				cmd.SetComputeVectorParam(computeVolume, UniformIDs._VolumeSize, VOLUME_SIZE * Vector3.one);
				cmd.SetComputeVectorParam(computeVolume, UniformIDs._VolumeWorldMin, volumeWorldMin);
				cmd.SetComputeVectorParam(computeVolume, UniformIDs._VolumeWorldMax, volumeWorldMax);

				using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeClear (GPU)"))
				{
					int clearThreadCountX = 16;
					int clearThreadGroupsX = VOLUME_SIZE / clearThreadCountX;
					int clearThreadGroupsY = 1;
					int clearThreadGroupsZ = 1;

					SetKernelBufferParams(cmd, computeVolume, kernelVolumeClear);
					cmd.DispatchCompute(computeVolume, kernelVolumeClear,
						clearThreadGroupsX,
						clearThreadGroupsY,
						clearThreadGroupsZ);
				}

				using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeDensity (GPU)"))
				{
					int densityThreadCountX = 16;
					int densityThreadGroupsX = particlePosition.count / densityThreadCountX;
					int densityThreadGroupsY = 1;
					int densityThreadGroupsZ = 1;

					SetKernelBufferParams(cmd, computeVolume, kernelVolumeDensity);
					cmd.DispatchCompute(computeVolume, kernelVolumeDensity,
						densityThreadGroupsX,
						densityThreadGroupsY,
						densityThreadGroupsZ);
				}

				//using (new ProfilingSample(cmd, "HairSim.Voxelize.VolumeGradient (GPU)"))
				//{
				//	var gradientThreadCountXYZ = 4;
				//	int gradientThreadGroupsX = VOLUME_SIZE / gradientThreadCountXYZ;
				//	int gradientThreadGroupsY = VOLUME_SIZE / gradientThreadCountXYZ;
				//	int gradientThreadGroupsZ = VOLUME_SIZE / gradientThreadCountXYZ;

				//	SetKernelBufferParams(cmd, computeVolume, kernelVolumeGradient);
				//	cmd.DispatchCompute(computeVolume, kernelVolumeGradient,
				//		gradientThreadGroupsX,
				//		gradientThreadGroupsY,
				//		gradientThreadGroupsZ);
				//}
			}
		}

		public void Draw(CommandBuffer cmd)
		{
			using (ProfilerMarkers.Draw.Auto())
			{
				if (!computeParams.Equals(configuration))
					Init(cmd, configuration);
				if (!computeParams.Equals(configuration))
					return;

				if (!debug.drawParticles && !debug.drawStrands && !debug.drawDensity)
					return;

				Vector3 volumeWorldMin = this.transform.position - 1.0f * computeParams.strandLength * Vector3.one;
				Vector3 volumeWorldMax = this.transform.position + 1.0f * computeParams.strandLength * Vector3.one;

				cmd.SetGlobalVector(UniformIDs._VolumeSize, VOLUME_SIZE * Vector3.one);
				cmd.SetGlobalVector(UniformIDs._VolumeWorldMin, volumeWorldMin);
				cmd.SetGlobalVector(UniformIDs._VolumeWorldMax, volumeWorldMax);

				cmd.SetGlobalInt(UniformIDs._StrandCount, computeParams.strandCount);
				cmd.SetGlobalInt(UniformIDs._StrandParticleCount, computeParams.strandParticleCount);

				cmd.SetGlobalBuffer(UniformIDs._ParticlePosition, particlePosition);
				cmd.SetGlobalBuffer(UniformIDs._ParticleVelocity, particleVelocity);

				cmd.SetGlobalTexture(UniformIDs._VolumeDensity, volumeDensity);
				cmd.SetGlobalTexture(UniformIDs._VolumeGradient, volumeGradient);

				if (debug.drawStrands)
				{
					cmd.SetGlobalColor(UniformIDs._DebugColor, Color.red);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 0, MeshTopology.LineStrip, computeParams.strandParticleCount, computeParams.strandCount);
				}

				if (debug.drawParticles)
				{
					cmd.SetGlobalColor(UniformIDs._DebugColor, Color.green);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 0, MeshTopology.Points, computeParams.strandParticleCount, computeParams.strandCount);
				}

				if (debug.drawDensity)
				{
					cmd.SetGlobalColor(UniformIDs._DebugColor, Color.green);
					cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, 1, MeshTopology.Points, 1, VOLUME_SIZE * VOLUME_SIZE * VOLUME_SIZE);
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

		void SetKernelBufferParams(CommandBuffer cmd, ComputeShader cs, int kernel)
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
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeGradient, volumeGradient);
		}
	}
}