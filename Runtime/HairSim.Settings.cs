using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairSim
	{
		[Serializable]
		public struct SettingsGeometry
		{
			public enum StrandScale
			{
				Fixed			= 0,
				UniformWorldMin	= 1,
				UniformWorldMax	= 2,
			}

			public enum BoundsMode
			{
				Automatic	= 0,
				Manual		= 1,
			}

			public enum StagingPrecision
			{
				Full	= 0,
				Half	= 1,
			}

			[LineHeader("Proportions")]

			public StrandScale strandScale;
			[ToggleGroup, Tooltip("Override strand length (otherwise supplied by group asset)")]
			public bool strandLength;
			[ToggleGroupItem, Range(0.001f, 5.0f), Tooltip("Strand length (in meters)")]
			public float strandLengthValue;
			[ToggleGroup, Tooltip("Override strand diameter (otherwise supplied by group asset)")]
			public bool strandDiameter;
			[ToggleGroupItem, Range(0.01f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
			public float strandDiameterValue;
			[Range(0.0f, 100.0f), Tooltip("Strand separation margin (in millimeters)")]
			public float strandSeparation;

			[LineHeader("Bounds")]

			public BoundsMode boundsMode;
			[VisibleIf(nameof(boundsMode), BoundsMode.Manual)]
			public Vector3 boundsCenter;
			[VisibleIf(nameof(boundsMode), BoundsMode.Manual)]
			public Vector3 boundsExtent;
			[ToggleGroup]
			public bool boundsScale;
			[ToggleGroupItem, Range(0.0f, 2.0f)]
			public float boundsScaleValue;

			[LineHeader("Staging")]

			public StagingPrecision stagingPrecision;
			[ToggleGroup]
			public bool stagingSubdivision;
			[ToggleGroupItem, Range(1, 10)]
			public uint stagingSubdivisionCount;

			public static readonly SettingsGeometry defaults = new SettingsGeometry()
			{
				strandScale = StrandScale.Fixed,
				strandDiameter = false,
				strandDiameterValue = 1.0f,
				strandLength = false,
				strandLengthValue = 1.0f,
				strandSeparation = 0.0f,

				boundsMode = BoundsMode.Automatic,
				boundsCenter = Vector3.zero,
				boundsExtent = Vector3.one,
				boundsScale = false,
				boundsScaleValue = 1.25f,

				stagingPrecision = StagingPrecision.Half,
				stagingSubdivision = false,
				stagingSubdivisionCount = 1,
			};
		}

		[Serializable]
		public struct SettingsPhysics
		{
			public enum Solver
			{
				GaussSeidelReference	= 0,
				GaussSeidel				= 1,
				Jacobi					= 2,
			}

			public enum TimeInterval
			{
				PerSecond	= 0,
				Per100ms	= 1,
				Per10ms		= 2,
				Per1ms		= 3,
			}

			public enum LocalCurvatureMode
			{
				Equals		= 0,
				LessThan	= 1,
				GreaterThan	= 2,
			}

			public enum LocalShapeMode
			{
				Forward		= 0,
				Stitched	= 1,
			}

			[LineHeader("Solver")]

			[Tooltip("Constraint solver")]
			public Solver solver;
			[Range(1, 12), Tooltip("Constraint solver substeps (number of invocations per time step)")]
			public uint solverSubsteps;
			[Range(1, 100), Tooltip("Constraint iterations")]
			public uint constraintIterations;
			[Range(0.0f, 1.0f), Tooltip("Constraint stiffness")]
			public float constraintStiffness;
			[Range(1.0f, 2.0f), Tooltip("Successive over-relaxation factor")]
			public float constraintSOR;

			[LineHeader("Forces")]

			[ToggleGroup, Tooltip("Enable linear damping")]
			public bool dampingLinear;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Linear damping factor (fraction of linear velocity to subtract per interval of time)")]
			public float dampingLinearFactor;
			[ToggleGroupItem, Tooltip("Interval of time over which to subtract fraction of linear velocity")]
			public TimeInterval dampingLinearInterval;

			[ToggleGroup, Tooltip("Enable angular damping")]
			public bool dampingAngular;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Angular damping factor (fraction of angular velocity to subtract per interval of time)")]
			public float dampingAngularFactor;
			[ToggleGroupItem, Tooltip("Interval of time over which to subtract fraction of angular velocity")]
			public TimeInterval dampingAngularInterval;

			[Range(0.0f, 1.0f), Tooltip("Scale factor for volume pressure impulse")]
			public float cellPressure;
			[Range(0.0f, 1.0f), Tooltip("Scale factor for volume velocity impulse (where 0 == FLIP, 1 == PIC)")]
			public float cellVelocity;
			[Range(0.0f, 1.0f), Tooltip("Scale factor for volume-accumulated external impulses (e.g. wind and drag)")]
			public float cellExternal;
			[Range(0.0f, 1.0f), Tooltip("Scale factor for volume gravity")]
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
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("FTL correction factor")]
			public float distanceFTLCorrection;

			[ToggleGroup, Tooltip("Enable bending curvature constraint")]
			public bool localCurvature;
			[ToggleGroupItem, Tooltip("Bending curvature constraint mode (=, <, >)")]
			public LocalCurvatureMode localCurvatureMode;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Scales to a bend of [0 .. 90] degrees")]
			public float localCurvatureValue;

			[ToggleGroup, Tooltip("Enable local shape constraint")]
			public bool localShape;
			[ToggleGroupItem, Tooltip("Local shape constraint application mode")]
			public LocalShapeMode localShapeMode;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Local shape constraint influence")]
			public float localShapeInfluence;
			[ToggleGroup, Tooltip("Enable local shape bias")]
			public bool localShapeBias;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Local shape bias (skews local solution towards global reference)")]
			public float localShapeBiasValue;

			[LineHeader("Reference")]

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

			[LineHeader("Solver LOD")]

			public SolverLODSelection kLODSelection;
			[VisibleIf(nameof(kLODSelection), SolverLODSelection.Manual), Range(0.0f, 1.0f)]
			public float kLODSelectionValue;
			[Range(0.0f, 1.0f)]
			public float kLODCeiling;
			[Range(0.0f, 2.0f)]
			public float kLODScale;
			[Range(-1.0f, 1.0f)]
			public float kLODBias;

			public static readonly SettingsPhysics defaults = new SettingsPhysics()
			{
				solver = Solver.GaussSeidel,
				solverSubsteps = 2,
				constraintIterations = 4,
				constraintStiffness = 0.8f,
				constraintSOR = 1.0f,

				dampingLinear = false,
				dampingLinearFactor = 0.5f,
				dampingLinearInterval = TimeInterval.PerSecond,
				dampingAngular = true,
				dampingAngularFactor = 0.5f,
				dampingAngularInterval = TimeInterval.PerSecond,
				cellPressure = 1.0f,
				cellVelocity = 0.05f,
				cellExternal = 1.0f,
				gravity = 1.0f,

				boundaryCollision = true,
				boundaryCollisionFriction = 0.5f,
				distance = true,
				distanceLRA = true,
				distanceFTL = false,
				distanceFTLCorrection = 0.8f,
				localCurvature = false,
				localCurvatureMode = LocalCurvatureMode.LessThan,
				localCurvatureValue = 0.1f,
				localShape = true,
				localShapeMode = LocalShapeMode.Stitched,
				localShapeInfluence = 1.0f,
				localShapeBias = true,
				localShapeBiasValue = 0.5f,

				globalPosition = false,
				globalPositionInfluence = 1.0f,
				globalPositionInterval = TimeInterval.PerSecond,
				globalRotation = false,
				globalRotationInfluence = 1.0f,
				globalFade = false,
				globalFadeOffset = 0.1f,
				globalFadeExtent = 0.2f,

				kLODSelection = SolverLODSelection.AutomaticPerGroup,
				kLODSelectionValue = 1.0f,
				kLODCeiling = 1.0f,
				kLODScale = 1.0f,
				kLODBias = 0.0f,
			};
		}

		[Serializable]
		public struct SettingsRendering
		{
			//public enum PerVertexData
			//{
			//	Disregard		= 0,
			//	Upload			= 1,
			//	UploadAndApply	= 2,
			//}

			public enum Renderer
			{
				Disabled				= 0,
				BuiltinLines			= 1,
				BuiltinStrips			= 2,
				BuiltinTubes			= 3,
				HDRPHighQualityLines	= 4,
			}

			[LineHeader("Material")]

			[ToggleGroup]
			public bool material;
			[ToggleGroupItem]
			public Material materialAsset;

			//TODO allow user to declare that material increases LOD
			//public float materialScaleCeiling;

			//public PerVertexData perVertexUV;
			//public PerVertexData perVertexDiameter;

			[LineHeader("Renderer")]

			public Renderer renderer;
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
			[VisibleIf(nameof(renderer), Renderer.HDRPHighQualityLines)]
			public LineRendering.RendererGroup rendererGroup;
#endif
			[RenderingLayerMask]
			public int rendererLayers;
			public ShadowCastingMode rendererShadows;
			public MotionVectorGenerationMode motionVectors;

			[LineHeader("Renderer LOD")]

			public RenderLODSelection kLODSelection;
			[VisibleIf(nameof(kLODSelection), (int)RenderLODSelection.Manual), Range(0.0f, 1.0f)]
			public float kLODSelectionValue;
			[Range(0.0f, 1.0f)]
			public float kLODCeiling;
			[Range(0.0f, 2.0f)]
			public float kLODScale;
			[Range(-1.0f, 1.0f)]
			public float kLODBias;
			[Range(0.0f, 1.0f)]
			public float clipThreshold;

			public static readonly SettingsRendering defaults = new SettingsRendering()
			{
				material = false,
				materialAsset = null,

				renderer = Renderer.BuiltinStrips,
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
				rendererGroup = LineRendering.RendererGroup.None,
#endif
				rendererShadows = ShadowCastingMode.On,
				rendererLayers = 0x0101,//TODO this is the HDRP default -- should decide based on active pipeline asset
				motionVectors = MotionVectorGenerationMode.Object,

				kLODSelection = RenderLODSelection.AutomaticPerSegment,
				kLODSelectionValue = 1.0f,
				kLODCeiling = 1.0f,
				kLODScale = 1.0f,
				kLODBias = 0.0f,
				clipThreshold = 0.05f,
			};
		}

		[Serializable]
		public struct SettingsEnvironment
		{
			public enum EmitterCaptureMode
			{
				JustTagged			= 0,
				IncludeWindZones	= 1,
			}

			public enum BoundaryCaptureMode
			{
				JustTagged			= 0,
				IncludeColliders	= 1,
			}

			[LineHeader("Gravity")]

			[Range(-1.0f, 1.0f), Tooltip("Scale factor to apply to scene gravity (Physics.gravity)")]
			public float gravityScale;
			[Tooltip("Rotation to apply to scaled scene gravity")]
			public Quaternion gravityRotation;

			[LineHeader("Wind")]

			[ToggleGroup, Tooltip("Collect and transfer wind emitters (via physics overlap)")]
			public bool emitterCapture;
			[ToggleGroupItem, Tooltip("Collect just tagged emitters, or also include untagged wind zones")]
			public EmitterCaptureMode emitterCaptureMode;
			[ToggleGroupItem(withLabel = true)]
			public LayerMask emitterCaptureLayer;
			[NonReorderable, Tooltip("Always-included emitters (these take priority over emitters collected from scene)")]
			public HairWind[] emitterResident;

			[LineHeader("Solids")]

			[ToggleGroup, Tooltip("Collect and transfer solid boundaries (via physics overlap)")]
			public bool boundaryCapture;
			[ToggleGroupItem, Tooltip("Collect just tagged boundaries, or also include untagged colliders")]
			public BoundaryCaptureMode boundaryCaptureMode;
			[ToggleGroupItem(withLabel = true)]
			public LayerMask boundaryCaptureLayer;
			[NonReorderable, Tooltip("Always-included boundaries (these take priority over boundaries collected from physics)")]
			public HairBoundary[] boundaryResident;

			[Range(0.0f, 10.0f), Tooltip("Default solid margin (in centimeters)")]
			public float defaultSolidMargin;
			[Range(0.0f, 10.0f)]
			public float defaultSolidDensity;
			//[Range(0.0f, 1.0f), Tooltip("Default solid friction")]
			//public float defaultSolidFriction;

			//TODO => boundaryDefaults { collisionMargin, occlusionDensity, occlusionMargin }

			public static readonly SettingsEnvironment defaults = new SettingsEnvironment()
			{
				gravityScale = 1.0f,
				gravityRotation = Quaternion.identity,

				emitterCapture = true,
				emitterCaptureMode = EmitterCaptureMode.JustTagged,
				emitterCaptureLayer = Physics.AllLayers,
				emitterResident = new HairWind[0],

				boundaryCapture = true,
				boundaryCaptureMode = BoundaryCaptureMode.IncludeColliders,
				boundaryCaptureLayer = Physics.AllLayers,
				boundaryResident = new HairBoundary[0],

				defaultSolidMargin = 0.25f,
				defaultSolidDensity = 5.0f,
			};
		}

		[Serializable]
		public struct SettingsVolume
		{
			public enum GridPrecision
			{
				Full	= 0,
				Half	= 1,
			}

			public enum SplatMethod
			{
				None				= 0,
				Compute				= 1,
				ComputeSplit		= 2,
				Rasterization		= 3,
				RasterizationNoGS	= 4,
			}

			public enum PressureSolution
			{
				DensityEquals	= 0,
				DensityLessThan	= 1,
			}

			public enum RestDensity
			{
				Uniform					= 0,
				InitialPose				= 1,
				InitialPoseInParticles	= 2,
			}

			public enum OcclusionMode
			{
				Discrete	= 0,
				Exact		= 1,
			}

			[LineHeader("Transfer")]

			public GridPrecision gridPrecision;
			[Range(8, 160)]
			public uint gridResolution;
			[HideInInspector, Tooltip("Increases precision of derivative quantities at the cost of volume splatting performance")]
			public bool gridStaggered;
			public SplatMethod splatMethod;
			public bool splatClusters;

			[LineHeader("Pressure")]

			[Range(0, 100), Tooltip("Number of pressure solve iterations (where 0 == Initialization by EOS, [1 .. n] == Jacobi iteration)")]
			public uint pressureIterations;
			[Tooltip("Pressure solve can either target an exact density (causing both compression and decompression), or a maximum density (causing just decompression)")]
			public PressureSolution pressureSolution;
			[Tooltip("Target density can be uniform (based on physical strand diameter), based on the initial pose, or based on initial pose carried in particles (a runtime average)")]
			public RestDensity restDensity;
			[Min(0.01f)]
			public float restDensityScale;
			[Range(0.0f, 1.0f), Tooltip("Influence of rest density vs. an always-present incompressibility term")]
			public float restDensityInfluence;

			[LineHeader("Scattering")]

			[ToggleGroup]
			public bool scatteringProbe;
			[ToggleGroupItem(withLabel = true), Range(1, 10)]
			public uint scatteringProbeCellSubsteps;
			[Range(0.0f, 2.0f)]
			public float scatteringProbeBias;
			[Range(0, 20)]
			public uint probeSamplesTheta;
			[Range(0, 20)]
			public uint probeSamplesPhi;
			[ToggleGroup]
			public bool probeOcclusion;
			[ToggleGroupItem]
			public OcclusionMode probeOcclusionMode;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 10.0f)]
			public float probeOcclusionSolidDensity;

			[LineHeader("Wind & Drag")]

			[ToggleGroup]
			public bool windPropagation;
			[ToggleGroupItem(withLabel = true), Range(1, 10)]
			public uint windPropagationCellSubsteps;
			[ToggleGroup]
			public bool windOcclusion;
			[ToggleGroupItem]
			public OcclusionMode windOcclusionMode;
			[Range(0.0f, 10.0f), Tooltip("Extinction distance in fully occupied volume (in centimeters)")]
			public float windDepth;

			public static readonly SettingsVolume defaults = new SettingsVolume()
			{
				gridResolution = 32,
				gridPrecision = GridPrecision.Full,
				gridStaggered = false,

				splatMethod = SplatMethod.Compute,
				splatClusters = true,

				pressureIterations = 5,
				pressureSolution = PressureSolution.DensityLessThan,
				restDensity = RestDensity.Uniform,
				restDensityScale = 1.0f,
				restDensityInfluence = 1.0f,

				scatteringProbe = false,
				scatteringProbeCellSubsteps = 1,
				scatteringProbeBias = 1.0f,
				probeSamplesTheta = 5,
				probeSamplesPhi = 10,
				probeOcclusion = true,
				probeOcclusionMode = OcclusionMode.Discrete,
				probeOcclusionSolidDensity = 5.0f,

				windPropagation = true,
				windPropagationCellSubsteps = 1,
				windOcclusion = true,
				windOcclusionMode = OcclusionMode.Exact,
				windDepth = 1.0f,
			};
		}

		[Serializable]
		public struct SettingsDebugging
		{
			[LineHeader("Strand Data")]

			public bool drawStrandRoots;
			public bool drawStrandParticles;
			public bool drawStrandVelocities;
			public bool drawStrandClusters;
			public int specificCluster;

			[LineHeader("Volume Cells")]

			public bool drawCellDensity;
			public bool drawCellGradient;
			[ToggleGroup]
			public bool drawIsosurface;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Trace threshold")]
			public float drawIsosurfaceDensity;
			[ToggleGroupItem(withLabel = true), Range(1, 10), Tooltip("Substeps within each cell")]
			public uint drawIsosurfaceSubsteps;

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
			[Range(0.0f, 7.0f), Tooltip("Scrubs between different layers")]
			public float drawSliceDivider;

			public static readonly SettingsDebugging defaults = new SettingsDebugging()
			{
				drawStrandRoots = false,
				drawStrandParticles = false,
				drawStrandVelocities = false,
				drawStrandClusters = false,
				specificCluster = -1,

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
	}
}