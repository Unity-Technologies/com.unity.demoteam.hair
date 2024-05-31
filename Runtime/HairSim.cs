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
			public static ProfilingSampler Solver_SubstepScene;
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
			//TODO move into per-step cbuffer?
			public static int _SubstepFractionLo;
			public static int _SubstepFractionHi;

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
			public static int KLODSelectionInit;
			public static int KLODSelection;
			public static int KSolveConstraints_GaussSeidelReference;
			public static int KSolveConstraints_GaussSeidel;
			public static int KSolveConstraints_Jacobi_16;
			public static int KSolveConstraints_Jacobi_32;
			public static int KSolveConstraints_Jacobi_64;
			public static int KSolveConstraints_Jacobi_128;
			public static int KInterpolate;
			public static int KInterpolateAdd;
			public static int KInterpolatePromote;
			public static int KStaging;
			public static int KStagingSubdivision;
			public static int KStagingHistory;
		}

		static class VolumeKernels
		{
			// bounds
			public static int KBoundsClear;
			public static int KBoundsGather;
			public static int KBoundsResolve;
			public static int KBoundsResolveCombined;
			public static int KBoundsHistory;
			public static int KBoundsCoverage;
			public static int KLODSelection;

			// grid
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

			// scene
			public static int KBoundariesAdvance;
			public static int KBoundariesSubstep;
			public static int KEmittersAdvance;
			public static int KEmittersSubstep;
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
				InitializeStructFields(ref SolverData.s_externalIDs, (string s) => Shader.PropertyToID(s));

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

				changed |= CreateBuffer(ref solverBuffers._RootPositionNext, "RootPosition_0", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootPositionPrev, "RootPosition_1", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootPosition, "RootPosition_2", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootFrameNext, "RootFrame_0", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootFramePrev, "RootFrame_1", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._RootFrame, "RootFrame_2", strandCount, particleStrideVector4);

				changed |= CreateBuffer(ref solverBuffers._SolverLODStage, "SolverLODStage", (int)SolverLODStage.__COUNT, sizeof(LODIndices));
				changed |= CreateBuffer(ref solverBuffers._SolverLODRange, "SolverLODRange", (int)SolverLODRange.__COUNT, particleStrideVector2);
				changed |= CreateBuffer(ref solverBuffers._SolverLODDispatch, "SolverLODDispatch", (int)SolverLODDispatch.__COUNT * 4, sizeof(uint), ComputeBufferType.Structured | ComputeBufferType.IndirectArguments);

				changed |= CreateBuffer(ref solverBuffers._InitialParticleOffset, "InitialParticleOffset", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._InitialParticleFrameDelta, "InitialParticleFrameDelta", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverBuffers._InitialParticleFrameDelta16, "InitialParticleFrameDelta16", particleCount, particleStrideVector2);

				changed |= CreateBuffer(ref solverBuffers._ParticlePosition, "ParticlePosition_0", particleCount, particleStrideVector3);
				changed |= CreateBuffer(ref solverBuffers._ParticlePositionPrev, "ParticlePosition_1", particleCount, particleStrideVector3);
				changed |= CreateBuffer(ref solverBuffers._ParticlePositionPrevPrev, "ParticlePosition_2", (Conf.SECOND_ORDER_UPDATE != 0) ? particleCount : 1, particleStrideVector3);
				changed |= CreateBuffer(ref solverBuffers._ParticleVelocity, "ParticleVelocity_0", particleCount, particleStrideVector3);
				changed |= CreateBuffer(ref solverBuffers._ParticleVelocityPrev, "ParticleVelocity_1", (Conf.SECOND_ORDER_UPDATE != 0) ? particleCount : 1, particleStrideVector3);

				changed |= CreateBuffer(ref solverBuffers._LODGuideCount, "LODGuideCount", Mathf.Max(1, lodCount), particleStrideIndex);
				changed |= CreateBuffer(ref solverBuffers._LODGuideIndex, "LODGuideIndex", Mathf.Max(1, lodCount) * strandCount, particleStrideIndex);
				changed |= CreateBuffer(ref solverBuffers._LODGuideCarry, "LODGuideCarry", Mathf.Max(1, lodCount) * strandCount, particleStrideScalar);
				changed |= CreateBuffer(ref solverBuffers._LODGuideReach, "LODGuideReach", Mathf.Max(1, lodCount) * strandCount, particleStrideScalar);

				CreateReadbackBuffer(ref solverData.buffersReadback._SolverLODStage, solverBuffers._SolverLODStage);

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

				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrixNext, "BoundaryMatrixNext", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrixPrevA, "BoundaryMatrixPrevA", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrixPrevQ, "BoundaryMatrixPrevQ", Conf.MAX_BOUNDARIES, sizeof(Quaternion));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrix, "BoundaryMatrix", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrixInv, "BoundaryMatrixInv", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryMatrixInvStep, "BoundaryMatrixInvStep", Conf.MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryShapeNext, "BoundaryShapeNext", Conf.MAX_BOUNDARIES, sizeof(HairBoundary.RuntimeShape.Data));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryShapePrevLUT, "BoundaryShapePrevLUT", Conf.MAX_BOUNDARIES, sizeof(int));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryShapePrev, "BoundaryShapePrev", Conf.MAX_BOUNDARIES, sizeof(HairBoundary.RuntimeShape.Data));
				changed |= CreateBuffer(ref volumeBuffers._BoundaryShape, "BoundaryShape", Conf.MAX_BOUNDARIES, sizeof(HairBoundary.RuntimeShape.Data));

				changed |= CreateBuffer(ref volumeBuffers._WindEmitterNext, "WindEmitterNext", Conf.MAX_EMITTERS, sizeof(HairWind.RuntimeEmitter));
				changed |= CreateBuffer(ref volumeBuffers._WindEmitterPrevLUT, "WindEmitterPrevLUT", Conf.MAX_EMITTERS, sizeof(int));
				changed |= CreateBuffer(ref volumeBuffers._WindEmitterPrev, "WindEmitterPrev", Conf.MAX_EMITTERS, sizeof(HairWind.RuntimeEmitter));
				changed |= CreateBuffer(ref volumeBuffers._WindEmitter, "WindEmitter", Conf.MAX_EMITTERS, sizeof(HairWind.RuntimeEmitter));

				changed |= CreateBuffer(ref volumeBuffers._BoundsMinMaxU, "BoundsMinMaxU", boundsCount * 2, particleStrideVector3);
				changed |= CreateBuffer(ref volumeBuffers._BoundsPrev, "BoundsPrev", boundsCount, sizeof(LODBounds));
				changed |= CreateBuffer(ref volumeBuffers._Bounds, "Bounds", boundsCount, sizeof(LODBounds));

				changed |= CreateBuffer(ref volumeBuffers._BoundsGeometry, "BoundsGeometry", boundsCount, sizeof(LODGeometry));
				changed |= CreateBuffer(ref volumeBuffers._BoundsCoverage, "BoundsCoverage", boundsCount, particleStrideVector2);

				changed |= CreateBuffer(ref volumeBuffers._VolumeLODStage, "VolumeLODStage", (int)VolumeLODStage.__COUNT, sizeof(VolumeLODGrid));
				changed |= CreateBuffer(ref volumeBuffers._VolumeLODDispatch, "VolumeLODDispatch", (int)VolumeLODDispatch.__COUNT * 4, sizeof(uint), ComputeBufferType.Structured | ComputeBufferType.IndirectArguments);

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
				CreateReadbackBuffer(ref volumeData.buffersReadback._VolumeLODStage, volumeBuffers._VolumeLODStage);
				//CreateReadbackBuffer(ref volumeData.buffersReadback._WindEmitter, volumeBuffers._WindEmitter);

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

			ReleaseBuffer(ref solverBuffers._RootPositionNext);
			ReleaseBuffer(ref solverBuffers._RootPositionPrev);
			ReleaseBuffer(ref solverBuffers._RootPosition);
			ReleaseBuffer(ref solverBuffers._RootFrameNext);
			ReleaseBuffer(ref solverBuffers._RootFramePrev);
			ReleaseBuffer(ref solverBuffers._RootFrame);

			ReleaseBuffer(ref solverBuffers._SolverLODStage);
			ReleaseBuffer(ref solverBuffers._SolverLODRange);
			ReleaseBuffer(ref solverBuffers._SolverLODDispatch);

			ReleaseBuffer(ref solverBuffers._InitialParticleOffset);
			ReleaseBuffer(ref solverBuffers._InitialParticleFrameDelta);
			ReleaseBuffer(ref solverBuffers._InitialParticleFrameDelta16);

			ReleaseBuffer(ref solverBuffers._ParticlePosition);
			ReleaseBuffer(ref solverBuffers._ParticlePositionPrev);
			ReleaseBuffer(ref solverBuffers._ParticlePositionPrevPrev);
			ReleaseBuffer(ref solverBuffers._ParticleVelocity);
			ReleaseBuffer(ref solverBuffers._ParticleVelocityPrev);
			ReleaseBuffer(ref solverBuffers._ParticleCorrection);

			ReleaseBuffer(ref solverBuffers._ParticleOptTexCoord);
			ReleaseBuffer(ref solverBuffers._ParticleOptDiameter);

			ReleaseBuffer(ref solverBuffers._LODGuideCount);
			ReleaseBuffer(ref solverBuffers._LODGuideIndex);
			ReleaseBuffer(ref solverBuffers._LODGuideCarry);
			ReleaseBuffer(ref solverBuffers._LODGuideReach);

			ReleaseBuffer(ref solverBuffers._StagingVertex);
			ReleaseBuffer(ref solverBuffers._StagingVertexPrev);

			ReleaseReadbackBuffer(ref solverData.buffersReadback._SolverLODStage);

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

			ReleaseBuffer(ref volumeBuffers._BoundaryMatrixNext);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrixPrevA);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrixPrevQ);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrix);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrixInv);
			ReleaseBuffer(ref volumeBuffers._BoundaryMatrixInvStep);
			ReleaseBuffer(ref volumeBuffers._BoundaryShapeNext);
			ReleaseBuffer(ref volumeBuffers._BoundaryShapePrevLUT);
			ReleaseBuffer(ref volumeBuffers._BoundaryShapePrev);
			ReleaseBuffer(ref volumeBuffers._BoundaryShape);

			ReleaseBuffer(ref volumeBuffers._WindEmitterNext);
			ReleaseBuffer(ref volumeBuffers._WindEmitterPrevLUT);
			ReleaseBuffer(ref volumeBuffers._WindEmitterPrev);
			ReleaseBuffer(ref volumeBuffers._WindEmitter);

			ReleaseBuffer(ref volumeBuffers._BoundsMinMaxU);
			ReleaseBuffer(ref volumeBuffers._BoundsPrev);
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
			ReleaseReadbackBuffer(ref volumeData.buffersReadback._VolumeLODStage);
			//ReleaseReadbackBuffer(ref volumeData.buffersReadback._WindEmitter);

			ReleaseCPUBuffer(ref volumeData.boundaryPrevHandle);
			ReleaseCPUBuffer(ref volumeData.boundaryPrevMatrix);
			ReleaseCPUBuffer(ref volumeData.emitterPrevHandle);

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

			target.BindComputeBuffer(SolverData.s_bufferIDs._RootPositionNext, solverBuffers._RootPositionNext);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootPositionPrev, solverBuffers._RootPositionPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootPosition, solverBuffers._RootPosition);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootFrameNext, solverBuffers._RootFrameNext);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootFramePrev, solverBuffers._RootFramePrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._RootFrame, solverBuffers._RootFrame);

			target.BindComputeBuffer(SolverData.s_bufferIDs._InitialParticleOffset, solverBuffers._InitialParticleOffset);
			target.BindComputeBuffer(SolverData.s_bufferIDs._InitialParticleFrameDelta, solverBuffers._InitialParticleFrameDelta);
			target.BindComputeBuffer(SolverData.s_bufferIDs._InitialParticleFrameDelta16, solverBuffers._InitialParticleFrameDelta16);

			target.BindComputeBuffer(SolverData.s_bufferIDs._SolverLODStage, solverBuffers._SolverLODStage);
			target.BindComputeBuffer(SolverData.s_bufferIDs._SolverLODRange, solverBuffers._SolverLODRange);
			target.BindComputeBuffer(SolverData.s_bufferIDs._SolverLODDispatch, solverBuffers._SolverLODDispatch);

			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticlePosition, solverBuffers._ParticlePosition);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticlePositionPrev, solverBuffers._ParticlePositionPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticlePositionPrevPrev, solverBuffers._ParticlePositionPrevPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleVelocity, solverBuffers._ParticleVelocity);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleVelocityPrev, solverBuffers._ParticleVelocityPrev);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleCorrection, solverBuffers._ParticleCorrection);

			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleOptTexCoord, solverBuffers._ParticleOptTexCoord);
			target.BindComputeBuffer(SolverData.s_bufferIDs._ParticleOptDiameter, solverBuffers._ParticleOptDiameter);

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

			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrixNext, volumeBuffers._BoundaryMatrixNext);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrixPrevA, volumeBuffers._BoundaryMatrixPrevA);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrixPrevQ, volumeBuffers._BoundaryMatrixPrevQ);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrix, volumeBuffers._BoundaryMatrix);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrixInv, volumeBuffers._BoundaryMatrixInv);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryMatrixInvStep, volumeBuffers._BoundaryMatrixInvStep);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryShapeNext, volumeBuffers._BoundaryShapeNext);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryShapePrevLUT, volumeBuffers._BoundaryShapePrevLUT);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryShapePrev, volumeBuffers._BoundaryShapePrev);
			target.BindComputeBuffer(volumeBufferIDs._BoundaryShape, volumeBuffers._BoundaryShape);

			target.BindComputeBuffer(volumeBufferIDs._WindEmitterNext, volumeBuffers._WindEmitterNext);
			target.BindComputeBuffer(volumeBufferIDs._WindEmitterPrevLUT, volumeBuffers._WindEmitterPrevLUT);
			target.BindComputeBuffer(volumeBufferIDs._WindEmitterPrev, volumeBuffers._WindEmitterPrev);
			target.BindComputeBuffer(volumeBufferIDs._WindEmitter, volumeBuffers._WindEmitter);

			target.BindComputeBuffer(volumeBufferIDs._BoundsMinMaxU, volumeBuffers._BoundsMinMaxU);
			target.BindComputeBuffer(volumeBufferIDs._BoundsPrev, volumeBuffers._BoundsPrev);
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

		public static void PushSolverRoots(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, ref SolverData solverData, Mesh rootMesh, in Matrix4x4 rootMeshMatrix, in Quaternion rootMeshSkinningRotation, int stepCount)
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

			solverConstantsRoots._RootMeshPositionOffset = (uint)vertexPositionOffset;
			solverConstantsRoots._RootMeshPositionStride = (uint)vertexPositionStride;
			solverConstantsRoots._RootMeshTangentOffset = (uint)vertexTangentOffset;
			solverConstantsRoots._RootMeshTangentStride = (uint)vertexTangentStride;
			solverConstantsRoots._RootMeshNormalOffset = (uint)normalOffset;
			solverConstantsRoots._RootMeshNormalStride = (uint)normalStride;
