#pragma warning disable 0649 // some fields are assigned via reflection

//#define LAYOUT_INTERLEAVED
//TODO !!

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

namespace Unity.DemoTeam.Hair
{
	using static HairSimUtility;

	[AddComponentMenu("")]
	public partial class HairSim : MonoBehaviour
	{
		static bool s_initialized = false;

		static ComputeShader s_solverCS;
		static ComputeShader s_volumeCS;

		static Material s_volumeRasterMat;
		static MaterialPropertyBlock s_volumeRasterMPB;
		static Material s_debugDrawMat;
		static MaterialPropertyBlock s_debugDrawMPB;

		static class MarkersCPU
		{
			public static ProfilerMarker Dummy;
		}
		static class MarkersGPU
		{
			public static ProfilingSampler Solver;
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
			public static ProfilingSampler DrawSolverData;
			public static ProfilingSampler DrawVolumeData;
		}

		public static class UniformIDs
		{
			// solver
			public static int SolverParams;

			public static int _RootPosition;
			public static int _RootDirection;

			public static int _ParticlePosition;
			public static int _ParticlePositionPrev;
			public static int _ParticlePositionCorr;
			public static int _ParticleVelocity;
			public static int _ParticleVelocityPrev;

			// volume
			public static int VolumeParams;

			public static int _AccuDensity;
			public static int _AccuVelocityX;
			public static int _AccuVelocityY;
			public static int _AccuVelocityZ;

			public static int _VolumeDensity;
			public static int _VolumeVelocity;
			public static int _VolumeDivergence;

			public static int _VolumePressure;
			public static int _VolumePressureNext;
			public static int _VolumePressureGrad;

			public static int _BoundaryPack;
			public static int _BoundaryMatrix;
			public static int _BoundaryMatrixInv;
			public static int _BoundaryMatrixW2PrevW;

			// debug
			public static int _DebugSliceAxis;
			public static int _DebugSliceOffset;
			public static int _DebugSliceDivider;
		}

		static class SolverKernels
		{
			public static int KSolveConstraints_GaussSeidelReference;
			public static int KSolveConstraints_GaussSeidel;
			public static int KSolveConstraints_Jacobi_16;
			public static int KSolveConstraints_Jacobi_32;
			public static int KSolveConstraints_Jacobi_64;
			public static int KSolveConstraints_Jacobi_128;
			public static int KUpdatePosition;
			public static int KUpdateVelocity;
		}
		static class VolumeKernels
		{
			public static int KVolumeClear;
			public static int KVolumeSplat;
			public static int KVolumeSplatDensity;
			public static int KVolumeSplatVelocityX;
			public static int KVolumeSplatVelocityY;
			public static int KVolumeSplatVelocityZ;
			public static int KVolumeResolve;
			public static int KVolumeResolveFromRasterization;
			public static int KVolumeDivergence;
			public static int KVolumePressureEOS;
			public static int KVolumePressureSolve;
			public static int KVolumePressureGradient;
		}

