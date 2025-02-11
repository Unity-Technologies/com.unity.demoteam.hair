using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class ProjectedCircleDensity : MonoBehaviour
{
	public enum Mode
	{
		Random,
		Sunflower,
	}

	[Range(0.0f, 1.0f)]
	public float circleRadius = 1.0f;
	[Range(0.0f, 1.0f)]
	public float circleDensity = 1.0f;
	[Range(0.0f, 1.0f)]
	public float circlePadding = 1.0f;
	
	public Mode sampleDistribution = Mode.Random;
	[Range(0.0f, 1.0f)]
	public float sampleRadius = 0.1f;
	public bool sampleRadiusLocked = false;
	[Min(1)]
	public int sampleCount = 10;
	public int sampleSeed = 0;
	Vector2[] samplePoint;

	public bool experimentGraph = false;
	[Range(2, 1000)]
	public int experimentSteps = 1000;
	public bool experimentLog = false;
	public bool experimentRadius = true;

	static float FitSampleRadius(int n, float r, float d)
	{
		var A = Mathf.PI * r * r;
		var a = d * (A / n);
		return Mathf.Sqrt(a / Mathf.PI);
	}

	static Vector2[] MakeRandom(int n, float rb, int seed)
	{
		var point = new Vector2[n];
		{
			UnityEngine.Random.InitState(seed);

			for (int i = 0; i != n; i++)
			{
				point[i] = UnityEngine.Random.insideUnitCircle * rb;
			}
		}
		return point;
	}

	// https://stackoverflow.com/questions/28567166/uniformly-distribute-x-points-inside-a-circle
	static Vector2[] MakeSunflower(int n, float rb, float alpha = 0)
	{
		var point = new Vector2[n];
		{
			var b = Mathf.RoundToInt(alpha * Mathf.Sqrt(n));// number of boundary points
			var phi = (Mathf.Sqrt(5.0f) + 1.0f) / 2.0f;// golden ratio

			static float radius(int k, int n, int b)
			{
				return (k > n - b) ? 1 : Mathf.Sqrt(k - 0.5f) / Mathf.Sqrt(n - (b + 1.0f) / 2.0f);
			}

			for (int k = 1; k <= n; k++)
			{
				var r = radius(k, n, b) * rb;
				var theta = (2.0f * Mathf.PI * k) / (phi * phi);
				point[k - 1] = new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
			}
		}
		return point;
	}

	float IntegrateProjectedCoverage(float sampleRadiusScale)
	{
		var d = 0.0f;
		{
			var r = sampleRadius * sampleRadiusScale;
			var x = samplePoint[0].x - r;

			for (int i = 0; i != samplePoint.Length; i++)
			{
				var x0 = samplePoint[i].x - r;
				var x1 = samplePoint[i].x + r;

				if (x < x1)
				{
					if (x > x0)
						d += (x1 - x);
					else
						d += (x1 - x0);

					x = x1;
				}
			}
		}
		return d / (2.0f * circleRadius);
	}

	float ComputeApproximateProjectedCoverage(float sampleRadiusScale)
	{
		return (1.0f - Mathf.Exp(-sampleRadius * sampleCount * sampleRadiusScale / circleRadius));
	}

	void OnValidate()
	{
		if (sampleRadiusLocked == false)
		{
			sampleRadius = FitSampleRadius(sampleCount, circleRadius, circleDensity);
		}

		switch (sampleDistribution)
		{
			case Mode.Random:
				samplePoint = MakeRandom(sampleCount, circleRadius - circlePadding * sampleRadius, sampleSeed);
				break;
			case Mode.Sunflower:
				samplePoint = MakeSunflower(sampleCount, circleRadius - circlePadding * sampleRadius);
				break;
		}

		Array.Sort<Vector2>(samplePoint, (a, b) => a.x.CompareTo(b.x));
	}

	static void GizmoBox(Vector2 p, Vector2 e)
	{
		Gizmos.DrawLine(new Vector3(p.x - e.x, p.y - e.y, 0.0f), new Vector3(p.x + e.x, p.y - e.y, 0.0f));
		Gizmos.DrawLine(new Vector3(p.x + e.x, p.y - e.y, 0.0f), new Vector3(p.x + e.x, p.y + e.y, 0.0f));
		Gizmos.DrawLine(new Vector3(p.x + e.x, p.y + e.y, 0.0f), new Vector3(p.x - e.x, p.y + e.y, 0.0f));
		Gizmos.DrawLine(new Vector3(p.x - e.x, p.y + e.y, 0.0f), new Vector3(p.x - e.x, p.y - e.y, 0.0f));
	}

	static void GizmoBoxFill(Vector2 p, Vector2 e)
	{
		Gizmos.DrawCube(p, 2.0f * e);
	}

	static void GizmoCircle(Vector2 p, float r)
	{
		var n = 32;
		var k = (Mathf.PI * 2.0f) / 32;

		var c_ = Mathf.Cos(k * (n - 1));
		var s_ = Mathf.Sin(k * (n - 1));

		for (int i = 0; i != n; i++)
		{
			var ci = Mathf.Cos(k * i);
			var si = Mathf.Sin(k * i);

			Gizmos.DrawLine(new Vector3(p.x + r * c_, p.y + r * s_, 0.0f), new Vector3(p.x + r * ci, p.y + r * si, 0.0f));

			c_ = ci;
			s_ = si;
		}
	}

	static void GizmoPlot(Vector2 p0, Vector2 p1, float t0, float t1, int n, Func<float, float> f, bool debug = false)
	{
		var e = p1 - p0;
		var k = 1.0f / (n - 1);

		var t_ = k * 0;
		var d_ = f(t_);

		if (debug)
		{
			Debug.Log(t_.ToString("G", CultureInfo.InvariantCulture) + " " + d_.ToString("G", CultureInfo.InvariantCulture));
		}

		for (int i = 1; i != n; i++)
		{
			var ti = k * i;
			var di = f(ti);

			Gizmos.DrawLine(p0 + e * new Vector2(t_, d_), p0 + e * new Vector2(ti, di));

			t_ = ti;
			d_ = di;

			if (debug)
			{
				Debug.Log(t_.ToString("G", CultureInfo.InvariantCulture) + " " + d_.ToString("G", CultureInfo.InvariantCulture));
			}
		}
	}

	struct GizmoScope : IDisposable
	{
		Matrix4x4 m;
		Color c;

		public GizmoScope(Color c) : this(Gizmos.matrix, c) { }
		public GizmoScope(Matrix4x4 m) : this(m, Gizmos.color) { }
		public GizmoScope(Matrix4x4 m, Color c)
		{
			this.m = Gizmos.matrix;
			this.c = Gizmos.color;
			Gizmos.matrix = m;
			Gizmos.color = c;
		}

		public void Dispose()
		{
			Gizmos.matrix = m;
			Gizmos.color = c;
		}
	}

	void OnDrawGizmos()
	{
		var m = this.transform.localToWorldMatrix;
		var c = Color.white;

		using (new GizmoScope(m, c))
		{
			var projectionHeight = circleRadius * 0.2f;
			var projectionOffset = circleRadius + projectionHeight;

			var experimentHeight = circleRadius * 2.0f;
			var experimentOffset = circleRadius * 3.0f + projectionHeight;

			// draw circle
			GizmoCircle(Vector2.zero, circleRadius);

			// draw circle projection
			GizmoBox(new Vector2(0.0f, -projectionOffset), new Vector2(circleRadius, 0.5f * projectionHeight));

			if (samplePoint != null)
			{
				using (new GizmoScope(m, Color.green))
				{
					// draw samples
					for (int i = 0; i != samplePoint.Length; i++)
					{
						GizmoCircle(samplePoint[i], sampleRadius);
					}

					// draw samples projection
					for (int i = 0; i != samplePoint.Length; i++)
					{
						GizmoBoxFill(new Vector2(samplePoint[i].x, -projectionOffset), new Vector2(sampleRadius, 0.4f * projectionHeight));
					}
				}
			}

			// draw integrated coverage
			using (new GizmoScope(Color.green))
			{
				if (samplePoint != null)
				{
					GizmoBoxFill(new Vector2(0.0f, -projectionOffset - projectionHeight), new Vector2(circleRadius * IntegrateProjectedCoverage(1.0f), 0.1f * projectionHeight));
				}
			}

			// draw approximated coverage
			using (new GizmoScope(Color.cyan))
			{
				GizmoBoxFill(new Vector2(0.0f, -projectionOffset - projectionHeight * 1.3f), new Vector2(circleRadius * ComputeApproximateProjectedCoverage(1.0f), 0.1f * projectionHeight));
			}

			// draw experiment
			if (samplePoint != null && experimentGraph)
			{
				var p = new Vector2(experimentOffset, 0.0f);
				var e = new Vector2(experimentHeight, 0.5f * experimentHeight);

				using (new GizmoScope(m, Color.black))
				{
					GizmoBoxFill(p, e + Vector2.one * (experimentHeight * 0.01f));
				}

				using (new GizmoScope(m, Color.grey))
				{
					GizmoBox(p, e);
				}

				// plot lower bound
				using (new GizmoScope(m, Color.red))
				{
					if (experimentRadius)
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => (sampleRadius / circleRadius) * (t));
					else
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => (sampleRadius / circleRadius) * Mathf.Sqrt(t));
				}

				// plot upper bound
				using (new GizmoScope(m, Color.yellow))
				{
					if (experimentRadius)
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => (sampleRadius / circleRadius) * (t * sampleCount));
					else
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => (sampleRadius / circleRadius) * (Mathf.Sqrt(t) * sampleCount));
				}

				// plot pow(t, 1 / n)
				using (new GizmoScope(m, Color.cyan))
				{
					//var r = Mathf.Sqrt(circleDensity);
					var sr = sampleRadius / circleRadius;
					var sd = circleDensity / sampleCount;
					var fillFraction = (sampleRadius * sampleRadius * sampleCount) / (circleRadius * circleRadius);
					//GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => Mathf.Pow(circleRadius * t, sr / 2.0f));
					//GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => Mathf.Lerp(sampleRadius * t * sampleCount, sampleRadius * t, Mathf.Sqrt(t)));
					//GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => sampleRadius * t * Mathf.Lerp(sampleCount, 1.0f, t));

					//GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => 0.9240695f - 0.9165581f * Mathf.Exp(-10.58568f * t));
					//*** GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => (1.0f - 1.0f * Mathf.Exp(-1f * circleDensity * t / sr)));
					//GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => (1.0f - Mathf.Exp(-circleDensity * t / sr)));

					if (experimentRadius)
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, ComputeApproximateProjectedCoverage, experimentLog);
					else
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => ComputeApproximateProjectedCoverage(Mathf.Sqrt(t)), experimentLog);
				}
				//using (new GizmoScope(m, Color.Lerp(Color.cyan, Color.magenta, 0.5f)))
				//{
				//	//var r = Mathf.Sqrt(circleDensity);
				//	var sampleArea = sampleRadius * sampleRadius;
				//	var circleArea = circleRadius * circleRadius;
				//	Debug.Log("sampleArea = " + sampleArea + ", circleArea = " + circleArea);

				//	GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => (1.0f - Mathf.Exp(-sampleArea * t / circleArea)));
				//}

				// plot sampled ground truth
				using (new GizmoScope(m, Color.green))
				{
					if (experimentRadius)
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, IntegrateProjectedCoverage, experimentLog);
					else
						GizmoPlot(p - e, p + e, 0.0f, 1.0f, experimentSteps, (t) => IntegrateProjectedCoverage(Mathf.Sqrt(t)), experimentLog);
				}
			}

			experimentLog = false;
		}
	}
}
