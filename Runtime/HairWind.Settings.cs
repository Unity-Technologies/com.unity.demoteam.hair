using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Unity.DemoTeam.Hair
{
	public partial class HairWind
	{
		[Serializable]
		public struct SettingsEmitter
		{
			public enum Mode
			{
				BindToComponent,
				Standalone,
			}

			public enum Type
			{
				Directional,// binds to component -> WindZone (Directional)
				Spherical,  // binds to component -> WindZone (Spherical)
				Turbine,
				Any,
			}

			[LineHeader("Mode")]

			public Mode mode;
			public Type type;

			public static readonly SettingsEmitter defaults = new SettingsEmitter
			{
				mode = Mode.Standalone,
				type = Type.Directional,
			};
		}

		[Serializable]
		public struct SettingsDirectional
		{
			public static readonly SettingsDirectional defaults = new SettingsDirectional { };
		}

		[Serializable]
		public struct SettingsSpherical
		{
			public static readonly SettingsSpherical defaults = new SettingsSpherical { };
		}

		[Serializable]
		public struct SettingsTurbine
		{
			[LineHeader("Geometry")]

			[Tooltip("Turbine base width (in meters)")]
			public float baseWidth;
			[Tooltip("Turbine nozzle width (in meters)")]
			public float nozzleWidth;
			[Tooltip("Turbine nozzle offset (in meters)")]
			public float nozzleOffset;

			public static readonly SettingsTurbine defaults = new SettingsTurbine
			{
				baseWidth = 1.0f,
				nozzleWidth = 1.5f,
				nozzleOffset = 1.0f,
			};
		}

		[Serializable]
		public struct SettingsFlow
		{
			public enum JitterSpace
			{
				Planar,
				Global,
			}

			[LineHeader("Flow")]

			[Tooltip("Base speed (in meters per second)")]
			public float baseSpeed;
			[Tooltip("Pulse amplitude (in meters per second)")]
			public float pulseAmplitude;
			[Tooltip("Pulse frequency (in cycles per second)")]
			public float pulseFrequency;

			[LineHeader("Noise")]

			[ToggleGroup, Tooltip("Enable noise-based timing jitter (to reduce uniformity of pulses)")]
			public bool timingJitter;
			[ToggleGroupItem, Tooltip("Controls whether the timing jitter varies over the emitter surface only (planar) or over the entire volume (global)")]
			public JitterSpace timingJitterSpace;
			[ToggleGroupItem, Range(0.0f, 4.0f), Tooltip("Timing jitter displacement (in pulse cycles)")]
			public float timingJitterDisplacement;
			[EditableIf(nameof(timingJitter), true), Tooltip("Timing jitter resolution (in noise cells per meter)")]
			public float timingJitterResolution;

			public static readonly SettingsFlow defaults = new SettingsFlow
			{
				baseSpeed = 0.25f,
				pulseAmplitude = 1.0f,
				pulseFrequency = 0.25f,

				timingJitter = true,
				timingJitterSpace = JitterSpace.Planar,
				timingJitterDisplacement = 0.25f,
				timingJitterResolution = 1.0f,
			};
		}
	}
}