#else
			solverConstantsRoots._RootMeshTangentStride = rootMesh.HasVertexAttribute(VertexAttribute.Tangent) ? 1u : 0u;
#endif

#if !HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN_0_2_1_PREVIEW
			solverConstantsRoots._RootMeshTangentStride = 0;//TODO remove this? (and therein assume that root mesh having tangents == root mesh having tangent frame)
#endif

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBufferRoots, solverConstantsRoots);

			// conditionally advance roots
			if (stepCount > 0)
			{
				CoreUtils.Swap(ref solverBuffers._RootPositionPrev, ref solverBuffers._RootPosition);
				CoreUtils.Swap(ref solverBuffers._RootFramePrev, ref solverBuffers._RootFrame);
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
					int numX = ((int)solverData.constants._StrandCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
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

					cmd.SetRandomWriteTarget(1, solverData.buffers._RootPositionNext);
					cmd.SetRandomWriteTarget(2, solverData.buffers._RootFrameNext);
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

		public static void PushSolverLODInit(CommandBuffer cmd, in SolverData solverData)
		{
			BindSolverData(cmd, s_solverCS, SolverKernels.KLODSelectionInit, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KLODSelectionInit, 1, 1, 1);
		}

		public static void PushSolverLOD(CommandBuffer cmd, ref SolverData solverData, in SettingsPhysics settingsPhysics, in SettingsRendering settingsRendering, in VolumeData volumeData, int stepCount)
		{
			ref var solverConstants = ref solverData.constants;

			// derive constants
			solverConstants._SolverLODMethod = ((uint)settingsPhysics.kLODSelection & 0xffffu) | (stepCount > 0 ? 0x10000u : 0x00000u);
			solverConstants._SolverLODCeiling = settingsPhysics.kLODCeiling;
			solverConstants._SolverLODScale = settingsPhysics.kLODScale;
			solverConstants._SolverLODBias = (settingsPhysics.kLODSelection == SolverLODSelection.Manual) ? settingsPhysics.kLODSelectionValue : settingsPhysics.kLODBias;

			solverConstants._RenderLODMethod = (uint)settingsRendering.kLODSelection;
			solverConstants._RenderLODCeiling = settingsRendering.kLODCeiling;
			solverConstants._RenderLODScale = settingsRendering.kLODScale;
			solverConstants._RenderLODBias = (settingsRendering.kLODSelection == RenderLODSelection.Manual) ? settingsRendering.kLODSelectionValue : settingsRendering.kLODBias;
			solverConstants._RenderLODClipThreshold = settingsRendering.clipThreshold;

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverConstants);

			// lod selection
			BindVolumeData(cmd, s_solverCS, SolverKernels.KLODSelection, volumeData);
			BindSolverData(cmd, s_solverCS, SolverKernels.KLODSelection, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KLODSelection, 1, 1, 1);

			// schedule readback
			solverData.buffersReadback._SolverLODStage.ScheduleCopy(cmd, solverData.buffers._SolverLODStage);
			/*
			solverData.buffersReadback._SolverLODRange.ScheduleCopy(cmd, solverData.buffers._SolverLODRange);
			{
				var data = solverData.buffersReadback._SolverLODRange.GetData<uint2>(true);
				
				var rangeAdd = data[(int)HairSim.SolverLODRange.InterpolateAdd];
				var rangePromote = data[(int)HairSim.SolverLODRange.InterpolatePromote];

				var countAdd = rangeAdd.y - rangeAdd.x;
				var countPromote = rangePromote.y - rangePromote.x;

				if (countAdd > 0 || countPromote > 0)
				{
					Debug.Log("---- ADD OR PROMOTE -----");
					for (int i = 0; i != (int)HairSim.SolverLODRange.__COUNT; i++)
					{
						Debug.LogFormat("range {0} = {1}", (HairSim.SolverLODRange)i, data[i]);
					}
				}
			}
			*/

			// add reentrant -> interpolated
			BindSolverData(cmd, s_solverCS, SolverKernels.KInterpolateAdd, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInterpolateAdd, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(SolverLODDispatch.InterpolateAdd));
		}

		public static void PushSolverStepBegin(CommandBuffer cmd, ref SolverData solverData, in SettingsPhysics settingsPhysics, float deltaTime)
		{
			ref var solverConstants = ref solverData.constants;
			ref var solverKeywords = ref solverData.keywords;

			// derive constants
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
				var particleStrideVector3 = sizeof(Vector3);

				CreateBuffer(ref solverData.buffers._ParticleCorrection, "ParticleCorrection", enableParticlePositionCorr ? particleCount : 1, particleStrideVector3);
			}

			// derive keywords
			solverKeywords.LAYOUT_INTERLEAVED = (solverData.memoryLayout == HairAsset.MemoryLayout.Interleaved);
			solverKeywords.LIVE_POSITIONS_3 = ((features & (SolverFeatures.CurvatureEQ | SolverFeatures.CurvatureGEQ | SolverFeatures.CurvatureLEQ | SolverFeatures.PoseLocalShapeRWD)) != 0);
			solverKeywords.LIVE_POSITIONS_2 = !solverKeywords.LIVE_POSITIONS_3 && ((features & (SolverFeatures.Distance | SolverFeatures.PoseLocalShape | SolverFeatures.PoseGlobalRotation)) != 0);
			solverKeywords.LIVE_POSITIONS_1 = !solverKeywords.LIVE_POSITIONS_3 && !solverKeywords.LIVE_POSITIONS_2 && ((features & ~SolverFeatures.PoseGlobalPosition) != 0);
			solverKeywords.LIVE_ROTATIONS_2 = ((features & (SolverFeatures.PoseLocalShape | SolverFeatures.PoseLocalShapeRWD | SolverFeatures.PoseGlobalRotation)) != 0);

			// update cbuffer
			PushConstantBufferData(cmd, solverData.buffers.SolverCBuffer, solverConstants);

			// promote interpolated -> simulated
			BindSolverData(cmd, s_solverCS, SolverKernels.KInterpolatePromote, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInterpolatePromote, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(SolverLODDispatch.InterpolatePromote));
		}

		public static void PushSolverStep(CommandBuffer cmd, ref SolverData solverData, in SettingsPhysics settingsPhysics, in VolumeData volumeData, float stepFracLo, float stepFracHi, bool stepFinal)
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
					solveDispatch = SolverLODDispatch.SolveGroupParticles;
					break;
			}

			using (new ProfilingScope(cmd, MarkersGPU.Solver))
			{
				var substepConstantsBackup = solverData.constants;
				var substepCount = solverData.constants._Substeps;

				for (int i = 0; i != substepCount; i++)
				{
					// substep per-frame scene data
					using (new ProfilingScope(cmd, MarkersGPU.Solver_SubstepScene))
					{
						var substepFracLo = Mathf.Lerp(stepFracLo, stepFracHi, (i + 0) / (float)substepCount);
						var substepFracHi = Mathf.Lerp(stepFracLo, stepFracHi, (i + 1) / (float)substepCount);

						// substep roots
						//TODO skip substepping if range stepFracLo, stepFracHi is [0, 1]
						{
							cmd.SetComputeFloatParam(s_solverCS, UniformIDs._SubstepFractionLo, substepFracLo);
							cmd.SetComputeFloatParam(s_solverCS, UniformIDs._SubstepFractionHi, substepFracHi);

							var resolveRootsInterpolated = stepFinal || (volumeData.keywords.VOLUME_SPLAT_CLUSTERS == false && i == substepCount - 1);
							var resolveRootsDispatch = resolveRootsInterpolated ?
								SolverLODDispatch.Staging :
								SolverLODDispatch.Solve;

							BindSolverData(cmd, s_solverCS, SolverKernels.KRootsSubstep, solverData);
							cmd.DispatchCompute(s_solverCS, SolverKernels.KRootsSubstep, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(resolveRootsDispatch));
						}

						// substep boundaries
						{
							cmd.SetComputeFloatParam(s_volumeCS, UniformIDs._SubstepFractionLo, substepFracLo);
							cmd.SetComputeFloatParam(s_volumeCS, UniformIDs._SubstepFractionHi, substepFracHi);

							int numX = (Conf.MAX_BOUNDARIES + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
							int numY = 1;
							int numZ = 1;

							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundariesSubstep, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundariesSubstep, numX, numY, numZ);
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
						BindSolverData(cmd, s_solverCS, solveKernel, solverData);
						cmd.DispatchCompute(s_solverCS, solveKernel, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(solveDispatch));
					}

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
			}

			if (volumeData.keywords.VOLUME_SPLAT_CLUSTERS == false)
			{
				using (new ProfilingScope(cmd, MarkersGPU.Solver_Interpolate))
				{
					BindSolverData(cmd, s_solverCS, SolverKernels.KInterpolate, solverData);
					cmd.DispatchCompute(s_solverCS, SolverKernels.KInterpolate, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(SolverLODDispatch.Interpolate));
				}
			}
		}

		public static void PushSolverStepEnd(CommandBuffer cmd, in SolverData solverData, in VolumeData volumeData)
		{
			if (volumeData.keywords.VOLUME_SPLAT_CLUSTERS)
			{
				using (new ProfilingScope(cmd, MarkersGPU.Solver_Interpolate))
				{
					BindSolverData(cmd, s_solverCS, SolverKernels.KInterpolate, solverData);
					cmd.DispatchCompute(s_solverCS, SolverKernels.KInterpolate, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(SolverLODDispatch.Interpolate));
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

			var stagingVertexFormat = (StagingVertexFormat)0;
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
				cmd.DispatchCompute(s_solverCS, stagingKernel, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(SolverLODDispatch.Staging));

				if (stagingBufferHistoryReset)
				{
					BindSolverData(cmd, s_solverCS, SolverKernels.KStagingHistory, solverData);
					cmd.DispatchCompute(s_solverCS, SolverKernels.KStagingHistory, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(SolverLODDispatch.Staging));
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
				CoreUtils.Swap(ref volumeData.buffers._Bounds, ref volumeData.buffers._BoundsPrev);

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
						PushComputeBufferData(cmd, volumeData.buffers._BoundsMinMaxU, minMaxUBuffer);
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

		public static void PushVolumeBoundsHistory(CommandBuffer cmd, in VolumeData volumeData)
		{
			var boundsCount = volumeData.constants._CombinedBoundsIndex + 1;
			int boundsNumX = ((int)boundsCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
			int boundsNumY = 1;
			int boundsNumZ = 1;

			BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundsHistory, volumeData);
			cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundsHistory, boundsNumX, boundsNumY, boundsNumZ);
		}

		public static void PushVolumeObservers(CommandBuffer cmd, ref VolumeData volumeData, CameraType cameraType)
		{
			ref var volumeConstantsEnvironment = ref volumeData.constantsEnvironment;

			// update frustums
			using (var frustums = AcquireLODFrustums(CameraType.Game | CameraType.SceneView, Allocator.Temp))
			{
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

				// write constants
				volumeConstantsEnvironment._LODFrustumCount = (uint)frustums.Length;

				// update buffers
				PushComputeBufferData(cmd, volumeData.buffers._LODFrustum, frustums.AsArray());
			}

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBufferEnvironment, volumeConstantsEnvironment);
		}

		public static void PushVolumeEnvironment(CommandBuffer cmd, ref VolumeData volumeData, in SettingsEnvironment settingsEnvironment, int stepCount, float frameFracHi)
		{
			ref var volumeConstantsScene = ref volumeData.constantsEnvironment;
			ref var volumeTextures = ref volumeData.textures;

			// update gravity
			volumeConstantsScene._WorldGravity = settingsEnvironment.gravityRotation * (Physics.gravity * settingsEnvironment.gravityScale);

			// update boundaries
			using (var bufRemap = new NativeArray<int>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufHandle = new NativeArray<int>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrix = new NativeArray<Matrix4x4>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrixA = new NativeArray<Matrix4x4>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrixQ = new NativeArray<Quaternion>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufShape = new NativeArray<HairBoundary.RuntimeShape.Data>(Conf.MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				CreateCPUBuffer(ref volumeData.boundaryPrevHandle, Conf.MAX_BOUNDARIES, Allocator.Persistent, NativeArrayOptions.ClearMemory);
				CreateCPUBuffer(ref volumeData.boundaryPrevMatrix, Conf.MAX_BOUNDARIES, Allocator.Persistent, NativeArrayOptions.ClearMemory);

				unsafe
				{
					var ptrRemap = (int*)bufRemap.GetUnsafePtr();
					var ptrHandle = (int*)bufHandle.GetUnsafePtr();
					var ptrMatrix = (Matrix4x4*)bufMatrix.GetUnsafePtr();
					var ptrMatrixA = (Matrix4x4*)bufMatrixA.GetUnsafePtr();
					var ptrMatrixQ = (Quaternion*)bufMatrixQ.GetUnsafePtr();
					var ptrShape = (HairBoundary.RuntimeShape.Data*)bufShape.GetUnsafePtr();

					// gather boundaries
					//TODO expose or always enable the volumeSort option which sorts active boundaries by distance
					var boundaryList = SpatialComponentFilter<HairBoundary, HairBoundary.RuntimeData, HairBoundaryProxy>.Gather(settingsEnvironment.boundaryResident, settingsEnvironment.boundaryCapture, GetVolumeBounds(volumeData), settingsEnvironment.boundaryCaptureLayer, volumeSort: false, (settingsEnvironment.boundaryCaptureMode == SettingsEnvironment.BoundaryCaptureMode.IncludeColliders));
					var boundaryCountDiscrete = 0;
					var boundaryCountCapsule = 0;
					var boundaryCountSphere = 0;
					var boundaryCountTorus = 0;
					var boundaryCountCube = 0;
					var boundaryCount = 0;

					var boundarySDFIndex = -1;
					var boundarySDFCellSize = 0.0f;
					 
					foreach (var data in boundaryList)
					{
						if (data.type == HairBoundary.RuntimeData.Type.Shape)
						{
							switch (data.shape.type)
							{
								case HairBoundary.RuntimeShape.Type.Capsule: boundaryCountCapsule++; break;
								case HairBoundary.RuntimeShape.Type.Sphere: boundaryCountSphere++; break;
								case HairBoundary.RuntimeShape.Type.Torus: boundaryCountTorus++; break;
								case HairBoundary.RuntimeShape.Type.Cube: boundaryCountCube++; break;
							}

							boundaryCount++;
						}
						else
						{
							if (boundarySDFIndex == -1)// only include first discrete
							{
								boundarySDFIndex = boundaryCount;
								boundarySDFCellSize = Mathf.Max(boundarySDFCellSize, data.sdf.sdfCellSize);

								boundaryCountDiscrete++;
								boundaryCount++;
							}
						}

						if (boundaryCount == Conf.MAX_BOUNDARIES)
							break;
					}

					// prepare delimiters
					var firstIndexDiscrete = 0;
					var firstIndexCapsule = firstIndexDiscrete + boundaryCountDiscrete;
					var firstIndexSphere = firstIndexCapsule + boundaryCountCapsule;
					var firstIndexTorus = firstIndexSphere + boundaryCountSphere;
					var firstIndexCube = firstIndexTorus + boundaryCountTorus;
					var firstIndexEnd = firstIndexCube + boundaryCountCube;

					// write constants
					volumeConstantsScene._BoundaryDelimDiscrete = (uint)firstIndexCapsule;
					volumeConstantsScene._BoundaryDelimCapsule = (uint)firstIndexSphere;
					volumeConstantsScene._BoundaryDelimSphere = (uint)firstIndexTorus;
					volumeConstantsScene._BoundaryDelimTorus = (uint)firstIndexCube;
					volumeConstantsScene._BoundaryDelimCube = (uint)firstIndexEnd;

					volumeConstantsScene._BoundaryWorldEpsilon = (boundarySDFIndex != -1) ? boundarySDFCellSize * 0.2f : 1e-4f;
					volumeConstantsScene._BoundaryWorldMargin = settingsEnvironment.defaultSolidMargin * 0.01f;

					// write textures
					volumeTextures._BoundarySDF = (boundarySDFIndex != -1) ? boundaryList[boundarySDFIndex].sdf.sdfTexture : null;

					// write attributes
					{
						var writeIndexDiscrete = firstIndexDiscrete;
						var writeIndexCapsule = firstIndexCapsule;
						var writeIndexSphere = firstIndexSphere;
						var writeIndexTorus = firstIndexTorus;
						var writeIndexCube = firstIndexCube;
						var writeCount = 0;

						foreach (var data in boundaryList)
						{
							var writeIndex = -1;

							if (data.type == HairBoundary.RuntimeData.Type.Shape)
							{
								switch (data.shape.type)
								{
									case HairBoundary.RuntimeShape.Type.Capsule: writeIndex = writeIndexCapsule++; break;
									case HairBoundary.RuntimeShape.Type.Sphere: writeIndex = writeIndexSphere++; break;
									case HairBoundary.RuntimeShape.Type.Torus: writeIndex = writeIndexTorus++; break;
									case HairBoundary.RuntimeShape.Type.Cube: writeIndex = writeIndexCube++; break;
								}
							}
							else
							{
								if (writeIndexDiscrete == 0)// only include first discrete
								{
									writeIndex = writeIndexDiscrete++;
								}
							}

							if (writeIndex != -1)
							{
								ptrHandle[writeIndex] = data.xform.handle;
								ptrMatrix[writeIndex] = data.xform.matrix;
								ptrShape[writeIndex] = data.shape.data;

								writeCount++;
							}

							if (writeCount == Conf.MAX_BOUNDARIES)
								break;
						}
					}

					// write remapping (current -> previous)
					{
						for (int i = 0; i != boundaryCount; i++)
						{
							ptrRemap[i] = NativeArrayExtensions.IndexOf<int, int>(volumeData.boundaryPrevHandle, ptrHandle[i]);
						}

						for (int i = boundaryCount; i != Conf.MAX_BOUNDARIES; i++)
						{
							ptrRemap[i] = -1;
						}
					}

					// write blend params
					{
						for (int i = 0; i != boundaryCount; i++)
						{
							var j = ptrRemap[i];
							if (j != -1)
							{
								// Ma * M = Mb
								// M = Ma^-1 * Mb
								// ..
								// Mb_t = Ma * M(t)
								//
								// where Ma is the transform of the current frame
								//   and Mb is the transform of the last substep of the previous frame

								var Ma = ptrMatrix[i];
								var Mb = volumeData.boundaryPrevMatrix[j];

								var M = Matrix4x4.Inverse(Ma) * Mb;
								var q = (Quaternion)svd.svdRotation((float3x3)((float4x4)M));

								ptrMatrixA[i] = M;
								ptrMatrixQ[i] = q;
							}
							else
							{
								ptrMatrixA[i] = Matrix4x4.identity;
								ptrMatrixQ[i] = Quaternion.identity;
							}
						}
					}

					// update buffers
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryMatrixNext, bufMatrix);
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryMatrixPrevA, bufMatrixA);
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryMatrixPrevQ, bufMatrixQ);
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryShapeNext, bufShape);
					PushComputeBufferData(cmd, volumeData.buffers._BoundaryShapePrevLUT, bufRemap);

					// update previous frame info
					if (stepCount > 0)
					{
						volumeData.boundaryPrevHandle.CopyFrom(bufHandle);
#if false
						volumeData.boundaryPrevMatrix.CopyFrom(bufMatrix);
#else
						// resolve boundaryPrevMatrix to match contents of _BoundaryMatrix after final substep in frame
						{
							var ptrMatrixPrev = (Matrix4x4*)volumeData.boundaryPrevMatrix.GetUnsafePtr();

							for (int i = 0; i != boundaryCount; i++)
							{
								ref var Ma = ref ptrMatrix[i];
								ref var M = ref ptrMatrixA[i];
								ref var q = ref ptrMatrixQ[i];

								var M_t = AffineUtility.AffineInterpolate4x4(M, ((quaternion)q).value, 1.0f - frameFracHi);
								var Mb_t = AffineUtility.AffineMul4x4(Ma, M_t);

								ptrMatrixPrev[i] = Mb_t;
							}
						}
#endif
					}

					volumeData.boundaryCount = boundaryCount;
					volumeData.boundaryCountDiscard = boundaryList.Count - boundaryCount;

					/* TODO remove
					fixed (void* outShape = volumeConstantsScene._CB_BoundaryShape)
					fixed (void* outMatrix = volumeConstantsScene._CB_BoundaryMatrix)
					fixed (void* outMatrixInv = volumeConstantsScene._CB_BoundaryMatrixInv)
					fixed (void* outMatrixW2PrevW = volumeConstantsScene._CB_BoundaryMatrixW2PrevW)
					{
						UnsafeUtility.MemCpy(outShape, ptrShape, boundaryCount * sizeof(HairBoundary.RuntimeShape.Data));

						static void CopyTransformRows3x4(Vector4* dst, Matrix4x4* src, int count)
						{
							for (int i = 0; i != count; i++)
							{
								ref readonly var A = ref src[i];
								dst[i * 3 + 0] = new Vector4(A.m00, A.m01, A.m02, A.m03);
								dst[i * 3 + 1] = new Vector4(A.m10, A.m11, A.m12, A.m13);
								dst[i * 3 + 2] = new Vector4(A.m20, A.m21, A.m22, A.m23);
							}
						}

						CopyTransformRows3x4((Vector4*)outMatrix, ptrMatrix, boundaryCount);
						CopyTransformRows3x4((Vector4*)outMatrixInv, ptrMatrixInv, boundaryCount);
						CopyTransformRows3x4((Vector4*)outMatrixW2PrevW, ptrMatrixW2PrevW, boundaryCount);
					}
					*/
				}
			}

			// update emitters
			using (var bufRemap = new NativeArray<int>(Conf.MAX_EMITTERS, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufHandle = new NativeArray<int>(Conf.MAX_EMITTERS, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufEmitter = new NativeArray<HairWind.RuntimeEmitter>(Conf.MAX_EMITTERS, Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				CreateCPUBuffer(ref volumeData.emitterPrevHandle, Conf.MAX_EMITTERS, Allocator.Persistent, NativeArrayOptions.ClearMemory);

				unsafe
				{
					var ptrRemap = (int*)bufRemap.GetUnsafePtr();
					var ptrHandle = (int*)bufHandle.GetUnsafePtr();
					var ptrEmitter = (HairWind.RuntimeEmitter*)bufEmitter.GetUnsafePtr();

					// gather emitters
					var emitterList = SpatialComponentFilter<HairWind, HairWind.RuntimeData, HairWindProxy>.Gather(settingsEnvironment.emitterResident, settingsEnvironment.emitterCapture, GetVolumeBounds(volumeData), settingsEnvironment.emitterCaptureLayer, volumeSort: false, (settingsEnvironment.emitterCaptureMode == SettingsEnvironment.EmitterCaptureMode.IncludeWindZones));
					var emitterCount = Mathf.Min(emitterList.Count, Conf.MAX_EMITTERS);

					// write constants
					volumeConstantsScene._WindEmitterCount = (uint)emitterCount;

					// write attributes
					{
						for (int i = 0; i != emitterCount; i++)
						{
							ptrHandle[i] = emitterList[i].xform.handle;
							ptrEmitter[i] = emitterList[i].emitter;
						}
					}

					// write remapping (current -> previous)
					{
						for (int i = 0; i != emitterCount; i++)
						{
							ptrRemap[i] = NativeArrayExtensions.IndexOf<int, int>(volumeData.emitterPrevHandle, ptrHandle[i]);
						}

						for (int i = emitterCount; i != Conf.MAX_EMITTERS; i++)
						{
							ptrRemap[i] = -1;
						}
					}

					// update buffers
					PushComputeBufferData(cmd, volumeData.buffers._WindEmitterNext, bufEmitter);
					PushComputeBufferData(cmd, volumeData.buffers._WindEmitterPrevLUT, bufRemap);

					// update previous frame info
					if (stepCount > 0)
					{
						volumeData.emitterPrevHandle.CopyFrom(bufHandle);
					}

					volumeData.emitterCount = emitterCount;
					volumeData.emitterCountDiscard = emitterList.Count - emitterCount;
				}
			}

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBufferEnvironment, volumeConstantsScene);

			// conditionally advance boundaries
			if (stepCount > 0)
			{
				int numX = (Conf.MAX_BOUNDARIES + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
				int numY = 1;
				int numZ = 1;

				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundariesAdvance, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundariesAdvance, numX, numY, numZ);
			}

			// conditionally advance emitters
			if (stepCount > 0)
			{
				int numX = (Conf.MAX_EMITTERS + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
				int numY = 1;
				int numZ = 1;

				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KEmittersAdvance, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KEmittersAdvance, numX, numY, numZ);
			}
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

			// schedule readback
			volumeData.buffersReadback._VolumeLODStage.ScheduleCopy(cmd, volumeData.buffers._VolumeLODStage);
		}

		public static void PushVolumeStepBegin(CommandBuffer cmd, ref VolumeData volumeData, in SettingsVolume settingsVolume)
		{
			ref var volumeConstants = ref volumeData.constants;
			ref var volumeKeywords = ref volumeData.keywords;

			// derive constants
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
			volumeKeywords.VOLUME_TARGET_INITIAL_POSE = (settingsVolume.restDensity == SettingsVolume.RestDensity.InitialPose);
			volumeKeywords.VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES = (settingsVolume.restDensity == SettingsVolume.RestDensity.InitialPoseInParticles);
			volumeKeywords.VOLUME_SUPPORT_CONTRACTION = (settingsVolume.pressureSolution == SettingsVolume.PressureSolution.DensityEquals);

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.buffers.VolumeCBuffer, volumeConstants);
		}

		public static void PushVolumeStep(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, ref VolumeData volumeData, in SettingsVolume settingsVolume, SolverData[] solverData, float stepFracLo, float stepFracHi, double stepTimeHi)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Volume))
			{
				// update clock
				{
					volumeData.constantsEnvironment._WindEmitterClock = (float)stepTimeHi;
					PushConstantBufferData(cmd, volumeData.buffers.VolumeCBufferEnvironment, volumeData.constantsEnvironment);
				}

				// substep per-frame scene data
				//TODO skip substepping if range stepFracLo, stepFracHi is [0, 1]
				if (stepFracHi > 0.0f)
				{
					cmd.SetComputeFloatParam(s_volumeCS, UniformIDs._SubstepFractionLo, stepFracLo);
					cmd.SetComputeFloatParam(s_volumeCS, UniformIDs._SubstepFractionHi, stepFracHi);

					// substep boundaries
					{
						int numX = (Conf.MAX_BOUNDARIES + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
						int numY = 1;
						int numZ = 1;

						BindVolumeData(cmd, s_volumeCS, VolumeKernels.KBoundariesSubstep, volumeData);
						cmd.DispatchCompute(s_volumeCS, VolumeKernels.KBoundariesSubstep, numX, numY, numZ);
					}

					// substep emitters
					{
						int numX = (Conf.MAX_EMITTERS + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
						int numY = 1;
						int numZ = 1;

						BindVolumeData(cmd, s_volumeCS, VolumeKernels.KEmittersSubstep, volumeData);
						cmd.DispatchCompute(s_volumeCS, VolumeKernels.KEmittersSubstep, numX, numY, numZ);

						/*
						volumeData.buffersReadback._WindEmitter.ScheduleCopy(cmd, volumeData.buffers._WindEmitter);
						{
							var data = volumeData.buffersReadback._WindEmitter.GetData<HairWind.RuntimeEmitter>(true);
							Debug.Log("---emitters---");
							for (int i = 0; i != volumeData.constantsEnvironment._WindEmitterCount; i++)
							{
								var e = data[i];
								Debug.LogFormat(
									"p {0}\tn {1}\tt0 {2}\th0 {3}\tm {4}\t" +
									"v {5}\tA {6}\tf {7}\t" +
									"jd {8}\tjw {9}\tjp {10}",
									e.p, e.n, e.t0, e.h0, e.m,
									e.v, e.A, e.f,
									e.jd, e.jw, e.jp);
							}
						}
						*/
					}
				}

				// build the volume
				{
					HairSim.PushVolumeClear(cmd, ref volumeData, settingsVolume);

					for (int i = 0; i != solverData.Length; i++)
					{
						HairSim.PushVolumeTransfer(cmd, cmdFlags, ref volumeData, settingsVolume, solverData[i]);
					}

					HairSim.PushVolumeResolve(cmd, ref volumeData, settingsVolume);
				}
			}
		}

		public static void PushVolumeStepEnd(CommandBuffer cmd, in VolumeData volumeData)
		{
			// defined for completeness
		}

		static void PushVolumeClear(CommandBuffer cmd, ref VolumeData volumeData, in SettingsVolume settingsVolume)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Volume_0_Clear))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeClear, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeClear, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
			}
		}

		static void PushVolumeTransfer(CommandBuffer cmd, CommandBufferExecutionFlags cmdFlags, ref VolumeData volumeData, in SettingsVolume settingsVolume, in SolverData solverData)
		{
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
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplat, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(transferDispatch));
						}
						break;

					case SettingsVolume.SplatMethod.ComputeSplit:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_Density))
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatDensity, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(transferDispatch));
							}

							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_VelocityXYZ))
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(transferDispatch));

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(transferDispatch));

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, solverData.buffers._SolverLODDispatch, GetSolverLODDispatchOffset(transferDispatch));
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
									cmd.DrawProceduralIndirect(Matrix4x4.identity, s_volumeRasterMat, 0, MeshTopology.Points, solverData.buffers._SolverLODDispatch, (int)GetSolverLODDispatchOffset(rasterDispatchPoints));
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
									cmd.DrawProceduralIndirect(Matrix4x4.identity, s_volumeRasterMat, 1, MeshTopology.Quads, solverData.buffers._SolverLODDispatch, (int)GetSolverLODDispatchOffset(rasterDispatchQuads));
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
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolve, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
						}
					}
					break;

				case SettingsVolume.SplatMethod.Rasterization:
				case SettingsVolume.SplatMethod.RasterizationNoGS:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_2_ResolveRaster))
						{
							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeResolveRaster, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolveRaster, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
						}
					}
					break;
			}

			// compute divergence
			using (new ProfilingScope(cmd, MarkersGPU.Volume_3_Divergence))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeDivergence, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeDivergence, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
			}

			// pressure eos (initial guess)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_4_PressureEOS))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureEOS, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureEOS, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
			}

			// pressure solve (jacobi)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_5_PressureSolve))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureSolve, volumeData);

				for (int i = 0; i != settingsVolume.pressureIterations; i++)
				{
					cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureSolve, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));

					CoreUtils.Swap(ref volumeData.textures._VolumePressure, ref volumeData.textures._VolumePressureNext);
					cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumePressureSolve, VolumeData.s_textureIDs._VolumePressure, volumeData.textures._VolumePressure);
					cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumePressureSolve, VolumeData.s_textureIDs._VolumePressureNext, volumeData.textures._VolumePressureNext);
				}
			}

			// pressure gradient
			using (new ProfilingScope(cmd, MarkersGPU.Volume_6_PressureGradient))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureGradient, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureGradient, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
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
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeScatteringPrep, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeScattering, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeScattering, VolumeData.s_textureIDs._VolumeDensity, VolumeData.s_textureIDs._VolumeDensityComp);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeScattering, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeScattering, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));

								cmd.ReleaseTemporaryRT(VolumeData.s_textureIDs._VolumeDensityComp);
							}
							break;

						case SettingsVolume.OcclusionMode.Exact:
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeScattering, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeScattering, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeScattering, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
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
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeWindPrep, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeWind, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeWind, VolumeData.s_textureIDs._VolumeDensity, VolumeData.s_textureIDs._VolumeDensityComp);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeWind, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeWind, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));

								cmd.ReleaseTemporaryRT(VolumeData.s_textureIDs._VolumeDensityComp);
							}
							break;

						case SettingsVolume.OcclusionMode.Exact:
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeWind, volumeData);
								cmd.SetComputeTextureParam(s_volumeCS, VolumeKernels.KVolumeWind, VolumeData.s_textureIDs._VolumeDensityPreComp, volumeData.textures._VolumeDensity);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeWind, volumeData.buffers._VolumeLODDispatch, GetVolumeLODDispatchOffset(VolumeLODDispatch.Resolve));
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
					cmd.DrawProceduralIndirect(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeCellDensity, MeshTopology.Points, volumeData.buffers._VolumeLODDispatch, (int)GetVolumeLODDispatchOffset(VolumeLODDispatch.RasterPoints));
					//cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_CELL_DENSITY, MeshTopology.Points, GetVolumeCellCount(volumeData), 1);
				}

				// cell gradient
				if (settingsDebugging.drawCellGradient)
				{
					cmd.DrawProceduralIndirect(Matrix4x4.identity, s_debugDrawMat, (int)DebugDrawPass.VolumeCellGradient, MeshTopology.Lines, volumeData.buffers._VolumeLODDispatch, (int)GetVolumeLODDispatchOffset(VolumeLODDispatch.RasterVectors));
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

		public static LODIndices GetSolverLODSelection(in SolverData solverData, SolverLODStage solverLODStage)
		{
			var lodDescBuffer = solverData.buffersReadback._SolverLODStage.GetData<LODIndices>();
			if (lodDescBuffer.IsCreated)
			{
				return lodDescBuffer[(int)solverLODStage];
			}
			else
			{
				return new LODIndices();
			}
		}

		public static VolumeLODGrid GetVolumeLODSelection(in VolumeData volumeData, VolumeLODStage volumeLODStage)
		{
			var lodGridBuffer = volumeData.buffersReadback._VolumeLODStage.GetData<VolumeLODGrid>();
			if (lodGridBuffer.IsCreated)
			{
				return lodGridBuffer[(int)volumeLODStage];
			}
			else
			{
				return new VolumeLODGrid();
			}
		}

		static uint GetSolverLODDispatchOffset(SolverLODDispatch index)
		{
			return (uint)index * 4 * sizeof(uint);
		}

		static uint GetVolumeLODDispatchOffset(VolumeLODDispatch index)
		{
			return (uint)index * 4 * sizeof(uint);
		}
	}
}