#pragma warning disable 0649 // some fields are assigned via reflection

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

namespace Unity.DemoTeam.Hair
{
	using static HairSimUtility;

	public static partial class HairSim
	{
		static bool s_initialized = false;

		static ComputeShader s_solverCS;
		static ComputeShader s_volumeCS;

		static Material s_solverRootsMat;
		static Material s_volumeRasterMat;

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

			public static int _RootUV;
			public static int _RootScale;
			public static int _RootPosition;
			public static int _RootDirection;
			public static int _RootFrame;

			public static int _InitialRootFrame;
			public static int _InitialParticleOffset;
			public static int _InitialParticleFrameDelta;

			public static int _ParticlePosition;
			public static int _ParticlePositionPrev;
			public static int _ParticlePositionPrevPrev;
			public static int _ParticlePositionCorr;
			public static int _ParticleVelocity;
			public static int _ParticleVelocityPrev;

			public static int _LODGuideCount;
			public static int _LODGuideIndex;
			public static int _LODGuideCarry;

			public static int _StagingPosition;
			public static int _StagingPositionPrev;

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

			public static int _BoundarySDF;
			public static int _BoundaryShape;
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
			public static int KInitialize;
			public static int KInitializePostVolume;
			public static int KSolveConstraints_GaussSeidelReference;
			public static int KSolveConstraints_GaussSeidel;
			public static int KSolveConstraints_Jacobi_16;
			public static int KSolveConstraints_Jacobi_32;
			public static int KSolveConstraints_Jacobi_64;
			public static int KSolveConstraints_Jacobi_128;
			public static int KInterpolate;
			public static int KInterpolateNearest;
			public static int KStaging;
			public static int KStagingSubdivision;
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

			public enum TimeInterval
			{
				PerSecond,
				Per100ms,
				Per10ms,
				Per1ms,
			}

			public enum LocalCurvatureMode
			{
				Equals,
				LessThan,
				GreaterThan,
			}

			[LineHeader("Solver")]

			[Tooltip("Constraint solver")]
			public Method method;
			[Range(1, 100), Tooltip("Constraint iterations")]
			public int iterations;
			[Range(1, 12), Tooltip("Solver substeps")]
			public int substeps;
			[Range(0.0f, 1.0f), Tooltip("Constraint stiffness")]
			public float stiffness;
			[Range(1.0f, 2.0f), Tooltip("Successive-over-relaxation factor")]
			public float kSOR;

			[LineHeader("Integration")]

			[ToggleGroup, Tooltip("Enable linear damping")]
			public bool damping;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Linear damping factor (fraction of linear velocity to subtract per interval of time)")]
			public float dampingFactor;
			[ToggleGroupItem, Tooltip("Interval of time over which to subtract fraction of linear velocity")]
			public TimeInterval dampingInterval;

			[ToggleGroup, Tooltip("Enable angular damping")]
			public bool angularDamping;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Angular damping factor (fraction of angular velocity to subtract per interval of time)")]
			public float angularDampingFactor;
			[ToggleGroupItem, Tooltip("Interval of time over which to subtract fraction of angular velocity")]
			public TimeInterval angularDampingInterval;

			[Range(0.0f, 1.0f), Tooltip("Scaling factor for volume pressure impulse")]
			public float cellPressure;
			[Range(0.0f, 1.0f), Tooltip("Scaling factor for volume velocity impulse (where 0 == FLIP, 1 == PIC)")]
			public float cellVelocity;
			[Range(-1.0f, 1.0f), Tooltip("Scaling factor for gravity (Physics.gravity)")]
			public float gravity;

			[LineHeader("Constraints")]

			[ToggleGroup, Tooltip("Enable boundary collision constraint")]
			public bool boundaryCollision;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Boundary collision friction")]
			public float boundaryCollisionFriction;

			[Tooltip("Enable particle-particle distance constraint")]
			public bool distance;
			[Tooltip("Enable 'long range attachment' distance constraint (root-particle maximum distance)")]
			public bool distanceLRA;
			[ToggleGroup, Tooltip("Enable 'follow the leader' distance constraint (hard particle-particle distance, non-physical)")]
			public bool distanceFTL;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("FTL correction / damping factor")]
			public float distanceFTLDamping;

			[ToggleGroup, Tooltip("Enable bending curvature constraint")]
			public bool localCurvature;
			[ToggleGroupItem, Tooltip("Bending curvature constraint mode (=, <, >)")]
			public LocalCurvatureMode localCurvatureMode;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Scales to a bend of [0 .. 90] degrees")]
			public float localCurvatureValue;

			[ToggleGroup, Tooltip("Enable local shape constraint")]
			public bool localShape;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Local shape influence")]
			public float localShapeInfluence;

			[LineHeader("Global Pose")]

			[ToggleGroup, Tooltip("Enable global position constraint")]
			public bool globalPosition;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Fraction of global position to apply per interval of time")]
			public float globalPositionInfluence;
			[ToggleGroupItem, Tooltip("Interval of time over which to apply fraction of global position")]
			public TimeInterval globalPositionInterval;
			[ToggleGroup, Tooltip("Enable global rotation constraint")]
			public bool globalRotation;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Global rotation influence")]
			public float globalRotationInfluence;
			[ToggleGroup, Tooltip("Fade influence of global constraints from root to tip")]
			public bool globalFade;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Normalized fade offset (normalized distance from root)")]
			public float globalFadeOffset;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Normalized fade extent (normalized distance from specified offset)")]
			public float globalFadeExtent;

			public static readonly SolverSettings defaults = new SolverSettings()
			{
				method = Method.GaussSeidel,
				iterations = 5,
				substeps = 1,
				stiffness = 1.0f,
				kSOR = 1.0f,

				damping = false,
				dampingFactor = 0.5f,
				dampingInterval = TimeInterval.PerSecond,
				angularDamping = false,
				angularDampingFactor = 0.5f,
				angularDampingInterval = TimeInterval.PerSecond,
				cellPressure = 1.0f,
				cellVelocity = 0.05f,
				gravity = 1.0f,

				boundaryCollision = true,
				boundaryCollisionFriction = 0.5f,
				distance = true,
				distanceLRA = true,
				distanceFTL = false,
				distanceFTLDamping = 0.8f,
				localCurvature = false,
				localCurvatureMode = LocalCurvatureMode.LessThan,
				localCurvatureValue = 0.1f,
				localShape = true,
				localShapeInfluence = 1.0f,

				globalPosition = false,
				globalPositionInfluence = 1.0f,
				globalPositionInterval = TimeInterval.PerSecond,
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

			public enum GridPrecision
			{
				Full,
				Half,
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

			public enum CollectMode
			{
				JustTagged,
				IncludeColliders,
			}

			[LineHeader("Volume")]

			[UnityEngine.Serialization.FormerlySerializedAs("volumeSplatMethod")]
			public SplatMethod splatMethod;
			[ToggleGroup]
			public bool splatDebug;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 9.0f)]
			public float splatDebugWidth;

