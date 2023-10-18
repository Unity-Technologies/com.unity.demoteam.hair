#pragma warning disable 0162 // some parts will be unreachable due to branching on configuration constants
#pragma warning disable 0649 // some fields are assigned via reflection

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Mathematics;

namespace Unity.DemoTeam.Hair
{
	using static HairSimUtility;

	public static partial class HairSim
	{
		static bool s_initialized = false;

		static ComputeShader s_solverCS;
		static Material s_solverRootsMat;

		static ComputeShader s_volumeCS;
		static Material s_volumeRasterMat;

		static Mesh s_debugDrawCube;
		static Material s_debugDrawMat;
		static MaterialPropertyBlock s_debugDrawPb;

		static RuntimeFlags s_runtimeFlags;

		[Flags]
		enum RuntimeFlags
		{
			None = 0,
			SupportsGeometryStage = 1 << 0,
			SupportsTextureAtomics = 1 << 1,// NOTE: needs to be in sync with HairSimData.hlsl (PLATFORM_SUPPORTS_TEXTURE_ATOMICS)
			SupportsVertexUAVWrites = 1 << 2,
		}

		static class MarkersCPU
		{
			public static ProfilerMarker Dummy;
		}

		static class MarkersGPU
		{
			public static ProfilingSampler Roots;
			public static ProfilingSampler Solver;
			public static ProfilingSampler Solver_SubstepRoots;
			public static ProfilingSampler Solver_SolveConstraints;
			public static ProfilingSampler Solver_Interpolate;
			public static ProfilingSampler Solver_Staging;
			public static ProfilingSampler Volume;
			public static ProfilingSampler Volume_0_Clear;
			public static ProfilingSampler Volume_1_Splat;
			public static ProfilingSampler Volume_1_Splat_Density;
			public static ProfilingSampler Volume_1_Splat_VelocityXYZ;
			public static ProfilingSampler Volume_1_Splat_Rasterization;
			public static ProfilingSampler Volume_1_Splat_RasterizationNoGS;
			public static ProfilingSampler Volume_2_Resolve;
			public static ProfilingSampler Volume_2_ResolveRaster;
			public static ProfilingSampler Volume_3_Divergence;
			public static ProfilingSampler Volume_4_PressureEOS;
			public static ProfilingSampler Volume_5_PressureSolve;
			public static ProfilingSampler Volume_6_PressureGradient;
			public static ProfilingSampler Volume_7_Scattering;
			public static ProfilingSampler Volume_8_Wind;
			public static ProfilingSampler DrawSolverData;
			public static ProfilingSampler DrawVolumeData;
		}

		static class UniformIDs
		{
			// solver
			public static int _RootSubstepFraction;

			// debug
			public static int _DebugCluster;
			public static int _DebugSliceAxis;
			public static int _DebugSliceOffset;
			public static int _DebugSliceDivider;
			public static int _DebugSliceOpacity;
			public static int _DebugIsosurfaceDensity;
			public static int _DebugIsosurfaceSubsteps;
		}

		static class SolverKernels
		{
			public static int KRoots;
			public static int KRootsHistory;
			public static int KRootsSubstep;
			public static int KInitialize;
			public static int KInitializePostVolume;
			public static int KLODSelection;
			public static int KSolveConstraints_GaussSeidelReference;
			public static int KSolveConstraints_GaussSeidel;
			public static int KSolveConstraints_Jacobi_16;
			public static int KSolveConstraints_Jacobi_32;
			public static int KSolveConstraints_Jacobi_64;
			public static int KSolveConstraints_Jacobi_128;
			public static int KInterpolate;
			public static int KStaging;
			public static int KStagingSubdivision;
			public static int KStagingHistory;
		}

		static class VolumeKernels
		{
			public static int KBoundsClear;
			public static int KBoundsGather;
			public static int KBoundsResolve;
			public static int KBoundsResolveCombined;
			public static int KBoundsCoverage;
			public static int KLODSelection;
			public static int KVolumeClear;
			public static int KVolumeSplat;
			public static int KVolumeSplatDensity;
			public static int KVolumeSplatVelocityX;
			public static int KVolumeSplatVelocityY;
			public static int KVolumeSplatVelocityZ;
			public static int KVolumeResolve;
			public static int KVolumeResolveRaster;
			public static int KVolumeDivergence;
			public static int KVolumePressureEOS;
			public static int KVolumePressureSolve;
			public static int KVolumePressureGradient;
			public static int KVolumeScatteringPrep;
			public static int KVolumeScattering;
			public static int KVolumeWindPrep;
			public static int KVolumeWind;
		}

		//TODO move to conf
		public const int THREAD_GROUP_SIZE = 64;

		//TODO move to conf
		public const int MIN_STRAND_COUNT = 64;
		public const int MAX_STRAND_COUNT = 64000;
		public const int MIN_STRAND_PARTICLE_COUNT = 3;
		public const int MAX_STRAND_PARTICLE_COUNT = 128;

		static HairSim()
		{
			if (s_initialized == false)
			{
				s_runtimeFlags = RuntimeFlags.None;
				{
					switch (SystemInfo.graphicsDeviceType)
					{
						case GraphicsDeviceType.Direct3D11:
						case GraphicsDeviceType.Direct3D12:
						case GraphicsDeviceType.Vulkan:
							s_runtimeFlags |= RuntimeFlags.SupportsGeometryStage;
							s_runtimeFlags |= RuntimeFlags.SupportsTextureAtomics;
							s_runtimeFlags |= RuntimeFlags.SupportsVertexUAVWrites;
							break;

						case GraphicsDeviceType.Metal:
							break;

						default:
							s_runtimeFlags |= RuntimeFlags.SupportsGeometryStage;
							s_runtimeFlags |= RuntimeFlags.SupportsTextureAtomics;
							break;
					}
				}

				var resources = HairSimResources.Load();
				{
					s_solverCS = resources.computeSolver;
					s_solverRootsMat = CoreUtils.CreateEngineMaterial(resources.computeRoots);

					s_volumeCS = resources.computeVolume;
					s_volumeRasterMat = CoreUtils.CreateEngineMaterial(resources.computeVolumeRaster);

					s_debugDrawCube = resources.debugDrawCube;
					s_debugDrawMat = CoreUtils.CreateEngineMaterial(resources.debugDraw);
					s_debugDrawPb = new MaterialPropertyBlock();
				}


				InitializeStructFields(ref SolverData.s_bufferIDs, (string s) => Shader.PropertyToID(s));
				InitializeStructFields(ref SolverData.s_textureIDs, (string s) => Shader.PropertyToID(s));

				InitializeStructFields(ref VolumeData.s_bufferIDs, (string s) => Shader.PropertyToID(s));
				InitializeStructFields(ref VolumeData.s_textureIDs, (string s) => Shader.PropertyToID(s));

				InitializeStaticFields(typeof(MarkersCPU), (string s) => new ProfilerMarker("HairSim." + s.Replace('_', '.')));
				InitializeStaticFields(typeof(MarkersGPU), (string s) => new ProfilingSampler("HairSim." + s.Replace('_', '.')));
				InitializeStaticFields(typeof(UniformIDs), (string s) => Shader.PropertyToID(s));

				InitializeStaticFields(typeof(SolverKernels), (string s) => s_solverCS.FindKernel(s));
				InitializeStaticFields(typeof(VolumeKernels), (string s) => s_volumeCS.FindKernel(s));

#if UNITY_EDITOR
				UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () =>
				{
					Material.DestroyImmediate(HairSim.s_solverRootsMat);
					Material.DestroyImmediate(HairSim.s_volumeRasterMat);
					Material.DestroyImmediate(HairSim.s_debugDrawMat);
				};
#endif

				s_initialized = true;
			}
		}

		public static bool PrepareSolverData(ref SolverData solverData, int strandCount, int strandParticleCount, int lodCount)
		{
			ref var solverBuffers = ref solverData.buffers;

			unsafe
			{
				bool changed = false;

				int particleCount = strandCount * strandParticleCount;
				int particleStrideIndex = sizeof(uint);
				int particleStrideScalar = sizeof(float);
				int particleStrideVector2 = sizeof(Vector2);
				int particleStrideVector3 = sizeof(Vector3);
				int particleStrideVector4 = sizeof(Vector4);

				changed |= CreateBuffer(ref solverBuffers.SolverCBufferRoots, "SolverCBufferRoots", 1, sizeof(SolverCBufferRoots), ComputeBufferType.Constant);
				changed |= CreateBuffer(ref solverBuffers.SolverCBuffer, "SolverCBuffer", 1, sizeof(SolverCBuffer), ComputeBufferType.Constant);

				changed |= CreateBuffer(ref solverBuffers._RootUV, "RootUV", strandCount, particleStrideVector2);
				changed |= CreateBuffer(ref solverBuffers._RootScale, "RootScale", strandCount, particleStrideVector4);

				changed |= CreateBuffer(ref solverBuffers._RootPosition, "RootPosition_0", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootPositionPrev, "RootPosition_1", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootPositionSubstep, "RootPosition_t", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootFrame, "RootFrame_0", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootFramePrev, "RootFrame_1", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootFrameSubstep, "RootFrame_t", strandCount, particleStrideVector4);

				changed |= CreateBuffer(ref solverBuffers._SolverLODStage, "SolverLODStage", (int)SolverLODStage.__COUNT, sizeof(LODIndices));
				changed |= CreateBuffer(ref solverBuffers._SolverLODDispatch, "SolverLODDispatch", (int)SolverLODDispatch.__COUNT, sizeof(uint) * 4);

				changed |= CreateBuffer(ref solverBuffers._InitialParticleOffset, "InitialParticleOffset", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._InitialParticleFrameDelta, "InitialParticleFrameDelta", particleCount, particleStrideVector4);

				changed |= CreateBuffer(ref solverBuffers._ParticlePosition, "ParticlePosition_0", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._ParticlePositionPrev, "ParticlePosition_1", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._ParticlePositionPrevPrev, "ParticlePosition_2", (Conf.SECOND_ORDER_UPDATE != 0) ? particleCount : 1, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._ParticleVelocity, "ParticleVelocity_0", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._ParticleVelocityPrev, "ParticleVelocity_1", (Conf.SECOND_ORDER_UPDATE != 0) ? particleCount : 1, particleStrideVector4);

				changed |= CreateBuffer(ref solverBuffers._LODGuideCount, "LODGuideCount", Mathf.Max(1, lodCount), particleStrideIndex);
				changed |= CreateBuffer(ref solverBuffers._LODGuideIndex, "LODGuideIndex", Mathf.Max(1, lodCount) * strandCount, particleStrideIndex);
				changed |= CreateBuffer(ref solverBuffers._LODGuideCarry, "LODGuideCarry", Mathf.Max(1, lodCount) * strandCount, particleStrideScalar);
				changed |= CreateBuffer(ref solverBuffers._LODGuideReach, "LODGuideReach", Mathf.Max(1, lodCount) * strandCount, particleStrideScalar);

				return changed;
			}
		}

		public static bool PrepareVolumeData(ref VolumeData volumeData, in SettingsVolume settingsVolume, int boundsCount)
		{
			ref var volumeBuffers = ref volumeData.buffers;
			ref var volumeTextures = ref volumeData.textures;

			unsafe
			{
				bool changed = false;

				int particleStrideVector2 = sizeof(Vector2);
				int particleStrideVector3 = sizeof(Vector3);

				var cellCount = (int)settingsVolume.gridResolution;
				var cellPrecision = settingsVolume.gridPrecision;
				var cellFormatScalar = (cellPrecision == SettingsVolume.GridPrecision.Half) ? RenderTextureFormat.RHalf : RenderTextureFormat.RFloat;
				var cellFormatVector = (cellPrecision == SettingsVolume.GridPrecision.Half) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;
				var cellAccuFormat = GraphicsFormat.R32_SInt;//TODO switch to R16_SInt ?
				var cellAccuStride = 4;// num. bytes

				changed |= CreateBuffer(ref volumeBuffers.VolumeCBufferEnvironment, "VolumeCBufferEnvironment", 1, sizeof(VolumeCBufferEnvironment), ComputeBufferType.Constant);
				changed |= CreateBuffer(ref volumeBuffers.VolumeCBuffer, "VolumeCBuffer", 1, sizeof(VolumeCBuffer), ComputeBufferType.Constant);

				changed |= CreateBuffer(ref volumeBuffers._LODFrustum, "LODFrustum", Conf.MAX_FRUSTUMS, sizeof(LODFrustum));

				changed |= CreateBuffer(ref volumeBuffers._BoundaryShape, "BoundaryShape", Conf.MAX_BOUNDARIES, sizeof(HairBoundary.RuntimeShape.Data));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrix, "BoundaryMatrix", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrixInv, "BoundaryMatrixInv", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrixW2PrevW, "BoundaryMatrixW2PrevW", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));