		[Serializable] public struct SolverSettings
		{
			public enum Method
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

			public static readonly SolverSettings defaults = new SolverSettings()
			{
				method = Method.GaussSeidel,
				iterations = 5,
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
		[Serializable] public struct VolumeSettings
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
			[HideInInspector]
			public Vector3 volumeWorldCenter;
			[HideInInspector]
			public Vector3 volumeWorldExtent;

			public static readonly VolumeSettings defaults = new VolumeSettings()
			{
				volumeSplatMethod = SplatMethod.Compute,
				volumeResolution = 48,
				volumeOffsetX = 0.0f,
				volumeOffsetY = 0.0f,
				volumeOffsetZ = 0.0f,
				volumePressureIterations = 0,
			};
		}
		[Serializable] public struct DebugSettings
		{
			public bool drawParticles;
			public bool drawStrands;
			public bool drawDensity;
			public bool drawSliceX;
			public bool drawSliceY;
			public bool drawSliceZ;

			[Range(0.0f, 1.0f)] public float drawSliceOffsetX;
			[Range(0.0f, 1.0f)] public float drawSliceOffsetY;
			[Range(0.0f, 1.0f)] public float drawSliceOffsetZ;
			[Range(0.0f, 4.0f)] public float drawSliceDivider;

			public static readonly DebugSettings defaults = new DebugSettings()
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

		[HideInInspector] public ComputeShader solverCS;
		[HideInInspector] public ComputeShader volumeCS;

		[HideInInspector] public Material volumeRasterMat;
		[HideInInspector] public Material debugDrawMat;

		public const int MAX_GROUP_SIZE = 64;
		public const int MAX_BOUNDARIES = 8;
		public const int MAX_STRAND_COUNT = 64000;
		public const int MAX_STRAND_PARTICLE_COUNT = 128;

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#else
		[RuntimeInitializeOnLoadMethod]
#endif
		static void StaticInitialize()
		{
			if (s_initialized == false)
			{
				var instance = ComponentSingleton<HairSim>.instance;
				{
					s_solverCS = instance.solverCS;
					s_volumeCS = instance.volumeCS;

					s_volumeRasterMat = new Material(instance.volumeRasterMat);
					s_volumeRasterMat.hideFlags = HideFlags.HideAndDontSave;
					s_volumeRasterMPB = new MaterialPropertyBlock();

					s_debugDrawMat = new Material(instance.debugDrawMat);
					s_debugDrawMat.hideFlags = HideFlags.HideAndDontSave;
					s_debugDrawMPB = new MaterialPropertyBlock();
				}

				InitializeStaticFields(typeof(MarkersCPU), (string s) => new ProfilerMarker("HairSim." + s.Replace('_', '.')));
				InitializeStaticFields(typeof(MarkersGPU), (string s) => new ProfilingSampler("HairSim." + s.Replace('_', '.')));
				InitializeStaticFields(typeof(UniformIDs), (string s) => Shader.PropertyToID(s));

				InitializeStaticFields(typeof(SolverKernels), (string s) => s_solverCS.FindKernel(s));
				InitializeStaticFields(typeof(VolumeKernels), (string s) => s_volumeCS.FindKernel(s));

				s_initialized = true;
			}
		}

		public static bool PrepareSolverData(ref SolverData solverData, int strandCount, int strandParticleCount)
		{
			unsafe
			{
				bool changed = false;

				int particleCount = strandCount * strandParticleCount;
				int particleStride = sizeof(Vector4);

				changed |= CreateBuffer(ref solverData.rootPosition, "RootPosition", strandCount, particleStride);
				changed |= CreateBuffer(ref solverData.rootDirection, "RootDirection", strandCount, particleStride);

				changed |= CreateBuffer(ref solverData.particlePosition, "ParticlePosition_0", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particlePositionPrev, "ParticlePosition_1", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particlePositionCorr, "ParticlePositionCorr", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particleVelocity, "ParticleVelocity_0", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particleVelocityPrev, "ParticleVelocity_1", particleCount, particleStride);

				return changed;
			}
		}

		public static void ReleaseSolverData(ref SolverData solverData)
		{
			ReleaseBuffer(ref solverData.rootPosition);
			ReleaseBuffer(ref solverData.rootDirection);

			ReleaseBuffer(ref solverData.particlePosition);
			ReleaseBuffer(ref solverData.particlePositionPrev);
			ReleaseBuffer(ref solverData.particlePositionCorr);
			ReleaseBuffer(ref solverData.particleVelocity);
			ReleaseBuffer(ref solverData.particleVelocityPrev);

			solverData = new SolverData();
		}

		public static bool PrepareVolumeData(ref VolumeData volumeData, int volumeCellCount)
		{
			unsafe
			{
				bool changed = false;

				changed |= CreateVolume(ref volumeData.accuDensity, "AccuDensity", volumeCellCount, GraphicsFormat.R32_SInt);//TODO switch to R16_SInt
				changed |= CreateVolume(ref volumeData.accuVelocityX, "AccuVelocityX", volumeCellCount, GraphicsFormat.R32_SInt);
				changed |= CreateVolume(ref volumeData.accuVelocityY, "AccuVelocityY", volumeCellCount, GraphicsFormat.R32_SInt);
				changed |= CreateVolume(ref volumeData.accuVelocityZ, "AccuVelocityZ", volumeCellCount, GraphicsFormat.R32_SInt);

				changed |= CreateVolume(ref volumeData.volumeDensity, "VolumeDensity", volumeCellCount, RenderTextureFormat.RFloat);
				changed |= CreateVolume(ref volumeData.volumeVelocity, "VolumeVelocity", volumeCellCount, RenderTextureFormat.ARGBFloat);
				changed |= CreateVolume(ref volumeData.volumeDivergence, "VolumeDivergence", volumeCellCount, RenderTextureFormat.RFloat);

				changed |= CreateVolume(ref volumeData.volumePressure, "VolumePressure_0", volumeCellCount, RenderTextureFormat.RFloat);
				changed |= CreateVolume(ref volumeData.volumePressureNext, "VolumePressure_1", volumeCellCount, RenderTextureFormat.RFloat);
				changed |= CreateVolume(ref volumeData.volumePressureGrad, "VolumePressureGrad", volumeCellCount, RenderTextureFormat.ARGBFloat);

				changed |= CreateBuffer(ref volumeData.boundaryPack, "BoundaryPack", MAX_BOUNDARIES, sizeof(HairSimBoundary.BoundaryPack));
				changed |= CreateBuffer(ref volumeData.boundaryMatrix, "BoundaryMatrix", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeData.boundaryMatrixInv, "BoundaryMatrixInv", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeData.boundaryMatrixW2PrevW, "BoundaryMatrixW2PrevW", MAX_BOUNDARIES, sizeof(Matrix4x4));

				if (volumeData.boundaryMatrixPrev.IsCreated && volumeData.boundaryMatrixPrev.Length != MAX_BOUNDARIES)
					volumeData.boundaryMatrixPrev.Dispose();
				if (volumeData.boundaryMatrixPrev.IsCreated == false)
					volumeData.boundaryMatrixPrev = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Persistent, NativeArrayOptions.ClearMemory);

				return changed;
			}
		}

		public static void ReleaseVolumeData(ref VolumeData volumeData)
		{
			ReleaseVolume(ref volumeData.accuDensity);
			ReleaseVolume(ref volumeData.accuVelocityX);
			ReleaseVolume(ref volumeData.accuVelocityY);
			ReleaseVolume(ref volumeData.accuVelocityZ);

			ReleaseVolume(ref volumeData.volumeDensity);
			ReleaseVolume(ref volumeData.volumeVelocity);

			ReleaseVolume(ref volumeData.volumeDivergence);
			ReleaseVolume(ref volumeData.volumePressure);
			ReleaseVolume(ref volumeData.volumePressureNext);
			ReleaseVolume(ref volumeData.volumePressureGrad);

			ReleaseBuffer(ref volumeData.boundaryPack);
			ReleaseBuffer(ref volumeData.boundaryMatrix);
			ReleaseBuffer(ref volumeData.boundaryMatrixInv);
			ReleaseBuffer(ref volumeData.boundaryMatrixW2PrevW);

			if (volumeData.boundaryMatrixPrev.IsCreated)
				volumeData.boundaryMatrixPrev.Dispose();

			volumeData = new VolumeData();
		}

		public static void UpdateSolverData(ref SolverData solverData, in SolverSettings solverSettings, float dt)
		{
			ref var cbuffer = ref solverData.cbuffer;

			// update strand parameters
			cbuffer._LocalToWorld = Matrix4x4.identity;
			cbuffer._LocalToWorldInvT = Matrix4x4.identity;

			//TODO find a better place for these
			//float strandCrossSectionArea = 0.25f * Mathf.PI * solverSettings.strandDiameter * solverSettings.strandDiameter;
			//float strandParticleInterval = solverSettings.strandLength / (cbuffer._StrandParticleCount - 1);
			//float strandParticleVolume = (1000.0f * strandParticleInterval) * strandCrossSectionArea;
			//cbuffer._StrandParticleInterval = strandParticleInterval;
			//cbuffer._StrandParticleVolume = strandParticleVolume;
			//cbuffer._StrandParticleContrib = solverSettings.strandParticleContrib;

			// update solver parameters
			cbuffer._DT = dt;
			cbuffer._Iterations = (uint)solverSettings.iterations;
			cbuffer._Stiffness = solverSettings.stiffness;
			cbuffer._Relaxation = solverSettings.relaxation;
			cbuffer._Damping = solverSettings.damping;
			cbuffer._Gravity = solverSettings.gravity * -Vector3.Magnitude(Physics.gravity);
			cbuffer._Repulsion = solverSettings.repulsion;
			cbuffer._Friction = solverSettings.friction;
			cbuffer._BendingCurvature = solverSettings.bendingCurvature * 0.5f * cbuffer._StrandParticleInterval;
			cbuffer._DampingFTL = solverSettings.dampingFTL;
		}

		public static void UpdateVolumeData(ref VolumeData volumeData, in VolumeSettings volumeSettings, List<HairSimBoundary> boundaries)
		{
			ref var cbuffer = ref volumeData.cbuffer;

			// update volume parameters
			cbuffer._VolumeCells = volumeSettings.volumeResolution * Vector3.one;
			cbuffer._VolumeWorldMin = volumeSettings.volumeWorldCenter - volumeSettings.volumeWorldExtent;
			cbuffer._VolumeWorldMax = volumeSettings.volumeWorldCenter + volumeSettings.volumeWorldExtent;

			// update boundary shapes
			cbuffer._BoundaryCapsuleCount = 0;
			cbuffer._BoundarySphereCount = 0;
			cbuffer._BoundaryTorusCount = 0;

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
					var ptrMatrixPrev = (Matrix4x4*)volumeData.boundaryMatrixPrev.GetUnsafePtr();

					foreach (HairSimBoundary boundary in boundaries)
					{
						if (boundary == null || !boundary.isActiveAndEnabled)
							continue;

						switch (boundary.type)
						{
							case HairSimBoundary.Type.Capsule:
								cbuffer._BoundaryCapsuleCount++;
								break;
							case HairSimBoundary.Type.Sphere:
								cbuffer._BoundarySphereCount++;
								break;
							case HairSimBoundary.Type.Torus:
								cbuffer._BoundaryTorusCount++;
								break;
						}
					}

					int boundaryCapsuleIndex = 0;
					int boundarySphereIndex = boundaryCapsuleIndex + cbuffer._BoundaryCapsuleCount;
					int boundaryTorusIndex = boundarySphereIndex + cbuffer._BoundarySphereCount;

					int boundaryCount = cbuffer._BoundaryCapsuleCount + cbuffer._BoundarySphereCount + boundaryTorusIndex;
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

						boundaryHash.Append(boundary.GetInstanceID());
					}

					if (volumeData.boundaryHash != boundaryHash)
					{
						Debug.Log("boundary hash changed: " + boundaryHash);
						volumeData.boundaryHash = boundaryHash;

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

					volumeData.boundaryPack.SetData(tmpPack, 0, 0, boundaryCount);
					volumeData.boundaryMatrix.SetData(tmpMatrix, 0, 0, boundaryCount);
					volumeData.boundaryMatrixInv.SetData(tmpMatrixInv, 0, 0, boundaryCount);
					volumeData.boundaryMatrixW2PrevW.SetData(tmpMatrixW2PrevW, 0, 0, boundaryCount);
					volumeData.boundaryMatrixPrev.CopyFrom(tmpMatrix);
				}
			}
		}

