#pragma warning disable 0649 // some fields are assigned via reflection

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.DemoTeam.Attributes;

namespace Unity.DemoTeam.Hair
{
	using static HairSimUtility;

	public partial class HairSim
	{
		static bool s_initialized = false;

		static Collider[] s_boundariesOverlapBuffer = new Collider[64];
		static List<HairSimBoundary> s_boundariesTmp = new List<HairSimBoundary>();

		static ComputeShader s_solverCS;
		static ComputeShader s_volumeCS;

		static Material s_solverRootsMat;
		static MaterialPropertyBlock s_solverRootsMPB;

		static Material s_volumeRasterMat;
		static MaterialPropertyBlock s_volumeRasterMPB;

		static Mesh s_debugDrawCube;
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
			public static ProfilingSampler Volume_1_Splat_Density;
			public static ProfilingSampler Volume_1_Splat_VelocityXYZ;
			public static ProfilingSampler Volume_1_Splat_Rasterization;
			public static ProfilingSampler Volume_1_Splat_RasterizationNoGS;
			public static ProfilingSampler Volume_2_Resolve;
			public static ProfilingSampler Volume_2_ResolveFromRasterization;
			public static ProfilingSampler Volume_3_Divergence;
			public static ProfilingSampler Volume_4_PressureEOS;
			public static ProfilingSampler Volume_5_PressureSolve;
			public static ProfilingSampler Volume_6_PressureGradient;
			public static ProfilingSampler DrawSolverData;
			public static ProfilingSampler DrawVolumeData;
		}

		static class UniformIDs
		{
			// solver
			public static int SolverCBuffer;

			public static int _RootScale;
			public static int _RootPosition;
			public static int _RootDirection;
			//public static int _RootFrame;

			public static int _InitialRootFrame;
			public static int _InitialParticleOffset;
			public static int _InitialParticleFrameDelta;

			public static int _ParticlePosition;
			public static int _ParticlePositionPrev;
			public static int _ParticlePositionCorr;
			public static int _ParticleVelocity;
			public static int _ParticleVelocityPrev;

			// volume
			public static int VolumeCBuffer;

			public static int _AccuWeight;
			public static int _AccuWeight0;
			public static int _AccuVelocityX;
			public static int _AccuVelocityY;
			public static int _AccuVelocityZ;

			public static int _VolumeDensity;
			public static int _VolumeDensity0;
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
			public static int _DebugSliceOpacity;
			public static int _DebugIsosurfaceDensity;
			public static int _DebugIsosurfaceSubsteps;
		}

		static class SolverKernels
		{
			public static int KInitParticles;
			public static int KInitParticlesPostVolume;
			public static int KSolveConstraints_GaussSeidelReference;
			public static int KSolveConstraints_GaussSeidel;
			public static int KSolveConstraints_Jacobi_16;
			public static int KSolveConstraints_Jacobi_32;
			public static int KSolveConstraints_Jacobi_64;
			public static int KSolveConstraints_Jacobi_128;
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

		[Serializable]
		public struct SolverSettings
		{
			public enum Method
			{
				GaussSeidelReference = 0,
				GaussSeidel = 1,
				Jacobi = 2,
			}

			public enum TimeSpan
			{
				PerSecond,
				Per100ms,
				Per10ms,
				Per1ms,
			}

			public enum BendCompare
			{
				Equals,
				LessThan,
				GreaterThan,
			}

			public enum LocalShapeMode
			{
				LocalRotation,
				LocalBendTwist,
			}

			[LineHeader("Solver")]

			[Tooltip("Constraint solver")]

			public Method method;
			[Range(1, 100), Tooltip("Constraint iterations")]
			public int iterations;
			[Range(0.0f, 1.0f), Tooltip("Constraint stiffness")]
			public float stiffness;
			[Range(1.0f, 2.0f), Tooltip("Successive-over-relaxation factor")]
			public float kSOR;

			[LineHeader("Integration")]

			[Range(0.0f, 1.0f), Tooltip("Scaling factor for volume pressure impulse")]
			public float cellPressure;
			[Range(0.0f, 1.0f), Tooltip("Scaling factor for volume velocity impulse (where 0 == FLIP, 1 == PIC)")]
			public float cellVelocity;
			[Range(0.0f, 1.0f), Tooltip("Linear damping factor (fraction of linear velocity to subtract per unit of time)")]
			public float damping;
			[HideInInspector, Tooltip("Unit of time")]//TODO expose in ui?
			public TimeSpan dampingPeriod;
			[Range(-1.0f, 1.0f), Tooltip("Scaling factor for gravity (Physics.gravity)")]
			public float gravity;

			[LineHeader("Constraints")]

			[Tooltip("Enable particle-particle distance constraint")]
			public bool distance;
			[Tooltip("Enable 'long range attachment' distance constraint (root-particle maximum distance)")]
			public bool distanceLRA;
			[ToggleGroup, Tooltip("Enable 'follow the leader' distance constraint (hard particle-particle distance, non-physical)")]
			public bool distanceFTL;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("FTL correction / damping factor")]
			public float distanceFTLDamping;

			[ToggleGroup, Tooltip("Enable boundary shape collision constraint")]
			public bool boundaryCollision;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Boundary shape friction")]
			public float boundaryCollisionFriction;

			[ToggleGroup, Tooltip("Enable bending curvature constraint")]
			public bool curvature;
			[ToggleGroupItem, Tooltip("Curvature constraint mode (=, <, >)")]
			public BendCompare curvatureCompare;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Scales to [0 .. 90] degrees")]
			public float curvatureCompareValue;

			[ToggleGroup, Tooltip("Enable local shape constraint")]
			public bool localShape;
			[ToggleGroupItem, Tooltip("Type of local shape constraint")]
			public LocalShapeMode localShapeMode;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Local shape influence")]
			public float localShapeInfluence;

			[LineHeader("Global Shape")]