				changed |= CreateBuffer(ref volumeBuffers._WindEmitter, "WindEmitter", Conf.MAX_EMITTERS, sizeof(HairWind.RuntimeEmitter));

				changed |= CreateBuffer(ref volumeBuffers._BoundsMinMaxU, "BoundsMinMaxU", boundsCount * 2, particleStrideVector3);
				changed |= CreateBuffer(ref volumeBuffers._Bounds, "Bounds", boundsCount, sizeof(LODBounds));
				changed |= CreateBuffer(ref volumeBuffers._BoundsGeometry, "BoundsGeometry", boundsCount, sizeof(LODGeometry));
				changed |= CreateBuffer(ref volumeBuffers._BoundsCoverage, "BoundsCoverage", boundsCount, particleStrideVector2);

				changed |= CreateBuffer(ref volumeBuffers._VolumeLODStage, "VolumeLODStage", (int)VolumeLODStage.__COUNT__, sizeof(VolumeLODGrid));
				changed |= CreateBuffer(ref volumeBuffers._VolumeLODDispatch, "VolumeLODDispatch", (int)VolumeLODDispatch.__COUNT__, sizeof(uint) * 4);

				if (s_runtimeFlags.HasFlag(RuntimeFlags.SupportsTextureAtomics))
				{
					changed |= CreateVolume(ref volumeTextures._AccuWeight, "AccuWeight", cellCount, cellAccuFormat);
					changed |= CreateVolume(ref volumeTextures._AccuWeight0, "AccuWeight0", cellCount, cellAccuFormat);
					changed |= CreateVolume(ref volumeTextures._AccuVelocityX, "AccuVelocityX", cellCount, cellAccuFormat);
					changed |= CreateVolume(ref volumeTextures._AccuVelocityY, "AccuVelocityY", cellCount, cellAccuFormat);
					changed |= CreateVolume(ref volumeTextures._AccuVelocityZ, "AccuVelocityZ", cellCount, cellAccuFormat);
				}
				else
				{
					int cellCountTotal = cellCount * cellCount * cellCount;

					changed |= CreateBuffer(ref volumeBuffers._AccuWeightBuffer, "AccuWeight", cellCountTotal, cellAccuStride);
					changed |= CreateBuffer(ref volumeBuffers._AccuWeight0Buffer, "AccuWeight0", cellCountTotal, cellAccuStride);
					changed |= CreateBuffer(ref volumeBuffers._AccuVelocityXBuffer, "AccuVelocityX", cellCountTotal, cellAccuStride);
					changed |= CreateBuffer(ref volumeBuffers._AccuVelocityYBuffer, "AccuVelocityY", cellCountTotal, cellAccuStride);
					changed |= CreateBuffer(ref volumeBuffers._AccuVelocityZBuffer, "AccuVelocityZ", cellCountTotal, cellAccuStride);
				}

				changed |= CreateVolume(ref volumeTextures._VolumeDensity, "VolumeDensity", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeTextures._VolumeDensity0, "VolumeDensity0", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeTextures._VolumeVelocity, "VolumeVelocity", cellCount, cellFormatVector);

				changed |= CreateVolume(ref volumeTextures._VolumeDivergence, "VolumeDivergence", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeTextures._VolumePressure, "VolumePressure_0", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeTextures._VolumePressureNext, "VolumePressure_1", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeTextures._VolumePressureGrad, "VolumePressureGrad", cellCount, cellFormatVector);

				changed |= CreateVolume(ref volumeTextures._VolumeScattering, "VolumeScattering", cellCount, cellFormatVector);
				changed |= CreateVolume(ref volumeTextures._VolumeImpulse, "VolumeImpulse", cellCount, cellFormatVector);

				changed |= CreateVolume(ref volumeTextures._BoundarySDF_undefined, "BoundarySDF_undefined", 1, RenderTextureFormat.RHalf);

				CreateReadbackBuffer(ref volumeData.buffersReadback._Bounds, volumeBuffers._Bounds);
				CreateReadbackBuffer(ref volumeData.buffersReadback._BoundsCoverage, volumeBuffers._BoundsCoverage);

