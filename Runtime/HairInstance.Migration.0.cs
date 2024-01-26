using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
#endif

namespace Unity.DemoTeam.Hair
{
	// data migration impl. checklist:
	//
	//	- block renamed: capture and migrate all fields
	//	- field(s) renamed: capture and migrate renamed fields
	//	- field(s) changed: capture and migrate changed fields
	//	- field(s) removed: no action
	//
	// data migration is then a simple two-step process:
	//
	//	1. capture old data into trimmed copies of old structures (via FormerlySerializedAs)
	//	2. migrate old data from trimmed copies
	//
	// (old structures are unfortunately also re-serialized)

	using __1__SettingsExecutive = HairInstance.SettingsExecutive;
	using __1__SettingsEnvironment = HairSim.SettingsEnvironment;
	using __1__SettingsVolumetrics = HairSim.SettingsVolume;

	using __1__SettingsGeometry = HairSim.SettingsGeometry;
	using __1__SettingsRendering = HairSim.SettingsRendering;
	using __1__SettingsPhysics = HairSim.SettingsPhysics;

	using __1__SolverLODSelection = HairSim.SolverLODSelection;

	public partial class HairInstance
	{
		[SerializeField, FormerlySerializedAs("settingsSystem")]
		__0__SettingsSystem data_0_settingsSystem = __0__SettingsSystem.defaults;

		[SerializeField, FormerlySerializedAs("settingsVolume"), FormerlySerializedAs("volumeSettings")]
		__0__VolumeSettings data_0_settingsVolume = __0__VolumeSettings.defaults;

		[SerializeField, FormerlySerializedAs("strandGroupDefaults")]
		__0__GroupSettings data_0_strandGroupDefaults = __0__GroupSettings.defaults;

		[SerializeField, FormerlySerializedAs("strandGroupSettings")]
		__0__GroupSettings[] data_0_strandGroupSettings;

		void PerformMigration_0()
		{
			ref var data_1_settingsExecutive = ref this.settingsExecutive;
			ref var data_1_settingsEnvironment = ref this.settingsEnvironment;
			ref var data_1_settingsVolumetrics = ref this.settingsVolumetrics;

			ref var data_1_strandGroupDefaults = ref this.strandGroupDefaults;
			ref var data_1_strandGroupSettings = ref this.strandGroupSettings;

			// migrate data_0_settingsSystem
			{
				ref readonly var in_0 = ref data_0_settingsSystem;

				// => data_1_settingsExecutive
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __1__SettingsExecutive out_1)
					{
						static __1__SettingsExecutive.UpdateMode TranslateUpdateMode(__0__SettingsSystem.UpdateMode x) => (__1__SettingsExecutive.UpdateMode)x;
						static __1__SettingsExecutive.UpdateRate TranslateSimulationRate(__0__SettingsSystem.SimulationRate x)
						{
							switch (x)
							{
								default:
								case __0__SettingsSystem.SimulationRate.Fixed30Hz: return __1__SettingsExecutive.UpdateRate.Fixed30Hz;
								case __0__SettingsSystem.SimulationRate.Fixed60Hz: return __1__SettingsExecutive.UpdateRate.Fixed60Hz;
								case __0__SettingsSystem.SimulationRate.Fixed120Hz: return __1__SettingsExecutive.UpdateRate.Fixed120Hz;
								case __0__SettingsSystem.SimulationRate.CustomTimeStep: return __1__SettingsExecutive.UpdateRate.CustomTimeStep;
							}
						}

						out_1.updateMode = TranslateUpdateMode(in_0.updateMode);
						out_1.updateSimulation = in_0.simulation;
						out_1.updateSimulationRate = TranslateSimulationRate(in_0.simulationRate);
						out_1.updateSimulationInEditor = in_0.simulationInEditor;
						out_1.updateTimeStep = in_0.simulationTimeStep;
						out_1.updateStepsMin = in_0.stepsMin;
						out_1.updateStepsMinValue = in_0.stepsMinValue;
						out_1.updateStepsMax = in_0.stepsMax;
						out_1.updateStepsMaxValue = in_0.stepsMaxValue;
					}

					TransferSettingsSystem(in_0, ref data_1_settingsExecutive);
				}

