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

	using __IMPL__SettingsExecutive = HairInstance.SettingsExecutive;
	using __IMPL__SettingsEnvironment = HairSim.SettingsEnvironment;
	using __IMPL__SettingsVolumetrics = HairSim.SettingsVolume;

	using __IMPL__SettingsGeometry = HairSim.SettingsGeometry;
	using __IMPL__SettingsRendering = HairSim.SettingsRendering;
	using __IMPL__SettingsPhysics = HairSim.SettingsPhysics;

	using __IMPL__SolverLODSelection = HairSim.SolverLODSelection;

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
			ref var data_IMPL_settingsExecutive = ref this.settingsExecutive;
			ref var data_IMPL_settingsEnvironment = ref this.settingsEnvironment;
			ref var data_IMPL_settingsVolumetrics = ref this.settingsVolumetrics;

			ref var data_IMPL_strandGroupDefaults = ref this.strandGroupDefaults;
			ref var data_IMPL_strandGroupSettings = ref this.strandGroupSettings;

			// migrate data_0_settingsSystem
			{
				ref readonly var in_0 = ref data_0_settingsSystem;

				// => data_IMPL_settingsExecutive
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __IMPL__SettingsExecutive out_IMPL)
					{
						static __IMPL__SettingsExecutive.UpdateMode TranslateUpdateMode(__0__SettingsSystem.UpdateMode x) => (__IMPL__SettingsExecutive.UpdateMode)x;
						static __IMPL__SettingsExecutive.UpdateRate TranslateSimulationRate(__0__SettingsSystem.SimulationRate x)
						{
							switch (x)
							{
								default:
								case __0__SettingsSystem.SimulationRate.Fixed30Hz: return __IMPL__SettingsExecutive.UpdateRate.Fixed30Hz;
								case __0__SettingsSystem.SimulationRate.Fixed60Hz: return __IMPL__SettingsExecutive.UpdateRate.Fixed60Hz;
								case __0__SettingsSystem.SimulationRate.Fixed120Hz: return __IMPL__SettingsExecutive.UpdateRate.Fixed120Hz;
								case __0__SettingsSystem.SimulationRate.CustomTimeStep: return __IMPL__SettingsExecutive.UpdateRate.CustomTimeStep;
							}
						}

						out_IMPL.updateMode = TranslateUpdateMode(in_0.updateMode);
						out_IMPL.updateSimulation = in_0.simulation;
						out_IMPL.updateSimulationRate = TranslateSimulationRate(in_0.simulationRate);
						out_IMPL.updateSimulationInEditor = in_0.simulationInEditor;
						out_IMPL.updateTimeStep = in_0.simulationTimeStep;
						out_IMPL.updateStepsMin = in_0.stepsMin;
						out_IMPL.updateStepsMinValue = in_0.stepsMinValue;
						out_IMPL.updateStepsMax = in_0.stepsMax;
						out_IMPL.updateStepsMaxValue = in_0.stepsMaxValue;
					}

					TransferSettingsSystem(in_0, ref data_IMPL_settingsExecutive);
				}

				// => data_IMPL_strandGroupDefaults.settingsGeometry
				// => data_IMPL_strandGroupSettings[].settingsGeometry
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __IMPL__SettingsGeometry out_IMPL)
					{
						static __IMPL__SettingsGeometry.BoundsMode TranslateBoundsMode(__0__SettingsSystem.BoundsMode x) => (__IMPL__SettingsGeometry.BoundsMode)x;

						out_IMPL.boundsMode = TranslateBoundsMode(in_0.boundsMode);
						out_IMPL.boundsCenter = in_0.boundsCenter;
						out_IMPL.boundsExtent = in_0.boundsExtent;
						out_IMPL.boundsScale = in_0.boundsScale;
						out_IMPL.boundsScaleValue = in_0.boundsScaleValue;
					}

					TransferSettingsSystem(in_0, ref data_IMPL_strandGroupDefaults.settingsGeometry);
					
					for (int i = 0; i != (data_IMPL_strandGroupSettings?.Length ?? 0); i++)
					{
						TransferSettingsSystem(in_0, ref data_IMPL_strandGroupSettings[i].settingsGeometry);
					}
				}

				// => data_IMPL_strandGroupDefaults.settingsPhysics
				// => data_IMPL_strandGroupSettings[].settingsPhysics
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __IMPL__SettingsPhysics out_IMPL)
					{
						static __IMPL__SolverLODSelection TranslateLODSelection(__0__SettingsSystem.LODSelection x)
						{
							switch (x)
							{
								default:
								case __0__SettingsSystem.LODSelection.Automatic: return __IMPL__SolverLODSelection.AutomaticPerGroup;
								case __0__SettingsSystem.LODSelection.Fixed: return __IMPL__SolverLODSelection.Manual;
							}
						};

						out_IMPL.kLODSelection = TranslateLODSelection(in_0.kLODSearch);
						out_IMPL.kLODSelectionValue = in_0.kLODSearchValue;
					}

					TransferSettingsSystem(in_0, ref data_IMPL_strandGroupDefaults.settingsPhysics);
					
					for (int i = 0; i != (data_IMPL_strandGroupSettings?.Length ?? 0); i++)
					{
						TransferSettingsSystem(in_0, ref data_IMPL_strandGroupSettings[i].settingsPhysics);
					}
				}

				// => data_IMPL_strandGroupDefaults.settingsRendering
				// => data_IMPL_strandGroupSettings[].settingsRendering
				{
					static void TransferSettingsSystem(in __0__SettingsSystem in_0, ref __IMPL__SettingsRendering out_IMPL)
					{
						static __IMPL__SettingsRendering.Renderer TranslateStrandRenderer(__0__SettingsSystem.StrandRenderer x)
						{
							switch (x)
							{
								case __0__SettingsSystem.StrandRenderer.Disabled: return __IMPL__SettingsRendering.Renderer.Disabled;
								default:
								case __0__SettingsSystem.StrandRenderer.BuiltinLines: return __IMPL__SettingsRendering.Renderer.BuiltinLines;
								case __0__SettingsSystem.StrandRenderer.BuiltinStrips: return __IMPL__SettingsRendering.Renderer.BuiltinStrips;
								case __0__SettingsSystem.StrandRenderer.BuiltinTubes: return __IMPL__SettingsRendering.Renderer.BuiltinTubes;
								case __0__SettingsSystem.StrandRenderer.HDRPHighQualityLines: return __IMPL__SettingsRendering.Renderer.HDRPHighQualityLines;
							}
						};

						out_IMPL.renderer = TranslateStrandRenderer(in_0.strandRenderer);
#if HAS_PACKAGE_UNITY_HDRP_15_0_2
						out_IMPL.rendererGroup = in_0.strandRendererGroup;
#endif
						out_IMPL.rendererLayers = in_0.strandLayers;
						out_IMPL.rendererShadows = in_0.strandShadows;
						out_IMPL.motionVectors = in_0.motionVectors;
					}

					TransferSettingsSystem(in_0, ref data_IMPL_strandGroupDefaults.settingsRendering);

					for (int i = 0; i != (data_IMPL_strandGroupSettings?.Length ?? 0); i++)
					{
						TransferSettingsSystem(in_0, ref data_IMPL_strandGroupSettings[i].settingsRendering);
					}
				}
			}

			// migrate data_0_settingsVolume
			{
				ref readonly var in_0 = ref data_0_settingsVolume;

				// => data_IMPL_settingsEnvironment
				{
					static void TransferSettingsVolume(in __0__VolumeSettings in_0, ref __IMPL__SettingsEnvironment out_IMPL)
					{
						static __IMPL__SettingsEnvironment.BoundaryCaptureMode TranslateCollectMode(__0__VolumeSettings.CollectMode x) => (__IMPL__SettingsEnvironment.BoundaryCaptureMode)x;

						out_IMPL.boundaryCapture = in_0.boundariesCollect;
						out_IMPL.boundaryCaptureMode = TranslateCollectMode(in_0.boundariesCollectMode);
						out_IMPL.boundaryCaptureLayer = in_0.boundariesCollectLayer;
						out_IMPL.boundaryResident = in_0.boundariesPriority;
						out_IMPL.defaultSolidMargin = in_0.collisionMargin;
						out_IMPL.defaultSolidDensity = in_0.probeOcclusionSolidDensity;
					}

					TransferSettingsVolume(in_0, ref data_IMPL_settingsEnvironment);
				}

				// => data_IMPL_settingsVolumetrics
				{
					static void TransferSettingsVolume(in __0__VolumeSettings in_0, ref __IMPL__SettingsVolumetrics out_IMPL)
					{
						static __IMPL__SettingsVolumetrics.GridPrecision TranslateGridPrecision(__0__VolumeSettings.GridPrecision x) => (__IMPL__SettingsVolumetrics.GridPrecision)x;
						static __IMPL__SettingsVolumetrics.SplatMethod TranslateSplatMethod(__0__VolumeSettings.SplatMethod x) => (__IMPL__SettingsVolumetrics.SplatMethod)x;
						static __IMPL__SettingsVolumetrics.PressureSolution TranslatePressureSolution(__0__VolumeSettings.PressureSolution x) => (__IMPL__SettingsVolumetrics.PressureSolution)x;
						static __IMPL__SettingsVolumetrics.RestDensity TranslateTargetDensity(__0__VolumeSettings.TargetDensity x) => (__IMPL__SettingsVolumetrics.RestDensity)x;
						static __IMPL__SettingsVolumetrics.OcclusionMode TranslateOcclusionMode(__0__VolumeSettings.OcclusionMode x) => (__IMPL__SettingsVolumetrics.OcclusionMode)x;

						out_IMPL.gridPrecision = TranslateGridPrecision(in_0.gridPrecision);
						out_IMPL.gridResolution = (uint)in_0.gridResolution;

						out_IMPL.splatMethod = TranslateSplatMethod(in_0.splatMethod);
						out_IMPL.splatClusters = in_0.splatClusters;

						out_IMPL.pressureIterations = (uint)in_0.pressureIterations;
						out_IMPL.pressureSolution = TranslatePressureSolution(in_0.pressureSolution);
						out_IMPL.restDensity = TranslateTargetDensity(in_0.targetDensity);
						out_IMPL.restDensityInfluence = in_0.targetDensityInfluence;

						out_IMPL.scatteringProbe = in_0.scatteringProbe;
						out_IMPL.scatteringProbeCellSubsteps = in_0.scatteringProbeCellSubsteps;
						out_IMPL.scatteringProbeBias = in_0.scatteringProbeBias;
						out_IMPL.probeSamplesTheta = in_0.probeSamplesTheta;
						out_IMPL.probeSamplesPhi = in_0.probeSamplesPhi;
						out_IMPL.probeOcclusion = in_0.probeOcclusion;
						out_IMPL.probeOcclusionMode = TranslateOcclusionMode(in_0.probeOcclusionMode);
						out_IMPL.probeOcclusionSolidDensity = in_0.probeOcclusionSolidDensity;

						out_IMPL.windPropagation = in_0.windPropagation;
						out_IMPL.windPropagationCellSubsteps = in_0.windPropagationCellSubsteps;
						out_IMPL.windOcclusion = in_0.windOcclusion;
						out_IMPL.windOcclusionMode = TranslateOcclusionMode(in_0.windOcclusionMode);
						out_IMPL.windDepth = in_0.windExtinction;
					}

					TransferSettingsVolume(in_0, ref data_IMPL_settingsVolumetrics);
				}
			}

			// migrate data_0_strandGroupDefaults
			// migrate data_0_strandGroupSettings[]
			{
				// *.settingsStrands
				{
					// => data_IMPL_strandGroupDefaults.settingsRendering
					// => data_IMPL_strandGroupSettings[].settingsRendering
					{
						static void TransferSettingsStrands(in __0__SettingsStrands in_0, ref __IMPL__SettingsRendering out_IMPL)
						{
							out_IMPL.material = in_0.material;
							out_IMPL.materialAsset = in_0.materialValue;
						}

						TransferSettingsStrands(data_0_strandGroupDefaults.settingsStrands, ref data_IMPL_strandGroupDefaults.settingsRendering);

						for (int i = 0; i != (data_IMPL_strandGroupSettings?.Length ?? 0); i++)
						{
							TransferSettingsStrands(data_0_strandGroupSettings[i].settingsStrands, ref data_IMPL_strandGroupSettings[i].settingsRendering);
							data_IMPL_strandGroupSettings[i].settingsRenderingToggle = data_0_strandGroupSettings[i].settingsStrandsToggle;
						}
					}

					// => data_IMPL_strandGroupDefaults.settingsGeometry
					// => data_IMPL_strandGroupSettings[].settingsGeometry
					{
						static void TransferSettingsStrands(in __0__SettingsStrands in_0, ref __IMPL__SettingsGeometry out_IMPL)
						{
							static __IMPL__SettingsGeometry.StrandScale TranslateStrandScale(__0__SettingsStrands.StrandScale x) => (__IMPL__SettingsGeometry.StrandScale)x;
							static __IMPL__SettingsGeometry.StagingPrecision TranslateStagingPrecision(__0__SettingsStrands.StagingPrecision x) => (__IMPL__SettingsGeometry.StagingPrecision)x;

							out_IMPL.strandScale = TranslateStrandScale(in_0.strandScale);
							out_IMPL.strandDiameter = true;
							out_IMPL.strandDiameterValue = in_0.strandDiameter;
							out_IMPL.strandSeparation = in_0.strandMargin;

							out_IMPL.stagingPrecision = TranslateStagingPrecision(in_0.stagingPrecision);
							out_IMPL.stagingSubdivision = (in_0.stagingSubdivision > 0);
							out_IMPL.stagingSubdivisionCount = (in_0.stagingSubdivision > 0) ? in_0.stagingSubdivision : 1;
						}

						TransferSettingsStrands(data_0_strandGroupDefaults.settingsStrands, ref data_IMPL_strandGroupDefaults.settingsGeometry);

						for (int i = 0; i != (data_IMPL_strandGroupSettings?.Length ?? 0); i++)
						{
							TransferSettingsStrands(data_0_strandGroupSettings[i].settingsStrands, ref data_IMPL_strandGroupSettings[i].settingsGeometry);
							data_IMPL_strandGroupSettings[i].settingsGeometryToggle = data_0_strandGroupSettings[i].settingsStrandsToggle;
						}
					}
				}

				// *.settingsSolver
				{
					// => data_IMPL_strandGroupDefaults.settingsPhysics
					// => data_IMPL_strandGroupSettings[].settingsPhysics
					{
						static void TransferSettingsSolver(in __0__SolverSettings in_0, ref __IMPL__SettingsPhysics out_IMPL)
						{
							static __IMPL__SettingsPhysics.Solver TranslateMethod(__0__SolverSettings.Method x) => (__IMPL__SettingsPhysics.Solver)x;
							static __IMPL__SettingsPhysics.TimeInterval TranslateTimeInterval(__0__SolverSettings.TimeInterval x) => (__IMPL__SettingsPhysics.TimeInterval)x;
							static __IMPL__SettingsPhysics.LocalCurvatureMode TranslateLocalCurvatureMode(__0__SolverSettings.LocalCurvatureMode x) => (__IMPL__SettingsPhysics.LocalCurvatureMode)x;
							static __IMPL__SettingsPhysics.LocalShapeMode TranslateLocalShapeMode(__0__SolverSettings.LocalShapeMode x) => (__IMPL__SettingsPhysics.LocalShapeMode)x;

							out_IMPL.solver = TranslateMethod(in_0.method);
							out_IMPL.solverSubsteps = (uint)in_0.substeps;
							out_IMPL.constraintIterations = (uint)in_0.iterations;
							out_IMPL.constraintStiffness = in_0.stiffness;
							out_IMPL.constraintSOR = in_0.kSOR;

							out_IMPL.dampingLinear = in_0.damping;
							out_IMPL.dampingLinearFactor = in_0.dampingFactor;
							out_IMPL.dampingLinearInterval = TranslateTimeInterval(in_0.dampingInterval);
							out_IMPL.dampingAngular = in_0.angularDamping;
							out_IMPL.dampingAngularFactor = in_0.angularDampingFactor;
							out_IMPL.dampingAngularInterval = TranslateTimeInterval(in_0.angularDampingInterval);
							out_IMPL.cellPressure = in_0.cellPressure;
							out_IMPL.cellVelocity = in_0.cellVelocity;
							out_IMPL.cellExternal = in_0.cellForces;
							out_IMPL.gravity = 1.0f;// global scale and rotation moves to data_IMPL_settingsEnvironment

							out_IMPL.boundaryCollision = in_0.boundaryCollision;
							out_IMPL.boundaryCollisionFriction = in_0.boundaryCollisionFriction;
							out_IMPL.distance = in_0.distance;
							out_IMPL.distanceLRA = in_0.distanceLRA;
							out_IMPL.distanceFTL = in_0.distanceFTL;
							out_IMPL.distanceFTLCorrection = in_0.distanceFTLCorrection;
							out_IMPL.localCurvature = in_0.localCurvature;
							out_IMPL.localCurvatureMode = TranslateLocalCurvatureMode(in_0.localCurvatureMode);
							out_IMPL.localCurvatureValue = in_0.localCurvatureValue;
							out_IMPL.localShape = in_0.localShape;
							out_IMPL.localShapeMode = TranslateLocalShapeMode(in_0.localShapeMode);
							out_IMPL.localShapeInfluence = in_0.localShapeInfluence;
							out_IMPL.localShapeBias = in_0.localShapeBias;
							out_IMPL.localShapeBiasValue = in_0.localShapeBiasValue;

							out_IMPL.globalPosition = in_0.globalPosition;
							out_IMPL.globalPositionInfluence = in_0.globalPositionInfluence;
							out_IMPL.globalPositionInterval = TranslateTimeInterval(in_0.globalPositionInterval);
							out_IMPL.globalRotation = in_0.globalRotation;
							out_IMPL.globalRotationInfluence = in_0.globalRotationInfluence;
							out_IMPL.globalFade = in_0.globalFade;
							out_IMPL.globalFadeOffset = in_0.globalFadeOffset;
							out_IMPL.globalFadeExtent = in_0.globalFadeExtent;
						}

						TransferSettingsSolver(data_0_strandGroupDefaults.settingsSolver, ref data_IMPL_strandGroupDefaults.settingsPhysics);

						for (int i = 0; i != (data_IMPL_strandGroupSettings?.Length ?? 0); i++)
						{
							TransferSettingsSolver(data_0_strandGroupSettings[i].settingsSolver, ref data_IMPL_strandGroupSettings[i].settingsPhysics);
							data_IMPL_strandGroupSettings[i].settingsPhysicsToggle = data_0_strandGroupSettings[i].settingsSolverToggle;
						}
					}

					// => data_IMPL_settingsEnvironment
					{
						static void TransferSettingsSolver(in __0__SolverSettings in_0, ref __IMPL__SettingsEnvironment out_IMPL)
						{
							out_IMPL.gravityScale = in_0.gravity;
							out_IMPL.gravityRotation = Quaternion.Euler(in_0.gravityRotation);
						}

						TransferSettingsSolver(data_0_strandGroupDefaults.settingsSolver, ref data_IMPL_settingsEnvironment);
					}
				}
			}
		}

		[Serializable]
		// captured @ 46a8b132
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
		// captured @ 46a8b132
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
		// captured @ 46a8b132
		struct __0__GroupSettings
		{
			//public List<GroupAssetReference> groupAssetReferences;

			//public __0__SettingsSkinning settingsSkinning;
			//public bool settingsSkinningToggle;

			public __0__SettingsStrands settingsStrands;
			public bool settingsStrandsToggle;

			public __0__SolverSettings settingsSolver;
			public bool settingsSolverToggle;

			public static __0__GroupSettings defaults => new __0__GroupSettings()
			{
				//groupAssetReferences = new List<GroupAssetReference>(1),

				//settingsSkinning = SettingsSkinning.defaults,
				//settingsSkinningToggle = false,

				settingsStrands = __0__SettingsStrands.defaults,
				settingsStrandsToggle = false,

				settingsSolver = __0__SolverSettings.defaults,
				settingsSolverToggle = false,
			};
		}

		[Serializable]
		// captured @ 46a8b132
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
		// captured @ 46a8b132
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
