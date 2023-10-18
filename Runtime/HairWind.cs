using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Unity.DemoTeam.Hair
{
	[ExecuteAlways]
	public class HairWind : MonoBehaviour
	{
		public static HashSet<HairWind> s_emitters = new HashSet<HairWind>();

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

		public SettingsEmitter settingsEmitter = SettingsEmitter.defaults;
		public SettingsDirectional settingsDirectional = SettingsDirectional.defaults;
		public SettingsSpherical settingsSpherical = SettingsSpherical.defaults;
		public SettingsTurbine settingsTurbine = SettingsTurbine.defaults;
		public SettingsFlow settingsFlow = SettingsFlow.defaults;

		private void OnEnable()
		{
			s_emitters.Add(this);
		}

		private void OnDisable()
		{
			s_emitters.Remove(this);
		}

		//-----------------------
		// runtime data: emitter

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
			};
		}

		public static RuntimeData GetRuntimeSpherical(WindZone windZone) => GetRuntimeSpherical(windZone.transform, MakeSettingsFlow(windZone), new SettingsSpherical { });
		public static RuntimeData GetRuntimeSpherical(Transform transform, in SettingsFlow flow, in SettingsSpherical spherical)
		{
			return new RuntimeData
			{
				type = RuntimeData.Type.Spherical,
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
				}
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
				}
			};
		}

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
			public RuntimeEmitter emitter;
		}

		//-----------
		// accessors

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

		//------------------
		// debugging gizmos

		public void OnDrawGizmosSelected()
		{
			DrawGizmos(Time.time, Time.deltaTime, selected: true);
		}

		public void DrawGizmos(float t, float dt, bool selected = false)
		{
			var data = new RuntimeData();

			if (TryGetData(this, ref data))
			{
				DrawGizmosRuntimeEmitter(data, selected ? 1.0f : 0.5f);

				if (selected)
				{
					if (isActiveAndEnabled)
					{
						DrawGizmosRuntimeEmitterFlow(data, selected ? 1.0f : 0.5f, 256, t, dt);
					}

					//DrawGizmosRuntimeEmitterGrid(data, 16);
				}
			}
		}

		public void DrawGizmosRuntimeEmitter(in RuntimeData data, float opacity)
		{
			var lossyScaleAbs = transform.lossyScale.Abs();
			var lossyScaleAbsMax = lossyScaleAbs.CMax();

			Gizmos.color = Color.Lerp(Color.clear, Color.cyan, opacity);
			Gizmos.matrix = Matrix4x4.identity;

			switch (data.type)
			{
				case RuntimeData.Type.Directional:
					{
						var p = data.emitter.p - data.emitter.n * 0.5f;
						var q = p + data.emitter.n;
						var r = p + data.emitter.n * 0.9f;

						var ux = transform.right;
						var uy = transform.up;

						var r00 = r + ux * 0.1f;
						var r01 = r + uy * 0.1f;
						var r10 = r - ux * 0.1f;
						var r11 = r - uy * 0.1f;

						Gizmos.DrawLine(p, q);
						Gizmos.DrawLine(q, r00);
						Gizmos.DrawLine(q, r01);
						Gizmos.DrawLine(q, r10);
						Gizmos.DrawLine(q, r11);
					}
					break;

				case RuntimeData.Type.Spherical:
					{
						Gizmos.matrix = Matrix4x4.TRS(data.emitter.p, transform.rotation, Vector3.one);
						Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
					}
					break;

				case RuntimeData.Type.Turbine:
					{
						var t0 = data.emitter.t0;
						var t1 = t0 + settingsTurbine.nozzleOffset * lossyScaleAbsMax;

						var h0 = data.emitter.h0;
						var h1 = 0.5f * settingsTurbine.nozzleWidth * lossyScaleAbsMax;

						var p0 = data.emitter.p + data.emitter.n * t0;
						var p1 = data.emitter.p + data.emitter.n * t1;

						var ux = transform.right;
						var uy = transform.up;

						Gizmos.DrawRay(p0 + uy * h0, (data.emitter.n + data.emitter.m * uy) * (t1 - t0));
						Gizmos.DrawRay(p0 - uy * h0, (data.emitter.n - data.emitter.m * uy) * (t1 - t0));
						Gizmos.DrawRay(p0 + ux * h0, (data.emitter.n + data.emitter.m * ux) * (t1 - t0));
						Gizmos.DrawRay(p0 - ux * h0, (data.emitter.n - data.emitter.m * ux) * (t1 - t0));

						Gizmos.matrix = Matrix4x4.TRS(p0, transform.rotation, Vector3.one);
						DrawGizmosWireCircleZ(Vector3.zero, h0);

						Gizmos.matrix = Matrix4x4.TRS(p1, transform.rotation, Vector3.one);
						DrawGizmosWireCircleZ(Vector3.zero, h1);
					}
					break;
			}
		}

		public void DrawGizmosRuntimeEmitterFlow(in RuntimeData data, float opacity, uint samples, float t, float dt)
		{
			dt = Mathf.Clamp(dt, 1.0f / 120.0f, 1.0f / 15.0f);

			var lossyScaleAbs = transform.lossyScale.Abs();
			var lossyScaleAbsMax = lossyScaleAbs.CMax();

			Gizmos.color = Color.Lerp(Color.clear, Color.cyan, opacity * 0.6f);
			Gizmos.matrix = Matrix4x4.identity;

			var size = 3.0f;
			var ppos = this.transform.position;

			var Lv = data.emitter.v + 0.5f * data.emitter.A;//TODO replace average with something that is more meaningful visually
			var Lt = 3.0f;// flow line lifetime
			var Ld = math.abs(Lv * Lt);
			var Nt = math.frac(t / Lt);

			var ux = this.transform.right;
			var uy = this.transform.up;

			for (uint i = 0; i != samples; i++)
			{
				var j = (uint)(i + samples * Nt) % samples;
				var R = Mathematics.Random.CreateFromIndex(j);

				var p = ppos;
				{
					switch (data.type)
					{
						case RuntimeData.Type.Directional:
							{
								var k = R.NextFloat3();
								p.x += (k.x * 2.0f - 1.0f) * size - (0.5f * Ld) * data.emitter.n.x;
								p.y += (k.y * 2.0f - 1.0f) * size - (0.5f * Ld) * data.emitter.n.y;
								p.z += (k.z * 2.0f - 1.0f) * size - (0.5f * Ld) * data.emitter.n.z;
							}
							break;

						case RuntimeData.Type.Spherical:
							{
								var k = R.NextFloat3Direction();
								p.x += 1e-4f * k.x;
								p.y += 1e-4f * k.y;
								p.z += 1e-4f * k.z;
							}
							break;

						case RuntimeData.Type.Turbine:
							{
								var k = R.NextFloat2();
								var h = 0.5f * settingsTurbine.baseWidth * lossyScaleAbsMax + 1e-4f * data.emitter.m;
								math.sincos(2.0f * Mathf.PI * k.x, out var sina, out var cosa);
								p.x += (k.y * cosa * h) * ux.x + (k.y * sina * h) * uy.x + (1e-4f) * data.emitter.n.x;
								p.y += (k.y * cosa * h) * ux.y + (k.y * sina * h) * uy.y + (1e-4f) * data.emitter.n.y;
								p.z += (k.y * cosa * h) * ux.z + (k.y * sina * h) * uy.z + (1e-4f) * data.emitter.n.z;
							}
							break;
					}
				}

				var n = (data.emitter.m == 0.0f) ? data.emitter.n : (Vector3.Normalize(p - data.emitter.p));
				var s = math.frac(Nt + (float)j / samples);

				if (Vector3.Dot(n, data.emitter.n) < 0.0f)
				{
					n = -n;
				}

				if (Lv < 0.0f)
				{
					Gizmos.DrawRay(p + n * (Ld - Ld * s), n * -(Lv * dt));
				}
				else
				{
					Gizmos.DrawRay(p + n * Ld * s, n * (Lv * dt));
				}
			}
		}

		void DrawGizmosRuntimeEmitterGrid(in RuntimeData data, uint samples)
		{
			Gizmos.color = Color.cyan;
			Gizmos.matrix = Matrix4x4.identity;

			var colorPass = Color.green;
			var colorFail = Color.cyan;
			{
				colorFail.a = 0.1f;
			}

			var size = 4.0f;
			var step = size / (samples - 1);

			var pext = (0.5f * size) * Vector3.one;
			var pmin = this.transform.position - pext;

			for (uint i = 0; i != samples; i++)
			{
				for (uint j = 0; j != samples; j++)
				{
					for (uint k = 0; k != samples; k++)
					{
						var p = pmin;
						{
							p.x += step * i;
							p.y += step * j;
							p.z += step * k;
						}

						var r = p - data.emitter.p;
						var t = Vector3.Dot(data.emitter.n, r);
						var h = data.emitter.m * (t - data.emitter.t0) + data.emitter.h0;

						var a = r - data.emitter.n * t;
						var b = Vector3.SqrMagnitude(a);

						var bInside = (b <= h * h);
						var bFacing = (t >= data.emitter.t0);

						var n = (0.5f * step) * ((data.emitter.m == 0.0f) ? data.emitter.n : Vector3.Normalize(r) * Mathf.Sign(t));

						Gizmos.color = (bInside && bFacing) ? colorPass : colorFail;
						Gizmos.DrawRay(p - 0.5f * n, n);
					}
				}
			}
		}

		void DrawGizmosWireCircleZ(in Vector3 center, float radius)
		{
			var stepCount = 16;
			var stepTheta = (2.0f * Mathf.PI) / stepCount;

			math.sincos(-stepTheta, out var sina, out var cosa);
			var pointPrev = new Vector3(center.x + radius * cosa, center.y + radius * sina, center.z);

			for (int i = 0; i != stepCount; i++)
			{
				math.sincos(i * stepTheta, out sina, out cosa);
				var point = new Vector3(center.x + radius * cosa, center.y + radius * sina, center.z);

				Gizmos.DrawLine(point, pointPrev);

				pointPrev = point;
			}
		}
	}
}