				// => data_1_strandGroupDefaults.settingsGeometry
				// => data_1_strandGroupSettings[].settingsGeometry
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __1__SettingsGeometry out_1)
					{
						static __1__SettingsGeometry.BoundsMode TranslateBoundsMode(__0__SettingsSystem.BoundsMode x) => (__1__SettingsGeometry.BoundsMode)x;

						out_1.boundsMode = TranslateBoundsMode(in_0.boundsMode);
						out_1.boundsCenter = in_0.boundsCenter;
						out_1.boundsExtent = in_0.boundsExtent;
						out_1.boundsScale = in_0.boundsScale;
						out_1.boundsScaleValue = in_0.boundsScaleValue;
					}

					TransferSettingsSystem(in_0, ref data_1_strandGroupDefaults.settingsGeometry);
					
					for (int i = 0; i != (data_1_strandGroupSettings?.Length ?? 0); i++)
					{
						TransferSettingsSystem(in_0, ref data_1_strandGroupSettings[i].settingsGeometry);
					}
				}

				// => data_1_strandGroupDefaults.settingsPhysics
				// => data_1_strandGroupSettings[].settingsPhysics
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __1__SettingsPhysics out_1)
					{
						static __1__SolverLODSelection TranslateLODSelection(__0__SettingsSystem.LODSelection x)
						{
							switch (x)
							{
								default:
								case __0__SettingsSystem.LODSelection.Automatic: return __1__SolverLODSelection.AutomaticPerGroup;
								case __0__SettingsSystem.LODSelection.Fixed: return __1__SolverLODSelection.Manual;
							}
						};

						out_1.kLODSelection = TranslateLODSelection(in_0.kLODSearch);
						out_1.kLODSelectionValue = in_0.kLODSearchValue;
					}

					TransferSettingsSystem(in_0, ref data_1_strandGroupDefaults.settingsPhysics);
					
					for (int i = 0; i != (data_1_strandGroupSettings?.Length ?? 0); i++)
					{
						TransferSettingsSystem(in_0, ref data_1_strandGroupSettings[i].settingsPhysics);
					}
				}

				// => data_1_strandGroupDefaults.settingsRendering
				// => data_1_strandGroupSettings[].settingsRendering
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __1__SettingsRendering out_1)
					{
						static __1__SettingsRendering.Renderer TranslateStrandRenderer(__0__SettingsSystem.StrandRenderer x)
						{
							switch (x)
							{
								case __0__SettingsSystem.StrandRenderer.Disabled: return __1__SettingsRendering.Renderer.Disabled;
								default:
								case __0__SettingsSystem.StrandRenderer.BuiltinLines: return __1__SettingsRendering.Renderer.BuiltinLines;
								case __0__SettingsSystem.StrandRenderer.BuiltinStrips: return __1__SettingsRendering.Renderer.BuiltinStrips;
								case __0__SettingsSystem.StrandRenderer.BuiltinTubes: return __1__SettingsRendering.Renderer.BuiltinTubes;
								case __0__SettingsSystem.StrandRenderer.HDRPHighQualityLines: return __1__SettingsRendering.Renderer.HDRPHighQualityLines;
							}
						};

						out_1.renderer = TranslateStrandRenderer(in_0.strandRenderer);
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
						out_1.rendererGroup = in_0.strandRendererGroup;
#endif
						out_1.rendererLayers = in_0.strandLayers;
						out_1.rendererShadows = in_0.strandShadows;
						out_1.motionVectors = in_0.motionVectors;
					}

					TransferSettingsSystem(in_0, ref data_1_strandGroupDefaults.settingsRendering);

					for (int i = 0; i != (data_1_strandGroupSettings?.Length ?? 0); i++)
					{
						TransferSettingsSystem(in_0, ref data_1_strandGroupSettings[i].settingsRendering);
					}
				}
			}

			// migrate data_0_settingsVolume
			{
				ref readonly var in_0 = ref data_0_settingsVolume;

				// => data_1_settingsEnvironment
				{
					static void TransferSettingsVolume(in __0__VolumeSettings in_0, ref __1__SettingsEnvironment out_1)
					{
						static __1__SettingsEnvironment.BoundaryCaptureMode TranslateCollectMode(__0__VolumeSettings.CollectMode x) => (__1__SettingsEnvironment.BoundaryCaptureMode)x;

						out_1.boundaryCapture = in_0.boundariesCollect;
						out_1.boundaryCaptureMode = TranslateCollectMode(in_0.boundariesCollectMode);
						out_1.boundaryCaptureLayer = in_0.boundariesCollectLayer;
						out_1.boundaryResident = in_0.boundariesPriority;
						out_1.defaultSolidMargin = in_0.collisionMargin;
						out_1.defaultSolidDensity = in_0.probeOcclusionSolidDensity;
					}

					TransferSettingsVolume(in_0, ref data_1_settingsEnvironment);
				}

				// => data_1_settingsVolumetrics
				{
					static void TransferSettingsVolume(in __0__VolumeSettings in_0, ref __1__SettingsVolumetrics out_1)
					{
						static __1__SettingsVolumetrics.GridPrecision TranslateGridPrecision(__0__VolumeSettings.GridPrecision x) => (__1__SettingsVolumetrics.GridPrecision)x;
						static __1__SettingsVolumetrics.SplatMethod TranslateSplatMethod(__0__VolumeSettings.SplatMethod x) => (__1__SettingsVolumetrics.SplatMethod)x;
						static __1__SettingsVolumetrics.PressureSolution TranslatePressureSolution(__0__VolumeSettings.PressureSolution x) => (__1__SettingsVolumetrics.PressureSolution)x;
						static __1__SettingsVolumetrics.RestDensity TranslateTargetDensity(__0__VolumeSettings.TargetDensity x) => (__1__SettingsVolumetrics.RestDensity)x;
						static __1__SettingsVolumetrics.OcclusionMode TranslateOcclusionMode(__0__VolumeSettings.OcclusionMode x) => (__1__SettingsVolumetrics.OcclusionMode)x;

						out_1.gridPrecision = TranslateGridPrecision(in_0.gridPrecision);
						out_1.gridResolution = (uint)in_0.gridResolution;

						out_1.splatMethod = TranslateSplatMethod(in_0.splatMethod);
						out_1.splatClusters = in_0.splatClusters;

						out_1.pressureIterations = (uint)in_0.pressureIterations;
						out_1.pressureSolution = TranslatePressureSolution(in_0.pressureSolution);
						out_1.restDensity = TranslateTargetDensity(in_0.targetDensity);
						out_1.restDensityInfluence = in_0.targetDensityInfluence;

						out_1.scatteringProbe = in_0.scatteringProbe;
						out_1.scatteringProbeCellSubsteps = in_0.scatteringProbeCellSubsteps;
						out_1.scatteringProbeBias = in_0.scatteringProbeBias;
						out_1.probeSamplesTheta = in_0.probeSamplesTheta;
						out_1.probeSamplesPhi = in_0.probeSamplesPhi;
						out_1.probeOcclusion = in_0.probeOcclusion;
						out_1.probeOcclusionMode = TranslateOcclusionMode(in_0.probeOcclusionMode);
						out_1.probeOcclusionSolidDensity = in_0.probeOcclusionSolidDensity;

						out_1.windPropagation = in_0.windPropagation;
						out_1.windPropagationCellSubsteps = in_0.windPropagationCellSubsteps;
						out_1.windOcclusion = in_0.windOcclusion;
						out_1.windOcclusionMode = TranslateOcclusionMode(in_0.windOcclusionMode);
						out_1.windDepth = in_0.windExtinction;
					}

					TransferSettingsVolume(in_0, ref data_1_settingsVolumetrics);
				}
			}

			// migrate data_0_strandGroupDefaults
			// migrate data_0_strandGroupSettings[]
			{
				// *.settingsStrands
				{
					// => data_1_strandGroupDefaults.settingsRendering
					// => data_1_strandGroupSettings[].settingsRendering
					{
						static void TransferSettingsStrands(in __0__SettingsStrands in_0, ref __1__SettingsRendering out_1)
						{
							out_1.material = in_0.material;
							out_1.materialAsset = in_0.materialValue;
						}

						TransferSettingsStrands(data_0_strandGroupDefaults.settingsStrands, ref data_1_strandGroupDefaults.settingsRendering);

						for (int i = 0; i != (data_1_strandGroupSettings?.Length ?? 0); i++)
						{
							TransferSettingsStrands(data_0_strandGroupSettings[i].settingsStrands, ref data_1_strandGroupSettings[i].settingsRendering);
							data_1_strandGroupSettings[i].settingsRenderingToggle = data_0_strandGroupSettings[i].settingsStrandsToggle;
						}
					}

					// => data_1_strandGroupDefaults.settingsGeometry
					// => data_1_strandGroupSettings[].settingsGeometry
					{
						static void TransferSettingsStrands(in __0__SettingsStrands in_0, ref __1__SettingsGeometry out_1)
						{
							static __1__SettingsGeometry.StrandScale TranslateStrandScale(__0__SettingsStrands.StrandScale x) => (__1__SettingsGeometry.StrandScale)x;
							static __1__SettingsGeometry.StagingPrecision TranslateStagingPrecision(__0__SettingsStrands.StagingPrecision x) => (__1__SettingsGeometry.StagingPrecision)x;

							out_1.strandScale = TranslateStrandScale(in_0.strandScale);
							out_1.strandDiameter = true;
							out_1.strandDiameterValue = in_0.strandDiameter;
							out_1.strandSeparation = in_0.strandMargin;

							out_1.stagingPrecision = TranslateStagingPrecision(in_0.stagingPrecision);
							out_1.stagingSubdivision = (in_0.stagingSubdivision > 0);
							out_1.stagingSubdivisionCount = (in_0.stagingSubdivision > 0) ? in_0.stagingSubdivision : 1;
						}

						TransferSettingsStrands(data_0_strandGroupDefaults.settingsStrands, ref data_1_strandGroupDefaults.settingsGeometry);

						for (int i = 0; i != (data_1_strandGroupSettings?.Length ?? 0); i++)
						{
							TransferSettingsStrands(data_0_strandGroupSettings[i].settingsStrands, ref data_1_strandGroupSettings[i].settingsGeometry);
							data_1_strandGroupSettings[i].settingsGeometryToggle = data_0_strandGroupSettings[i].settingsStrandsToggle;
						}
					}
				}

				// *.settingsSolver
				{
					// => data_1_strandGroupDefaults.settingsPhysics
					// => data_1_strandGroupSettings[].settingsPhysics
					{
						static void TransferSettingsSolver(in __0__SolverSettings in_0, ref __1__SettingsPhysics out_1)
						{
							static __1__SettingsPhysics.Solver TranslateMethod(__0__SolverSettings.Method x) => (__1__SettingsPhysics.Solver)x;
							static __1__SettingsPhysics.TimeInterval TranslateTimeInterval(__0__SolverSettings.TimeInterval x) => (__1__SettingsPhysics.TimeInterval)x;
							static __1__SettingsPhysics.LocalCurvatureMode TranslateLocalCurvatureMode(__0__SolverSettings.LocalCurvatureMode x) => (__1__SettingsPhysics.LocalCurvatureMode)x;
							static __1__SettingsPhysics.LocalShapeMode TranslateLocalShapeMode(__0__SolverSettings.LocalShapeMode x) => (__1__SettingsPhysics.LocalShapeMode)x;

							out_1.solver = TranslateMethod(in_0.method);
							out_1.solverSubsteps = (uint)in_0.substeps;
							out_1.constraintIterations = (uint)in_0.iterations;
							out_1.constraintStiffness = in_0.stiffness;
							out_1.constraintSOR = in_0.kSOR;

							out_1.dampingLinear = in_0.damping;
							out_1.dampingLinearFactor = in_0.dampingFactor;
							out_1.dampingLinearInterval = TranslateTimeInterval(in_0.dampingInterval);
							out_1.dampingAngular = in_0.angularDamping;
							out_1.dampingAngularFactor = in_0.angularDampingFactor;
							out_1.dampingAngularInterval = TranslateTimeInterval(in_0.angularDampingInterval);
							out_1.cellPressure = in_0.cellPressure;
							out_1.cellVelocity = in_0.cellVelocity;
							out_1.cellExternal = in_0.cellForces;
							out_1.gravity = 1.0f;// global scale and rotation moves to data_1_settingsEnvironment

							out_1.boundaryCollision = in_0.boundaryCollision;
							out_1.boundaryCollisionFriction = in_0.boundaryCollisionFriction;
							out_1.distance = in_0.distance;
							out_1.distanceLRA = in_0.distanceLRA;
							out_1.distanceFTL = in_0.distanceFTL;
							out_1.distanceFTLCorrection = in_0.distanceFTLCorrection;
							out_1.localCurvature = in_0.localCurvature;
							out_1.localCurvatureMode = TranslateLocalCurvatureMode(in_0.localCurvatureMode);
							out_1.localCurvatureValue = in_0.localCurvatureValue;
							out_1.localShape = in_0.localShape;
							out_1.localShapeMode = TranslateLocalShapeMode(in_0.localShapeMode);
							out_1.localShapeInfluence = in_0.localShapeInfluence;
							out_1.localShapeBias = in_0.localShapeBias;
							out_1.localShapeBiasValue = in_0.localShapeBiasValue;

							out_1.globalPosition = in_0.globalPosition;
							out_1.globalPositionInfluence = in_0.globalPositionInfluence;
							out_1.globalPositionInterval = TranslateTimeInterval(in_0.globalPositionInterval);
							out_1.globalRotation = in_0.globalRotation;
							out_1.globalRotationInfluence = in_0.globalRotationInfluence;
							out_1.globalFade = in_0.globalFade;
							out_1.globalFadeOffset = in_0.globalFadeOffset;
							out_1.globalFadeExtent = in_0.globalFadeExtent;
						}

						TransferSettingsSolver(data_0_strandGroupDefaults.settingsSolver, ref data_1_strandGroupDefaults.settingsPhysics);

						for (int i = 0; i != (data_1_strandGroupSettings?.Length ?? 0); i++)
						{
							TransferSettingsSolver(data_0_strandGroupSettings[i].settingsSolver, ref data_1_strandGroupSettings[i].settingsPhysics);
							data_1_strandGroupSettings[i].settingsPhysicsToggle = data_0_strandGroupSettings[i].settingsSolverToggle;
						}
					}

					// => data_1_settingsEnvironment
					{
						static void TransferSettingsSolver(in __0__SolverSettings in_0, ref __1__SettingsEnvironment out_1)
						{
							out_1.gravityScale = in_0.gravity;
							out_1.gravityRotation = Quaternion.Euler(in_0.gravityRotation);
						}

						TransferSettingsSolver(data_0_strandGroupDefaults.settingsSolver, ref data_1_settingsEnvironment);
					}
				}
			}
		}

		[Serializable]
		struct __0__SettingsSystem
		{
			public enum BoundsMode
			{
				Automatic,
				Fixed,
			}

			public enum LODSelection
			{
				Automatic,
				Fixed,
			}

			public enum StrandRenderer
			{
				Disabled = 0,
				BuiltinLines = 1,
				BuiltinStrips = 2,
				BuiltinTubes = 4,
				HDRPHighQualityLines = 3,
			}

			public enum UpdateMode
			{
				BuiltinEvent,
				External,
			}

			public enum SimulationRate
			{
				Fixed30Hz,
				Fixed60Hz,
				Fixed120Hz,
				CustomTimeStep,
			}

			public BoundsMode boundsMode;
			public Vector3 boundsCenter;
			public Vector3 boundsExtent;
			public bool boundsScale;
			public float boundsScaleValue;

			public LODSelection kLODSearch;
			public CameraType kLODSearchViews;
			public AnimationCurve kLODSearchCurve;
			public float kLODSearchValue;
			public bool kLODBlending;

			public StrandRenderer strandRenderer;
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
			[FormerlySerializedAs("strandRendererGroupingValue")]
			public LineRendering.RendererGroup strandRendererGroup;
#endif
			public ShadowCastingMode strandShadows;
			public int strandLayers;
			public MotionVectorGenerationMode motionVectors;

			public UpdateMode updateMode;

			public bool simulation;
			public SimulationRate simulationRate;
			public bool simulationInEditor;
			public float simulationTimeStep;
			public bool stepsMin;
			public int stepsMinValue;
			public bool stepsMax;
			public int stepsMaxValue;

			public static readonly __0__SettingsSystem defaults = new __0__SettingsSystem()
			{
				boundsMode = BoundsMode.Automatic,
				boundsCenter = new Vector3(0.0f, 0.0f, 0.0f),
				boundsExtent = new Vector3(1.0f, 1.0f, 1.0f),
				boundsScale = false,
				boundsScaleValue = 1.25f,

				kLODSearch = LODSelection.Fixed,
				kLODSearchViews = ~CameraType.SceneView,
				kLODSearchCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f),
				kLODSearchValue = 1.0f,
				kLODBlending = false,

				strandRenderer = StrandRenderer.BuiltinLines,
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
				strandRendererGroup = LineRendering.RendererGroup.None,
#endif
				strandShadows = ShadowCastingMode.On,
				strandLayers = 0x0101,//TODO this is the HDRP default -- should decide based on active pipeline asset
				motionVectors = MotionVectorGenerationMode.Object,

				updateMode = UpdateMode.BuiltinEvent,

				simulation = true,
				simulationRate = SimulationRate.Fixed30Hz,
				simulationInEditor = true,
				simulationTimeStep = 1.0f / 100.0f,
				stepsMin = false,
				stepsMinValue = 1,
				stepsMax = true,
				stepsMaxValue = 1,
			};
		}

		[Serializable]
		struct __0__VolumeSettings
		{
			public enum GridPrecision
			{
				Full,
				Half,
			}

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

			public enum OcclusionMode
			{
				Discrete,
				Exact,
			}

			public enum CollectMode
			{
				JustTagged,
				IncludeColliders,
			}

			public GridPrecision gridPrecision;
			[FormerlySerializedAs("volumeGridResolution")]
			public int gridResolution;

			[FormerlySerializedAs("volumeSplatMethod")]
			public SplatMethod splatMethod;
			public bool splatClusters;

			public int pressureIterations;
			public PressureSolution pressureSolution;
			public TargetDensity targetDensity;
			public float targetDensityInfluence;

			[FormerlySerializedAs("strandCountProbe")]
			public bool scatteringProbe;
			[FormerlySerializedAs("strandCountProbeCellSubsteps")]
			public uint scatteringProbeCellSubsteps;
			[FormerlySerializedAs("strandCountBias")]
			public float scatteringProbeBias;
			[FormerlySerializedAs("probeStepsTheta")]
			public uint probeSamplesTheta;
			[FormerlySerializedAs("probeStepsPhi")]
			public uint probeSamplesPhi;
			public bool probeOcclusion;
			public OcclusionMode probeOcclusionMode;
			public float probeOcclusionSolidDensity;

			public bool windPropagation;
			public uint windPropagationCellSubsteps;
			public bool windOcclusion;
			public OcclusionMode windOcclusionMode;
			public float windExtinction;

			public float collisionMargin;
			public bool boundariesCollect;
			public CollectMode boundariesCollectMode;
			public LayerMask boundariesCollectLayer;
			public HairBoundary[] boundariesPriority;

			public static readonly __0__VolumeSettings defaults = new __0__VolumeSettings()
			{
				splatMethod = SplatMethod.Compute,
				splatClusters = true,

				gridResolution = 32,
				gridPrecision = GridPrecision.Full,

				pressureIterations = 3,
				pressureSolution = PressureSolution.DensityLessThan,
				targetDensity = TargetDensity.Uniform,
				targetDensityInfluence = 1.0f,

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
				windExtinction = 1.0f,

				collisionMargin = 0.25f,
				boundariesCollect = true,
				boundariesCollectMode = CollectMode.IncludeColliders,
				boundariesCollectLayer = Physics.AllLayers,
				boundariesPriority = new HairBoundary[0],
			};
		}

		[Serializable]
		struct __0__GroupSettings
		{
			public __0__SettingsStrands settingsStrands;
			public bool settingsStrandsToggle;

			public __0__SolverSettings settingsSolver;
			public bool settingsSolverToggle;

			public static __0__GroupSettings defaults => new __0__GroupSettings()
			{
				settingsStrands = __0__SettingsStrands.defaults,
				settingsStrandsToggle = false,

				settingsSolver = __0__SolverSettings.defaults,
				settingsSolverToggle = false,
			};
		}

		[Serializable]
		struct __0__SettingsStrands
		{
			public enum StrandScale
			{
				Fixed,
				UniformWorldMin,
				UniformWorldMax,
			}

			public enum StagingPrecision
			{
				Full,
				Half,
			}

			[FormerlySerializedAs("strandMaterial")]
			public bool material;
			[FormerlySerializedAs("strandMaterialValue")]
			public Material materialValue;

			public StrandScale strandScale;
			public float strandDiameter;
			public float strandMargin;

			public StagingPrecision stagingPrecision;
			public uint stagingSubdivision;

			public static readonly __0__SettingsStrands defaults = new __0__SettingsStrands()
			{
				material = false,
				materialValue = null,

				strandScale = StrandScale.Fixed,
				strandDiameter = 1.0f,
				strandMargin = 0.0f,

				stagingSubdivision = 0,
				stagingPrecision = StagingPrecision.Half,
			};
		}

		[Serializable]
		struct __0__SolverSettings
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

			public enum LocalShapeMode
			{
				Forward,
				Stitched,
			}

			public Method method;
			public int iterations;
			public int substeps;
			public float stiffness;
			public float kSOR;

			public bool damping;
			public float dampingFactor;
			public TimeInterval dampingInterval;
			public bool angularDamping;
			public float angularDampingFactor;
			public TimeInterval angularDampingInterval;
			public float cellPressure;
			public float cellVelocity;
			public float cellForces;
			public float gravity;
			public Vector3 gravityRotation;

			public bool boundaryCollision;
			public float boundaryCollisionFriction;
			public bool distance;
			public bool distanceLRA;
			public bool distanceFTL;
			[FormerlySerializedAs("distanceFTLDamping")]
			public float distanceFTLCorrection;
			public bool localCurvature;
			public LocalCurvatureMode localCurvatureMode;
			public float localCurvatureValue;
			public bool localShape;
			public LocalShapeMode localShapeMode;
			public float localShapeInfluence;
			public bool localShapeBias;
			public float localShapeBiasValue;

			public bool globalPosition;
			public float globalPositionInfluence;
			public TimeInterval globalPositionInterval;
			public bool globalRotation;
			public float globalRotationInfluence;
			public bool globalFade;
			public float globalFadeOffset;
			public float globalFadeExtent;

			public static readonly __0__SolverSettings defaults = new __0__SolverSettings()
			{
				method = Method.GaussSeidel,
				substeps = 1,
				iterations = 3,
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
				cellForces = 1.0f,
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
			};
		}
	}
}
