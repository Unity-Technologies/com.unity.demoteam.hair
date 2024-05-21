using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Unity.DemoTeam.Hair
{
	public partial class HairWind
	{
		void OnDrawGizmosSelected()
		{
			var data = new RuntimeData();
			{
				if (TryGetData(this, ref data))
				{
					DrawGizmosRuntimeData(data, Time.time, Time.deltaTime, active: isActiveAndEnabled, selected: true);
				}
			}
		}

		public static void DrawGizmosRuntimeData(in RuntimeData data, float t, float dt, bool active, bool selected)
		{
			DrawGizmosRuntimeEmitter(data, selected ? 1.0f : 0.5f);

			if (selected)
			{
				if (active)
				{
					DrawGizmosRuntimeEmitterFlow(data, selected ? 1.0f : 0.5f, 256, t, dt);
				}

				//DrawGizmosRuntimeEmitterGrid(data, 16);
			}
		}

		static void DrawGizmosRuntimeEmitter(in RuntimeData data, float opacity)
		{
			Gizmos.color = Color.Lerp(Color.clear, Color.cyan, opacity);
			Gizmos.matrix = Matrix4x4.identity;

			switch (data.type)
			{
				case RuntimeData.Type.Directional:
					{
						var p = data.emitter.p - data.emitter.n * 0.5f;
						var q = p + data.emitter.n;
						var r = p + data.emitter.n * 0.9f;

						var ux = data.gizmo.rotation * Vector3.right;
						var uy = data.gizmo.rotation * Vector3.up;

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
						Gizmos.matrix = Matrix4x4.TRS(data.emitter.p, data.gizmo.rotation, Vector3.one);
						Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
					}
					break;

				case RuntimeData.Type.Turbine:
					{
						var t0 = data.emitter.t0;
						var t1 = t0 + data.gizmo.turbineNozzleOffset;

						var h0 = data.emitter.h0;
						var h1 = 0.5f * data.gizmo.turbineNozzleWidth;

						var p0 = data.emitter.p + data.emitter.n * t0;
						var p1 = data.emitter.p + data.emitter.n * t1;

						var ux = data.gizmo.rotation * Vector3.right;
						var uy = data.gizmo.rotation * Vector3.up;

						Gizmos.DrawRay(p0 + uy * h0, (data.emitter.n + data.emitter.m * uy) * (t1 - t0));
						Gizmos.DrawRay(p0 - uy * h0, (data.emitter.n - data.emitter.m * uy) * (t1 - t0));
						Gizmos.DrawRay(p0 + ux * h0, (data.emitter.n + data.emitter.m * ux) * (t1 - t0));
						Gizmos.DrawRay(p0 - ux * h0, (data.emitter.n - data.emitter.m * ux) * (t1 - t0));

						Gizmos.matrix = Matrix4x4.TRS(p0, data.gizmo.rotation, Vector3.one);
						DrawGizmosWireCircleZ(Vector3.zero, h0);

						Gizmos.matrix = Matrix4x4.TRS(p1, data.gizmo.rotation, Vector3.one);
						DrawGizmosWireCircleZ(Vector3.zero, h1);
					}
					break;
			}
		}

		static void DrawGizmosRuntimeEmitterFlow(in RuntimeData data, float opacity, uint samples, float t, float dt)
		{
			dt = Mathf.Clamp(dt, 1.0f / 120.0f, 1.0f / 15.0f);

			Gizmos.color = Color.Lerp(Color.clear, Color.cyan, opacity * 0.6f);
			Gizmos.matrix = Matrix4x4.identity;

			var Lv = data.emitter.v + 0.5f * data.emitter.A;//TODO replace average with something that is more meaningful visually
			var Lt = 3.0f;// flow line lifetime
			var Ld = math.abs(Lv * Lt);
			var Nt = math.frac(t / Lt);

			var ux = data.gizmo.rotation * Vector3.right;
			var uy = data.gizmo.rotation * Vector3.up;

			for (uint i = 0; i != samples; i++)
			{
				var j = (uint)(i + samples * Nt) % samples;
				var R = Mathematics.Random.CreateFromIndex(j);

				var p = data.emitter.p;
				{
					switch (data.type)
					{
						case RuntimeData.Type.Directional:
							{
								var h = 3.0f;
								var k = R.NextFloat3();
								p.x += (k.x * 2.0f - 1.0f) * h - (0.5f * Ld) * data.emitter.n.x;
								p.y += (k.y * 2.0f - 1.0f) * h - (0.5f * Ld) * data.emitter.n.y;
								p.z += (k.z * 2.0f - 1.0f) * h - (0.5f * Ld) * data.emitter.n.z;
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
								var h = data.emitter.h0 + 1e-4f * data.emitter.m;
								math.sincos(2.0f * Mathf.PI * k.x, out var sina, out var cosa);
								p.x += (k.y * cosa * h) * ux.x + (k.y * sina * h) * uy.x + (1e-4f + data.emitter.t0) * data.emitter.n.x;
								p.y += (k.y * cosa * h) * ux.y + (k.y * sina * h) * uy.y + (1e-4f + data.emitter.t0) * data.emitter.n.y;
								p.z += (k.y * cosa * h) * ux.z + (k.y * sina * h) * uy.z + (1e-4f + data.emitter.t0) * data.emitter.n.z;
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

		static void DrawGizmosRuntimeEmitterGrid(in RuntimeData data, uint samples)
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
			var pmin = data.emitter.p - pext;

			if (data.type == RuntimeData.Type.Turbine)
			{
				pmin += data.emitter.n * data.emitter.t0;
			}

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

		static void DrawGizmosWireCircleZ(in Vector3 center, float radius)
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