				return changed;
			}
		}

		public static void ReleaseSolverData(ref SolverData solverData)
		{
			ref var solverBuffers = ref solverData.buffers;

			ReleaseBuffer(ref solverBuffers.SolverCBuffer);
			ReleaseBuffer(ref solverBuffers.SolverCBufferRoots);

			ReleaseBuffer(ref solverBuffers._RootUV);
			ReleaseBuffer(ref solverBuffers._RootScale);

			ReleaseBuffer(ref solverBuffers._RootPosition);
			ReleaseBuffer(ref solverBuffers._RootPositionPrev);
			ReleaseBuffer(ref solverBuffers._RootPositionSubstep);
			ReleaseBuffer(ref solverBuffers._RootFrame);
			ReleaseBuffer(ref solverBuffers._RootFramePrev);
			ReleaseBuffer(ref solverBuffers._RootFrameSubstep);

			ReleaseBuffer(ref solverBuffers._SolverLODStage);
			ReleaseBuffer(ref solverBuffers._SolverLODDispatch);

			ReleaseBuffer(ref solverBuffers._InitialParticleOffset);
			ReleaseBuffer(ref solverBuffers._InitialParticleFrameDelta);

			ReleaseBuffer(ref solverBuffers._ParticlePosition);
			ReleaseBuffer(ref solverBuffers._ParticlePositionPrev);
			ReleaseBuffer(ref solverBuffers._ParticlePositionPrevPrev);
			ReleaseBuffer(ref solverBuffers._ParticleVelocity);
			ReleaseBuffer(ref solverBuffers._ParticleVelocityPrev);
			ReleaseBuffer(ref solverBuffers._ParticleCorrection);

			ReleaseBuffer(ref solverBuffers._ParticleExtTexCoord);
			ReleaseBuffer(ref solverBuffers._ParticleExtDiameter);

			ReleaseBuffer(ref solverBuffers._LODGuideCount);
			ReleaseBuffer(ref solverBuffers._LODGuideIndex);
			ReleaseBuffer(ref solverBuffers._LODGuideCarry);
			ReleaseBuffer(ref solverBuffers._LODGuideReach);

			ReleaseBuffer(ref solverBuffers._StagingVertex);
			ReleaseBuffer(ref solverBuffers._StagingVertexPrev);

			if (solverData.lodThreshold.IsCreated)
				solverData.lodThreshold.Dispose();

			solverData = new SolverData();
		}

		public static void ReleaseVolumeData(ref VolumeData volumeData)
		{
			ref var volumeBuffers = ref volumeData.buffers;
			ref var volumeTextures = ref volumeData.textures;

			ReleaseBuffer(ref volumeBuffers.VolumeCBuffer);
			ReleaseBuffer(ref volumeBuffers.VolumeCBufferEnvironment);

			ReleaseBuffer(ref volumeBuffers._LODFrustum);

			ReleaseBuffer(ref volumeBuffers._BoundaryShape);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrix);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrixInv);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrixW2PrevW);

			ReleaseBuffer(ref volumeBuffers._WindEmitter);

			ReleaseBuffer(ref volumeBuffers._BoundsMinMaxU);
			ReleaseBuffer(ref volumeBuffers._Bounds);
			ReleaseBuffer(ref volumeBuffers._BoundsGeometry);
			ReleaseBuffer(ref volumeBuffers._BoundsCoverage);

			ReleaseBuffer(ref volumeBuffers._VolumeLODStage);
			ReleaseBuffer(ref volumeBuffers._VolumeLODDispatch);

			ReleaseBuffer(ref volumeBuffers._AccuWeightBuffer);
			ReleaseBuffer(ref volumeBuffers._AccuWeight0Buffer);
			ReleaseBuffer(ref volumeBuffers._AccuVelocityXBuffer);
			ReleaseBuffer(ref volumeBuffers._AccuVelocityYBuffer);
			ReleaseBuffer(ref volumeBuffers._AccuVelocityZBuffer);

			ReleaseVolume(ref volumeTextures._AccuWeight);
			ReleaseVolume(ref volumeTextures._AccuWeight0);
			ReleaseVolume(ref volumeTextures._AccuVelocityX);
			ReleaseVolume(ref volumeTextures._AccuVelocityY);
			ReleaseVolume(ref volumeTextures._AccuVelocityZ);

			ReleaseVolume(ref volumeTextures._VolumeDensity);
			ReleaseVolume(ref volumeTextures._VolumeDensity0);
			ReleaseVolume(ref volumeTextures._VolumeVelocity);

			ReleaseVolume(ref volumeTextures._VolumeDivergence);
			ReleaseVolume(ref volumeTextures._VolumePressure);
			ReleaseVolume(ref volumeTextures._VolumePressureNext);
			ReleaseVolume(ref volumeTextures._VolumePressureGrad);

			ReleaseVolume(ref volumeTextures._VolumeScattering);
			ReleaseVolume(ref volumeTextures._VolumeImpulse);

			ReleaseVolume(ref volumeTextures._BoundarySDF_undefined);

			ReleaseReadbackBuffer(ref volumeData.buffersReadback._Bounds);
			ReleaseReadbackBuffer(ref volumeData.buffersReadback._BoundsCoverage);

			if (volumeData.boundaryPrevXform.IsCreated)
				volumeData.boundaryPrevXform.Dispose();

			volumeData = new VolumeData();
		}

		public static void BindSolverData(Material mat, in SolverData solverData) => BindSolverData(new BindTargetMaterial(mat), solverData);
		public static void BindSolverData(CommandBuffer cmd, in SolverData solverData) => BindSolverData(new BindTargetGlobalCmd(cmd), solverData);
		public static void BindSolverData(CommandBuffer cmd, ComputeShader cs, int kernel, in SolverData solverData) => BindSolverData(new BindTargetComputeCmd(cmd, cs, kernel), solverData);
		public static void BindSolverData<T>(T target, in SolverData solverData) where T : IBindTarget
		{
			ref readonly var solverBuffers = ref solverData.buffers;
			ref readonly var solverKeywords = ref solverData.keywords;

			target.BindConstantBuffer(SolverData.s_bufferIDs.SolverCBufferRoots, solverBuffers.SolverCBufferRoots);
			target.BindConstantBuffer(SolverData.s_bufferIDs.SolverCBuffer, solverBuffers.SolverCBuffer);

			target.BindComputeBuffer(SolverData.s_bufferIDs._RootUV, solverBuffers._RootUV);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootScale, solverBuffers._RootScale);

			target.BindComputeBuffer(SolverData.s_bufferIDs._RootPosition, solverBuffers._RootPosition);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootPositionPrev, solverBuffers._RootPositionPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootPositionSubstep, solverBuffers._RootPositionSubstep);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootFrame, solverBuffers._RootFrame);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootFramePrev, solverBuffers._RootFramePrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootFrameSubstep, solverBuffers._RootFrameSubstep);

			target.BindComputeBuffer(SolverData.s_bufferIDs._InitialParticleOffset, solverBuffers._InitialParticleOffset);
			target.BindComputeBuffer(SolverData.s_bufferIDs._InitialParticleFrameDelta, solverBuffers._InitialParticleFrameDelta);

			target.BindComputeBuffer(SolverData.s_bufferIDs._SolverLODStage, solverBuffers._SolverLODStage);
			target.BindComputeBuffer(SolverData.s_bufferIDs._SolverLODDispatch, solverBuffers._SolverLODDispatch);

			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticlePosition, solverBuffers._ParticlePosition);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticlePositionPrev, solverBuffers._ParticlePositionPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticlePositionPrevPrev, solverBuffers._ParticlePositionPrevPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleVelocity, solverBuffers._ParticleVelocity);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleVelocityPrev, solverBuffers._ParticleVelocityPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleCorrection, solverBuffers._ParticleCorrection);

			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleExtTexCoord, solverBuffers._ParticleExtTexCoord);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleExtDiameter, solverBuffers._ParticleExtDiameter);

			target.BindComputeBuffer(SolverData.s_bufferIDs._LODGuideCount, solverBuffers._LODGuideCount);
			target.BindComputeBuffer(SolverData.s_bufferIDs._LODGuideIndex, solverBuffers._LODGuideIndex);
			target.BindComputeBuffer(SolverData.s_bufferIDs._LODGuideCarry, solverBuffers._LODGuideCarry);
			target.BindComputeBuffer(SolverData.s_bufferIDs._LODGuideReach, solverBuffers._LODGuideReach);

			target.BindComputeBuffer(SolverData.s_bufferIDs._StagingVertex, solverBuffers._StagingVertex);
			target.BindComputeBuffer(SolverData.s_bufferIDs._StagingVertexPrev, solverBuffers._StagingVertexPrev);

			target.BindKeyword("LAYOUT_INTERLEAVED", solverKeywords.LAYOUT_INTERLEAVED);
			target.BindKeyword("LIVE_POSITIONS_3", solverKeywords.LIVE_POSITIONS_3);
			target.BindKeyword("LIVE_POSITIONS_2", solverKeywords.LIVE_POSITIONS_2);
			target.BindKeyword("LIVE_POSITIONS_1", solverKeywords.LIVE_POSITIONS_1);
			target.BindKeyword("LIVE_ROTATIONS_2", solverKeywords.LIVE_ROTATIONS_2);
		}

		public static void BindVolumeData(Material mat, in VolumeData volumeData) => BindVolumeData(new BindTargetMaterial(mat), volumeData);
		public static void BindVolumeData(CommandBuffer cmd, VolumeData volumeData) => BindVolumeData(new BindTargetGlobalCmd(cmd), volumeData);
		public static void BindVolumeData(CommandBuffer cmd, ComputeShader cs, int kernel, in VolumeData volumeData) => BindVolumeData(new BindTargetComputeCmd(cmd, cs, kernel), volumeData);
		public static void BindVolumeData<T>(T target, in VolumeData volumeData) where T : IBindTarget
		{
			ref readonly var volumeBuffers = ref volumeData.buffers;
			ref readonly var volumeTextures = ref volumeData.textures;
			ref readonly var volumeKeywords = ref volumeData.keywords;

			ref readonly var volumeBufferIDs = ref VolumeData.s_bufferIDs;
			ref readonly var volumeTextureIDs = ref VolumeData.s_textureIDs;

			target.BindConstantBuffer(volumeBufferIDs.VolumeCBuffer, volumeBuffers.VolumeCBuffer);
			target.BindConstantBuffer(volumeBufferIDs.VolumeCBufferEnvironment, volumeBuffers.VolumeCBufferEnvironment);

			target.BindComputeBuffer(volumeBufferIDs._LODFrustum, volumeBuffers._LODFrustum);

			target.BindComputeBuffer(volumeBufferIDs._BoundaryShape, volumeBuffers._BoundaryShape);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrix, volumeBuffers._BoundaryMatrix);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrixInv, volumeBuffers._BoundaryMatrixInv);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrixW2PrevW, volumeBuffers._BoundaryMatrixW2PrevW);

			target.BindComputeBuffer(volumeBufferIDs._WindEmitter, volumeBuffers._WindEmitter);

			target.BindComputeBuffer(volumeBufferIDs._BoundsMinMaxU, volumeBuffers._BoundsMinMaxU);
			target.BindComputeBuffer(volumeBufferIDs._Bounds, volumeBuffers._Bounds);
			target.BindComputeBuffer(volumeBufferIDs._BoundsGeometry, volumeBuffers._BoundsGeometry);
			target.BindComputeBuffer(volumeBufferIDs._BoundsCoverage, volumeBuffers._BoundsCoverage);

			target.BindComputeBuffer(volumeBufferIDs._VolumeLODStage, volumeBuffers._VolumeLODStage);
			target.BindComputeBuffer(volumeBufferIDs._VolumeLODDispatch, volumeBuffers._VolumeLODDispatch);

			if (s_runtimeFlags.HasFlag(RuntimeFlags.SupportsTextureAtomics))
			{
				target.BindComputeTexture(volumeTextureIDs._AccuWeight, volumeTextures._AccuWeight);
				target.BindComputeTexture(volumeTextureIDs._AccuWeight0, volumeTextures._AccuWeight0);
				target.BindComputeTexture(volumeTextureIDs._AccuVelocityX, volumeTextures._AccuVelocityX);
				target.BindComputeTexture(volumeTextureIDs._AccuVelocityY, volumeTextures._AccuVelocityY);
				target.BindComputeTexture(volumeTextureIDs._AccuVelocityZ, volumeTextures._AccuVelocityZ);
			}
			else
			{
				target.BindComputeBuffer(volumeTextureIDs._AccuWeight, volumeBuffers._AccuWeightBuffer);
				target.BindComputeBuffer(volumeTextureIDs._AccuWeight0, volumeBuffers._AccuWeight0Buffer);
				target.BindComputeBuffer(volumeTextureIDs._AccuVelocityX, volumeBuffers._AccuVelocityXBuffer);
				target.BindComputeBuffer(volumeTextureIDs._AccuVelocityY, volumeBuffers._AccuVelocityYBuffer);
				target.BindComputeBuffer(volumeTextureIDs._AccuVelocityZ, volumeBuffers._AccuVelocityZBuffer);
			}

			target.BindComputeTexture(volumeTextureIDs._VolumeDensity, volumeTextures._VolumeDensity);
			target.BindComputeTexture(volumeTextureIDs._VolumeDensity0, volumeTextures._VolumeDensity0);
			target.BindComputeTexture(volumeTextureIDs._VolumeVelocity, volumeTextures._VolumeVelocity);
			target.BindComputeTexture(volumeTextureIDs._VolumeDivergence, volumeTextures._VolumeDivergence);

			target.BindComputeTexture(volumeTextureIDs._VolumePressure, volumeTextures._VolumePressure);
			target.BindComputeTexture(volumeTextureIDs._VolumePressureNext, volumeTextures._VolumePressureNext);
			target.BindComputeTexture(volumeTextureIDs._VolumePressureGrad, volumeTextures._VolumePressureGrad);

			target.BindComputeTexture(volumeTextureIDs._VolumeScattering, volumeTextures._VolumeScattering);
			target.BindComputeTexture(volumeTextureIDs._VolumeImpulse, volumeTextures._VolumeImpulse);

			target.BindComputeTexture(volumeTextureIDs._BoundarySDF, (volumeTextures._BoundarySDF != null) ? volumeTextures._BoundarySDF : volumeTextures._BoundarySDF_undefined);

			target.BindKeyword("VOLUME_SPLAT_CLUSTERS", volumeKeywords.VOLUME_SPLAT_CLUSTERS);
			target.BindKeyword("VOLUME_SUPPORT_CONTRACTION", volumeKeywords.VOLUME_SUPPORT_CONTRACTION);
			target.BindKeyword("VOLUME_TARGET_INITIAL_POSE", volumeKeywords.VOLUME_TARGET_INITIAL_POSE);
			target.BindKeyword("VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES", volumeKeywords.VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES);
		}

		public static void InitSolverData(CommandBuffer cmd, in SolverData solverData)
		{
			int numX = ((int)solverData.constants._StrandCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
			int numY = 1;
			int numZ = 1;

			BindSolverData(cmd, s_solverCS, SolverKernels.KInitialize, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInitialize, numX, numY, numZ);
		}

		public static void InitSolverDataPostVolume(CommandBuffer cmd, in SolverData solverData, in VolumeData volumeData)
		{
			int numX = ((int)solverData.constants._StrandCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
			int numY = 1;
			int numZ = 1;

			BindVolumeData(cmd, s_solverCS, SolverKernels.KInitializePostVolume, volumeData);
			BindSolverData(cmd, s_solverCS, SolverKernels.KInitializePostVolume, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInitializePostVolume, numX, numY, numZ);
		}

		public static void PushSolverRoots(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, ref SolverData solverData, Mesh rootMesh, in Matrix4x4 rootMeshMatrix, in Quaternion rootMeshSkinningRotation, float deltaTime)
		{
			ref var solverBuffers = ref solverData.buffers;
			ref var solverConstantsRoots = ref solverData.constantsRoots;

			// derive constants
			var rootMeshRotation = rootMeshMatrix.rotation;
			var rootMeshRotationInv = Quaternion.Inverse(rootMeshRotation);

			solverConstantsRoots._RootMeshMatrix = rootMeshMatrix;
			solverConstantsRoots._RootMeshRotation = rootMeshRotation.ToVector4();
			solverConstantsRoots._RootMeshRotationInv = rootMeshRotationInv.ToVector4();
			solverConstantsRoots._RootMeshSkinningRotation = rootMeshSkinningRotation.ToVector4();

#if UNITY_2021_2_OR_NEWER
			if (rootMesh.vertexBufferTarget.HasFlag(GraphicsBuffer.Target.Raw) == false)
				rootMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

			var vertexPositionStream = rootMesh.GetVertexAttributeStream(VertexAttribute.Position);
			var vertexPositionOffset = rootMesh.GetVertexAttributeOffset(VertexAttribute.Position);
			var vertexPositionStride = rootMesh.GetVertexBufferStride(vertexPositionStream);

			var vertexTangentStream = rootMesh.GetVertexAttributeStream(VertexAttribute.Tangent);
			var vertexTangentOffset = rootMesh.GetVertexAttributeOffset(VertexAttribute.Tangent);
			var vertexTangentStride = rootMesh.GetVertexBufferStride(vertexTangentStream);

			var normalStream = rootMesh.GetVertexAttributeStream(VertexAttribute.Normal);
			var normalOffset = rootMesh.GetVertexAttributeOffset(VertexAttribute.Normal);
			var normalStride = rootMesh.GetVertexBufferStride(normalStream);

			constants._RootMeshPositionOffset = vertexPositionOffset;
			constants._RootMeshPositionStride = vertexPositionStride;
			constants._RootMeshTangentOffset = vertexTangentOffset;
			constants._RootMeshTangentStride = vertexTangentStride;
			constants._RootMeshNormalOffset = normalOffset;
			constants._RootMeshNormalStride = normalStride;
#else
			solverConstantsRoots._RootMeshTangentStride = rootMesh.HasVertexAttribute(VertexAttribute.Tangent) ? 1u : 0u;
#endif

#if !HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_1_PREVIEW
			solverConstantsRoots._RootMeshTangentStride = 0;//TODO remove this? (and therein assume that root mesh having tangents == root mesh having tangent frame)
#endif

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBufferRoots, solverConstantsRoots);

			// update roots
			if (deltaTime > 0.0f)
			{
				CoreUtils.Swap(ref solverBuffers._RootPosition, ref solverBuffers._RootPositionPrev);
				CoreUtils.Swap(ref solverBuffers._RootFrame, ref solverBuffers._RootFramePrev);
			}

			using (new ProfilingScope(cmd, MarkersGPU.Roots))
			{
#if UNITY_2021_2_OR_NEWER
				if (rootMesh.vertexBufferTarget.HasFlag(GraphicsBuffer.Target.Raw) == false)
					rootMesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
				
				using (GraphicsBuffer vertexPosition = rootMesh.GetVertexBuffer(vertexPositionStream))
				using (GraphicsBuffer vertexTangent = (vertexTangentStride != 0) ? rootMesh.GetVertexBuffer(vertexTangentStream) : null)
				using (GraphicsBuffer vertexNormal = rootMesh.GetVertexBuffer(normalStream))
				{
					int numX = ((int)solverData.constants._StrandCount + PARTICLE_GROUP_SIZE - 1) / PARTICLE_GROUP_SIZE;
					int numY = 1;
					int numZ = 1;

					cmd.SetComputeBufferParam(s_solverCS, SolverKernels.KRoots, SolverData.s_externalIDs._RootMeshPosition, vertexPosition);
					cmd.SetComputeBufferParam(s_solverCS, SolverKernels.KRoots, SolverData.s_externalIDs._RootMeshTangent, vertexTangent ?? vertexNormal);
					cmd.SetComputeBufferParam(s_solverCS, SolverKernels.KRoots, SolverData.s_externalIDs._RootMeshNormal, vertexNormal);

					BindSolverData(cmd, s_solverCS, SolverKernels.KRoots, solverData);
					cmd.DispatchCompute(s_solverCS, SolverKernels.KRoots, numX, numY, numZ);
				}
#else
				if (s_runtimeFlags.HasFlag(RuntimeFlags.SupportsVertexUAVWrites) && cmdFlags.HasFlag(CommandBufferExecutionFlags.AsyncCompute) == false)
				{
					// this path uses UAV writes from the vertex stage to funnel out data from the root mesh.
					// despite not actually rendering anything (all fragments clipped), we need to also bind
					// a dummy render target to avoid an unspecified target being bound for the draw.
					{
						cmd.GetTemporaryRT(SolverData.s_externalIDs._RootResolveDummyRT, 1, 1);
						cmd.SetRenderTarget(SolverData.s_externalIDs._RootResolveDummyRT);
					}

					BindSolverData(cmd, solverData);

					cmd.SetRandomWriteTarget(1, solverData.buffers._RootPosition);
					cmd.SetRandomWriteTarget(2, solverData.buffers._RootFrame);
					cmd.DrawMesh(rootMesh, Matrix4x4.identity, s_solverRootsMat);
					cmd.ClearRandomWriteTargets();
				}
				else
				{
					// some platforms (Metal, for example) do not support unconditional vertex stage UAV writes.
					// we use the (default) compute path to support those platforms, but since that path requires
					// vertex buffer access from C# it is available only with Unity 2021.2+. (this limitation is
					// also what prevents async scheduling on versions prior.)
					Debug.LogError("Unable to resolve roots (vertex UAV writes not supported and compute path requires 2021.2+)");
				}
#endif
			}
		}

		public static void PushSolverRootsHistory(CommandBuffer cmd, in SolverData solverData)
		{
			int numX = ((int)solverData.constants._StrandCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
			int numY = 1;
			int numZ = 1;

			BindSolverData(cmd, s_solverCS, SolverKernels.KRootsHistory, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KRootsHistory, numX, numY, numZ);
		}

		public static void PushSolverGeometry(CommandBuffer cmd, ref SolverData solverData, in SettingsGeometry settingsGeometry, Matrix4x4 localToWorld)
		{
			ref var solverConstants = ref solverData.constants;

			// derive constants
			var localToWorldScale = 1.0f;
			{
				switch (settingsGeometry.strandScale)
				{
					case SettingsGeometry.StrandScale.Fixed:
						break;

					case SettingsGeometry.StrandScale.UniformWorldMin:
						localToWorldScale = localToWorld.lossyScale.Abs().CMin();
						break;

					case SettingsGeometry.StrandScale.UniformWorldMax:
						localToWorldScale = localToWorld.lossyScale.Abs().CMax();
						break;
				}
			}

			var localToWorldScaleLength = localToWorldScale;
			{
				if (settingsGeometry.strandLength)
				{
					localToWorldScaleLength *= settingsGeometry.strandLengthValue / solverData.initialMaxStrandLength;
				}
			}

			var localToWorldScaleDiameter = localToWorldScale;
			{
				if (settingsGeometry.strandDiameter)
				{
					localToWorldScaleDiameter *= (settingsGeometry.strandDiameterValue * 0.001f) / solverData.initialMaxStrandDiameter;
				}
			}

			var worldMaxParticleInterval = localToWorldScaleLength * (solverData.initialMaxStrandLength / (solverConstants._StrandParticleCount - 1));
			var worldMaxParticleDiameter = localToWorldScaleDiameter * solverData.initialMaxStrandDiameter;
			var worldAvgParticleDiameter = localToWorldScaleDiameter * solverData.initialAvgStrandDiameter;

			solverConstants._GroupScale = localToWorldScaleLength;
			solverConstants._GroupMaxParticleVolume = worldMaxParticleInterval * (0.25f * Mathf.PI * worldMaxParticleDiameter * worldMaxParticleDiameter);
			solverConstants._GroupMaxParticleInterval = worldMaxParticleInterval;
			solverConstants._GroupMaxParticleDiameter = worldMaxParticleDiameter;
			solverConstants._GroupAvgParticleDiameter = worldAvgParticleDiameter;
			solverConstants._GroupAvgParticleMargin = localToWorldScale * settingsGeometry.strandSeparation * 0.001f;

			solverConstants._GroupBoundsPadding = settingsGeometry.boundsScale ? settingsGeometry.boundsScaleValue : 1.25f;

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverConstants);

			// update manual bounds
			solverData.manualBounds = (settingsGeometry.boundsMode == SettingsGeometry.BoundsMode.Manual);
			solverData.manualBoundsMin = settingsGeometry.boundsCenter - settingsGeometry.boundsExtent;
			solverData.manualBoundsMax = settingsGeometry.boundsCenter + settingsGeometry.boundsExtent;
		}

		public static void PushSolverLOD(CommandBuffer cmd, ref SolverData solverData, in SettingsPhysics settingsPhysics, in SettingsRendering settingsRendering, in VolumeData volumeData)
		{
			ref var solverConstants = ref solverData.constants;

			// derive constants
			solverConstants._SolverLODMethod = (uint)settingsPhysics.kLODSelection;
			solverConstants._SolverLODCeiling = settingsPhysics.kLODCeiling;
			solverConstants._SolverLODScale = settingsPhysics.kLODScale;
			solverConstants._SolverLODBias = (settingsPhysics.kLODSelection == SolverLODSelection.Manual) ? settingsPhysics.kLODValue : settingsPhysics.kLODBias;

			solverConstants._RenderLODMethod = (uint)settingsRendering.kLODSelection;
			solverConstants._RenderLODCeiling = settingsRendering.kLODCeiling;
			solverConstants._RenderLODScale = settingsRendering.kLODScale;
			solverConstants._RenderLODBias = (settingsRendering.kLODSelection == RenderLODSelection.Manual) ? settingsRendering.kLODValue : settingsRendering.kLODBias;
			solverConstants._RenderLODClipThreshold = settingsRendering.clipThreshold;

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverConstants);

			// lod selection
			BindVolumeData(cmd, s_solverCS, SolverKernels.KLODSelection, volumeData);
			BindSolverData(cmd, s_solverCS, SolverKernels.KLODSelection, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KLODSelection, 1, 1, 1);
		}

		public static void PushSolverSettings(CommandBuffer cmd, ref SolverData solverData, in SettingsPhysics settingsPhysics, float deltaTime)
		{
			static float IntervalToSeconds(SettingsPhysics.TimeInterval interval)
			{
				switch (interval)
				{
					default:
					case SettingsPhysics.TimeInterval.PerSecond: return 1.0f;
					case SettingsPhysics.TimeInterval.Per100ms: return 0.1f;
					case SettingsPhysics.TimeInterval.Per10ms: return 0.01f;
					case SettingsPhysics.TimeInterval.Per1ms: return 0.001f;
				}
			}

			ref var solverConstants = ref solverData.constants;
			ref var solverKeywords = ref solverData.keywords;

			// derive constants
			solverConstants._DT = deltaTime / Mathf.Max(1, settingsPhysics.solverSubsteps);
			solverConstants._Substeps = (uint)Mathf.Max(1, settingsPhysics.solverSubsteps);
			solverConstants._Iterations = (uint)settingsPhysics.constraintIterations;
			solverConstants._Stiffness = settingsPhysics.constraintStiffness;
			solverConstants._SOR = (settingsPhysics.constraintIterations > 1) ? settingsPhysics.constraintSOR : 1.0f;

			solverConstants._LinearDamping = settingsPhysics.dampingLinear ? settingsPhysics.dampingLinearFactor : 0.0f;
			solverConstants._LinearDampingInterval = IntervalToSeconds(settingsPhysics.dampingLinearInterval);
			solverConstants._AngularDamping = settingsPhysics.dampingAngular ? settingsPhysics.dampingAngularFactor : 0.0f;
			solverConstants._AngularDampingInterval = IntervalToSeconds(settingsPhysics.dampingAngularInterval);
			solverConstants._CellPressure = settingsPhysics.cellPressure;
			solverConstants._CellVelocity = settingsPhysics.cellVelocity;
			solverConstants._CellExternal = settingsPhysics.cellExternal;
			solverConstants._GravityScale = settingsPhysics.gravity;

			solverConstants._BoundaryFriction = settingsPhysics.boundaryCollisionFriction;
			solverConstants._FTLCorrection = settingsPhysics.distanceFTLCorrection;
			solverConstants._LocalCurvature = settingsPhysics.localCurvatureValue * 0.5f;
			solverConstants._LocalShape = settingsPhysics.localShapeInfluence;
			solverConstants._LocalShapeBias = settingsPhysics.localShapeBias ? settingsPhysics.localShapeBiasValue : 0.0f;

			solverConstants._GlobalPosition = settingsPhysics.globalPositionInfluence;
			solverConstants._GlobalPositionInterval = IntervalToSeconds(settingsPhysics.globalPositionInterval);
			solverConstants._GlobalRotation = settingsPhysics.globalRotationInfluence;
			solverConstants._GlobalFadeOffset = settingsPhysics.globalFade ? settingsPhysics.globalFadeOffset : 1e9f;
			solverConstants._GlobalFadeExtent = settingsPhysics.globalFade ? settingsPhysics.globalFadeExtent : 1e9f;

			// derive features
			SolverFeatures features = 0;
			{
				features |= (settingsPhysics.boundaryCollision && settingsPhysics.boundaryCollisionFriction == 0.0f) ? SolverFeatures.Boundary : 0;
				features |= (settingsPhysics.boundaryCollision && settingsPhysics.boundaryCollisionFriction > 0.0f) ? SolverFeatures.BoundaryFriction : 0;
				features |= (settingsPhysics.distance) ? SolverFeatures.Distance : 0;
				features |= (settingsPhysics.distanceLRA) ? SolverFeatures.DistanceLRA : 0;
				features |= (settingsPhysics.distanceFTL) ? SolverFeatures.DistanceFTL : 0;
				features |= (settingsPhysics.localCurvature && settingsPhysics.localCurvatureMode == SettingsPhysics.LocalCurvatureMode.Equals) ? SolverFeatures.CurvatureEQ : 0;
				features |= (settingsPhysics.localCurvature && settingsPhysics.localCurvatureMode == SettingsPhysics.LocalCurvatureMode.GreaterThan) ? SolverFeatures.CurvatureGEQ : 0;
				features |= (settingsPhysics.localCurvature && settingsPhysics.localCurvatureMode == SettingsPhysics.LocalCurvatureMode.LessThan) ? SolverFeatures.CurvatureLEQ : 0;
				features |= (settingsPhysics.localShape && settingsPhysics.localShapeInfluence > 0.0f && settingsPhysics.localShapeMode == SettingsPhysics.LocalShapeMode.Forward) ? SolverFeatures.PoseLocalShape : 0;
				features |= (settingsPhysics.localShape && settingsPhysics.localShapeInfluence > 0.0f && settingsPhysics.localShapeMode == SettingsPhysics.LocalShapeMode.Stitched) ? SolverFeatures.PoseLocalShapeRWD : 0;
				features |= (settingsPhysics.globalPosition && settingsPhysics.globalPositionInfluence > 0.0f) ? SolverFeatures.PoseGlobalPosition : 0;
				features |= (settingsPhysics.globalRotation && settingsPhysics.globalRotationInfluence > 0.0f) ? SolverFeatures.PoseGlobalRotation : 0;

				if (features.HasFlag(SolverFeatures.PoseLocalShapeRWD) && settingsPhysics.solver != SettingsPhysics.Solver.GaussSeidel)
				{
					features &= ~SolverFeatures.PoseLocalShapeRWD;
					features |= SolverFeatures.PoseLocalShape;
				}
			}
			solverConstants._SolverFeatures = (uint)features;

			// derive feature-dependent buffers
			unsafe
			{
				var enableParticlePositionCorr = true;
				{
					enableParticlePositionCorr = enableParticlePositionCorr && features.HasFlag(SolverFeatures.DistanceFTL);
					enableParticlePositionCorr = enableParticlePositionCorr && (settingsPhysics.solver != SettingsPhysics.Solver.Jacobi);
				}

				var particleCount = (int)solverData.constants._StrandCount * (int)solverData.constants._StrandParticleCount;
				var particleStrideVector4 = sizeof(Vector4);

				CreateBuffer(ref solverData.buffers._ParticleCorrection, "ParticleCorrection", enableParticlePositionCorr ? particleCount : 1, particleStrideVector4);
			}

			// derive keywords
			solverKeywords.LAYOUT_INTERLEAVED = (solverData.memoryLayout == HairAsset.MemoryLayout.Interleaved);
			solverKeywords.LIVE_POSITIONS_3 = ((features & (SolverFeatures.CurvatureEQ | SolverFeatures.CurvatureGEQ | SolverFeatures.CurvatureLEQ | SolverFeatures.PoseLocalShapeRWD)) != 0);
			solverKeywords.LIVE_POSITIONS_2 = !solverKeywords.LIVE_POSITIONS_3 && ((features & (SolverFeatures.Distance | SolverFeatures.PoseLocalShape | SolverFeatures.PoseGlobalRotation)) != 0);
			solverKeywords.LIVE_POSITIONS_1 = !solverKeywords.LIVE_POSITIONS_3 && !solverKeywords.LIVE_POSITIONS_2 && ((features & ~SolverFeatures.PoseGlobalPosition) != 0);
			solverKeywords.LIVE_ROTATIONS_2 = ((features & (SolverFeatures.PoseLocalShape | SolverFeatures.PoseLocalShapeRWD | SolverFeatures.PoseGlobalRotation)) != 0);

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverConstants);
		}

		public static void PushSolverStep(CommandBuffer cmd, ref SolverData solverData, in SettingsPhysics settingsPhysics, in VolumeData volumeData, float stepFracLo, float stepFracHi)
		{
			var solveKernel = SolverKernels.KSolveConstraints_GaussSeidelReference;
			var solveDispatch = SolverLODDispatch.Solve;

			switch (settingsPhysics.solver)
			{
				case SettingsPhysics.Solver.GaussSeidelReference:
					solveKernel = SolverKernels.KSolveConstraints_GaussSeidelReference;
					solveDispatch = SolverLODDispatch.Solve;
					break;

				case SettingsPhysics.Solver.GaussSeidel:
					solveKernel = SolverKernels.KSolveConstraints_GaussSeidel;
					solveDispatch = SolverLODDispatch.Solve;
					break;

				case SettingsPhysics.Solver.Jacobi:
					switch (solverData.constants._StrandParticleCount)
					{
						case 16:
							solveKernel = SolverKernels.KSolveConstraints_Jacobi_16;
							break;

						case 32:
							solveKernel = SolverKernels.KSolveConstraints_Jacobi_32;
							break;

						case 64:
							solveKernel = SolverKernels.KSolveConstraints_Jacobi_64;
							break;

						case 128:
							solveKernel = SolverKernels.KSolveConstraints_Jacobi_128;
							break;
					}
					solveDispatch = SolverLODDispatch.SolveParallelParticles;
					break;
			}

			static SolverData WithSubstepData(in SolverData data, bool dataSubstep)
			{
				var dataCopy = data;
				if (dataSubstep)
				{
					CoreUtils.Swap(ref dataCopy.buffers._RootPositionSubstep, ref dataCopy.buffers._RootPosition);
					CoreUtils.Swap(ref dataCopy.buffers._RootFrameSubstep, ref dataCopy.buffers._RootFrame);
				}
				return dataCopy;
			}

			using (new ProfilingScope(cmd, MarkersGPU.Solver))
			{
				var substepConstantsBackup = solverData.constants;
				var substepCount = solverData.constants._Substeps;

				for (int i = 0; i != substepCount; i++)
				{
					var substepFraction = Mathf.Lerp(stepFracLo, stepFracHi, (i + 1) / (float)substepCount);
					if (substepFraction < (1.0f - float.Epsilon))
					{
						var rootsNumX = ((int)solverData.constants._StrandCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
						var rootsNumY = 1;
						var rootsNumZ = 1;

						using (new ProfilingScope(cmd, MarkersGPU.Solver_SubstepRoots))
						{
							//TODO move into cbuffer?
							cmd.SetComputeFloatParam(s_solverCS, UniformIDs._RootSubstepFraction, substepFraction);

							BindSolverData(cmd, s_solverCS, SolverKernels.KRootsSubstep, solverData);
							cmd.DispatchCompute(s_solverCS, SolverKernels.KRootsSubstep, rootsNumX, rootsNumY, rootsNumZ);
						}
					}

					//Debug.Log("substep " + i + ": " + (substepFrac < (1.0f - float.Epsilon)) + " (lo " + stepFracLo + " hi " + stepFracHi + ")");

					using (new ProfilingScope(cmd, MarkersGPU.Solver_SolveConstraints))
					{
						if (Conf.SECOND_ORDER_UPDATE != 0)
						{
							CoreUtils.Swap(ref solverData.buffers._ParticlePosition, ref solverData.buffers._ParticlePositionPrev);     // A B C -> b a c
							CoreUtils.Swap(ref solverData.buffers._ParticlePosition, ref solverData.buffers._ParticlePositionPrevPrev); // b a c -> C A B
							CoreUtils.Swap(ref solverData.buffers._ParticleVelocity, ref solverData.buffers._ParticleVelocityPrev);
						}
						else
						{
							CoreUtils.Swap(ref solverData.buffers._ParticlePosition, ref solverData.buffers._ParticlePositionPrev);     // A B -> B A
						}

						BindVolumeData(cmd, s_solverCS, solveKernel, volumeData);
						BindSolverData(cmd, s_solverCS, solveKernel, WithSubstepData(solverData, substepFraction < (1.0f - float.Epsilon)));
						cmd.DispatchCompute(s_solverCS, solveKernel, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)solveDispatch);
					}

					/* TODO remove
					// interpolate follow-strands
					//TODO move this out of the substep loop (bit finicky due to the buffer shuffling per substep)
					var interpolateStrandCount = solverData.cbuffer._StrandCount - solverData.cbuffer._SolverStrandCount;
					if (interpolateStrandCount > 0)
					{
						int kernelInterpolate = (solverData.cbuffer._LODIndexLo != solverData.cbuffer._LODIndexHi)
							? SolverKernels.KInterpolate
							: SolverKernels.KInterpolateNearest;

						var interpolateNumX = ((int)interpolateStrandCount + PARTICLE_GROUP_SIZE - 1) / PARTICLE_GROUP_SIZE;
						var interpolateNumY = 1;
						var interpolateNumZ = 1;

						using (new ProfilingScope(cmd, MarkersGPU.Solver_Interpolate))
						{
							BindSolverData(cmd, s_solverCS, kernelInterpolate, WithSubstepData(solverData, substepFrac < (1.0f - float.Epsilon)));
							cmd.DispatchCompute(s_solverCS, kernelInterpolate, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)SolverLODDispatch.Interpolate);
						}
					}
					*/

					// volume impulse is only applied for first substep
					if (substepCount > 1)
					{
						solverData.constants._CellPressure = 0.0f;
						solverData.constants._CellVelocity = 0.0f;
						solverData.constants._CellExternal = 0.0f;
						PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverData.constants);
					}
				}

				if (substepCount > 1)
				{
					solverData.constants = substepConstantsBackup;
					PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverData.constants);
				}

				using (new ProfilingScope(cmd, MarkersGPU.Solver_Interpolate))
				{
					BindSolverData(cmd, s_solverCS, SolverKernels.KInterpolate, WithSubstepData(solverData, stepFracHi < (1.0f - float.Epsilon)));
					cmd.DispatchCompute(s_solverCS, SolverKernels.KInterpolate, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)SolverLODDispatch.Interpolate);
				}
			}
		}

		public static void PushSolverStaging(CommandBuffer cmd, ref SolverData solverData, in SettingsGeometry settingsGeometry, in SettingsRendering settingsRendering, in VolumeData volumeData)
		{
			ref var solverBuffers = ref solverData.buffers;
			ref var solverConstants = ref solverData.constants;

			// derive constants
			var subdivisionCount = (settingsGeometry.stagingSubdivision ? settingsGeometry.stagingSubdivisionCount : 0);
			var subdivisionCountChanged = (subdivisionCount != solverConstants._StagingSubdivision);
			var subdivisionSegmentCount = (subdivisionCount + 1) * (solverConstants._StrandParticleCount - 1);

			var stagingVertexFormat = StagingVertexFormat.Undefined;
			var stagingVertexStride = 0;

			unsafe
			{
				switch (settingsGeometry.stagingPrecision)
				{
					default:
					case SettingsGeometry.StagingPrecision.Full:
						stagingVertexFormat = StagingVertexFormat.Uncompressed;
						stagingVertexStride = sizeof(Vector3);
						break;

					case SettingsGeometry.StagingPrecision.Half:
						stagingVertexFormat = StagingVertexFormat.Compressed;
						stagingVertexStride = sizeof(Vector2);
						break;
				}
			}

			solverConstants._StagingSubdivision = subdivisionCount;
			solverConstants._StagingVertexFormat = (uint)stagingVertexFormat;
			solverConstants._StagingVertexStride = (uint)stagingVertexStride;

			solverConstants._StagingStrandVertexCount = subdivisionSegmentCount + 1;
			solverConstants._StagingStrandVertexOffset = solverConstants._StrandParticleOffset;

			// derive constant-dependent buffers
			var stagingBufferVertexCount = (int)(solverConstants._StrandCount * (subdivisionSegmentCount + 1));
			var stagingBufferHistoryReset = false;
			{
				stagingBufferHistoryReset |= subdivisionCountChanged;
				stagingBufferHistoryReset |= CreateBuffer(ref solverBuffers._StagingVertex, "StagingVertex_0", stagingBufferVertexCount, stagingVertexStride, ComputeBufferType.Raw);
				stagingBufferHistoryReset |= CreateBuffer(ref solverBuffers._StagingVertexPrev, "StagingVertex_1", stagingBufferVertexCount, stagingVertexStride, ComputeBufferType.Raw);
			}

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverConstants);

			// update staging
			int stagingKernel = (solverConstants._StagingSubdivision == 0)
				? SolverKernels.KStaging
				: SolverKernels.KStagingSubdivision;

			using (new ProfilingScope(cmd, MarkersGPU.Solver_Staging))
			{
				CoreUtils.Swap(ref solverData.buffers._StagingVertex, ref solverData.buffers._StagingVertexPrev);

				BindVolumeData(cmd, s_solverCS, stagingKernel, volumeData);
				BindSolverData(cmd, s_solverCS, stagingKernel, solverData);
				cmd.DispatchCompute(s_solverCS, stagingKernel, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)SolverLODDispatch.Staging);

				if (stagingBufferHistoryReset)
				{
					BindSolverData(cmd, s_solverCS, SolverKernels.KStagingHistory, solverData);
					cmd.DispatchCompute(s_solverCS, SolverKernels.KStagingHistory, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)SolverLODDispatch.Staging);
				}
			}
		}

		public static void PushVolumeGeometry(CommandBuffer cmd, ref VolumeData volumeData, SolverData[] solverData)
		{
			ref var volumeConstants = ref volumeData.constants;

			// derive constants
			var allGroupsMaxParticleVolume = 0.0f;
			var allGroupsMaxParticleInterval = 0.0f;
			var allGroupsMaxParticleDiameter = 0.0f;
			var allGroupsAvgParticleDiameterAccu = Vector2.zero;
			var allGroupsAvgParticleMarginAccu = Vector2.zero;

			for (int i = 0; i != solverData.Length; i++)
			{
				ref readonly var solverConstants = ref solverData[i].constants;
				ref readonly var solverWeight = ref solverData[i].initialSumStrandLength;

				allGroupsMaxParticleVolume = Mathf.Max(allGroupsMaxParticleVolume, solverConstants._GroupMaxParticleVolume);
				allGroupsMaxParticleInterval = Mathf.Max(allGroupsMaxParticleInterval, solverConstants._GroupMaxParticleInterval);
				allGroupsMaxParticleDiameter = Mathf.Max(allGroupsMaxParticleDiameter, solverConstants._GroupMaxParticleDiameter);
				allGroupsAvgParticleDiameterAccu.x += solverWeight * solverConstants._GroupAvgParticleDiameter;
				allGroupsAvgParticleDiameterAccu.y += solverWeight;
				allGroupsAvgParticleMarginAccu.x += solverWeight * solverConstants._GroupAvgParticleMargin;
				allGroupsAvgParticleMarginAccu.y += solverWeight;
			}

			if (allGroupsAvgParticleDiameterAccu.y > 0.0f)
				allGroupsAvgParticleDiameterAccu.x /= allGroupsAvgParticleDiameterAccu.y;
			if (allGroupsAvgParticleMarginAccu.y > 0.0f)
				allGroupsAvgParticleMarginAccu.x /= allGroupsAvgParticleMarginAccu.y;

			volumeConstants._AllGroupsMaxParticleVolume = allGroupsMaxParticleVolume;
			volumeConstants._AllGroupsMaxParticleInterval = allGroupsMaxParticleInterval;
			volumeConstants._AllGroupsMaxParticleDiameter = allGroupsMaxParticleDiameter;
			volumeConstants._AllGroupsAvgParticleDiameter = allGroupsAvgParticleDiameterAccu.x;
			volumeConstants._AllGroupsAvgParticleMargin = allGroupsAvgParticleMarginAccu.x;

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBuffer, volumeConstants);
		}

		public static void PushVolumeBounds(CommandBuffer cmd, ref VolumeData volumeData, in SolverData[] solverData)
		{
			var boundsCount = solverData.Length + 1;
			int boundsNumX = ((int)boundsCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
			int boundsNumY = 1;
			int boundsNumZ = 1;

			// update bounds
			{
				// clear
				using (var minMaxUBuffer = new NativeArray<uint>(6 * boundsCount, Allocator.Temp))
				{
					var manualBounds = false;

					unsafe
					{
						const uint clearMinU = 0xFFFFFFFFu;
						const uint clearMaxU = 0x00000000u;

						var minMaxUPtr = (uint*)minMaxUBuffer.GetUnsafePtr();

						static unsafe uint FloatToUnsignedSortable(uint x)
						{
							// see: http://stereopsis.com/radix.html
							// see: https://lemire.me/blog/2020/12/14/converting-floating-point-numbers-to-integers-while-preserving-order/
							uint mask = math.asuint(math.asint(x) >> 31) | 0x80000000u;
							return x ^ mask;
						}

						for (int i = 0; i != solverData.Length; i++)
						{
							if (solverData[i].manualBounds)
							{
								*(minMaxUPtr++) = FloatToUnsignedSortable(math.asuint(solverData[i].manualBoundsMin.x));
								*(minMaxUPtr++) = FloatToUnsignedSortable(math.asuint(solverData[i].manualBoundsMin.y));
								*(minMaxUPtr++) = FloatToUnsignedSortable(math.asuint(solverData[i].manualBoundsMin.z));
								*(minMaxUPtr++) = FloatToUnsignedSortable(math.asuint(solverData[i].manualBoundsMax.x));
								*(minMaxUPtr++) = FloatToUnsignedSortable(math.asuint(solverData[i].manualBoundsMax.y));
								*(minMaxUPtr++) = FloatToUnsignedSortable(math.asuint(solverData[i].manualBoundsMax.z));

								manualBounds = true;
							}
							else
							{
								*(minMaxUPtr++) = clearMinU;
								*(minMaxUPtr++) = clearMinU;
								*(minMaxUPtr++) = clearMinU;
								*(minMaxUPtr++) = clearMaxU;
								*(minMaxUPtr++) = clearMaxU;
								*(minMaxUPtr++) = clearMaxU;
							}
						}

						*(minMaxUPtr++) = clearMinU;
						*(minMaxUPtr++) = clearMinU;
						*(minMaxUPtr++) = clearMinU;
						*(minMaxUPtr++) = clearMaxU;
						*(minMaxUPtr++) = clearMaxU;
						*(minMaxUPtr++) = clearMaxU;
					}

					if (manualBounds)
					{
						cmd.SetComputeBufferData(volumeData.buffers._BoundsMinMaxU, minMaxUBuffer);
					}
					else
					{
						BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundsClear, volumeData);
						cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundsClear, boundsNumX, boundsNumY, boundsNumZ);
					}
				}

				// gather
				for (int i = 0; i != solverData.Length; i++)
				{
					if (solverData[i].manualBounds == false)
					{
						int rootsNumX = ((int)solverData[i].constants._StrandCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
						int rootsNumY = 1;
						int rootsNumZ = 1;

						BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundsGather, volumeData);
						BindSolverData(cmd, s_volumeCS, VolumeKernels.KBoundsGather, solverData[i]);
						cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundsGather, rootsNumX, rootsNumY, rootsNumZ);
					}
				}

				// resolve
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundsResolve, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundsResolve, boundsNumX, boundsNumY, boundsNumZ);

				// resolve combined
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundsResolveCombined, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundsResolveCombined, 1, 1, 1);
			}

			// update bounds coverage
			{
				using (var lodGeometryBuffer = new NativeArray<LODGeometry>(boundsCount, Allocator.Temp))
				{
					unsafe
					{
						var lodGeometryPtr = (LODGeometry*)lodGeometryBuffer.GetUnsafePtr();

						for (int i = 0; i != solverData.Length; i++)
						{
							lodGeometryPtr[solverData[i].constants._GroupBoundsIndex] = new LODGeometry
							{
								maxParticleDiameter = solverData[i].constants._GroupMaxParticleDiameter,
								maxParticleInterval = solverData[i].constants._GroupMaxParticleInterval,
							};
						}

						lodGeometryPtr[volumeData.constants._CombinedBoundsIndex] = new LODGeometry
						{
							maxParticleDiameter = volumeData.constants._AllGroupsMaxParticleDiameter,
							maxParticleInterval = volumeData.constants._AllGroupsMaxParticleInterval,
						};
					}

					PushComputeBufferData(cmd, volumeData.buffers._BoundsGeometry, lodGeometryBuffer);
				}

				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundsCoverage, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundsCoverage, boundsNumX, boundsNumY, boundsNumZ);
			}

			// schedule readback
			{
				volumeData.buffersReadback._Bounds.ScheduleCopy(cmd, volumeData.buffers._Bounds);
				volumeData.buffersReadback._BoundsCoverage.ScheduleCopy(cmd, volumeData.buffers._BoundsCoverage);
			}
		}

		public static void PushVolumeObservers(CommandBuffer cmd, ref VolumeData volumeData, CameraType cameraType)
		{
			ref var volumeConstantsScene = ref volumeData.constantsEnvironment;

			// capture frustums
			using (var frustums = AcquireLODFrustums(CameraType.Game | CameraType.SceneView, Allocator.Temp))
			{
				// derive constants
				volumeConstantsScene._LODFrustumCount = (uint)frustums.Length;

				//int i = 0;
				//foreach (var frustum in frustums)
				//{
				//	Debug.Log("--- FRUSTUM " + (i++) + " ---");
				//	Debug.Log("frustum.cameraPosition = " + frustum.cameraPosition);
				//	Debug.Log("frustum.cameraForward = " + frustum.cameraForward);
				//	Debug.Log("frustum.cameraNearClipPlane = " + frustum.cameraNearClipPlane);
				//	Debug.Log("frustum.cameraUnitSpanSubpixelDepth = " + frustum.cameraUnitSpanSubpixelDepth);
				//	Debug.Log("frustum.plane0 = " + frustum.plane0);
				//	Debug.Log("frustum.plane1 = " + frustum.plane1);
				//	Debug.Log("frustum.plane2 = " + frustum.plane2);
				//	Debug.Log("frustum.plane3 = " + frustum.plane3);
				//	Debug.Log("frustum.plane4 = " + frustum.plane4);
				//	Debug.Log("frustum.plane5 = " + frustum.plane5);
				//}

				// update buffers
				PushComputeBufferData(cmd, volumeData.buffers._LODFrustum, frustums.AsArray());
			}

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBufferEnvironment, volumeConstantsScene);
		}

		public static void PushVolumeEnvironment(CommandBuffer cmd, ref VolumeData volumeData, in SettingsEnvironment settingsEnvironment, float time)
		{
			ref var volumeConstantsScene = ref volumeData.constantsEnvironment;
			ref var volumeTextures = ref volumeData.textures;

			// capture gravity
			volumeConstantsScene._WorldGravity = settingsEnvironment.gravityRotation * (Physics.gravity * settingsEnvironment.gravityScale);

			// capture boundaries
			using (var bufShape = new NativeArray<HairBoundary.RuntimeShape.Data>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufXform = new NativeArray<HairBoundary.RuntimeTransform>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrix = new NativeArray<Matrix4x4>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrixInv = new NativeArray<Matrix4x4>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrixW2PrevW = new NativeArray<Matrix4x4>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				if (volumeData.boundaryPrevXform.IsCreated && volumeData.boundaryPrevXform.Length != Conf.MAX_BOUNDARIES)
					volumeData.boundaryPrevXform.Dispose();
				if (volumeData.boundaryPrevXform.IsCreated == false)
					volumeData.boundaryPrevXform = new NativeArray<HairBoundary.RuntimeTransform>(Conf.MAX_BOUNDARIES, Allocator.Persistent, NativeArrayOptions.ClearMemory);

				unsafe
				{
					var ptrShape = (HairBoundary.RuntimeShape.Data*)bufShape.GetUnsafePtr();
					var ptrXform = (HairBoundary.RuntimeTransform*)bufXform.GetUnsafePtr();
					var ptrXformPrev = (HairBoundary.RuntimeTransform*)volumeData.boundaryPrevXform.GetUnsafePtr();

					var ptrMatrix = (Matrix4x4*)bufMatrix.GetUnsafePtr();
					var ptrMatrixInv = (Matrix4x4*)bufMatrixInv.GetUnsafePtr();
					var ptrMatrixW2PrevW = (Matrix4x4*)bufMatrixW2PrevW.GetUnsafePtr();

					// gather boundary shapes
					//TODO expose or always enable the volumeSort option which sorts active boundaries by distance
					var boundaryList = HairBoundaryUtility.Gather(settingsEnvironment.boundaryResident, settingsEnvironment.boundaryCapture, GetVolumeBounds(volumeData), settingsEnvironment.boundaryCaptureLayer, captureSort: false, (settingsEnvironment.boundaryCaptureMode == SettingsEnvironment.BoundaryCaptureMode.IncludeColliders));
					var boundaryCount = 0;
					var boundarySDFIndex = -1;
					var boundarySDFCellSize = 0.0f;

					// count boundaries
					volumeConstantsScene._BoundaryCountDiscrete = 0;
					volumeConstantsScene._BoundaryCountCapsule = 0;
					volumeConstantsScene._BoundaryCountSphere = 0;
					volumeConstantsScene._BoundaryCountTorus = 0;
					volumeConstantsScene._BoundaryCountCube = 0;

					for (int i = 0; i != boundaryList.Count; i++)
					{
						var data = boundaryList[i];
						if (data.type == HairBoundary.RuntimeData.Type.SDF)
						{
							volumeConstantsScene._BoundaryCountDiscrete++;
							boundaryCount++;
							boundarySDFIndex = i;
							boundarySDFCellSize = Mathf.Max(boundarySDFCellSize, data.sdf.worldCellSize);
							break;
						}
					}

					foreach (var data in boundaryList)
					{
						if (data.type == HairBoundary.RuntimeData.Type.Shape)
						{
							switch (data.shape.type)
							{
								case HairBoundary.RuntimeShape.Type.Capsule: volumeConstantsScene._BoundaryCountCapsule++; break;
								case HairBoundary.RuntimeShape.Type.Sphere: volumeConstantsScene._BoundaryCountSphere++; break;
								case HairBoundary.RuntimeShape.Type.Torus: volumeConstantsScene._BoundaryCountTorus++; break;
								case HairBoundary.RuntimeShape.Type.Cube: volumeConstantsScene._BoundaryCountCube++; break;
							}

							boundaryCount++;
						}

						if (boundaryCount == Conf.MAX_BOUNDARIES)
							break;
					}

					volumeConstantsScene._BoundaryWorldEpsilon = (boundarySDFIndex != -1) ? boundarySDFCellSize * 0.2f : 1e-4f;
					volumeConstantsScene._BoundaryWorldMargin = settingsEnvironment.defaultSolidMargin * 0.01f;

					// pack boundaries
					int firstIndexDiscrete = 0;
					int firstIndexCapsule = firstIndexDiscrete + (int)volumeConstantsScene._BoundaryCountDiscrete;
					int firstIndexSphere = firstIndexCapsule + (int)volumeConstantsScene._BoundaryCountCapsule;
					int firstIndexTorus = firstIndexSphere + (int)volumeConstantsScene._BoundaryCountSphere;
					int firstIndexCube = firstIndexTorus + (int)volumeConstantsScene._BoundaryCountTorus;

					int writeIndexDiscrete = firstIndexDiscrete;
					int writeIndexCapsule = firstIndexCapsule;
					int writeIndexSphere = firstIndexSphere;
					int writeIndexTorus = firstIndexTorus;
					int writeIndexCube = firstIndexCube;
					int writeCount = 0;

					if (boundarySDFIndex != -1)
					{
						ptrXform[writeIndexDiscrete] = boundaryList[boundarySDFIndex].xform;
						ptrShape[writeIndexDiscrete++] = new HairBoundary.RuntimeShape.Data();
						writeCount++;
					}

					foreach (HairBoundary.RuntimeData data in boundaryList)
					{
						if (data.type == HairBoundary.RuntimeData.Type.Shape)
						{
							int writeIndex = -1;

							switch (data.shape.type)
							{
								case HairBoundary.RuntimeShape.Type.Capsule: writeIndex = writeIndexCapsule++; break;
								case HairBoundary.RuntimeShape.Type.Sphere: writeIndex = writeIndexSphere++; break;
								case HairBoundary.RuntimeShape.Type.Torus: writeIndex = writeIndexTorus++; break;
								case HairBoundary.RuntimeShape.Type.Cube: writeIndex = writeIndexCube++; break;
							}

							if (writeIndex != -1)
							{
								ptrXform[writeIndex] = data.xform;
								ptrShape[writeIndex] = data.shape.data;
								writeCount++;
							}
						}

						if (writeCount == Conf.MAX_BOUNDARIES)
							break;
					}

					// update sdf
					if (boundarySDFIndex != -1)
						volumeTextures._BoundarySDF = boundaryList[boundarySDFIndex].sdf.sdfTexture as RenderTexture;
					else
						volumeTextures._BoundarySDF = null;

					// update matrices
					for (int i = 0; i != boundaryCount; i++)
					{
						int ptrXformPrevIndex = -1;

						for (int j = 0; j != volumeData.boundaryPrevCount; j++)
						{
							if (ptrXformPrev[j].handle == ptrXform[i].handle)
							{
								ptrXformPrevIndex = j;
								break;
							}
						}

						ptrMatrix[i] = ptrXform[i].matrix;
						ptrMatrixInv[i] = Matrix4x4.Inverse(ptrMatrix[i]);

						// world to previous world
						if (ptrXformPrevIndex != -1)
							ptrMatrixW2PrevW[i] = ptrXformPrev[ptrXformPrevIndex].matrix * ptrMatrixInv[i];
						else
							ptrMatrixW2PrevW[i] = Matrix4x4.identity;

						// world to UVW for discrete
						if (i < volumeConstantsScene._BoundaryCountDiscrete && boundarySDFIndex != -1)
						{
							ptrMatrixInv[i] = boundaryList[boundarySDFIndex].sdf.worldToUVW;
						}
						// world to prim for cube
						else if (i < volumeConstantsScene._BoundaryCountCube + firstIndexCube && i >= firstIndexCube)
						{
							ptrMatrixInv[i] = Matrix4x4.Inverse(ptrMatrix[i].WithoutScale());
						}
					}

					// update previous frame info
					volumeData.boundaryPrevXform.CopyFrom(bufXform);
					volumeData.boundaryPrevCount = boundaryCount;
					volumeData.boundaryPrevCountDiscard = boundaryList.Count - boundaryCount;

					// update buffers
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryShape, bufShape);
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryMatrix, bufMatrix);
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryMatrixInv, bufMatrixInv);
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryMatrixW2PrevW, bufMatrixW2PrevW);
				}
			}

			// capture emitters
			using (var bufEmitter = new NativeArray<HairWind.RuntimeEmitter>(Conf.MAX_EMITTERS, Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				unsafe
				{
					var ptrEmitter = (HairWind.RuntimeEmitter*)bufEmitter.GetUnsafePtr();

					// derive emitters
					//TODO move to HairWindUtility
					var emitterList = new UnsafeList<HairWind.RuntimeData>(Conf.MAX_EMITTERS, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
					var emitterData = new HairWind.RuntimeData();

					foreach (var emitter in HairWind.s_emitters)
					{
						if (emitter == null || emitter.isActiveAndEnabled == false)
							continue;

						if (HairWind.TryGetData(emitter, ref emitterData))
						{
							emitterList.Add(emitterData);
						}
					}

					// derive constants
					volumeConstantsScene._WindEmitterCount = (uint)Mathf.Min(Conf.MAX_EMITTERS, emitterList.Length);
					volumeConstantsScene._WindEmitterClock = time;

					// update emitter data
					for (int i = 0; i != volumeConstantsScene._WindEmitterCount; i++)
					{
						ptrEmitter[i] = emitterList[i].emitter;
					}

					emitterList.Dispose();

					// update buffers
					PushComputeBufferData(cmd, volumeData.buffers._WindEmitter, bufEmitter);
				}
			}

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBufferEnvironment, volumeConstantsScene);
		}

		public static void PushVolumeLOD(CommandBuffer cmd, ref VolumeData volumeData, in SettingsVolume settingsVolume)
		{
			ref var volumeConstants = ref volumeData.constants;

			// derive constants
			volumeConstants._GridResolution = settingsVolume.gridResolution;

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBuffer, volumeConstants);

			// lod selection
			BindVolumeData(cmd, s_volumeCS, VolumeKernels.KLODSelection, volumeData);
			cmd.DispatchCompute(s_volumeCS, VolumeKernels.KLODSelection, 1, 1, 1);
		}

		public static void PushVolumeSettings(CommandBuffer cmd, ref VolumeData volumeData, in SettingsVolume settingsVolume, float deltaTime)
		{
			ref var volumeConstants = ref volumeData.constants;
			ref var volumeKeywords = ref volumeData.keywords;

			// derive constants
			volumeConstants._VolumeDT = deltaTime;

			var allGroupsAvgRestSpan = volumeConstants._AllGroupsAvgParticleDiameter + volumeConstants._AllGroupsAvgParticleMargin;
			var allGroupsAvgRestDensity = (volumeConstants._AllGroupsAvgParticleDiameter * volumeConstants._AllGroupsAvgParticleDiameter) / (allGroupsAvgRestSpan * allGroupsAvgRestSpan);

			volumeConstants._TargetDensityScale = Mathf.Max(1e-7f, settingsVolume.restDensityScale * allGroupsAvgRestDensity);
			volumeConstants._TargetDensityInfluence = settingsVolume.restDensityInfluence;

			volumeConstants._ScatteringProbeUnitWidth = volumeConstants._AllGroupsAvgParticleDiameter * (1.0f / Mathf.Max(1e-7f, settingsVolume.scatteringProbeBias));
			volumeConstants._ScatteringProbeSubsteps = settingsVolume.scatteringProbeCellSubsteps;
			volumeConstants._ScatteringProbeSamplesTheta = settingsVolume.probeSamplesTheta;
			volumeConstants._ScatteringProbeSamplesPhi = settingsVolume.probeSamplesPhi;
			volumeConstants._ScatteringProbeOccluderDensity = (settingsVolume.probeOcclusion ? settingsVolume.probeOcclusionSolidDensity : 0.0f);
			volumeConstants._ScatteringProbeOccluderMargin = 1.0f;// multiplier for cell radius //TODO expose more control if necessary

			volumeConstants._WindPropagationSubsteps = settingsVolume.windPropagationCellSubsteps;
			volumeConstants._WindPropagationExtinction = 1.0f / Mathf.Max(1e-7f, settingsVolume.windDepth * 0.01f);
			volumeConstants._WindPropagationOccluderDensity = (settingsVolume.windOcclusion ? float.PositiveInfinity : 0.0f);
			volumeConstants._WindPropagationOccluderMargin = (settingsVolume.windOcclusionMode == SettingsVolume.OcclusionMode.Discrete) ? 1.0f : 0.0f;// multiplier for cell radius //TODO expose more control if necessary

			// derive features
			VolumeFeatures features = 0;
			{
				features |= (settingsVolume.scatteringProbe) ? VolumeFeatures.Scattering : 0;
				features |= (settingsVolume.scatteringProbe && (volumeConstants._ScatteringProbeOccluderDensity == 0.0f || settingsVolume.probeOcclusionMode == SettingsVolume.OcclusionMode.Discrete)) ? VolumeFeatures.ScatteringFastpath : 0;
				features |= (settingsVolume.windPropagation) ? VolumeFeatures.Wind : 0;
				features |= (settingsVolume.windPropagation && (volumeConstants._WindPropagationOccluderDensity == 0.0f || settingsVolume.windOcclusionMode == SettingsVolume.OcclusionMode.Discrete)) ? VolumeFeatures.WindFastpath : 0;
			}
			volumeConstants._VolumeFeatures = (uint)features;

			// derive keywords
			volumeKeywords.VOLUME_SPLAT_CLUSTERS = (settingsVolume.splatClusters);
			volumeKeywords.VOLUME_TARGET_INITIAL_POSE = (settingsVolume.restDensity == SettingsVolume.TargetDensity.InitialPose);
			volumeKeywords.VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES = (settingsVolume.restDensity == SettingsVolume.TargetDensity.InitialPoseInParticles);
			volumeKeywords.VOLUME_SUPPORT_CONTRACTION = (settingsVolume.pressureSolution == SettingsVolume.PressureSolution.DensityEquals);

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBuffer, volumeConstants);
		}

		public static void PushVolumeStep(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, ref VolumeData volumeData, in SettingsVolume settingsVolume, SolverData[] solverData, float stepFracHi)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Volume))
			{
				HairSim.PushVolumeClear(cmd, ref volumeData, settingsVolume);

				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.PushVolumeTransfer(cmd, cmdFlags, ref volumeData, settingsVolume, solverData[i]);
				}

				HairSim.PushVolumeResolve(cmd, ref volumeData, settingsVolume);
			}
		}

		static void PushVolumeClear(CommandBuffer cmd, ref VolumeData volumeData, in SettingsVolume settingsVolume)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Volume_0_Clear))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeClear, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeClear, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
			}
		}

		static void PushVolumeTransfer(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, ref VolumeData volumeData, in SettingsVolume settingsVolume, in SolverData solverData)
		{
			//var splatStrandCount = volumeData.keywords.VOLUME_SPLAT_CLUSTERS ? solverData.constants._SolverStrandCount : solverData.constants._StrandCount;
			//var splatParticleCount = splatStrandCount * solverData.constants._StrandParticleCount;

			//int numX = ((int)splatParticleCount + PARTICLE_GROUP_SIZE - 1) / PARTICLE_GROUP_SIZE;
			//int numY = 1;
			//int numZ = 1;

			var transferDispatch = volumeData.keywords.VOLUME_SPLAT_CLUSTERS ?
				SolverLODDispatch.Transfer :
				SolverLODDispatch.TransferAll;

			using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat))
			{
				switch (settingsVolume.splatMethod)
				{
					case SettingsVolume.SplatMethod.Compute:
						{
							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, volumeData);
							BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplat, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)transferDispatch);
						}
						break;

					case SettingsVolume.SplatMethod.ComputeSplit:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_Density))
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatDensity, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)transferDispatch);
							}

							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_VelocityXYZ))
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)transferDispatch);

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)transferDispatch);

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, solverData.buffers._SolverLODDispatch, (uint)solverData.buffers._SolverLODDispatch.stride * (uint)transferDispatch);
							}
						}
						break;

					case SettingsVolume.SplatMethod.Rasterization:
						{
							if (cmdFlags.HasFlag(CommandBufferExecutionFlags.AsyncCompute))
							{
								goto case SettingsVolume.SplatMethod.Compute;
							}
							else if (s_runtimeFlags.HasFlag(RuntimeFlags.SupportsGeometryStage))
							{
								var rasterDispatchPoints = volumeData.keywords.VOLUME_SPLAT_CLUSTERS ?
									SolverLODDispatch.RasterPoints :
									SolverLODDispatch.RasterPointsAll;

								using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_Rasterization))
								{
									CoreUtils.SetRenderTarget(cmd, volumeData.textures._VolumeVelocity, ClearFlag.Color);

									BindVolumeData(cmd, volumeData);
									BindSolverData(cmd, solverData);
									cmd.DrawProceduralIndirect(Matrix4x4.identity, s_volumeRasterMat, 0, MeshTopology.Points, solverData.buffers._SolverLODDispatch, (int)solverData.buffers._SolverLODDispatch.stride * (int)rasterDispatchPoints);
									//cmd.DrawProcedural(Matrix4x4.identity, s_volumeRasterMat, 0, MeshTopology.Points, (int)splatParticleCount, 1);
								}
							}
							else
							{
								goto case SettingsVolume.SplatMethod.RasterizationNoGS;
							}
						}
						break;

					case SettingsVolume.SplatMethod.RasterizationNoGS:
						{
							if (cmdFlags.HasFlag(CommandBufferExecutionFlags.AsyncCompute))
							{
								goto case SettingsVolume.SplatMethod.Compute;
							}
							else
							{
								var rasterDispatchQuads = volumeData.keywords.VOLUME_SPLAT_CLUSTERS ?
									SolverLODDispatch.RasterQuads :
									SolverLODDispatch.RasterQuadsAll;

								using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_RasterizationNoGS))
								{
									CoreUtils.SetRenderTarget(cmd, volumeData.textures._VolumeVelocity, ClearFlag.Color);

									BindVolumeData(cmd, volumeData);
									BindSolverData(cmd, solverData);
									cmd.DrawProceduralIndirect(Matrix4x4.identity, s_volumeRasterMat, 1, MeshTopology.Quads, solverData.buffers._SolverLODDispatch, (int)solverData.buffers._SolverLODDispatch.stride * (int)rasterDispatchQuads);
									//cmd.DrawProcedural(Matrix4x4.identity, s_volumeRasterMat, 1, MeshTopology.Quads, (int)splatParticleCount * 8, 1);
								}
							}
						}
						break;
				}
			}
		}

		static void PushVolumeResolve(CommandBuffer cmd, ref VolumeData volumeData, in SettingsVolume settingsVolume)
		{
			// resolve density, velocity
			switch (settingsVolume.splatMethod)
			{
				case SettingsVolume.SplatMethod.Compute:
				case SettingsVolume.SplatMethod.ComputeSplit:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_2_Resolve))
						{
							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeResolve, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolve, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
						}
					}
					break;

				case SettingsVolume.SplatMethod.Rasterization:
				case SettingsVolume.SplatMethod.RasterizationNoGS:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_2_ResolveRaster))
						{
							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeResolveRaster, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolveRaster, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
						}
					}
					break;
			}

			// compute divergence
			using (new ProfilingScope(cmd, MarkersGPU.Volume_3_Divergence))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeDivergence, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeDivergence, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
			}

			// pressure eos (initial guess)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_4_PressureEOS))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureEOS, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureEOS, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
			}

			// pressure solve (jacobi)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_5_PressureSolve))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureSolve, volumeData);

				for (int i = 0; i != settingsVolume.pressureIterations; i++)
				{
					cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureSolve, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);

					CoreUtils.Swap(ref volumeData.textures._VolumePressure, ref volumeData.textures._VolumePressureNext);
					cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumePressureSolve, VolumeData.s_textureIDs._VolumePressure, volumeData.textures._VolumePressure);
					cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumePressureSolve, VolumeData.s_textureIDs._VolumePressureNext, volumeData.textures._VolumePressureNext);
				}
			}

			// pressure gradient
			using (new ProfilingScope(cmd, MarkersGPU.Volume_6_PressureGradient))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureGradient, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureGradient, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
			}

			// scattering probe
			if (settingsVolume.scatteringProbe)
			{
				using (new ProfilingScope(cmd, MarkersGPU.Volume_7_Scattering))
				{
					switch (settingsVolume.probeOcclusionMode)
					{
						case SettingsVolume.OcclusionMode.Discrete:
							{
								if (volumeData.constants._ScatteringProbeOccluderDensity > 0.0f)
								{
									goto case SettingsVolume.OcclusionMode.Exact;
								}

								cmd.GetTemporaryRT(VolumeData.s_textureIDs._VolumeDensityComp, MakeVolumeDesc((int)settingsVolume.gridResolution, RenderTextureFormat.RHalf));

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeScatteringPrep, volumeData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeScatteringPrep, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeScattering, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeScattering, VolumeData.s_textureIDs._VolumeDensity, VolumeData.s_textureIDs._VolumeDensityComp);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeScattering, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeScattering, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);

								cmd.ReleaseTemporaryRT(VolumeData.s_textureIDs._VolumeDensityComp);
							}
							break;

						case SettingsVolume.OcclusionMode.Exact:
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeScattering, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeScattering, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeScattering, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
							}
							break;
					}
				}
			}

			// wind propagation
			if (settingsVolume.windPropagation)
			{
				using (new ProfilingScope(cmd, MarkersGPU.Volume_8_Wind))
				{
					switch (settingsVolume.windOcclusionMode)
					{
						case SettingsVolume.OcclusionMode.Discrete:
							{
								if (volumeData.constants._WindPropagationOccluderDensity == 0.0f)
								{
									goto case SettingsVolume.OcclusionMode.Exact;
								}

								cmd.GetTemporaryRT(VolumeData.s_textureIDs._VolumeDensityComp, MakeVolumeDesc((int)settingsVolume.gridResolution, RenderTextureFormat.RHalf));

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeWindPrep, volumeData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeWindPrep, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeWind, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeWind, VolumeData.s_textureIDs._VolumeDensity, VolumeData.s_textureIDs._VolumeDensityComp);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeWind, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeWind, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);

								cmd.ReleaseTemporaryRT(VolumeData.s_textureIDs._VolumeDensityComp);
							}
							break;

						case SettingsVolume.OcclusionMode.Exact:
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeWind, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeWind, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeWind, volumeData.buffers._VolumeLODDispatch, (uint)volumeData.buffers._VolumeLODDispatch.stride * (uint)VolumeLODDispatch.Resolve);
							}
							break;
					}
				}
			}
		}

		enum DebugDrawPass
		{
			StrandRootFrame			= 0,
			StrandParticlePosition	= 1,
			StrandParticleVelocity	= 2,
			StrandParticleClusters	= 3,
			VolumeCellDensity		= 4,
			VolumeCellGradient		= 5,
			VolumeSliceAbove		= 6,
			VolumeSliceBelow		= 7,
			VolumeIsosurface		= 8,
		};

		public static void DrawSolverData(CommandBuffer cmd, in SolverData solverData, in SettingsDebugging settingsDebugging)
		{
			using (new ProfilingScope(cmd, MarkersGPU.DrawSolverData))
			{
				if (!settingsDebugging.drawStrandRoots &&
					!settingsDebugging.drawStrandParticles &&
					!settingsDebugging.drawStrandVelocities &&
					!settingsDebugging.drawStrandClusters)
					return;

				BindSolverData(cmd, solverData);

				s_debugDrawPb.SetInt(UniformIDs._DebugCluster, settingsDebugging.specificCluster);

				// strand roots
				if (settingsDebugging.drawStrandRoots)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.StrandRootFrame, MeshTopology.Lines, vertexCount: 6, (int)solverData.constants._StrandCount, s_debugDrawPb);
				}

				// strand particles
				if (settingsDebugging.drawStrandParticles)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.StrandParticlePosition, MeshTopology.Points, vertexCount: (int)solverData.constants._StrandParticleCount, (int)solverData.constants._StrandCount, s_debugDrawPb);
				}

				// strand velocities
				if (settingsDebugging.drawStrandVelocities)
				{
					if (Conf.SECOND_ORDER_UPDATE != 0)
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.StrandParticleVelocity, MeshTopology.Lines, vertexCount: 4 * (int)solverData.constants._StrandParticleCount, (int)solverData.constants._StrandCount, s_debugDrawPb);
					else
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.StrandParticleVelocity, MeshTopology.Lines, vertexCount: 2 * (int)solverData.constants._StrandParticleCount, (int)solverData.constants._StrandCount, s_debugDrawPb);
				}

				// strand clusters
				if (settingsDebugging.drawStrandClusters)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.StrandParticleClusters, MeshTopology.Lines, vertexCount: 2, (int)solverData.constants._StrandCount, s_debugDrawPb);
				}
			}
		}

		public static void DrawVolumeData(CommandBuffer cmd, in VolumeData volumeData, in SettingsDebugging settingsDebugging)
		{
			using (new ProfilingScope(cmd, MarkersGPU.DrawVolumeData))
			{
				if (!settingsDebugging.drawCellDensity &&
					!settingsDebugging.drawCellGradient &&
					!settingsDebugging.drawSliceX &&
					!settingsDebugging.drawSliceY &&
					!settingsDebugging.drawSliceZ &&
					!settingsDebugging.drawIsosurface)
					return;

				BindVolumeData(cmd, volumeData);

				// cell density
				if (settingsDebugging.drawCellDensity)
				{
					cmd.DrawProceduralIndirect(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeCellDensity, MeshTopology.Points, volumeData.buffers._VolumeLODDispatch, (int)volumeData.buffers._VolumeLODDispatch.stride * (int)VolumeLODDispatch.RasterPoints);
					//cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_CELL_DENSITY, MeshTopology.Points, GetVolumeCellCount(volumeData), 1);
				}

				// cell gradient
				if (settingsDebugging.drawCellGradient)
				{
					cmd.DrawProceduralIndirect(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeCellGradient, MeshTopology.Lines, volumeData.buffers._VolumeLODDispatch, (int)volumeData.buffers._VolumeLODDispatch.stride * (int)VolumeLODDispatch.RasterVectors);
					//cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_CELL_GRADIENT, MeshTopology.Lines, 2 * GetVolumeCellCount(volumeData), 1);
				}

				// volume slices
				if (settingsDebugging.drawSliceX || settingsDebugging.drawSliceY || settingsDebugging.drawSliceZ)
				{
					s_debugDrawPb.SetFloat(UniformIDs._DebugSliceDivider, settingsDebugging.drawSliceDivider);

					if (settingsDebugging.drawSliceX)
					{
						s_debugDrawPb.SetInt(UniformIDs._DebugSliceAxis, 0);
						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOffset, settingsDebugging.drawSliceXOffset);

						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeSliceAbove, MeshTopology.Quads, 4, 1, s_debugDrawPb);
						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeSliceBelow, MeshTopology.Quads, 4, 1, s_debugDrawPb);
					}
					if (settingsDebugging.drawSliceY)
					{
						s_debugDrawPb.SetInt(UniformIDs._DebugSliceAxis, 1);
						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOffset, settingsDebugging.drawSliceYOffset);

						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeSliceAbove, MeshTopology.Quads, 4, 1, s_debugDrawPb);
						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeSliceBelow, MeshTopology.Quads, 4, 1, s_debugDrawPb);
					}
					if (settingsDebugging.drawSliceZ)
					{
						s_debugDrawPb.SetInt(UniformIDs._DebugSliceAxis, 2);
						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOffset, settingsDebugging.drawSliceZOffset);

						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeSliceAbove, MeshTopology.Quads, 4, 1, s_debugDrawPb);
						s_debugDrawPb.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeSliceBelow, MeshTopology.Quads, 4, 1, s_debugDrawPb);
					}
				}

				// volume isosurface
				if (settingsDebugging.drawIsosurface)
				{
					var volumeBounds = GetVolumeBounds(volumeData);

					s_debugDrawPb.SetFloat(UniformIDs._DebugIsosurfaceDensity, settingsDebugging.drawIsosurfaceDensity);
					s_debugDrawPb.SetInt(UniformIDs._DebugIsosurfaceSubsteps, (int)settingsDebugging.drawIsosurfaceSubsteps);

					cmd.DrawMesh(s_debugDrawCube, Matrix4x4.TRS(volumeBounds.center, Quaternion.identity, 2.0f * volumeBounds.extents), s_debugDrawMat, 0, (int)DebugDrawPass.VolumeIsosurface, s_debugDrawPb);
				}
			}
		}

		//TODO move elsewhere
		//maybe 'HairSimDataUtility' ?

		public static Bounds GetSolverBounds(in SolverData solverData, in VolumeData volumeData)
		{
			var boundsBuffer = volumeData.buffersReadback._Bounds.GetData<LODBounds>();
			if (boundsBuffer.IsCreated)
			{
				var bounds = boundsBuffer[(int)solverData.constants._GroupBoundsIndex];
				return new Bounds(bounds.center, 2.0f * bounds.extent);
			}
			else
			{
				return new Bounds();
			}
		}

		public static Bounds GetVolumeBounds(in VolumeData volumeData)
		{
			var boundsBuffer = volumeData.buffersReadback._Bounds.GetData<LODBounds>();
			if (boundsBuffer.IsCreated)
			{
				var bounds = boundsBuffer[(int)volumeData.constants._CombinedBoundsIndex];
				return new Bounds(bounds.center, 2.0f * bounds.extent).ToSquare();
			}
			else
			{
				return new Bounds();
			}
		}
	}
}