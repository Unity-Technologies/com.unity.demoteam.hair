using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public partial class HairInstance
	{
		[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
		[Obsolete("Renamed HairInstance.settingsSystem (UnityUpgradable) -> settingsExecutive", true)]
		public SettingsSystem settingsSystem;

		[StructLayout(LayoutKind.Explicit)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Renamed HairInstance.SettingsSystem (UnityUpgradable) -> HairInstance.SettingsExecutive", true)]
		public struct SettingsSystem
		{
			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.simulation (UnityUpgradable) -> updateSimulation", true)]
			public bool simulation;

			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.simulationRate (UnityUpgradable) -> updateSimulationRate", true)]
			public SettingsExecutive.UpdateRate simulationRate;

			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.simulationInEditor (UnityUpgradable) -> updateSimulationInEditor", true)]
			public bool simulationInEditor;

			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.simulationTimeStep (UnityUpgradable) -> updateTimeStep", true)]
			public float simulationTimeStep;

			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.stepsMin (UnityUpgradable) -> updateStepsMin", true)]
			public bool stepsMin;

			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.stepsMinValue (UnityUpgradable) -> updateStepsMinValue", true)]
			public int stepsMinValue;

			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.stepsMax (UnityUpgradable) -> updateStepsMax", true)]
			public bool stepsMax;

			[FieldOffset(0)]
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsSystem.stepsMaxValue (UnityUpgradable) -> updateStepsMaxValue", true)]
			public int stepsMaxValue;
		}
	}
}
