using System;
using UnityEngine;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
#endif

namespace Unity.DemoTeam.Hair
{
	public partial class HairInstance
	{
		[Serializable]
		public struct SettingsExecutive
		{
			public enum UpdateMode
			{
				BuiltinEvent	= 0,
				ExternalCall	= 1,
			}

			public enum UpdateRate
			{
				Fixed30Hz		= 0,
				Fixed60Hz		= 1,
				Fixed90Hz		= 2,
				Fixed120Hz		= 3,
				CustomTimeStep	= 4,
			}

			[LineHeader("Scheduling")]

			[Tooltip("Specifies whether updates are scheduled and dispatched via builtin event, or scheduled via external call (with caller being responsible for subsequent dispatch)")]
			public UpdateMode updateMode;

			[LineHeader("Simulation")]

			[ToggleGroup, Tooltip("Enable simulation")]
			public bool updateSimulation;
			[ToggleGroupItem, Tooltip("Simulation update rate")]
			public UpdateRate updateSimulationRate;
			[ToggleGroupItem(withLabel = true), Tooltip("Enable simulation in Edit Mode")]
			public bool updateSimulationInEditor;
			[VisibleIf(nameof(updateSimulationRate), UpdateRate.CustomTimeStep), Tooltip("Simulation time step (in seconds)")]
			public float updateTimeStep;
			[ToggleGroup, Tooltip("Enable minimum number of simulation steps per rendered frame")]
			public bool updateStepsMin;
			[ToggleGroupItem, Tooltip("Minimum number of simulation steps per rendered frame")]
			public int updateStepsMinValue;
			[ToggleGroup, Tooltip("Enable maximum number of simulation steps per rendered frame")]
			public bool updateStepsMax;
			[ToggleGroupItem, Tooltip("Maximum number of simulation steps per rendered frame")]
			public int updateStepsMaxValue;

			public static readonly SettingsExecutive defaults = new SettingsExecutive()
			{
				updateMode = UpdateMode.BuiltinEvent,

				updateSimulation = true,
				updateSimulationRate = UpdateRate.Fixed30Hz,
				updateSimulationInEditor = true,
				updateTimeStep = 1.0f / 100.0f,
				updateStepsMin = false,
				updateStepsMinValue = 1,
				updateStepsMax = true,
				updateStepsMaxValue = 4,
			};
		}

		[Serializable]
		public struct SettingsSkinning
		{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
			[LineHeader("Skinning")]

			[ToggleGroup]
			public bool rootsAttach;
			[ToggleGroupItem]
			public SkinAttachmentTarget rootsAttachTarget;
			[HideInInspector]
			public PrimarySkinningBone rootsAttachTargetBone;
#endif

			public static readonly SettingsSkinning defaults = new SettingsSkinning()
			{
#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
				rootsAttach = false,
				rootsAttachTarget = null,
#endif
			};
		}
	}
}
