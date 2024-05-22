using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	[ExecuteAlways]
	public partial class HairWind : MonoBehaviour
	{
		public SettingsEmitter settingsEmitter = SettingsEmitter.defaults;
		public SettingsDirectional settingsDirectional = SettingsDirectional.defaults;
		public SettingsSpherical settingsSpherical = SettingsSpherical.defaults;
		public SettingsTurbine settingsTurbine = SettingsTurbine.defaults;
		public SettingsFlow settingsFlow = SettingsFlow.defaults;

		//--------------
		// runtime data

		public struct RuntimeData
		{
			public enum Type
			{
				Directional,
				Spherical,
				Turbine,
				Plane,
			}

			public Type type;
			public RuntimeTransform xform;
			public RuntimeEmitter emitter;
			public RuntimeGizmo gizmo;
		}

		public struct RuntimeTransform
		{
			public int handle;
		}

		public struct RuntimeEmitter
		{
			public Vector3 p;	// emitter origin
			public Vector3 n;	// emitter forward
			public float t0;	// emitter base offset
			public float h0;	// emitter base radius
			public float m;		// emitter slope

			public float v;		// flow speed
			public float A;		// flow pulse amplitude
			public float f;		// flow pulse frequency

			public float jd;	// jitter displacement
			public float jw;	// jitter resolution
			public float jp;	// jitter planar
		}

		public struct RuntimeGizmo
		{
			public Quaternion rotation;
			public float turbineBaseWidth;
			public float turbineNozzleWidth;
			public float turbineNozzleOffset;
		}

		//--------------------
		// runtime conversion

		public static SettingsFlow MakeSettingsFlow(WindZone windZone)
		{
			return new SettingsFlow
			{
				baseSpeed = windZone.windMain,
				pulseAmplitude = windZone.windPulseMagnitude,
				pulseFrequency = windZone.windPulseFrequency,

				timingJitter = false,
				timingJitterDisplacement = 0.0f,
				timingJitterResolution = 0.0f,
			};
		}

		public static RuntimeData GetRuntimeDirectional(WindZone windZone) => GetRuntimeDirectional(windZone.transform, MakeSettingsFlow(windZone), new SettingsDirectional { });
		public static RuntimeData GetRuntimeDirectional(Transform transform, in SettingsFlow flow, in SettingsDirectional directional)
		{
			return new RuntimeData
			{
				type = RuntimeData.Type.Directional,
				xform = new RuntimeTransform
				{
					handle = transform.GetInstanceID(),
				},
				emitter = new RuntimeEmitter
				{
					p = transform.position,
					n = transform.forward,

					t0 = float.MinValue,
					h0 = float.PositiveInfinity,
					m = 0.0f,

					v = flow.baseSpeed,
					A = flow.pulseAmplitude,
					f = flow.pulseFrequency,

					jd = flow.timingJitter ? flow.timingJitterDisplacement : 0.0f,
					jw = flow.timingJitterResolution,
					jp = (flow.timingJitterSpace == SettingsFlow.JitterSpace.Planar) ? 1.0f : 0.0f,
				},
				gizmo = new RuntimeGizmo
				{
					rotation = transform.rotation,
				},
			};
		}

		public static RuntimeData GetRuntimeSpherical(WindZone windZone) => GetRuntimeSpherical(windZone.transform, MakeSettingsFlow(windZone), new SettingsSpherical { });
		public static RuntimeData GetRuntimeSpherical(Transform transform, in SettingsFlow flow, in SettingsSpherical spherical)
		{
			return new RuntimeData
			{
				type = RuntimeData.Type.Spherical,
				xform = new RuntimeTransform
				{
					handle = transform.GetInstanceID(),
				},
				emitter = new RuntimeEmitter
				{
					p = transform.position,
					n = Vector3.zero,

					t0 = 0.0f,
					h0 = float.PositiveInfinity,
					m = float.PositiveInfinity,

					v = flow.baseSpeed,
					A = flow.pulseAmplitude,
					f = flow.pulseFrequency,

					jd = flow.timingJitter ? flow.timingJitterDisplacement : 0.0f,
					jw = flow.timingJitterResolution,
					jp = (flow.timingJitterSpace == SettingsFlow.JitterSpace.Planar) ? 1.0f : 0.0f,
				},
				gizmo = new RuntimeGizmo
				{
					rotation = transform.rotation,
				},
			};
		}

		public static RuntimeData GetRuntimeTurbine(Transform transform, in SettingsFlow flow, SettingsTurbine turbine)
		{
			var lossyScaleAbs = transform.lossyScale.Abs();
			var lossyScaleAbsMax = lossyScaleAbs.CMax();

			var t0 = 0.0f;
			var h0 = 0.5f * turbine.baseWidth;
			var m = 0.0f;
			{
				var dt = turbine.nozzleOffset;
				if (dt != 0.0f)
				{
					var dh = 0.5f * (turbine.nozzleWidth - turbine.baseWidth);
					if (dh != 0.0f)
					{
						// calc slope
						m = dh / dt;

						// calc base offset
						t0 = h0 / m;
					}
				}
			}

			t0 *= lossyScaleAbsMax;
			h0 *= lossyScaleAbsMax;

			return new RuntimeData
			{
				type = RuntimeData.Type.Turbine,
				xform = new RuntimeTransform
				{
					handle = transform.GetInstanceID(),
				},
				emitter = new RuntimeEmitter
				{
					p = transform.position - t0 * transform.forward,
					n = transform.forward,

					t0 = t0,
					h0 = h0,
					m = m,

					v = flow.baseSpeed,
					A = flow.pulseAmplitude,
					f = flow.pulseFrequency,

					jd = flow.timingJitter ? flow.timingJitterDisplacement : 0.0f,
					jw = flow.timingJitterResolution,
					jp = (flow.timingJitterSpace == SettingsFlow.JitterSpace.Planar) ? 1.0f : 0.0f,
				},
				gizmo = new RuntimeGizmo
				{
					rotation = transform.rotation,
					turbineNozzleWidth = turbine.nozzleWidth * lossyScaleAbsMax,
					turbineNozzleOffset = turbine.nozzleOffset * lossyScaleAbsMax,
				},
			};
		}

		//-----------
		// accessors

		public static bool TryGetData(HairWind wind, ref RuntimeData data)
		{
			if (wind.settingsEmitter.mode == SettingsEmitter.Mode.BindToComponent)
			{
				if (TryGetMatchingComponent(wind, out var component))
				{
					return TryGetComponentData(component, ref data);
				}
			}
			else
			{
				return TryGetStandaloneData(wind, ref data);
			}
			return false;
		}

		public static bool TryGetMatchingComponent(HairWind wind, SettingsEmitter.Type type, out Component component)
		{
			var windZone = wind.GetComponent<WindZone>();
			if (windZone == null)
				windZone = null;

			switch (type)
			{
				case SettingsEmitter.Type.Directional:
					component = (windZone?.mode == WindZoneMode.Directional ? windZone : null); break;
				case SettingsEmitter.Type.Spherical:
					component = (windZone?.mode == WindZoneMode.Spherical ? windZone : null); break;
				default:
					component = null; break;
			}
			return (component != null);
		}

		public static bool TryGetMatchingComponent(HairWind wind, out Component component)
		{
			if (wind.settingsEmitter.type == SettingsEmitter.Type.Any)
			{
				return
					TryGetMatchingComponent(wind, SettingsEmitter.Type.Directional, out component) ||
					TryGetMatchingComponent(wind, SettingsEmitter.Type.Spherical, out component);
			}
			else
			{
				return TryGetMatchingComponent(wind, wind.settingsEmitter.type, out component);
			}
		}

		public static bool TryGetComponentData(Component component, ref RuntimeData data)
		{
			var windZone = component as WindZone;
			if (windZone == null)
				windZone = component.GetComponent<WindZone>();

			if (windZone != null)
			{
				switch (windZone.mode)
				{
					case WindZoneMode.Directional:
						data = GetRuntimeDirectional(windZone); return true;
					case WindZoneMode.Spherical:
						data = GetRuntimeSpherical(windZone); return true;
				}
			}
			return false;
		}

		public static bool TryGetStandaloneData(HairWind wind, ref RuntimeData data)
		{
			switch (wind.settingsEmitter.type)
			{
				case SettingsEmitter.Type.Directional:
					data = GetRuntimeDirectional(wind.transform, wind.settingsFlow, wind.settingsDirectional); return true;
				case SettingsEmitter.Type.Spherical:
					data = GetRuntimeSpherical(wind.transform, wind.settingsFlow, wind.settingsSpherical); return true;
				case SettingsEmitter.Type.Turbine:
					data = GetRuntimeTurbine(wind.transform, wind.settingsFlow, wind.settingsTurbine); return true;
				default:
					return false;
			}
		}
	}
}