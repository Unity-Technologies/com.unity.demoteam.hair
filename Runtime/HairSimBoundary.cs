using UnityEngine;
using Unity.DemoTeam.Attributes;

namespace Unity.DemoTeam.Hair
{
	public class HairSimBoundary : MonoBehaviour
	{
		const float MARGIN = 0.005f;// 5mm

		public enum Type
		{
			Capsule,
			Sphere,
			Torus,
		}

		public Type type = Type.Sphere;
		[VisibleIf(nameof(type), Type.Torus)]
		public float outerRadius;

		public struct BoundaryCapsule { public Vector3 centerA; public float radius; public Vector3 centerB; public float __pad__; };
		public struct BoundarySphere { public Vector3 center; public float radius; };
		public struct BoundaryTorus { public Vector3 center; public float radiusA; public Vector3 axis; public float radiusB; }
		public struct BoundaryPack
		{
			//  shape   |   capsule     sphere      torus
			//  ----------------------------------------------
			//  float3  |   centerA     center      center
			//  float   |   radius      radius      radiusA
			//  float3  |   centerB     __pad__     axis
			//  float   |   __pad__     __pad__     radiusB

			public Vector3 pA;
			public float tA;
			public Vector3 pB;
			public float tB;
		}

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

			float radius = 0.5f * Mathf.Max(dim.x, dim.z) + MARGIN;
			float extent = dim.y - radius;

			return new BoundaryCapsule()
			{
				centerA = pos - dir * extent,
				centerB = pos + dir * extent,
				radius = radius + MARGIN,
			};
		}

		public BoundarySphere GetSphere()
		{
			return new BoundarySphere()
			{
				center = this.transform.position,
				radius = 0.5f * ComponentMax(Abs(this.transform.localScale)) + MARGIN,
			};
		}

		public BoundaryTorus GetTorus()
		{
			return new BoundaryTorus()
			{
				center = this.transform.position,
				axis = this.transform.up,
				radiusA = 0.5f * ComponentMax(Abs(this.transform.localScale)) + MARGIN,
				radiusB = this.outerRadius + MARGIN,
			};
		}

		public static BoundaryPack Pack(in BoundaryCapsule capsule)
		{
			return new BoundaryPack()
			{
				pA = capsule.centerA,
				pB = capsule.centerB,
				tA = capsule.radius,
			};
		}

		public static BoundaryPack Pack(in BoundarySphere sphere)
		{
			return new BoundaryPack()
			{
				pA = sphere.center,
				tA = sphere.radius,
				pB = Vector3.positiveInfinity,
				tB = 0.0f,
			};
		}

		//TODO support this?
		//public static BoundaryPack Pack(in BoundarySphere sphereA, in BoundarySphere sphereB)
		//{
		//	return new BoundaryPack()
		//	{
		//		pA = sphereA.center,
		//		tA = sphereA.radius,
		//		pB = sphereB.center,
		//		tB = sphereB.radius,
		//	};
		//}

		public static BoundaryPack Pack(in BoundaryTorus torus)
		{
			return new BoundaryPack()
			{
				pA = torus.center,
				pB = torus.axis,
				tA = torus.radiusA,
				tB = torus.radiusB,
			};
		}
	}
}