			[ToggleGroup, Tooltip("Enable global position constraint")]
			public bool globalPosition;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Fraction of global position to apply per unit of time")]
			public float globalPositionInfluence;
			[ToggleGroupItem, Tooltip("Unit of time during which to apply fraction of global position")]
			public TimeSpan globalPositionPeriod;
			[ToggleGroup, Tooltip("Enable global rotation constraint")]
			public bool globalRotation;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Global rotation influence")]
			public float globalRotationInfluence;
			[ToggleGroup, Tooltip("Fade influence of global constraints towards tip of strand")]
			public bool globalFade;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Fade offset from root of strand")]
			public float globalFadeOffset;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Fade extent from fade offset")]
			public float globalFadeExtent;

			public static readonly SolverSettings defaults = new SolverSettings()
			{
				method = Method.GaussSeidel,
				iterations = 5,
				stiffness = 1.0f,
				kSOR = 1.0f,

				cellPressure = 1.0f,
				cellVelocity = 0.05f,
				damping = 0.0f,
				dampingPeriod = TimeSpan.PerSecond,
				gravity = 1.0f,

				distance = true,
				distanceLRA = true,
				distanceFTL = false,
				distanceFTLDamping = 0.8f,
				boundaryCollision = true,
				boundaryCollisionFriction = 0.5f,
				curvature = false,
				curvatureCompare = BendCompare.LessThan,
				curvatureCompareValue = 0.1f,

				localShape = false,
				localShapeMode = LocalShapeMode.LocalBendTwist,
				localShapeInfluence = 1.0f,

				globalPosition = false,
				globalPositionInfluence = 1.0f,
				globalPositionPeriod = TimeSpan.PerSecond,
				globalRotation = false,
				globalRotationInfluence = 1.0f,
				globalFade = false,
				globalFadeOffset = 0.1f,
				globalFadeExtent = 0.2f,
			};
		}

		[Serializable]
		public struct VolumeSettings
		{
			public enum SplatMethod
			{
				None,
				Compute,
				ComputeSplit,
				Rasterization,
				RasterizationNoGS,
			}

			public enum PressureSolution
			{
				DensityEquals,
				DensityLessThan,
			}

			public enum TargetDensity
			{
				Uniform,
				InitialPose,
				InitialPoseInParticles,
			}

