using UnityEngine;
using Unity.DemoTeam.Attributes;

namespace Unity.DemoTeam.Hair
{
	public class HairSimBoundary : MonoBehaviour
	{
		public enum Type
		{
			Capsule,
			Sphere,
			Torus,
		}

		public Type type = Type.Sphere;

		[VisibleIf("type", Type.Torus)]
		public float outerRadius;

		public struct BoundaryCapsule { public Vector3 positionA; public float radius; public Vector3 positionB; public float __pad; };
		public struct BoundarySphere { public Vector3 position; public float radius; };
		public struct BoundaryTorus { public Vector3 position; public float radiusA; public Vector3 axis; public float radiusB; }

		private static Vector3 Abs(in Vector3 v)
		{
			return new Vector3()
			{
				x = Mathf.Abs(v.x),
				y = Mathf.Abs(v.y),
				z = Mathf.Abs(v.z),
			};
		}

		private static float ComponentMax(in Vector3 v)
		{
			return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
		}

		private static float ComponentMin(in Vector3 v)
		{
			return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
		}

		public BoundaryCapsule GetCapsule()
		{
			Vector3 dim = Abs(this.transform.localScale);
			Vector3 pos = this.transform.position;
			Vector3 dir = this.transform.up;

			float radius = 0.5f * Mathf.Max(dim.x, dim.z);
			float extent = dim.y - radius;

			return new BoundaryCapsule()
			{
				positionA = pos - dir * extent,
				positionB = pos + dir * extent,
				radius = radius,
			};
		}

		public BoundarySphere GetSphere()
		{
			return new BoundarySphere()
			{
				position = this.transform.position,
				radius = 0.5f * ComponentMax(Abs(this.transform.localScale)),
			};
		}

		public BoundaryTorus GetTorus()
		{
			return new BoundaryTorus()
			{
				position = this.transform.position,
				axis = this.transform.up,
				//radiusA = this.radius,
				//radiusB = this.innerRadius,
			};
		}
	}
}
