using UnityEngine;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace Unity.DemoTeam.Hair
{
	public struct HairBoundaryProxy : ISpatialComponentProxy<HairBoundary, HairBoundary.RuntimeData>
	{
		public bool TryGetData(HairBoundary component, ref HairBoundary.RuntimeData data)
			=> HairBoundary.TryGetData(component, ref data);
		public bool TryGetComponentData(Component component, ref HairBoundary.RuntimeData data)
			=> HairBoundary.TryGetComponentData(component, ref data);

		public int ResolveDataHandle(in HairBoundary.RuntimeData data)
			=> data.xform.handle;
		public float ResolveDataDistance(in HairBoundary.RuntimeData data, in Vector3 p)
			=> HairBoundaryUtility.SdBoundary(p, data);
	}

	public static class HairBoundaryUtility
	{
		//-----------------
		// signed distance

		public static float SdBoundary(in Vector3 p, in HairBoundary.RuntimeData data)
		{
			switch (data.type)
			{
				case HairBoundary.RuntimeData.Type.SDF:
					return SdDiscrete(p, data);

				case HairBoundary.RuntimeData.Type.Shape:
					{
						switch (data.shape.type)
						{
							case HairBoundary.RuntimeShape.Type.Capsule: return SdCapsule(p, data.shape.data);
							case HairBoundary.RuntimeShape.Type.Sphere: return SdSphere(p, data.shape.data);
							case HairBoundary.RuntimeShape.Type.Torus: return SdTorus(p, data.shape.data);
							case HairBoundary.RuntimeShape.Type.Cube: return SdCube(p, data.shape.data, Matrix4x4.Inverse(data.xform.matrix));
						}
					}
					break;
			}
			return 1e+7f;
		}

		public static float SdDiscrete(in Vector3 p, in HairBoundary.RuntimeData data) => SdDiscrete(p, data.shape.data.tA, Matrix4x4.Inverse(data.xform.matrix), data.sdf.sdfTexture as Texture3D);
		public static float SdDiscrete(in Vector3 p, in float scale, in Matrix4x4 invM, Texture3D sdf)
		{
			float3 uvw = mul(invM, float4(p, 1.0f)).xyz;
			return scale * sdf.GetPixelBilinear(uvw.x, uvw.y, uvw.z).r;
		}

		public static float SdCapsule(in float3 p, in HairBoundary.RuntimeShape.Data capsule) => SdCapsule(p, capsule.pA, capsule.pB, capsule.tA);
		public static float SdCapsule(in float3 p, in float3 centerA, in float3 centerB, float radius)
		{
			// see: "distance functions" by Inigo Quilez
			// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

			float3 pa = p - centerA;
			float3 ba = centerB - centerA;

			float h = saturate(dot(pa, ba) / dot(ba, ba));
			float r = radius;

			return (length(pa - ba * h) - r);
		}

		public static float SdSphere(in float3 p, in HairBoundary.RuntimeShape.Data sphere) => SdSphere(p, sphere.pA, sphere.tA);
		public static float SdSphere(in float3 p, in float3 center, float radius)
		{
			// see: "distance functions" by Inigo Quilez
			// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

			return (length(p - center) - radius);
		}

		public static float SdTorus(in float3 p, in HairBoundary.RuntimeShape.Data torus) => SdTorus(p, torus.pA, torus.pB, torus.tA, torus.tB);
		public static float SdTorus(float3 p, in float3 center, in float3 axis, float radiusA, float radiusB)
		{
			float3 basisX = (abs(axis.y) > 1.0f - 1e-4f) ? float3(1.0f, 0.0f, 0.0f) : normalize(cross(axis, float3(0.0f, 1.0f, 0.0f)));
			float3 basisY = axis;
			float3 basisZ = cross(basisX, axis);
			float3x3 invM = float3x3(basisX, basisY, basisZ);

			p = mul(invM, p - center);

			// see: "distance functions" by Inigo Quilez
			// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

			float2 t = float2(radiusA, radiusB);
			float2 q = float2(length(p.xz) - t.x, p.y);

			return length(q) - t.y;
		}

		public static float SdCube(in float3 p, in HairBoundary.RuntimeShape.Data cube, in float4x4 invM) => SdCube(p, cube.pB, invM);
		public static float SdCube(float3 p, in float3 extent, in float4x4 invM)
		{
			p = mul(invM, float4(p, 1.0f)).xyz;
			// assuming TRS, can apply scale post-transform to preserve primitive scale
			// T R S x_local = x_world
			//       x_local = S^-1 R^-1 T^-1 x_world
			//     S x_local = S S^-1 R^-1 T^-1 world
			p *= 2.0f * extent;

			// see: "distance functions" by Inigo Quilez
			// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

			float3 b = extent;
			float3 q = abs(p) - b;

			return length(max(q, 0.0f)) + min(max(q.x, max(q.y, q.z)), 0.0f);
		}
	}
}