			public SplatMethod volumeSplatMethod;
			[ToggleGroup]
			public bool volumeSplatDebug;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 9.0f)]
			public float volumeSplatDebugWidth;
			[Range(8, 160)]
			public int volumeGridResolution;
			[HideInInspector, Tooltip("Increases precision of derivative quantities at the cost of volume splatting performance")]
			public bool volumeGridStaggered;
			[HideInInspector]
			public bool volumeGridSquare;

			[LineHeader("Pressure")]

			[Range(0, 100), Tooltip("Number of pressure iterations (where 0 == Initialization by EOS, [1 .. n] == Jacobi iteration)")]
			public int pressureIterations;
			[Tooltip("Pressure solution can either target an exact density (causing both compression and decompression), or a maximum density (causing just decompression)")]
			public PressureSolution pressureSolution;
			[Tooltip("Target density can be uniform (based on physical strand diameter), based on the initial pose, or based on initial pose carried in particles (a runtime average)")]
			public TargetDensity targetDensity;
			[Range(0.0f, 1.0f), Tooltip("Influence of target density vs. an always-present incompressibility term")]
			public float targetDensityInfluence;

			[LineHeader("Boundaries")]

			[ToggleGroup]
			public bool boundarySDF;
			[ToggleGroupItem(withLabel = true)]
			public Texture3DWithBounds boundarySDFTexture;
			[ToggleGroup]
			public bool boundaryShapes;
			[ToggleGroupItem(withLabel = true)]
			public LayerMask boundaryShapesLayerMask;
			[NonReorderable]
			public HairSimBoundary[] boundaryShapesResident;

			public static readonly VolumeSettings defaults = new VolumeSettings()
			{
				volumeSplatMethod = SplatMethod.Compute,
				volumeSplatDebug = false,
				volumeSplatDebugWidth = 1.0f,

				volumeGridResolution = 32,
				volumeGridStaggered = false,
				volumeGridSquare = true,

				pressureIterations = 3,
				pressureSolution = PressureSolution.DensityEquals,
				targetDensity = TargetDensity.Uniform,
				targetDensityInfluence = 1.0f,

				boundaryShapes = true,
				boundaryShapesLayerMask = Physics.AllLayers,
				boundaryShapesResident = new HairSimBoundary[0],
				boundarySDF = false,
				boundarySDFTexture = null,
			};
		}

		[Serializable]
		public struct DebugSettings
		{
			public bool drawStrandRoots;
			public bool drawStrandParticles;
			public bool drawCellDensity;
			public bool drawCellGradient;

			[LineHeader("Volume Visualization")]

			[ToggleGroup]
			public bool drawIsosurface;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Trace threshold")]
			public float drawIsosurfaceDensity;
			[ToggleGroupItem(withLabel = true), Range(1, 10), Tooltip("Substeps within each cell")]
			public int drawIsosurfaceSubsteps;

			[ToggleGroup]
			public bool drawSliceX;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Position of slice along X")]
			public float drawSliceXOffset;
			[ToggleGroup]
			public bool drawSliceY;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Position of slice along Y")]
			public float drawSliceYOffset;
			[ToggleGroup]
			public bool drawSliceZ;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Position of slice along Z")]
			public float drawSliceZOffset;
			[Range(0.0f, 5.0f), Tooltip("Scrubs between different layers")]
			public float drawSliceDivider;

			public static readonly DebugSettings defaults = new DebugSettings()
			{
				drawStrandRoots = false,
				drawStrandParticles = false,
				drawCellDensity = false,
				drawCellGradient = false,

				drawIsosurface = false,
				drawIsosurfaceDensity = 0.5f,
				drawIsosurfaceSubsteps = 4,

				drawSliceX = false,
				drawSliceXOffset = 0.5f,
				drawSliceY = false,
				drawSliceYOffset = 0.5f,
				drawSliceZ = false,
				drawSliceZOffset = 0.5f,
				drawSliceDivider = 0.0f,
			};
		}

		public const int PARTICLE_GROUP_SIZE = 64;

		public const int MAX_BOUNDARIES = 8;
		public const int MAX_STRAND_COUNT = 64000;
		public const int MAX_STRAND_PARTICLE_COUNT = 128;

		static HairSim()
		{
			if (s_initialized == false)
			{
				var resources = HairSimResources.Load();
				{
					s_solverCS = resources.computeSolver;
					s_volumeCS = resources.computeVolume;

					s_solverRootsMat = new Material(resources.computeRoots);
					s_solverRootsMat.hideFlags = HideFlags.HideAndDontSave;
					s_solverRootsMPB = new MaterialPropertyBlock();

					s_volumeRasterMat = new Material(resources.computeVolumeRaster);
					s_volumeRasterMat.hideFlags = HideFlags.HideAndDontSave;
					s_volumeRasterMPB = new MaterialPropertyBlock();

					s_debugDrawCube = resources.debugDrawCube;
					s_debugDrawMat = new Material(resources.debugDraw);
					s_debugDrawMat.hideFlags = HideFlags.HideAndDontSave;
					s_debugDrawMPB = new MaterialPropertyBlock();
				}

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

		public static bool PrepareSolverData(ref SolverData solverData, int strandCount, int strandParticleCount)
		{
			unsafe
			{
				bool changed = false;

				int particleCount = strandCount * strandParticleCount;
				int particleStride = sizeof(Vector4);

				changed |= CreateBuffer(ref solverData.rootScale, "RootScale", strandCount, sizeof(float));
				changed |= CreateBuffer(ref solverData.rootPosition, "RootPosition", strandCount, particleStride);
				changed |= CreateBuffer(ref solverData.rootDirection, "RootDirection", strandCount, particleStride);
				changed |= CreateBuffer(ref solverData.rootFrame, "RootFrame", strandCount, particleStride);

				changed |= CreateBuffer(ref solverData.initialRootFrame, "InitialRootFrame", strandCount, particleStride);
				changed |= CreateBuffer(ref solverData.initialParticleOffset, "InitialParticleOffset", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.initialParticleFrameDelta, "InitialParticleFrameDelta", particleCount, particleStride);

				changed |= CreateBuffer(ref solverData.particlePosition, "ParticlePosition_0", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particlePositionPrev, "ParticlePosition_1", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particlePositionCorr, "ParticlePositionCorr", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particleVelocity, "ParticleVelocity_0", particleCount, particleStride);
				changed |= CreateBuffer(ref solverData.particleVelocityPrev, "ParticleVelocity_1", particleCount, particleStride);

				return changed;
			}
		}

		public static bool PrepareVolumeData(ref VolumeData volumeData, int volumeCellCount, bool halfPrecision)
		{
			unsafe
			{
				bool changed = false;

				changed |= CreateVolume(ref volumeData.accuWeight, "AccuWeight", volumeCellCount, GraphicsFormat.R32_SInt);//TODO switch to R16_SInt ?
				changed |= CreateVolume(ref volumeData.accuWeight0, "AccuWeight0", volumeCellCount, GraphicsFormat.R32_SInt);
				changed |= CreateVolume(ref volumeData.accuVelocityX, "AccuVelocityX", volumeCellCount, GraphicsFormat.R32_SInt);
				changed |= CreateVolume(ref volumeData.accuVelocityY, "AccuVelocityY", volumeCellCount, GraphicsFormat.R32_SInt);
				changed |= CreateVolume(ref volumeData.accuVelocityZ, "AccuVelocityZ", volumeCellCount, GraphicsFormat.R32_SInt);

				var fmtFloatR = halfPrecision ? RenderTextureFormat.RHalf : RenderTextureFormat.RFloat;
				var fmtFloatRGBA = halfPrecision ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;
				{
					changed |= CreateVolume(ref volumeData.volumeDensity, "VolumeDensity", volumeCellCount, fmtFloatR);
					changed |= CreateVolume(ref volumeData.volumeDensity0, "VolumeDensity0", volumeCellCount, fmtFloatR);
					changed |= CreateVolume(ref volumeData.volumeVelocity, "VolumeVelocity", volumeCellCount, fmtFloatRGBA);
					changed |= CreateVolume(ref volumeData.volumeDivergence, "VolumeDivergence", volumeCellCount, fmtFloatR);

					changed |= CreateVolume(ref volumeData.volumePressure, "VolumePressure_0", volumeCellCount, fmtFloatR);
					changed |= CreateVolume(ref volumeData.volumePressureNext, "VolumePressure_1", volumeCellCount, fmtFloatR);
					changed |= CreateVolume(ref volumeData.volumePressureGrad, "VolumePressureGrad", volumeCellCount, fmtFloatRGBA);
				}

				changed |= CreateBuffer(ref volumeData.boundaryPack, "BoundaryPack", MAX_BOUNDARIES, sizeof(HairSimBoundary.BoundaryPack));
				changed |= CreateBuffer(ref volumeData.boundaryMatrix, "BoundaryMatrix", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeData.boundaryMatrixInv, "BoundaryMatrixInv", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeData.boundaryMatrixW2PrevW, "BoundaryMatrixW2PrevW", MAX_BOUNDARIES, sizeof(Matrix4x4));

				return changed;
			}
		}

		public static void ReleaseSolverData(ref SolverData solverData)
		{
			ReleaseBuffer(ref solverData.rootScale);
			ReleaseBuffer(ref solverData.rootPosition);
			ReleaseBuffer(ref solverData.rootDirection);
			ReleaseBuffer(ref solverData.rootFrame);

			ReleaseBuffer(ref solverData.initialRootFrame);
			ReleaseBuffer(ref solverData.initialParticleOffset);
			ReleaseBuffer(ref solverData.initialParticleFrameDelta);

			ReleaseBuffer(ref solverData.particlePosition);
			ReleaseBuffer(ref solverData.particlePositionPrev);
			ReleaseBuffer(ref solverData.particlePositionCorr);
			ReleaseBuffer(ref solverData.particleVelocity);
			ReleaseBuffer(ref solverData.particleVelocityPrev);

			solverData = new SolverData();
		}

		public static void ReleaseVolumeData(ref VolumeData volumeData)
		{
			ReleaseVolume(ref volumeData.accuWeight);
			ReleaseVolume(ref volumeData.accuWeight0);
			ReleaseVolume(ref volumeData.accuVelocityX);
			ReleaseVolume(ref volumeData.accuVelocityY);
			ReleaseVolume(ref volumeData.accuVelocityZ);

			ReleaseVolume(ref volumeData.volumeDensity);
			ReleaseVolume(ref volumeData.volumeDensity0);
			ReleaseVolume(ref volumeData.volumeVelocity);

			ReleaseVolume(ref volumeData.volumeDivergence);
			ReleaseVolume(ref volumeData.volumePressure);
			ReleaseVolume(ref volumeData.volumePressureNext);
			ReleaseVolume(ref volumeData.volumePressureGrad);

			ReleaseBuffer(ref volumeData.boundaryPack);
			ReleaseBuffer(ref volumeData.boundaryMatrix);
			ReleaseBuffer(ref volumeData.boundaryMatrixInv);
			ReleaseBuffer(ref volumeData.boundaryMatrixW2PrevW);

			if (volumeData.boundaryPrev.IsCreated)
				volumeData.boundaryPrev.Dispose();

			volumeData = new VolumeData();
		}

		public static void UpdateSolverRoots(CommandBuffer cmd, Mesh rootMesh, in Matrix4x4 rootTransform, in SolverData solverData)
		{
			var solverDataCopy = solverData;
			{
				solverDataCopy.cbuffer._LocalToWorld = rootTransform;
				solverDataCopy.cbuffer._LocalToWorldInvT = rootTransform.inverse.transpose;
			}

			PushSolverData(cmd, s_solverRootsMat, s_solverRootsMPB, solverDataCopy);

			cmd.SetRandomWriteTarget(1, solverData.rootPosition);
			cmd.SetRandomWriteTarget(2, solverData.rootDirection);
			cmd.SetRandomWriteTarget(3, solverData.rootFrame);
			cmd.DrawMesh(rootMesh, Matrix4x4.identity, s_solverRootsMat, 0, 0, s_solverRootsMPB);
			cmd.ClearRandomWriteTargets();
		}

		public static void UpdateSolverData(ref SolverData solverData, in SolverSettings solverSettings, in Matrix4x4 strandTransform, float strandScale, float dt)
		{
			ref var cbuffer = ref solverData.cbuffer;
			ref var keywords = ref solverData.keywords;

			// update strand parameters
			cbuffer._LocalToWorld = strandTransform;
			cbuffer._LocalToWorldInvT = strandTransform.inverse.transpose;
			cbuffer._WorldRotation = new Vector4(strandTransform.rotation.x, strandTransform.rotation.y, strandTransform.rotation.z, strandTransform.rotation.w);

			cbuffer._StrandScale = strandScale;

			// update solver parameters
			float GetSeconds(SolverSettings.TimeSpan period)
			{
				switch (period)
				{
					default:
					case SolverSettings.TimeSpan.PerSecond: return 1.0f;
					case SolverSettings.TimeSpan.Per100ms: return 0.1f;
					case SolverSettings.TimeSpan.Per10ms: return 0.01f;
					case SolverSettings.TimeSpan.Per1ms: return 0.001f;
				}
			}

			cbuffer._DT = dt;
			cbuffer._Iterations = (uint)solverSettings.iterations;
			cbuffer._Stiffness = solverSettings.stiffness;
			cbuffer._SOR = (solverSettings.iterations > 1) ? solverSettings.kSOR : 1.0f;

			cbuffer._CellPressure = solverSettings.cellPressure;
			cbuffer._CellVelocity = solverSettings.cellVelocity;
			cbuffer._Damping = solverSettings.damping;
			cbuffer._DampingPeriod = GetSeconds(solverSettings.dampingPeriod);
			cbuffer._Gravity = solverSettings.gravity * -Vector3.Magnitude(Physics.gravity);

			cbuffer._FTLDamping = solverSettings.distanceFTLDamping;
			cbuffer._BoundaryFriction = solverSettings.boundaryCollisionFriction;
			cbuffer._BendingCurvature = solverSettings.curvatureCompareValue * 0.5f;

			cbuffer._GlobalPosition = solverSettings.globalPositionInfluence;
			cbuffer._GlobalPositionPeriod = GetSeconds(solverSettings.globalPositionPeriod);
			cbuffer._GlobalRotation = solverSettings.globalRotationInfluence;
			cbuffer._GlobalFadeOffset = solverSettings.globalFade ? solverSettings.globalFadeOffset : 1e9f;
			cbuffer._GlobalFadeExtent = solverSettings.globalFade ? solverSettings.globalFadeExtent : 1e9f;
			cbuffer._LocalShape = solverSettings.localShapeInfluence;

			// update keywords
			keywords.LAYOUT_INTERLEAVED = (solverData.memoryLayout == HairAsset.MemoryLayout.Interleaved);
			keywords.ENABLE_DISTANCE = solverSettings.distance;
			keywords.ENABLE_DISTANCE_LRA = solverSettings.distanceLRA;
			keywords.ENABLE_DISTANCE_FTL = solverSettings.distanceFTL;
			keywords.ENABLE_BOUNDARY = (solverSettings.boundaryCollision && solverSettings.boundaryCollisionFriction == 0.0f);
			keywords.ENABLE_BOUNDARY_FRICTION = (solverSettings.boundaryCollision && solverSettings.boundaryCollisionFriction > 0.0f);
			keywords.ENABLE_CURVATURE_EQ = (solverSettings.curvature && solverSettings.curvatureCompare == SolverSettings.BendCompare.Equals);
			keywords.ENABLE_CURVATURE_GEQ = (solverSettings.curvature && solverSettings.curvatureCompare == SolverSettings.BendCompare.GreaterThan);
			keywords.ENABLE_CURVATURE_LEQ = (solverSettings.curvature && solverSettings.curvatureCompare == SolverSettings.BendCompare.LessThan);
			keywords.ENABLE_POSE_GLOBAL_POSITION = (solverSettings.globalPosition && solverSettings.globalPositionInfluence > 0.0f);
			keywords.ENABLE_POSE_GLOBAL_ROTATION = (solverSettings.globalRotation && solverSettings.globalRotationInfluence > 0.0f);
			keywords.ENABLE_POSE_LOCAL_ROTATION = (solverSettings.localShape && solverSettings.localShapeMode == SolverSettings.LocalShapeMode.LocalRotation && solverSettings.localShapeInfluence > 0.0f);
			keywords.ENABLE_POSE_LOCAL_BEND_TWIST = (solverSettings.localShape && solverSettings.localShapeMode == SolverSettings.LocalShapeMode.LocalBendTwist && solverSettings.localShapeInfluence > 0.0f);
		}

		public static void UpdateVolumeBoundaries(ref VolumeData volumeData, in VolumeSettings volumeSettings, in Bounds volumeBounds)
		{
			ref var cbuffer = ref volumeData.cbuffer;

			// gather distinct boundaries
			s_boundariesTmp.Clear();
			{
				foreach (var shape in volumeSettings.boundaryShapesResident)
				{
					if (shape != null)
					{
						if (s_boundariesTmp.Contains(shape) == false)
							s_boundariesTmp.Add(shape);
					}
				}

				if (volumeSettings.boundaryShapes)
				{
					var result = s_boundariesOverlapBuffer;
					var resultCount = Physics.OverlapBoxNonAlloc(volumeBounds.center, volumeBounds.extents, result, Quaternion.identity);

					for (int i = 0; i != resultCount; i++)
					{
						var shape = result[i].GetComponent<HairSimBoundary>();
						if (shape != null)
						{
							if (s_boundariesTmp.Contains(shape) == false)
								s_boundariesTmp.Add(shape);
						}
					}
				}

				s_boundariesTmp.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

				if (volumeSettings.boundarySDF)
				{
					//TODO
				}
			}

			// update boundary shapes
			cbuffer._BoundaryCapsuleCount = 0;
			cbuffer._BoundarySphereCount = 0;
			cbuffer._BoundaryTorusCount = 0;

			using (var tmpPack = new NativeArray<HairSimBoundary.BoundaryPack>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var tmpMatrix = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var tmpMatrixInv = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var tmpMatrixW2PrevW = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var tmpInfo = new NativeArray<VolumeData.BoundaryInfo>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				if (volumeData.boundaryPrev.IsCreated && volumeData.boundaryPrev.Length != MAX_BOUNDARIES)
					volumeData.boundaryPrev.Dispose();
				if (volumeData.boundaryPrev.IsCreated == false)
					volumeData.boundaryPrev = new NativeArray<VolumeData.BoundaryInfo>(MAX_BOUNDARIES, Allocator.Persistent, NativeArrayOptions.ClearMemory);

				unsafe
				{
					var ptrPack = (HairSimBoundary.BoundaryPack*)tmpPack.GetUnsafePtr();
					var ptrMatrix = (Matrix4x4*)tmpMatrix.GetUnsafePtr();
					var ptrMatrixInv = (Matrix4x4*)tmpMatrixInv.GetUnsafePtr();
					var ptrMatrixW2PrevW = (Matrix4x4*)tmpMatrixW2PrevW.GetUnsafePtr();
					var ptrInfo = (VolumeData.BoundaryInfo*)tmpInfo.GetUnsafePtr();
					var ptrInfoPrev = (VolumeData.BoundaryInfo*)volumeData.boundaryPrev.GetUnsafePtr();

					// count boundary shapes
					int boundaryCount = 0;

					foreach (HairSimBoundary boundary in s_boundariesTmp)
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

						if ((boundaryCount++) == MAX_BOUNDARIES)
							break;
					}

					// pack boundary shapes
					int boundaryCapsuleIndex = 0;
					int boundarySphereIndex = boundaryCapsuleIndex + cbuffer._BoundaryCapsuleCount;
					int boundaryTorusIndex = boundarySphereIndex + cbuffer._BoundarySphereCount;
					int boundaryCountPack = 0;

					foreach (HairSimBoundary boundary in s_boundariesTmp)
					{
						if (boundary == null || !boundary.isActiveAndEnabled)
							continue;

						switch (boundary.type)
						{
							case HairSimBoundary.Type.Capsule:
								ptrInfo[boundaryCapsuleIndex] = new VolumeData.BoundaryInfo{ instanceID = boundary.transform.GetInstanceID(), matrix = boundary.transform.localToWorldMatrix };
								ptrPack[boundaryCapsuleIndex++] = HairSimBoundary.Pack(boundary.GetCapsule());
								break;
							case HairSimBoundary.Type.Sphere:
								ptrInfo[boundarySphereIndex] = new VolumeData.BoundaryInfo { instanceID = boundary.transform.GetInstanceID(), matrix = boundary.transform.localToWorldMatrix };
								ptrPack[boundarySphereIndex++] = HairSimBoundary.Pack(boundary.GetSphere());
								break;
							case HairSimBoundary.Type.Torus:
								ptrInfo[boundaryTorusIndex] = new VolumeData.BoundaryInfo { instanceID = boundary.transform.GetInstanceID(), matrix = boundary.transform.localToWorldMatrix };
								ptrPack[boundaryTorusIndex++] = HairSimBoundary.Pack(boundary.GetTorus());
								break;
						}

						if ((boundaryCountPack++) == MAX_BOUNDARIES)
							break;
					}

					// update matrices
					for (int i = 0; i != boundaryCount; i++)
					{
						int ptrInfoPrevIndex = -1;

						for (int j = 0; j != volumeData.boundaryPrevCount; j++)
						{
							if (ptrInfoPrev[j].instanceID == ptrInfo[i].instanceID)
							{
								ptrInfoPrevIndex = j;
								break;
							}
						}

						ptrMatrix[i] = ptrInfo[i].matrix;
						ptrMatrixInv[i] = Matrix4x4.Inverse(ptrMatrix[i]);

						// world to previous world
						if (ptrInfoPrevIndex != -1)
							ptrMatrixW2PrevW[i] = ptrInfoPrev[ptrInfoPrevIndex].matrix * ptrMatrixInv[i];
						else
							ptrMatrixW2PrevW[i] = Matrix4x4.identity;
					}

					volumeData.boundaryPack.SetData(tmpPack, 0, 0, boundaryCount);
					volumeData.boundaryMatrix.SetData(tmpMatrix, 0, 0, boundaryCount);
					volumeData.boundaryMatrixInv.SetData(tmpMatrixInv, 0, 0, boundaryCount);
					volumeData.boundaryMatrixW2PrevW.SetData(tmpMatrixW2PrevW, 0, 0, boundaryCount);

					volumeData.boundaryPrev.CopyFrom(tmpInfo);
					volumeData.boundaryPrevCount = boundaryCount;
					volumeData.boundaryPrevCountDiscarded = s_boundariesTmp.Count - boundaryCount;
				}
			}
		}

		public static void UpdateVolumeData(ref VolumeData volumeData, in VolumeSettings volumeSettings, in Bounds volumeBounds, float strandDiameter, float strandScale)
		{
			ref var cbuffer = ref volumeData.cbuffer;
			ref var keywords = ref volumeData.keywords;

			// update grid parameters
			cbuffer._VolumeCells = volumeSettings.volumeGridResolution * Vector3.one;
			cbuffer._VolumeWorldMin = volumeBounds.min;
			cbuffer._VolumeWorldMax = volumeBounds.max;

			// update resolve parameters
			float resolveCrossSection = 0.25f * Mathf.PI * strandDiameter * strandDiameter;
			float resolveUnitVolume = (1000.0f * volumeData.allGroupsMaxParticleInterval) * resolveCrossSection;

			cbuffer._ResolveUnitVolume = resolveUnitVolume * (strandScale * strandScale * strandScale);
			cbuffer._ResolveUnitDebugWidth = volumeSettings.volumeSplatDebugWidth;

			// update pressure parameters
			cbuffer._TargetDensityFactor = volumeSettings.targetDensityInfluence;

			// update keywords
			keywords.VOLUME_SUPPORT_CONTRACTION = (volumeSettings.pressureSolution == VolumeSettings.PressureSolution.DensityEquals);
			keywords.VOLUME_TARGET_INITIAL_POSE = (volumeSettings.targetDensity == VolumeSettings.TargetDensity.InitialPose);
			keywords.VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES = (volumeSettings.targetDensity == VolumeSettings.TargetDensity.InitialPoseInParticles);
		}

		public static void PushSolverData<T>(CommandBuffer cmd, T target, in SolverData solverData) where T : IPushTarget
		{
			target.PushConstantBuffer(cmd, UniformIDs.SolverCBuffer, solverData.cbuffer);

			target.PushComputeBuffer(cmd, UniformIDs._RootScale, solverData.rootScale);
			target.PushComputeBuffer(cmd, UniformIDs._RootPosition, solverData.rootPosition);
			target.PushComputeBuffer(cmd, UniformIDs._RootDirection, solverData.rootDirection);

			target.PushComputeBuffer(cmd, UniformIDs._InitialRootFrame, solverData.initialRootFrame);
			target.PushComputeBuffer(cmd, UniformIDs._InitialParticleOffset, solverData.initialParticleOffset);
			target.PushComputeBuffer(cmd, UniformIDs._InitialParticleFrameDelta, solverData.initialParticleFrameDelta);

			target.PushComputeBuffer(cmd, UniformIDs._ParticlePosition, solverData.particlePosition);
			target.PushComputeBuffer(cmd, UniformIDs._ParticlePositionPrev, solverData.particlePositionPrev);
			target.PushComputeBuffer(cmd, UniformIDs._ParticlePositionCorr, solverData.particlePositionCorr);
			target.PushComputeBuffer(cmd, UniformIDs._ParticleVelocity, solverData.particleVelocity);
			target.PushComputeBuffer(cmd, UniformIDs._ParticleVelocityPrev, solverData.particleVelocityPrev);
	
			target.PushKeyword(cmd, "LAYOUT_INTERLEAVED", solverData.keywords.LAYOUT_INTERLEAVED);
			target.PushKeyword(cmd, "ENABLE_DISTANCE", solverData.keywords.ENABLE_DISTANCE);
			target.PushKeyword(cmd, "ENABLE_DISTANCE_LRA", solverData.keywords.ENABLE_DISTANCE_LRA);
			target.PushKeyword(cmd, "ENABLE_DISTANCE_FTL", solverData.keywords.ENABLE_DISTANCE_FTL);
			target.PushKeyword(cmd, "ENABLE_BOUNDARY", solverData.keywords.ENABLE_BOUNDARY);
			target.PushKeyword(cmd, "ENABLE_BOUNDARY_FRICTION", solverData.keywords.ENABLE_BOUNDARY_FRICTION);
			target.PushKeyword(cmd, "ENABLE_CURVATURE_EQ", solverData.keywords.ENABLE_CURVATURE_EQ);
			target.PushKeyword(cmd, "ENABLE_CURVATURE_GEQ", solverData.keywords.ENABLE_CURVATURE_GEQ);
			target.PushKeyword(cmd, "ENABLE_CURVATURE_LEQ", solverData.keywords.ENABLE_CURVATURE_LEQ);
			target.PushKeyword(cmd, "ENABLE_POSE_GLOBAL_POSITION", solverData.keywords.ENABLE_POSE_GLOBAL_POSITION);
			target.PushKeyword(cmd, "ENABLE_POSE_GLOBAL_ROTATION", solverData.keywords.ENABLE_POSE_GLOBAL_ROTATION);
			target.PushKeyword(cmd, "ENABLE_POSE_LOCAL_ROTATION", solverData.keywords.ENABLE_POSE_LOCAL_ROTATION);
			target.PushKeyword(cmd, "ENABLE_POSE_LOCAL_BEND_TWIST", solverData.keywords.ENABLE_POSE_LOCAL_BEND_TWIST);
		}

		public static void PushSolverData(CommandBuffer cmd, ComputeShader cs, int kernel, in SolverData solverData) => PushSolverData(cmd, new PushTargetCompute(cs, kernel), solverData);
		public static void PushSolverData(CommandBuffer cmd, Material mat, MaterialPropertyBlock mpb, in SolverData solverData) => PushSolverData(cmd, new PushTargetMaterial(mat, mpb), solverData);

		public static void PushVolumeData<T>(CommandBuffer cmd, T target, in VolumeData volumeData) where T : IPushTarget
		{
			target.PushConstantBuffer(cmd, UniformIDs.VolumeCBuffer, volumeData.cbuffer);

			target.PushComputeBuffer(cmd, UniformIDs._BoundaryPack, volumeData.boundaryPack);
			target.PushComputeBuffer(cmd, UniformIDs._BoundaryMatrix, volumeData.boundaryMatrix);
			target.PushComputeBuffer(cmd, UniformIDs._BoundaryMatrixInv, volumeData.boundaryMatrixInv);
			target.PushComputeBuffer(cmd, UniformIDs._BoundaryMatrixW2PrevW, volumeData.boundaryMatrixW2PrevW);

			target.PushComputeTexture(cmd, UniformIDs._AccuWeight, volumeData.accuWeight);
			target.PushComputeTexture(cmd, UniformIDs._AccuWeight0, volumeData.accuWeight0);
			target.PushComputeTexture(cmd, UniformIDs._AccuVelocityX, volumeData.accuVelocityX);
			target.PushComputeTexture(cmd, UniformIDs._AccuVelocityY, volumeData.accuVelocityY);
			target.PushComputeTexture(cmd, UniformIDs._AccuVelocityZ, volumeData.accuVelocityZ);

			target.PushComputeTexture(cmd, UniformIDs._VolumeDensity, volumeData.volumeDensity);
			target.PushComputeTexture(cmd, UniformIDs._VolumeDensity0, volumeData.volumeDensity0);
			target.PushComputeTexture(cmd, UniformIDs._VolumeVelocity, volumeData.volumeVelocity);
			target.PushComputeTexture(cmd, UniformIDs._VolumeDivergence, volumeData.volumeDivergence);

			target.PushComputeTexture(cmd, UniformIDs._VolumePressure, volumeData.volumePressure);
			target.PushComputeTexture(cmd, UniformIDs._VolumePressureNext, volumeData.volumePressureNext);
			target.PushComputeTexture(cmd, UniformIDs._VolumePressureGrad, volumeData.volumePressureGrad);

			target.PushKeyword(cmd, "VOLUME_SUPPORT_CONTRACTION", volumeData.keywords.VOLUME_SUPPORT_CONTRACTION);
			target.PushKeyword(cmd, "VOLUME_TARGET_INITIAL_POSE", volumeData.keywords.VOLUME_TARGET_INITIAL_POSE);
			target.PushKeyword(cmd, "VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES", volumeData.keywords.VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES);
		}

		public static void PushVolumeData(CommandBuffer cmd, ComputeShader cs, int kernel, in VolumeData volumeData) => PushVolumeData(cmd, new PushTargetCompute(cs, kernel), volumeData);
		public static void PushVolumeData(CommandBuffer cmd, Material mat, MaterialPropertyBlock mpb, in VolumeData volumeData) => PushVolumeData(cmd, new PushTargetMaterial(mat, mpb), volumeData);

		public static void InitSolverParticles(CommandBuffer cmd, in SolverData solverData, Matrix4x4 rootTransform)
		{
			var solverDataCopy = solverData;
			{
				solverDataCopy.cbuffer._LocalToWorld = rootTransform;
				solverDataCopy.cbuffer._LocalToWorldInvT = rootTransform.inverse.transpose;
			}

			int numX = (int)solverData.cbuffer._StrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)solverData.cbuffer._StrandCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			PushSolverData(cmd, s_solverCS, SolverKernels.KInitParticles, solverDataCopy);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInitParticles, numX, numY, numZ);
		}

		public static void InitSolverParticlesPostVolume(CommandBuffer cmd, in SolverData solverData, in VolumeData volumeData)
		{
			int numX = (int)solverData.cbuffer._StrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)solverData.cbuffer._StrandCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			PushVolumeData(cmd, s_solverCS, SolverKernels.KInitParticlesPostVolume, volumeData);
			PushSolverData(cmd, s_solverCS, SolverKernels.KInitParticlesPostVolume, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInitParticlesPostVolume, numX, numY, numZ);
		}

		public static void StepSolverData(CommandBuffer cmd, ref SolverData solverData, in SolverSettings solverSettings, in VolumeData volumeData)
		{
			using (new ProfilingScope(cmd, MarkersGPU.Solver))
			{
				int kernel = SolverKernels.KSolveConstraints_GaussSeidelReference;
				int numX = (int)solverData.cbuffer._StrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)solverData.cbuffer._StrandCount % PARTICLE_GROUP_SIZE);
				int numY = 1;
				int numZ = 1;

				switch (solverSettings.method)
				{
					case SolverSettings.Method.GaussSeidelReference:
						kernel = SolverKernels.KSolveConstraints_GaussSeidelReference;
						break;

					case SolverSettings.Method.GaussSeidel:
						kernel = SolverKernels.KSolveConstraints_GaussSeidel;
						break;

					case SolverSettings.Method.Jacobi:
						switch (solverData.cbuffer._StrandParticleCount)
						{
							case 16:
								kernel = SolverKernels.KSolveConstraints_Jacobi_16;
								numX = (int)solverData.cbuffer._StrandCount;
								break;

							case 32:
								kernel = SolverKernels.KSolveConstraints_Jacobi_32;
								numX = (int)solverData.cbuffer._StrandCount;
								break;

							case 64:
								kernel = SolverKernels.KSolveConstraints_Jacobi_64;
								numX = (int)solverData.cbuffer._StrandCount;
								break;

							case 128:
								kernel = SolverKernels.KSolveConstraints_Jacobi_128;
								numX = (int)solverData.cbuffer._StrandCount;
								break;
						}
						break;
				}

				CoreUtils.Swap(ref solverData.particlePosition, ref solverData.particlePositionPrev);
				CoreUtils.Swap(ref solverData.particleVelocity, ref solverData.particleVelocityPrev);

				PushVolumeData(cmd, s_solverCS, kernel, volumeData);
				PushSolverData(cmd, s_solverCS, kernel, solverData);
				cmd.DispatchCompute(s_solverCS, kernel, numX, numY, numZ);
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
			int numX = volumeSettings.volumeGridResolution / 8;
			int numY = volumeSettings.volumeGridResolution / 8;
			int numZ = volumeSettings.volumeGridResolution;

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

			int numX = particleCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, particleCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			// accumulate
			using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat))
			{
				switch (volumeSettings.volumeSplatMethod)
				{
					case VolumeSettings.SplatMethod.Compute:
						{
							PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, volumeData);
							PushSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplat, numX, numY, numZ);
						}
						break;

					case VolumeSettings.SplatMethod.ComputeSplit:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_Density))
							{
								PushVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, volumeData);
								PushSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatDensity, numX, numY, numZ);
							}

							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_VelocityXYZ))
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
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_Rasterization))
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
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_RasterizationNoGS))
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
		}

		private static void StepVolumeData_Resolve(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings)
		{
			int numX = volumeSettings.volumeGridResolution / 8;
			int numY = volumeSettings.volumeGridResolution / 8;
			int numZ = volumeSettings.volumeGridResolution;

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
				for (int i = 0; i != volumeSettings.pressureIterations; i++)
				{
					cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureSolve, numX, numY, numZ);

					CoreUtils.Swap(ref volumeData.volumePressure, ref volumeData.volumePressureNext);
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

		public static void DrawSolverData(CommandBuffer cmd, RenderTargetIdentifier color, RenderTargetIdentifier depth, in SolverData solverData, in DebugSettings debugSettings)
		{
			using (new ProfilingScope(cmd, MarkersGPU.DrawSolverData))
			{
				if (!debugSettings.drawStrandRoots &&
					!debugSettings.drawStrandParticles)
					return;

				PushSolverData(cmd, s_debugDrawMat, s_debugDrawMPB, solverData);

				CoreUtils.SetRenderTarget(cmd, color, depth);

				// strand roots
				if (debugSettings.drawStrandRoots)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 0, MeshTopology.Lines, 2, (int)solverData.cbuffer._StrandCount, s_debugDrawMPB);
				}

				// strand particles
				if (debugSettings.drawStrandParticles)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 0, MeshTopology.Points, (int)solverData.cbuffer._StrandParticleCount, (int)solverData.cbuffer._StrandCount, s_debugDrawMPB);
				}
			}
		}

		public static void DrawVolumeData(CommandBuffer cmd, RenderTargetIdentifier color, RenderTargetIdentifier depth, in VolumeData volumeData, in DebugSettings debugSettings)
		{
			using (new ProfilingScope(cmd, MarkersGPU.DrawVolumeData))
			{
				if (!debugSettings.drawCellDensity &&
					!debugSettings.drawCellGradient &&
					!debugSettings.drawSliceX &&
					!debugSettings.drawSliceY &&
					!debugSettings.drawSliceZ &&
					!debugSettings.drawIsosurface)
					return;

				CoreUtils.SetRenderTarget(cmd, color, depth);

				PushVolumeData(cmd, s_debugDrawMat, s_debugDrawMPB, volumeData);

				// cell density
				if (debugSettings.drawCellDensity)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 2, MeshTopology.Points, GetCellCount(volumeData), 1, s_debugDrawMPB);
				}

				// cell gradient
				if (debugSettings.drawCellGradient)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 3, MeshTopology.Lines, 2 * GetCellCount(volumeData), 1, s_debugDrawMPB);
				}

				// volume slices
				if (debugSettings.drawSliceX || debugSettings.drawSliceY || debugSettings.drawSliceZ)
				{
					s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceDivider, debugSettings.drawSliceDivider);

					if (debugSettings.drawSliceX)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 0);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceXOffset);

						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 4, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 5, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
					if (debugSettings.drawSliceY)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 1);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceYOffset);

						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 4, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 5, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
					if (debugSettings.drawSliceZ)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 2);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceZOffset);

						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 4, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, 5, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
				}

				// volume isosurface
				if (debugSettings.drawIsosurface)
				{
					var worldMin = volumeData.cbuffer._VolumeWorldMin;
					var worldMax = volumeData.cbuffer._VolumeWorldMax;
					var worldCenter = 0.5f * (worldMin + worldMax);
					var worldExtent = 0.5f * (worldMax - worldMin);

					s_debugDrawMPB.SetFloat(UniformIDs._DebugIsosurfaceDensity, debugSettings.drawIsosurfaceDensity);
					s_debugDrawMPB.SetInt(UniformIDs._DebugIsosurfaceSubsteps, debugSettings.drawIsosurfaceSubsteps);

					cmd.DrawMesh(s_debugDrawCube, Matrix4x4.TRS(worldCenter, Quaternion.identity, 2.0f * worldExtent), s_debugDrawMat, 0, 6, s_debugDrawMPB);
				}
			}
		}

		//TODO move elsewhere
		//maybe 'HairSimDataUtility' ?

		public static Vector3 GetVolumeCellSize(in VolumeData volumeData)
		{
			return new Vector3(
				(volumeData.cbuffer._VolumeWorldMax.x - volumeData.cbuffer._VolumeWorldMin.x) / volumeData.cbuffer._VolumeCells.x,
				(volumeData.cbuffer._VolumeWorldMax.y - volumeData.cbuffer._VolumeWorldMin.y) / volumeData.cbuffer._VolumeCells.y,
				(volumeData.cbuffer._VolumeWorldMax.z - volumeData.cbuffer._VolumeWorldMin.z) / volumeData.cbuffer._VolumeCells.z
			);
		}

		public static float GetVolumeCellVolume(in VolumeData volumeData)
		{
			var cellSize = GetVolumeCellSize(volumeData);
			return cellSize.x * cellSize.y * cellSize.z;
		}

		public static int GetCellCount(in VolumeData volumeData)
		{
			var nx = (int)volumeData.cbuffer._VolumeCells.x;
			var ny = (int)volumeData.cbuffer._VolumeCells.y;
			var nz = (int)volumeData.cbuffer._VolumeCells.z;
			return nx * ny * nz;
		}

		public static Vector3 GetVolumeCenter(in VolumeData volumeData)
		{
			return (volumeData.cbuffer._VolumeWorldMax + volumeData.cbuffer._VolumeWorldMin) * 0.5f;
		}

		public static Vector3 GetVolumeExtent(in VolumeData volumeData)
		{
			return (volumeData.cbuffer._VolumeWorldMax - volumeData.cbuffer._VolumeWorldMin) * 0.5f;
		}
	}
}