			[UnityEngine.Serialization.FormerlySerializedAs("volumeGridResolution"), Range(8, 160)]
			public int gridResolution;
			public GridPrecision gridPrecision;
			[HideInInspector, Tooltip("Increases precision of derivative quantities at the cost of volume splatting performance")]
			public bool gridStaggered;
			[HideInInspector]
			public bool gridSquare;

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

			[Range(0.0f, 10.0f), Tooltip("Collision margin (in centimeters)")]
			public float collisionMargin;
			[ToggleGroup, Tooltip("Collect boundaries from physics")]
			public bool boundariesCollect;
			[ToggleGroupItem, Tooltip("Collect just tagged boundaries, or also include untagged colliders")]
			public CollectMode boundariesCollectMode;
			[ToggleGroupItem(withLabel = true)]
			public LayerMask boundariesCollectLayer;
			[NonReorderable, Tooltip("Always-included boundaries (these take priority over boundaries collected from physics)")]
			public HairBoundary[] boundariesPriority;

			public static readonly VolumeSettings defaults = new VolumeSettings()
			{
				splatMethod = SplatMethod.Compute,
				splatDebug = false,
				splatDebugWidth = 1.0f,

				gridResolution = 32,
				gridPrecision = GridPrecision.Full,
				gridStaggered = false,
				gridSquare = true,

				pressureIterations = 3,
				pressureSolution = PressureSolution.DensityLessThan,
				targetDensity = TargetDensity.Uniform,
				targetDensityInfluence = 1.0f,

				collisionMargin = 0.25f,
				boundariesCollect = true,
				boundariesCollectMode = CollectMode.IncludeColliders,
				boundariesCollectLayer = Physics.AllLayers,
				boundariesPriority = new HairBoundary[0],
			};
		}

		[Serializable]
		public struct DebugSettings
		{
			[LineHeader("Strands")]

			public bool drawStrandRoots;
			public bool drawStrandParticles;
			public bool drawStrandVelocities;
			public bool drawStrandClusters;

			[LineHeader("Volume Cells")]

			public bool drawCellDensity;
			public bool drawCellGradient;
			[ToggleGroup]
			public bool drawIsosurface;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Trace threshold")]
			public float drawIsosurfaceDensity;
			[ToggleGroupItem(withLabel = true), Range(1, 10), Tooltip("Substeps within each cell")]
			public int drawIsosurfaceSubsteps;

			[LineHeader("Volume Slices")]

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
				drawStrandVelocities = false,
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

					s_volumeRasterMat = new Material(resources.computeVolumeRaster);
					s_volumeRasterMat.hideFlags = HideFlags.HideAndDontSave;

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

		public static bool PrepareSolverData(ref SolverData solverData, int strandCount, int strandParticleCount, int lodCount)
		{
			unsafe
			{
				bool changed = false;

				int particleCount = strandCount * strandParticleCount;

				int particleStrideIndex = sizeof(uint);
				int particleStrideScalar = sizeof(float);
				int particleStrideVector2 = sizeof(Vector2);
				int particleStrideVector4 = sizeof(Vector4);

				changed |= CreateBuffer(ref solverData.cbufferStorage, "SolverCBuffer", 1, sizeof(SolverCBuffer), ComputeBufferType.Constant);

				changed |= CreateBuffer(ref solverData.rootUV, "RootUV", strandCount, particleStrideVector2);
				changed |= CreateBuffer(ref solverData.rootScale, "RootScale", strandCount, particleStrideScalar);
				changed |= CreateBuffer(ref solverData.rootPosition, "RootPosition", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.rootDirection, "RootDirection", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.rootFrame, "RootFrame", strandCount, particleStrideVector4);

				changed |= CreateBuffer(ref solverData.initialRootFrame, "InitialRootFrame", strandCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.initialParticleOffset, "InitialParticleOffset", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.initialParticleFrameDelta, "InitialParticleFrameDelta", particleCount, particleStrideVector4);

				changed |= CreateBuffer(ref solverData.particlePosition, "ParticlePosition_0", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.particlePositionPrev, "ParticlePosition_1", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.particlePositionPrevPrev, "ParticlePosition_2", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.particlePositionCorr, "ParticlePositionCorr", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.particleVelocity, "ParticleVelocity_0", particleCount, particleStrideVector4);
				changed |= CreateBuffer(ref solverData.particleVelocityPrev, "ParticleVelocity_1", particleCount, particleStrideVector4);

				changed |= CreateBuffer(ref solverData.lodGuideCount, "LODGuideCount", Mathf.Max(1, lodCount), particleStrideIndex);
				changed |= CreateBuffer(ref solverData.lodGuideIndex, "LODGuideIndex", Mathf.Max(1, lodCount) * strandCount, particleStrideIndex);
				changed |= CreateBuffer(ref solverData.lodGuideCarry, "LODGuideCarry", Mathf.Max(1, lodCount) * strandCount, particleStrideScalar);

				return changed;
			}
		}

		public static bool PrepareSolverStaging(ref SolverData solverData, bool stagingCompression, uint stagingSubdivision)
		{
			unsafe
			{
				bool changed = false;

				var segmentCount = solverData.cbuffer._StrandParticleCount - 1;
				var segmentCountStaging = segmentCount * (1 + stagingSubdivision);

				var vertexCount = (int)(solverData.cbuffer._StrandCount * (segmentCountStaging + 1));
				var vertexStridePosition = stagingCompression ? sizeof(Vector2) : sizeof(Vector3);

				changed |= CreateBuffer(ref solverData.stagingPosition, "StagingPosition_0", vertexCount, vertexStridePosition);
				changed |= CreateBuffer(ref solverData.stagingPositionPrev, "StagingPosition_1", vertexCount, vertexStridePosition);

				changed |= (solverData.cbuffer._StagingVertexCount == 0);
				changed |= (solverData.cbuffer._StagingSubdivision != stagingSubdivision);

				return changed;
			}
		}

		public static bool PrepareVolumeData(ref VolumeData volumeData, in VolumeSettings volumeSettings)
		{
			unsafe
			{
				bool changed = false;

				var cellCount = volumeSettings.gridResolution;
				var cellPrecision = volumeSettings.gridPrecision;

				var cellFormatAccu = GraphicsFormat.R32_SInt;//TODO switch to R16_SInt ?
				var cellFormatScalar = (cellPrecision == VolumeSettings.GridPrecision.Half) ? RenderTextureFormat.RHalf : RenderTextureFormat.RFloat;
				var cellFormatVector = (cellPrecision == VolumeSettings.GridPrecision.Half) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;

				changed |= CreateBuffer(ref volumeData.cbufferStorage, "VolumeCBuffer", 1, sizeof(VolumeCBuffer), ComputeBufferType.Constant);

				changed |= CreateVolume(ref volumeData.accuWeight, "AccuWeight", cellCount, cellFormatAccu);//TODO switch to R16_SInt ?
				changed |= CreateVolume(ref volumeData.accuWeight0, "AccuWeight0", cellCount, cellFormatAccu);
				changed |= CreateVolume(ref volumeData.accuVelocityX, "AccuVelocityX", cellCount, cellFormatAccu);
				changed |= CreateVolume(ref volumeData.accuVelocityY, "AccuVelocityY", cellCount, cellFormatAccu);
				changed |= CreateVolume(ref volumeData.accuVelocityZ, "AccuVelocityZ", cellCount, cellFormatAccu);

				changed |= CreateVolume(ref volumeData.volumeDensity, "VolumeDensity", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeData.volumeDensity0, "VolumeDensity0", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeData.volumeVelocity, "VolumeVelocity", cellCount, cellFormatVector);

				changed |= CreateVolume(ref volumeData.volumeDivergence, "VolumeDivergence", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeData.volumePressure, "VolumePressure_0", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeData.volumePressureNext, "VolumePressure_1", cellCount, cellFormatScalar);
				changed |= CreateVolume(ref volumeData.volumePressureGrad, "VolumePressureGrad", cellCount, cellFormatVector);

				changed |= CreateVolume(ref volumeData.boundarySDF_undefined, "BoundarySDF_undefined", 1, RenderTextureFormat.RHalf);

				changed |= CreateBuffer(ref volumeData.boundaryShape, "BoundaryShape", MAX_BOUNDARIES, sizeof(HairBoundary.RuntimeShape.Data));
				changed |= CreateBuffer(ref volumeData.boundaryMatrix, "BoundaryMatrix", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeData.boundaryMatrixInv, "BoundaryMatrixInv", MAX_BOUNDARIES, sizeof(Matrix4x4));
				changed |= CreateBuffer(ref volumeData.boundaryMatrixW2PrevW, "BoundaryMatrixW2PrevW", MAX_BOUNDARIES, sizeof(Matrix4x4));

				return changed;
			}
		}

		public static void ReleaseSolverData(ref SolverData solverData)
		{
			ReleaseBuffer(ref solverData.cbufferStorage);

			ReleaseBuffer(ref solverData.rootUV);
			ReleaseBuffer(ref solverData.rootScale);
			ReleaseBuffer(ref solverData.rootPosition);
			ReleaseBuffer(ref solverData.rootDirection);
			ReleaseBuffer(ref solverData.rootFrame);

			ReleaseBuffer(ref solverData.initialRootFrame);
			ReleaseBuffer(ref solverData.initialParticleOffset);
			ReleaseBuffer(ref solverData.initialParticleFrameDelta);

			ReleaseBuffer(ref solverData.particlePosition);
			ReleaseBuffer(ref solverData.particlePositionPrev);
			ReleaseBuffer(ref solverData.particlePositionPrevPrev);
			ReleaseBuffer(ref solverData.particlePositionCorr);
			ReleaseBuffer(ref solverData.particleVelocity);
			ReleaseBuffer(ref solverData.particleVelocityPrev);

			ReleaseBuffer(ref solverData.lodGuideCount);
			ReleaseBuffer(ref solverData.lodGuideIndex);
			ReleaseBuffer(ref solverData.lodGuideCarry);

			if (solverData.lodGuideCountCPU.IsCreated)
				solverData.lodGuideCountCPU.Dispose();

			if (solverData.lodThreshold.IsCreated)
				solverData.lodThreshold.Dispose();

			ReleaseBuffer(ref solverData.stagingPosition);
			ReleaseBuffer(ref solverData.stagingPositionPrev);

			solverData = new SolverData();
		}

		public static void ReleaseVolumeData(ref VolumeData volumeData)
		{
			ReleaseBuffer(ref volumeData.cbufferStorage);

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

			ReleaseVolume(ref volumeData.boundarySDF_undefined);

			ReleaseBuffer(ref volumeData.boundaryShape);
			ReleaseBuffer(ref volumeData.boundaryMatrix);
			ReleaseBuffer(ref volumeData.boundaryMatrixInv);
			ReleaseBuffer(ref volumeData.boundaryMatrixW2PrevW);

			if (volumeData.boundaryPrevXform.IsCreated)
				volumeData.boundaryPrevXform.Dispose();

			volumeData = new VolumeData();
		}

		public static void BindSolverData(Material mat, in SolverData solverData) => BindSolverData(new BindTargetMaterial(mat), solverData);
		public static void BindSolverData(CommandBuffer cmd, in SolverData solverData) => BindSolverData(new BindTargetGlobalCmd(cmd), solverData);
		public static void BindSolverData(CommandBuffer cmd, ComputeShader cs, int kernel, in SolverData solverData) => BindSolverData(new BindTargetComputeCmd(cmd, cs, kernel), solverData);
		public static void BindSolverData<T>(T target, in SolverData solverData) where T : IBindTarget
		{
			target.BindConstantBuffer(UniformIDs.SolverCBuffer, solverData.cbufferStorage);

			target.BindComputeBuffer(UniformIDs._RootUV, solverData.rootUV);
			target.BindComputeBuffer(UniformIDs._RootScale, solverData.rootScale);
			target.BindComputeBuffer(UniformIDs._RootPosition, solverData.rootPosition);
			target.BindComputeBuffer(UniformIDs._RootDirection, solverData.rootDirection);
			target.BindComputeBuffer(UniformIDs._RootFrame, solverData.rootFrame);

			target.BindComputeBuffer(UniformIDs._InitialRootFrame, solverData.initialRootFrame);
			target.BindComputeBuffer(UniformIDs._InitialParticleOffset, solverData.initialParticleOffset);
			target.BindComputeBuffer(UniformIDs._InitialParticleFrameDelta, solverData.initialParticleFrameDelta);

			target.BindComputeBuffer(UniformIDs._ParticlePosition, solverData.particlePosition);
			target.BindComputeBuffer(UniformIDs._ParticlePositionPrev, solverData.particlePositionPrev);
			target.BindComputeBuffer(UniformIDs._ParticlePositionPrevPrev, solverData.particlePositionPrevPrev);
			target.BindComputeBuffer(UniformIDs._ParticlePositionCorr, solverData.particlePositionCorr);
			target.BindComputeBuffer(UniformIDs._ParticleVelocity, solverData.particleVelocity);
			target.BindComputeBuffer(UniformIDs._ParticleVelocityPrev, solverData.particleVelocityPrev);

			target.BindComputeBuffer(UniformIDs._LODGuideCount, solverData.lodGuideCount);
			target.BindComputeBuffer(UniformIDs._LODGuideIndex, solverData.lodGuideIndex);
			target.BindComputeBuffer(UniformIDs._LODGuideCarry, solverData.lodGuideCarry);

			target.BindComputeBuffer(UniformIDs._StagingPosition, solverData.stagingPosition);
			target.BindComputeBuffer(UniformIDs._StagingPositionPrev, solverData.stagingPositionPrev);

			target.BindKeyword("LAYOUT_INTERLEAVED", solverData.keywords.LAYOUT_INTERLEAVED);
			target.BindKeyword("APPLY_VOLUME_IMPULSE", solverData.keywords.APPLY_VOLUME_IMPULSE);
			target.BindKeyword("ENABLE_BOUNDARY", solverData.keywords.ENABLE_BOUNDARY);
			target.BindKeyword("ENABLE_BOUNDARY_FRICTION", solverData.keywords.ENABLE_BOUNDARY_FRICTION);
			target.BindKeyword("ENABLE_DISTANCE", solverData.keywords.ENABLE_DISTANCE);
			target.BindKeyword("ENABLE_DISTANCE_LRA", solverData.keywords.ENABLE_DISTANCE_LRA);
			target.BindKeyword("ENABLE_DISTANCE_FTL", solverData.keywords.ENABLE_DISTANCE_FTL);
			target.BindKeyword("ENABLE_CURVATURE_EQ", solverData.keywords.ENABLE_CURVATURE_EQ);
			target.BindKeyword("ENABLE_CURVATURE_GEQ", solverData.keywords.ENABLE_CURVATURE_GEQ);
			target.BindKeyword("ENABLE_CURVATURE_LEQ", solverData.keywords.ENABLE_CURVATURE_LEQ);
			target.BindKeyword("ENABLE_POSE_LOCAL_BEND_TWIST", solverData.keywords.ENABLE_POSE_LOCAL_BEND_TWIST);
			target.BindKeyword("ENABLE_POSE_GLOBAL_POSITION", solverData.keywords.ENABLE_POSE_GLOBAL_POSITION);
			target.BindKeyword("ENABLE_POSE_GLOBAL_ROTATION", solverData.keywords.ENABLE_POSE_GLOBAL_ROTATION);
			target.BindKeyword("STAGING_COMPRESSION", solverData.keywords.STAGING_COMPRESSION);
		}

		public static void BindVolumeData(Material mat, in VolumeData volumeData) => BindVolumeData(new BindTargetMaterial(mat), volumeData);
		public static void BindVolumeData(CommandBuffer cmd, VolumeData volumeData) => BindVolumeData(new BindTargetGlobalCmd(cmd), volumeData);
		public static void BindVolumeData(CommandBuffer cmd, ComputeShader cs, int kernel, in VolumeData volumeData) => BindVolumeData(new BindTargetComputeCmd(cmd, cs, kernel), volumeData);
		public static void BindVolumeData<T>(T target, in VolumeData volumeData) where T : IBindTarget
		{
			target.BindConstantBuffer(UniformIDs.VolumeCBuffer, volumeData.cbufferStorage);

			target.BindComputeTexture(UniformIDs._AccuWeight, volumeData.accuWeight);
			target.BindComputeTexture(UniformIDs._AccuWeight0, volumeData.accuWeight0);
			target.BindComputeTexture(UniformIDs._AccuVelocityX, volumeData.accuVelocityX);
			target.BindComputeTexture(UniformIDs._AccuVelocityY, volumeData.accuVelocityY);
			target.BindComputeTexture(UniformIDs._AccuVelocityZ, volumeData.accuVelocityZ);

			target.BindComputeTexture(UniformIDs._VolumeDensity, volumeData.volumeDensity);
			target.BindComputeTexture(UniformIDs._VolumeDensity0, volumeData.volumeDensity0);
			target.BindComputeTexture(UniformIDs._VolumeVelocity, volumeData.volumeVelocity);
			target.BindComputeTexture(UniformIDs._VolumeDivergence, volumeData.volumeDivergence);

			target.BindComputeTexture(UniformIDs._VolumePressure, volumeData.volumePressure);
			target.BindComputeTexture(UniformIDs._VolumePressureNext, volumeData.volumePressureNext);
			target.BindComputeTexture(UniformIDs._VolumePressureGrad, volumeData.volumePressureGrad);

			target.BindComputeTexture(UniformIDs._BoundarySDF, (volumeData.boundarySDF != null) ? volumeData.boundarySDF : volumeData.boundarySDF_undefined);
			target.BindComputeBuffer(UniformIDs._BoundaryShape, volumeData.boundaryShape);
			target.BindComputeBuffer(UniformIDs._BoundaryMatrix, volumeData.boundaryMatrix);
			target.BindComputeBuffer(UniformIDs._BoundaryMatrixInv, volumeData.boundaryMatrixInv);
			target.BindComputeBuffer(UniformIDs._BoundaryMatrixW2PrevW, volumeData.boundaryMatrixW2PrevW);

			target.BindKeyword("VOLUME_SUPPORT_CONTRACTION", volumeData.keywords.VOLUME_SUPPORT_CONTRACTION);
			target.BindKeyword("VOLUME_TARGET_INITIAL_POSE", volumeData.keywords.VOLUME_TARGET_INITIAL_POSE);
			target.BindKeyword("VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES", volumeData.keywords.VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES);
		}

		public static void PushSolverParams(CommandBuffer cmd, ref SolverData solverData, in SolverSettings solverSettings, in Matrix4x4 rootTransform, in Quaternion strandRotation, float strandDiameter, float strandScale, float dt)
		{
			float IntervalToSeconds(SolverSettings.TimeInterval interval)
			{
				switch (interval)
				{
					default:
					case SolverSettings.TimeInterval.PerSecond: return 1.0f;
					case SolverSettings.TimeInterval.Per100ms: return 0.1f;
					case SolverSettings.TimeInterval.Per10ms: return 0.01f;
					case SolverSettings.TimeInterval.Per1ms: return 0.001f;
				}
			}

			ref var cbuffer = ref solverData.cbuffer;
			ref var keywords = ref solverData.keywords;

			// derive constants
			cbuffer._LocalToWorld = rootTransform;
			cbuffer._LocalToWorldInvT = rootTransform.inverse.transpose;

			cbuffer._WorldRotation = new Vector4(strandRotation.x, strandRotation.y, strandRotation.z, strandRotation.w);

			cbuffer._StrandScale = strandScale;
			cbuffer._StrandDiameter = strandDiameter * 0.001f;

			cbuffer._DT = dt / Mathf.Max(1, solverSettings.substeps);
			cbuffer._Iterations = (uint)solverSettings.iterations;
			cbuffer._Stiffness = solverSettings.stiffness;
			cbuffer._SOR = (solverSettings.iterations > 1) ? solverSettings.kSOR : 1.0f;

			cbuffer._Damping = solverSettings.damping ? solverSettings.dampingFactor : 0.0f;
			cbuffer._DampingInterval = IntervalToSeconds(solverSettings.dampingInterval);
			cbuffer._AngularDamping = solverSettings.angularDamping ? solverSettings.angularDampingFactor : 0.0f;
			cbuffer._AngularDampingInterval = IntervalToSeconds(solverSettings.angularDampingInterval);
			cbuffer._CellPressure = solverSettings.cellPressure;
			cbuffer._CellVelocity = solverSettings.cellVelocity;
			cbuffer._Gravity = solverSettings.gravity * -Vector3.Magnitude(Physics.gravity);

			cbuffer._BoundaryFriction = solverSettings.boundaryCollisionFriction;
			cbuffer._FTLDamping = solverSettings.distanceFTLDamping;
			cbuffer._LocalCurvature = solverSettings.localCurvatureValue * 0.5f;
			cbuffer._LocalShape = solverSettings.localShapeInfluence;

			cbuffer._GlobalPosition = solverSettings.globalPositionInfluence;
			cbuffer._GlobalPositionInterval = IntervalToSeconds(solverSettings.globalPositionInterval);
			cbuffer._GlobalRotation = solverSettings.globalRotationInfluence;
			cbuffer._GlobalFadeOffset = solverSettings.globalFade ? solverSettings.globalFadeOffset : 1e9f;
			cbuffer._GlobalFadeExtent = solverSettings.globalFade ? solverSettings.globalFadeExtent : 1e9f;

			// derive keywords
			keywords.LAYOUT_INTERLEAVED = (solverData.memoryLayout == HairAsset.MemoryLayout.Interleaved);
			keywords.APPLY_VOLUME_IMPULSE = (solverSettings.cellPressure > 0.0f) || (solverSettings.cellVelocity > 0.0f);
			keywords.ENABLE_BOUNDARY = (solverSettings.boundaryCollision && solverSettings.boundaryCollisionFriction == 0.0f);
			keywords.ENABLE_BOUNDARY_FRICTION = (solverSettings.boundaryCollision && solverSettings.boundaryCollisionFriction > 0.0f);
			keywords.ENABLE_DISTANCE = solverSettings.distance;
			keywords.ENABLE_DISTANCE_LRA = solverSettings.distanceLRA;
			keywords.ENABLE_DISTANCE_FTL = solverSettings.distanceFTL;
			keywords.ENABLE_CURVATURE_EQ = (solverSettings.localCurvature && solverSettings.localCurvatureMode == SolverSettings.LocalCurvatureMode.Equals);
			keywords.ENABLE_CURVATURE_GEQ = (solverSettings.localCurvature && solverSettings.localCurvatureMode == SolverSettings.LocalCurvatureMode.GreaterThan);
			keywords.ENABLE_CURVATURE_LEQ = (solverSettings.localCurvature && solverSettings.localCurvatureMode == SolverSettings.LocalCurvatureMode.LessThan);
			keywords.ENABLE_POSE_LOCAL_BEND_TWIST = (solverSettings.localShape && solverSettings.localShapeInfluence > 0.0f);
			keywords.ENABLE_POSE_GLOBAL_POSITION = (solverSettings.globalPosition && solverSettings.globalPositionInfluence > 0.0f);
			keywords.ENABLE_POSE_GLOBAL_ROTATION = (solverSettings.globalRotation && solverSettings.globalRotationInfluence > 0.0f);

			// update cbuffer
			PushConstantBufferData(cmd, solverData.cbufferStorage, cbuffer);
		}

		public static void PushSolverLOD(CommandBuffer cmd, ref SolverData solverData, float lodValue, bool lodBlending)
		{
			ref var cbuffer = ref solverData.cbuffer;

			// derive constants
			var lodIndexHi = solverData.lodThreshold.BinarySearch(lodValue);
			var lodIndexLo = lodIndexHi;
			var lodBlendFrac = 0.0f;

			if (lodIndexHi < 0)
			{
				lodIndexHi = ~lodIndexHi;
				lodIndexLo = Mathf.Max(0, lodIndexHi - 1);

				var lodValueLo = solverData.lodThreshold[lodIndexLo];
				var lodValueHi = solverData.lodThreshold[lodIndexHi];
				{
					lodBlendFrac = (lodValueLo != lodValueHi) ? Mathf.Clamp01((lodValue - lodValueLo) / (lodValueHi - lodValueLo)) : 0.0f;
				}
			}

			if (lodBlending)
			{
				cbuffer._LODIndexLo = (uint)lodIndexLo;
				cbuffer._LODIndexHi = (uint)lodIndexHi;
				cbuffer._LODBlendFraction = lodBlendFrac;
			}
			else
			{
				cbuffer._LODIndexLo = (uint)((lodBlendFrac > 0.5f) ? lodIndexHi : lodIndexLo);
				cbuffer._LODIndexHi = cbuffer._LODIndexLo;
				cbuffer._LODBlendFraction = 0.0f;
			}

			cbuffer._SolverStrandCount = (uint)solverData.lodGuideCountCPU[(int)cbuffer._LODIndexHi];
			cbuffer._SolverStrandCountFinal = (uint)solverData.lodGuideCountCPU[(int)cbuffer._LODIndexLo];

			// update cbuffer
			PushConstantBufferData(cmd, solverData.cbufferStorage, solverData.cbuffer);
		}

		public static void PushSolverLOD(CommandBuffer cmd, ref SolverData solverData, int lodIndex)
		{
			ref var cbuffer = ref solverData.cbuffer;

			// derive constants
			var clampedLODIndex = (uint)Mathf.Clamp(lodIndex, 0, solverData.lodThreshold.Length);
			{
				cbuffer._LODIndexLo = clampedLODIndex;
				cbuffer._LODIndexHi = clampedLODIndex;
				cbuffer._LODBlendFraction = 0.0f;
			}

			cbuffer._SolverStrandCount = (uint)solverData.lodGuideCountCPU[(int)cbuffer._LODIndexHi];
			cbuffer._SolverStrandCountFinal = (uint)solverData.lodGuideCountCPU[(int)cbuffer._LODIndexLo];

			// update cbuffer
			PushConstantBufferData(cmd, solverData.cbufferStorage, solverData.cbuffer);
		}

		public static void PushSolverRoots(CommandBuffer cmd, in SolverData solverData, Mesh rootMesh)
		{
			BindSolverData(cmd, solverData);

			cmd.SetRandomWriteTarget(1, solverData.rootPosition);
			cmd.SetRandomWriteTarget(2, solverData.rootDirection);
			cmd.SetRandomWriteTarget(3, solverData.rootFrame);
			cmd.DrawMesh(rootMesh, Matrix4x4.identity, s_solverRootsMat);
			cmd.ClearRandomWriteTargets();
		}

		public static void PushSolverStaging(CommandBuffer cmd, ref SolverData solverData, bool stagingCompression, uint stagingSubdivisions, in VolumeData volumeData)
		{
			ref var cbuffer = ref solverData.cbuffer;
			ref var keywords = ref solverData.keywords;

			// derive constants
			var stagingOrigin = GetVolumeCenter(volumeData);
			var stagingExtent = GetVolumeExtent(volumeData).Abs().CMax();

			cbuffer._StagingOriginExtentPrev = cbuffer._StagingOriginExtent;
			cbuffer._StagingOriginExtent = new Vector4(stagingOrigin.x, stagingOrigin.y, stagingOrigin.z, stagingExtent);

			var segmentCount = (cbuffer._StrandParticleCount - 1);
			var segmentCountStaging = segmentCount * (1 + stagingSubdivisions);

			cbuffer._StagingVertexCount = segmentCountStaging + 1;
			cbuffer._StagingSubdivision = stagingSubdivisions;

			// derive keywords
			keywords.STAGING_COMPRESSION = stagingCompression;

			// update cbuffer
			PushConstantBufferData(cmd, solverData.cbufferStorage, solverData.cbuffer);

			// staging
			int numX = (int)cbuffer._StrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)cbuffer._StrandCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			int kernelStaging = (cbuffer._StagingSubdivision == 0)
				? SolverKernels.KStaging
				: SolverKernels.KStagingSubdivision;

			using (new ProfilingScope(cmd, MarkersGPU.Solver_Staging))
			{
				CoreUtils.Swap(ref solverData.stagingPosition, ref solverData.stagingPositionPrev);

				BindSolverData(cmd, s_solverCS, kernelStaging, solverData);
				cmd.DispatchCompute(s_solverCS, kernelStaging, numX, numY, numZ);
			}
		}

		public static void PushVolumeParams(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings, in Bounds volumeBounds, float strandDiameter, float strandScale)
		{
			ref var cbuffer = ref volumeData.cbuffer;
			ref var keywords = ref volumeData.keywords;

			// derive constants
			cbuffer._VolumeCells = volumeSettings.gridResolution * Vector3.one;
			cbuffer._VolumeWorldMin = volumeBounds.min;
			cbuffer._VolumeWorldMax = volumeBounds.max;

			float resolveCrossSection = 0.25f * Mathf.PI * strandDiameter * strandDiameter;
			float resolveUnitVolume = (1000.0f * volumeData.allGroupsMaxParticleInterval) * resolveCrossSection;

			cbuffer._ResolveUnitVolume = resolveUnitVolume * (strandScale * strandScale * strandScale);
			cbuffer._ResolveUnitDebugWidth = volumeSettings.splatDebugWidth;

			cbuffer._TargetDensityFactor = volumeSettings.targetDensityInfluence;

			// derive keywords
			keywords.VOLUME_SUPPORT_CONTRACTION = (volumeSettings.pressureSolution == VolumeSettings.PressureSolution.DensityEquals);
			keywords.VOLUME_TARGET_INITIAL_POSE = (volumeSettings.targetDensity == VolumeSettings.TargetDensity.InitialPose);
			keywords.VOLUME_TARGET_INITIAL_POSE_IN_PARTICLES = (volumeSettings.targetDensity == VolumeSettings.TargetDensity.InitialPoseInParticles);

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.cbufferStorage, volumeData.cbuffer);
		}

		public static void PushVolumeBoundaries(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings, in Bounds volumeBounds)
		{
			ref var cbuffer = ref volumeData.cbuffer;

			// update boundary data
			using (var bufShape = new NativeArray<HairBoundary.RuntimeShape.Data>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufXform = new NativeArray<HairBoundary.RuntimeTransform>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrix = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrixInv = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			using (var bufMatrixW2PrevW = new NativeArray<Matrix4x4>(MAX_BOUNDARIES, Allocator.Temp, NativeArrayOptions.ClearMemory))
			{
				if (volumeData.boundaryPrevXform.IsCreated && volumeData.boundaryPrevXform.Length != MAX_BOUNDARIES)
					volumeData.boundaryPrevXform.Dispose();
				if (volumeData.boundaryPrevXform.IsCreated == false)
					volumeData.boundaryPrevXform = new NativeArray<HairBoundary.RuntimeTransform>(MAX_BOUNDARIES, Allocator.Persistent, NativeArrayOptions.ClearMemory);

				unsafe
				{
					var ptrShape = (HairBoundary.RuntimeShape.Data*)bufShape.GetUnsafePtr();
					var ptrXform = (HairBoundary.RuntimeTransform*)bufXform.GetUnsafePtr();
					var ptrXformPrev = (HairBoundary.RuntimeTransform*)volumeData.boundaryPrevXform.GetUnsafePtr();

					var ptrMatrix = (Matrix4x4*)bufMatrix.GetUnsafePtr();
					var ptrMatrixInv = (Matrix4x4*)bufMatrixInv.GetUnsafePtr();
					var ptrMatrixW2PrevW = (Matrix4x4*)bufMatrixW2PrevW.GetUnsafePtr();

					// gather boundaries
					//TODO expose or always enable the volumeSort option which sorts active boundaries by distance
					var boundaryList = HairBoundaryUtility.Gather(volumeSettings.boundariesPriority, volumeSort: false, volumeSettings.boundariesCollect, volumeBounds, Quaternion.identity, volumeSettings.boundariesCollectMode == VolumeSettings.CollectMode.IncludeColliders);
					var boundaryCount = 0;
					var boundarySDFIndex = -1;
					var boundarySDFCellSize = 0.0f;

					// count boundaries
					cbuffer._BoundaryCountDiscrete = 0;
					cbuffer._BoundaryCountCapsule = 0;
					cbuffer._BoundaryCountSphere = 0;
					cbuffer._BoundaryCountTorus = 0;
					cbuffer._BoundaryCountCube = 0;

					for (int i = 0; i != boundaryList.Count; i++)
					{
						var data = boundaryList[i];
						if (data.type == HairBoundary.RuntimeData.Type.SDF)
						{
							cbuffer._BoundaryCountDiscrete++;
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
								case HairBoundary.RuntimeShape.Type.Capsule: cbuffer._BoundaryCountCapsule++; break;
								case HairBoundary.RuntimeShape.Type.Sphere: cbuffer._BoundaryCountSphere++; break;
								case HairBoundary.RuntimeShape.Type.Torus: cbuffer._BoundaryCountTorus++; break;
								case HairBoundary.RuntimeShape.Type.Cube: cbuffer._BoundaryCountCube++; break;
							}

							boundaryCount++;
						}

						if (boundaryCount == MAX_BOUNDARIES)
							break;
					}

					cbuffer._BoundaryWorldEpsilon = (boundarySDFIndex != -1) ? boundarySDFCellSize * 0.2f : 1e-4f;
					cbuffer._BoundaryWorldMargin = volumeSettings.collisionMargin * 0.01f;

					// pack boundaries
					int writeIndexDiscrete = 0;
					int writeIndexCapsule = writeIndexDiscrete + (int)cbuffer._BoundaryCountDiscrete;
					int writeIndexSphere = writeIndexCapsule + (int)cbuffer._BoundaryCountCapsule;
					int writeIndexTorus = writeIndexSphere + (int)cbuffer._BoundaryCountSphere;
					int writeIndexCube = writeIndexTorus + (int)cbuffer._BoundaryCountTorus;
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

						if (writeCount == MAX_BOUNDARIES)
							break;
					}

					// update sdf
					if (boundarySDFIndex != -1)
						volumeData.boundarySDF = boundaryList[boundarySDFIndex].sdf.sdfTexture as RenderTexture;
					else
						volumeData.boundarySDF = null;

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
						if (i < cbuffer._BoundaryCountDiscrete && boundarySDFIndex != -1)
						{
							ptrMatrixInv[i] = boundaryList[boundarySDFIndex].sdf.worldToUVW;
						}
					}

					// update previous frame info
					volumeData.boundaryPrevXform.CopyFrom(bufXform);
					volumeData.boundaryPrevCount = boundaryCount;
					volumeData.boundaryPrevCountDiscard = boundaryList.Count - boundaryCount;

					// update buffers
					PushComputeBufferData(cmd, volumeData.boundaryShape, bufShape);
					PushComputeBufferData(cmd, volumeData.boundaryMatrix, bufMatrix);
					PushComputeBufferData(cmd, volumeData.boundaryMatrixInv, bufMatrixInv);
					PushComputeBufferData(cmd, volumeData.boundaryMatrixW2PrevW, bufMatrixW2PrevW);
				}
			}

			// update cbuffer
			PushConstantBufferData(cmd, volumeData.cbufferStorage, volumeData.cbuffer);
		}

		public static void InitSolverData(CommandBuffer cmd, in SolverData solverData)
		{
			int numX = (int)solverData.cbuffer._StrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)solverData.cbuffer._StrandCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			BindSolverData(cmd, s_solverCS, SolverKernels.KInitialize, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInitialize, numX, numY, numZ);
		}

		public static void InitSolverDataPostVolume(CommandBuffer cmd, in SolverData solverData, in VolumeData volumeData)
		{
			int numX = (int)solverData.cbuffer._StrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)solverData.cbuffer._StrandCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			BindVolumeData(cmd, s_solverCS, SolverKernels.KInitializePostVolume, volumeData);
			BindSolverData(cmd, s_solverCS, SolverKernels.KInitializePostVolume, solverData);
			cmd.DispatchCompute(s_solverCS, SolverKernels.KInitializePostVolume, numX, numY, numZ);
		}

		public static void StepSolverData(CommandBuffer cmd, ref SolverData solverData, in SolverSettings solverSettings, in VolumeData volumeData)
		{
			int numX = (int)solverData.cbuffer._SolverStrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)solverData.cbuffer._SolverStrandCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			int kernelSolveConstraints = SolverKernels.KSolveConstraints_GaussSeidelReference;

			switch (solverSettings.method)
			{
				case SolverSettings.Method.GaussSeidelReference:
					kernelSolveConstraints = SolverKernels.KSolveConstraints_GaussSeidelReference;
					break;

				case SolverSettings.Method.GaussSeidel:
					kernelSolveConstraints = SolverKernels.KSolveConstraints_GaussSeidel;
					break;

				case SolverSettings.Method.Jacobi:
					switch (solverData.cbuffer._StrandParticleCount)
					{
						case 16:
							kernelSolveConstraints = SolverKernels.KSolveConstraints_Jacobi_16;
							break;

						case 32:
							kernelSolveConstraints = SolverKernels.KSolveConstraints_Jacobi_32;
							break;

						case 64:
							kernelSolveConstraints = SolverKernels.KSolveConstraints_Jacobi_64;
							break;

						case 128:
							kernelSolveConstraints = SolverKernels.KSolveConstraints_Jacobi_128;
							break;
					}
					numX = (int)solverData.cbuffer._SolverStrandCount;
					break;
			}

			using (new ProfilingScope(cmd, MarkersGPU.Solver))
			{
				BindVolumeData(cmd, s_solverCS, kernelSolveConstraints, volumeData);

				var stateApplyVolumeImpulse = solverData.keywords.APPLY_VOLUME_IMPULSE;

				for (int i = 0; i != Mathf.Max(1, solverSettings.substeps); i++)
				{
					CoreUtils.Swap(ref solverData.particlePosition, ref solverData.particlePositionPrev);       // [0 1 2] -> (1 0 2)
					CoreUtils.Swap(ref solverData.particlePosition, ref solverData.particlePositionPrevPrev);   // (1 0 2) -> [2 0 1]
					CoreUtils.Swap(ref solverData.particleVelocity, ref solverData.particleVelocityPrev);

					BindSolverData(cmd, s_solverCS, kernelSolveConstraints, solverData);
					cmd.DispatchCompute(s_solverCS, kernelSolveConstraints, numX, numY, numZ);

					// volume impulse is only applied for first substep
					solverData.keywords.APPLY_VOLUME_IMPULSE = false;
				}
			}

			var interpolateStrandCount = solverData.cbuffer._StrandCount - solverData.cbuffer._SolverStrandCountFinal;
			if (interpolateStrandCount > 0)
			{
				int kernelInterpolate = (solverData.cbuffer._LODIndexLo != solverData.cbuffer._LODIndexHi)
					? SolverKernels.KInterpolate
					: SolverKernels.KInterpolateNearest;

				using (new ProfilingScope(cmd, MarkersGPU.Solver_Interpolate))
				{
					numX = (int)interpolateStrandCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, (int)interpolateStrandCount % PARTICLE_GROUP_SIZE);
					numY = 1;
					numZ = 1;

					BindSolverData(cmd, s_solverCS, kernelInterpolate, solverData);
					cmd.DispatchCompute(s_solverCS, kernelInterpolate, numX, numY, numZ);
				}
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
			int numX = volumeSettings.gridResolution / 8;
			int numY = volumeSettings.gridResolution / 8;
			int numZ = volumeSettings.gridResolution;

			// clear
			using (new ProfilingScope(cmd, MarkersGPU.Volume_0_Clear))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeClear, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeClear, numX, numY, numZ);
			}
		}

		private static void StepVolumeData_Insert(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings, in SolverData solverData)
		{
			int particleCount = (int)solverData.cbuffer._SolverStrandCount * (int)solverData.cbuffer._StrandParticleCount;

			int numX = particleCount / PARTICLE_GROUP_SIZE + Mathf.Min(1, particleCount % PARTICLE_GROUP_SIZE);
			int numY = 1;
			int numZ = 1;

			// accumulate
			using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat))
			{
				switch (volumeSettings.splatMethod)
				{
					case VolumeSettings.SplatMethod.Compute:
						{
							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, volumeData);
							BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplat, solverData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplat, numX, numY, numZ);
						}
						break;

					case VolumeSettings.SplatMethod.ComputeSplit:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_Density))
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatDensity, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatDensity, numX, numY, numZ);
							}

							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_VelocityXYZ))
							{
								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityX, numX, numY, numZ);

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityY, numX, numY, numZ);

								BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, volumeData);
								BindSolverData(cmd, s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, solverData);
								cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeSplatVelocityZ, numX, numY, numZ);
							}
						}
						break;

					case VolumeSettings.SplatMethod.Rasterization:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_Rasterization))
							{
								CoreUtils.SetRenderTarget(cmd, volumeData.volumeVelocity, ClearFlag.Color);
								BindVolumeData(cmd, volumeData);
								BindSolverData(cmd, solverData);
								cmd.DrawProcedural(Matrix4x4.identity, s_volumeRasterMat, 0, MeshTopology.Points, particleCount, 1);
							}
						}
						break;

					case VolumeSettings.SplatMethod.RasterizationNoGS:
						{
							using (new ProfilingScope(cmd, MarkersGPU.Volume_1_Splat_RasterizationNoGS))
							{
								CoreUtils.SetRenderTarget(cmd, volumeData.volumeVelocity, ClearFlag.Color);
								BindVolumeData(cmd, volumeData);
								BindSolverData(cmd, solverData);
								cmd.DrawProcedural(Matrix4x4.identity, s_volumeRasterMat, 1, MeshTopology.Quads, particleCount * 8, 1);
							}
						}
						break;
				}
			}
		}

		private static void StepVolumeData_Resolve(CommandBuffer cmd, ref VolumeData volumeData, in VolumeSettings volumeSettings)
		{
			int numX = volumeSettings.gridResolution / 8;
			int numY = volumeSettings.gridResolution / 8;
			int numZ = volumeSettings.gridResolution;

			// resolve accumulated
			switch (volumeSettings.splatMethod)
			{
				case VolumeSettings.SplatMethod.Compute:
				case VolumeSettings.SplatMethod.ComputeSplit:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_2_Resolve))
						{
							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeResolve, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolve, numX, numY, numZ);
						}
					}
					break;

				case VolumeSettings.SplatMethod.Rasterization:
				case VolumeSettings.SplatMethod.RasterizationNoGS:
					{
						using (new ProfilingScope(cmd, MarkersGPU.Volume_2_ResolveFromRasterization))
						{
							BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeResolveFromRasterization, volumeData);
							cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeResolveFromRasterization, numX, numY, numZ);
						}
					}
					break;
			}

			// compute divergence
			using (new ProfilingScope(cmd, MarkersGPU.Volume_3_Divergence))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumeDivergence, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumeDivergence, numX, numY, numZ);
			}

			// pressure eos (initial guess)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_4_PressureEOS))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureEOS, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureEOS, numX, numY, numZ);
			}

			// pressure solve (jacobi)
			using (new ProfilingScope(cmd, MarkersGPU.Volume_5_PressureSolve))
			{
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureSolve, volumeData);

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
				BindVolumeData(cmd, s_volumeCS, VolumeKernels.KVolumePressureGradient, volumeData);
				cmd.DispatchCompute(s_volumeCS, VolumeKernels.KVolumePressureGradient, numX, numY, numZ);
			}
		}

		const int DEBUG_PASS_STRAND_POSITION = 0;
		const int DEBUG_PASS_STRAND_VELOCITY = 1;
		const int DEBUG_PASS_STRAND_CLUSTERS = 7;
		const int DEBUG_PASS_VOLUME_CELL_DENSITY = 2;
		const int DEBUG_PASS_VOLUME_CELL_GRADIENT = 3;
		const int DEBUG_PASS_VOLUME_SLICE_ABOVE = 4;
		const int DEBUG_PASS_VOLUME_SLICE_BELOW = 5;
		const int DEBUG_PASS_VOLUME_ISOSURFACE = 6;

		public static void DrawSolverData(CommandBuffer cmd, in SolverData solverData, in DebugSettings debugSettings)
		{
			using (new ProfilingScope(cmd, MarkersGPU.DrawSolverData))
			{
				if (!debugSettings.drawStrandRoots &&
					!debugSettings.drawStrandParticles &&
					!debugSettings.drawStrandVelocities &&
					!debugSettings.drawStrandClusters)
					return;

				BindSolverData(cmd, solverData);

				// strand roots
				if (debugSettings.drawStrandRoots)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_STRAND_POSITION, MeshTopology.Lines, vertexCount: 2, (int)solverData.cbuffer._StrandCount);
				}

				// strand particles
				if (debugSettings.drawStrandParticles)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_STRAND_POSITION, MeshTopology.Points, vertexCount: (int)solverData.cbuffer._StrandParticleCount, (int)solverData.cbuffer._StrandCount);
				}

				// strand velocities
				if (debugSettings.drawStrandVelocities)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_STRAND_VELOCITY, MeshTopology.Lines, vertexCount: 4 * (int)solverData.cbuffer._StrandParticleCount, (int)solverData.cbuffer._StrandCount);
				}

				// strand clusters
				if (debugSettings.drawStrandClusters)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_STRAND_CLUSTERS, MeshTopology.Lines, vertexCount: 2, (int)solverData.cbuffer._StrandCount);
				}
			}
		}

		public static void DrawVolumeData(CommandBuffer cmd, in VolumeData volumeData, in DebugSettings debugSettings)
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

				BindVolumeData(cmd, volumeData);

				// cell density
				if (debugSettings.drawCellDensity)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_CELL_DENSITY, MeshTopology.Points, GetVolumeCellCount(volumeData), 1);
				}

				// cell gradient
				if (debugSettings.drawCellGradient)
				{
					cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_CELL_GRADIENT, MeshTopology.Lines, 2 * GetVolumeCellCount(volumeData), 1);
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
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_SLICE_ABOVE, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_SLICE_BELOW, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
					if (debugSettings.drawSliceY)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 1);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceYOffset);

						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_SLICE_ABOVE, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_SLICE_BELOW, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
					if (debugSettings.drawSliceZ)
					{
						s_debugDrawMPB.SetInt(UniformIDs._DebugSliceAxis, 2);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOffset, debugSettings.drawSliceZOffset);

						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.8f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_SLICE_ABOVE, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
						s_debugDrawMPB.SetFloat(UniformIDs._DebugSliceOpacity, 0.2f);
						cmd.DrawProcedural(Matrix4x4.identity, s_debugDrawMat, DEBUG_PASS_VOLUME_SLICE_BELOW, MeshTopology.Quads, 4, 1, s_debugDrawMPB);
					}
				}

				// volume isosurface
				if (debugSettings.drawIsosurface)
				{
					var worldCenter = GetVolumeCenter(volumeData);
					var worldExtent = GetVolumeExtent(volumeData);

					s_debugDrawMPB.SetFloat(UniformIDs._DebugIsosurfaceDensity, debugSettings.drawIsosurfaceDensity);
					s_debugDrawMPB.SetInt(UniformIDs._DebugIsosurfaceSubsteps, debugSettings.drawIsosurfaceSubsteps);

					cmd.DrawMesh(s_debugDrawCube, Matrix4x4.TRS(worldCenter, Quaternion.identity, 2.0f * worldExtent), s_debugDrawMat, 0, DEBUG_PASS_VOLUME_ISOSURFACE, s_debugDrawMPB);
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

		public static int GetVolumeCellCount(in VolumeData volumeData)
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