		public static void PushSolverData(CommandBuffer cmd, ComputeShader cs, int kernel, in SolverData solverData)
		{
			ConstantBuffer.Push(cmd, solverData.cbuffer, cs, UniformIDs.SolverParams);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootPosition, solverData.rootPosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._RootDirection, solverData.rootDirection);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePosition, solverData.particlePosition);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionPrev, solverData.particlePositionPrev);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticlePositionCorr, solverData.particlePositionCorr);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocity, solverData.particleVelocity);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._ParticleVelocityPrev, solverData.particleVelocityPrev);
		}

		public static void PushSolverData(CommandBuffer cmd, Material mat, MaterialPropertyBlock mpb, in SolverData solverData)
		{
			ConstantBuffer.Push(cmd, solverData.cbuffer, mat, UniformIDs.SolverParams);

			mpb.SetBuffer(UniformIDs._RootPosition, solverData.rootPosition);
			mpb.SetBuffer(UniformIDs._RootDirection, solverData.rootDirection);

			mpb.SetBuffer(UniformIDs._ParticlePosition, solverData.particlePosition);
			mpb.SetBuffer(UniformIDs._ParticlePositionPrev, solverData.particlePositionPrev);
			mpb.SetBuffer(UniformIDs._ParticlePositionCorr, solverData.particlePositionCorr);
			mpb.SetBuffer(UniformIDs._ParticleVelocity, solverData.particleVelocity);
			mpb.SetBuffer(UniformIDs._ParticleVelocityPrev, solverData.particleVelocityPrev);
		}

		public static void PushVolumeData(CommandBuffer cmd, ComputeShader cs, int kernel, in VolumeData volumeData)
		{
			ConstantBuffer.Push(cmd, volumeData.cbuffer, cs, UniformIDs.VolumeParams);

			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryPack, volumeData.boundaryPack);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryMatrix, volumeData.boundaryMatrix);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryMatrixInv, volumeData.boundaryMatrixInv);
			cmd.SetComputeBufferParam(cs, kernel, UniformIDs._BoundaryMatrixW2PrevW, volumeData.boundaryMatrixW2PrevW);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuDensity, volumeData.accuDensity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuVelocityX, volumeData.accuVelocityX);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuVelocityY, volumeData.accuVelocityY);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._AccuVelocityZ, volumeData.accuVelocityZ);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeDensity, volumeData.volumeDensity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeVelocity, volumeData.volumeVelocity);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumeDivergence, volumeData.volumeDivergence);

			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumePressure, volumeData.volumePressure);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumePressureNext, volumeData.volumePressureNext);
			cmd.SetComputeTextureParam(cs, kernel, UniformIDs._VolumePressureGrad, volumeData.volumePressureGrad);
		}

		public static void PushVolumeData(CommandBuffer cmd, Material mat, MaterialPropertyBlock mpb, in VolumeData volumeData)
		{
			ConstantBuffer.Push(cmd, volumeData.cbuffer, mat, UniformIDs.VolumeParams);

			mpb.SetBuffer(UniformIDs._BoundaryPack, volumeData.boundaryPack);
			mpb.SetBuffer(UniformIDs._BoundaryMatrix, volumeData.boundaryMatrix);
			mpb.SetBuffer(UniformIDs._BoundaryMatrixInv, volumeData.boundaryMatrixInv);
			mpb.SetBuffer(UniformIDs._BoundaryMatrixW2PrevW, volumeData.boundaryMatrixW2PrevW);

			mpb.SetTexture(UniformIDs._AccuDensity, volumeData.accuDensity);
			mpb.SetTexture(UniformIDs._AccuVelocityX, volumeData.accuVelocityX);
			mpb.SetTexture(UniformIDs._AccuVelocityY, volumeData.accuVelocityY);
			mpb.SetTexture(UniformIDs._AccuVelocityZ, volumeData.accuVelocityZ);

			mpb.SetTexture(UniformIDs._VolumeDensity, volumeData.volumeDensity);
			mpb.SetTexture(UniformIDs._VolumeVelocity, volumeData.volumeVelocity);
			mpb.SetTexture(UniformIDs._VolumeDivergence, volumeData.volumeDivergence);
			
			mpb.SetTexture(UniformIDs._VolumePressure, volumeData.volumePressure);
			mpb.SetTexture(UniformIDs._VolumePressureNext, volumeData.volumePressureNext);
			mpb.SetTexture(UniformIDs._VolumePressureGrad, volumeData.volumePressureGrad);
		}

		public static void StepSolverData(CommandBuffer cmd, ref SolverData solverData, in SolverSettings solverSettings, in VolumeData volumeData)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Solver))
			{
				int solveKernel = SolverKernels.KSolveConstraints_GaussSeidelReference;
				int groupCountX = (int)solverData.cbuffer._StrandCount / MAX_GROUP_SIZE;
				int groupCountY = 1;
				int groupCountZ = 1;

				switch (solverSettings.method)
				{
					case SolverSettings.Method.GaussSeidelReference:
						solveKernel = SolverKernels.KSolveConstraints_GaussSeidelReference;
						break;

					case SolverSettings.Method.GaussSeidel:
						solveKernel = SolverKernels.KSolveConstraints_GaussSeidel;
						break;

					case SolverSettings.Method.Jacobi:
						switch (solverData.cbuffer._StrandParticleCount)
						{
							case 16:
								solveKernel = SolverKernels.KSolveConstraints_Jacobi_16;
								groupCountX = (int)solverData.cbuffer._StrandCount;
								break;

							case 32:
								solveKernel = SolverKernels.KSolveConstraints_Jacobi_32;
								groupCountX = (int)solverData.cbuffer._StrandCount;
								break;

							case 64:
								solveKernel = SolverKernels.KSolveConstraints_Jacobi_64;
								groupCountX = (int)solverData.cbuffer._StrandCount;
								break;

							case 128:
								solveKernel = SolverKernels.KSolveConstraints_Jacobi_128;
								groupCountX = (int)solverData.cbuffer._StrandCount;
								break;
						}
						break;
				}

				SwapBuffers(ref solverData.particlePosition, ref solverData.particlePositionPrev);
				SwapBuffers(ref solverData.particleVelocity, ref solverData.particleVelocityPrev);

				PushVolumeData(cmd, s_solverCS, solveKernel, volumeData);
				PushSolverData(cmd, s_solverCS, solveKernel, solverData);

				cmd.DispatchCompute(s_solverCS, solveKernel, groupCountX, groupCountY, groupCountZ);
			}
		}

		public static void StepVolumeData(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings, in SolverData[] solverData)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Volume))
			{
				StepVolumeData_Clear(cmd, ref volumeData, volumeSettings);

				for (int i = 0; i != solverData.Length; i++)
				{
					StepVolumeData_Insert(cmd, ref volumeData, volumeSettings, solverData[i]);
				}

				StepVolumeData_Resolve(cmd, ref volumeData, volumeSettings);
			}
		}

		private static void StepVolumeData_Clear(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings)
		{
			int numX = volumeSettings.volumeResolution / 8;
			int numY = volumeSettings.volumeResolution / 8;
			int numZ = volumeSettings.volumeResolution;

			// clear
			using (new ProfilingScope(cmd, MarkersGPU.Volume_0_Clear))
			{
				PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeClear, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeClear, numX, numY, numZ);
			}
		}

		private static void StepVolumeData_Insert(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings, in SolverData solverData)
		{
			int particleCount = (int)solverData.cbuffer._StrandCount * (int)solverData.cbuffer._StrandParticleCount;

			int numX = particleCount / MAX_GROUP_SIZE;
			int numY = 1;
			int numZ = 1;

			// accumulate
			switch (volumeSettings.volumeSplatMethod)
			{
				case VolumeSettings.SplatMethod.Compute:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat))
						{
							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, volumeData);
							PushSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplat, numX, numY, numZ);
						}
					}
					break;

				case VolumeSettings.SplatMethod.ComputeSplit:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatDensity))
						{
							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, volumeData);
							PushSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatDensity, numX, numY, numZ);
						}

						using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatVelocityXYZ))
						{
							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, volumeData);
							PushSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, numX, numY, numZ);

							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, volumeData);
							PushSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, numX, numY, numZ);

							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, volumeData);
							PushSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, numX, numY, numZ);
						}
					}
					break;

				case VolumeSettings.SplatMethod.Rasterization:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatByRasterization))
						{
							CoreUtils.SetRenderTarget(cmd, volumeData.volumeVelocity, ClearFlag.Color);
							PushVolumeData(cmd, s_volumeRasterMat, s_volumeRasterMPB, volumeData);
							PushSolverData(cmd, s_volumeRasterMat, s_volumeRasterMPB, solverData);
							cmd.DrawProcedural(Matrix4x4.identity, s_volumeRasterMat, 0, MeshTopology.Points, particleCount, 1, s_volumeRasterMPB);
						}
					}
					break;

				case VolumeSettings.SplatMethod.RasterizationNoGS:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_1_SplatByRasterizationNoGS))
						{
							CoreUtils.SetRenderTarget(cmd, volumeData.volumeVelocity, ClearFlag.Color);
							PushVolumeData(cmd, s_volumeRasterMat, s_volumeRasterMPB, volumeData);
							PushSolverData(cmd, s_volumeRasterMat, s_volumeRasterMPB, solverData);
							cmd.DrawProcedural(Matrix4x4.identity, s_volumeRasterMat, 1, MeshTopology.Quads, particleCount * 8, 1, s_volumeRasterMPB);
						}
					}
					break;
			}
		}

		private static void StepVolumeData_Resolve(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings)
		{
			int numX = volumeSettings.volumeResolution / 8;
			int numY = volumeSettings.volumeResolution / 8;
			int numZ = volumeSettings.volumeResolution;

			// resolve accumulated
			switch (volumeSettings.volumeSplatMethod)
			{
				case VolumeSettings.SplatMethod.Compute:
				case VolumeSettings.SplatMethod.ComputeSplit:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_2_Resolve))
						{
							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeResolve, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolve, numX, numY, numZ);
						}
					}
					break;

				case VolumeSettings.SplatMethod.Rasterization:
				case VolumeSettings.SplatMethod.RasterizationNoGS:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_2_ResolveFromRasterization))
						{
							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeResolveFromRasterization, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolveFromRasterization, numX, numX, numX);
						}
					}
					break;
			}

			// compute divergence
			using (new ProfilingScope(cmd, MarkersGPU.Volume_3_Divergence))
			{
				PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeDivergence, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeDivergence, numX, numY, numZ);
			}

			// pressure eos (initial guess)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_4_PressureEOS))
			{
				PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureEOS, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureEOS, numX, numY, numZ);
			}

			// pressure solve (jacobi)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_5_PressureSolve))
			{
				PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureSolve, volumeData);
				for (int i = 0; i != volumeSettings.volumePressureIterations; i++)
				{
					cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureSolve, numX, numY, numZ);

					SwapVolumes(ref volumeData.volumePressure, ref volumeData.volumePressureNext);
					cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumePressureSolve, UniformIDs._VolumePressure, volumeData.volumePressure);
					cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumePressureSolve, UniformIDs._VolumePressureNext, volumeData.volumePressureNext);
				}
			}

			// pressure gradient
			using (new ProfilingScope(cmd, MarkersGPU.Volume_6_PressureGradient))
			{
				PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureGradient, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureGradient, numX, numY, numZ);
			}
		}

		public static void DrawSolverData(CommandBuffer cmd, RTHandle color, RTHandle depth, RTHandle movec, in SolverData solverData, in DebugSettings debugSettings)
		{
			using (new ProfilingScope(cmd, MarkersGPU.DrawSolverData))
			{
				if (!debugSettings.drawParticles &&
					!debugSettings.drawStrands)
					return;

				PushSolverData(cmd, s_debugDrawMat, s_debugDrawMPB, solverData);

				CoreUtils.SetRenderTarget(cmd, color, depth);

				// solver particles
				if (debugSettings.drawParticles)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 0, MeshTopology.Points, (int)solverData.cbuffer._StrandParticleCount, (int)solverData.cbuffer._StrandCount, s_debugDrawMPB);
				}

				// solver strands + movecs
				if (debugSettings.drawStrands)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 0, MeshTopology.LineStrip, (int)solverData.cbuffer._StrandParticleCount, (int)solverData.cbuffer._StrandCount, s_debugDrawMPB);

					CoreUtils.SetRenderTarget(cmd, movec, depth);
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 4, MeshTopology.LineStrip, (int)solverData.cbuffer._StrandParticleCount, (int)solverData.cbuffer._StrandCount);
				}
			}
		}

		public static void DrawVolumeData(CommandBuffer cmd, RTHandle color, RTHandle depth, in VolumeData volumeData, in DebugSettings debugSettings)
		{
			using (new ProfilingScope(cmd, MarkersGPU.DrawVolumeData))
			{
				if (!debugSettings.drawDensity &&
					!debugSettings.drawSliceX &&
					!debugSettings.drawSliceY &&
					!debugSettings.drawSliceZ)
					return;

				PushVolumeData(cmd, s_debugDrawMat, s_debugDrawMPB, volumeData);

				CoreUtils.SetRenderTarget(cmd, color, depth);

				// volume density
				if (debugSettings.drawDensity)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 1, MeshTopology.Points, GetCellCount(volumeData.cbuffer), 1);
				}

				// volume slices
				if (debugSettings.drawSliceX || debugSettings.drawSliceY || debugSettings.drawSliceZ)
				{
					s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceDivider, debugSettings.drawSliceDivider);

					if (debugSettings.drawSliceX)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 0);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceOffsetX);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 3, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
					if (debugSettings.drawSliceY)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 1);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceOffsetY);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 3, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
					if (debugSettings.drawSliceZ)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 2);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceOffsetZ);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 3, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
				}
			}
		}

		//TODO move elsewhere
		//maybe 'HairSimDataUtility' ?

		public static Vector3 GetCellSize(in VolumeParams volumeParams)
		{
			var cellSize = new Vector3(
				(volumeParams._VolumeWorldMax.x - volumeParams._VolumeWorldMin.x) / volumeParams._VolumeCells.x,
				(volumeParams._VolumeWorldMax.y - volumeParams._VolumeWorldMin.y) / volumeParams._VolumeCells.y,
				(volumeParams._VolumeWorldMax.z - volumeParams._VolumeWorldMin.z) / volumeParams._VolumeCells.z);
			return cellSize;
		}

		public static float GetCellVolume(in VolumeParams volumeParams)
		{
			var cellSize = GetCellSize(volumeParams);
			return cellSize.x * cellSize.y * cellSize.z;
		}

		public static int GetCellCount(in VolumeParams volumeParams)
		{
			return (int)volumeParams._VolumeCells.x * (int)volumeParams._VolumeCells.y * (int)volumeParams._VolumeCells.z;
		}

		public static Vector3 GetVolumeCenter(in VolumeParams volumeParams)
		{
			return (volumeParams._VolumeWorldMax + volumeParams._VolumeWorldMin) * 0.5f;
		}

		public static Vector3 GetVolumeExtent(in VolumeParams volumeParams)
		{
			return (volumeParams._VolumeWorldMax - volumeParams._VolumeWorldMin) * 0.5f;
		}
	}
}