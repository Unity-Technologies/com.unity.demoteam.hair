using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace Unity.DemoTeam.Hair
{
	public static class HairBoundaryUtility
	{
		public const int MAX_OVERLAP_COUNT = 32;

		static Collider[] s_managedColliders = new Collider[MAX_OVERLAP_COUNT];
		static List<HairBoundary> s_managedBoundaries = new List<HairBoundary>();

		static HashSet<int> s_gatherMask = new HashSet<int>();
		static List<HairBoundary.RuntimeData> s_gatherList = new List<HairBoundary.RuntimeData>();
		static List<HairBoundary.RuntimeData> s_gatherListVolume = new List<HairBoundary.RuntimeData>();

		//-----------
		// filtering

		public static void FilterBoundary(HairBoundary boundary, HashSet<int> mask, List<HairBoundary.RuntimeData> list, ref HairBoundary.RuntimeData item)
		{
			if (boundary == null || boundary.isActiveAndEnabled == false)
				return;

			if (HairBoundary.TryGetData(boundary, ref item))
			{
				if (mask.Contains(item.xform.handle) == false)
				{
					mask.Add(item.xform.handle);
					list.Add(item);
				}
			}
		}

		public static void FilterCollider(Collider collider, HashSet<int> mask, List<HairBoundary.RuntimeData> list, ref HairBoundary.RuntimeData item)
		{
			if (collider == null || collider.isTrigger)
				return;

			if (HairBoundary.TryGetComponentShape(collider, ref item))
			{
				if (mask.Contains(item.xform.handle) == false)
				{
					mask.Add(item.xform.handle);
					list.Add(item);
				}
			}
		}

		public static List<HairBoundary.RuntimeData> Gather(HairBoundary[] resident, bool volumeSort, bool volumeQuery, in Bounds volumeBounds, in Quaternion volumeOrientation, bool includeColliders, Allocator allocator = Allocator.Temp)
		{
			var item = new HairBoundary.RuntimeData();

			s_gatherMask.Clear();
			s_gatherList.Clear();
			s_gatherListVolume.Clear();

			// gather resident
			if (resident != null)
			{
				foreach (var boundary in resident)
				{
					FilterBoundary(boundary, s_gatherMask, s_gatherList, ref item);
				}
			}

			// gather from volume
			if (volumeQuery)
			{
				var boundaryBuffer = s_managedBoundaries;
				var colliderBuffer = s_managedColliders;
				var colliderCount = Physics.OverlapBoxNonAlloc(volumeBounds.center, volumeBounds.extents, colliderBuffer, volumeOrientation);

				// filter bound / standalone
				for (int i = 0; i != colliderCount; i++)
				{
					colliderBuffer[i].GetComponents(s_managedBoundaries);

					foreach (var boundary in s_managedBoundaries)
					{
						FilterBoundary(boundary, s_gatherMask, s_gatherListVolume, ref item);
					}
				}

				// filter untagged colliders
				if (includeColliders)
				{
					for (int i = 0; i != colliderCount; i++)
					{
						FilterCollider(colliderBuffer[i], s_gatherMask, s_gatherListVolume, ref item);
					}
				}

				// sort and append
				unsafe
				{
					using (var sortedIndices = new NativeArray<ulong>(s_gatherListVolume.Count, Allocator.Temp))
					{
						var sortedIndicesPtr = (ulong*)sortedIndices.GetUnsafePtr();

						var volumeOrigin = volumeBounds.center;
						var volumeExtent = volumeBounds.extents.Abs().CMax();

						for (int i = 0; i != s_gatherListVolume.Count; i++)
						{
							var volumeSortValue = 0u;
							if (volumeSort)
							{
								var sdClippedDoubleExtent = Mathf.Clamp(SdBoundary(volumeOrigin, s_gatherListVolume[i]) / volumeExtent, -1.0f, 1.0f);
								var udClippedDoubleExtent = Mathf.Clamp01(sdClippedDoubleExtent * 0.5f + 0.5f);
								{
									volumeSortValue = (uint)(udClippedDoubleExtent * UInt16.MaxValue);
								}
							}

							var sortDistance = ((ulong)volumeSortValue) << 48;
							var sortHandle = (((ulong)s_gatherListVolume[i].xform.handle) << 16) & 0xffffffff0000uL;
							var sortIndex = ((ulong)i) & 0xffffuL;
							{
								sortedIndicesPtr[i] = sortDistance | sortHandle | sortIndex;
							}
						}

						sortedIndices.Sort();

						for (int i = 0; i != s_gatherListVolume.Count; i++)
						{
							var index = (int)(sortedIndicesPtr[i] & 0xffffuL);
							{
								s_gatherList.Add(s_gatherListVolume[index]);
							}
						}
					}
				}
			}

			// done
			return s_gatherList;
		}

		//---------------------------
		// signed distance functions

		public static float SdBoundary(in Vector3 p, in HairBoundary.RuntimeData data)
		{
			switch (data.type)
			{
				case HairBoundary.RuntimeData.Type.SDF:
					return SdDiscrete(p, data.sdf);

				case HairBoundary.RuntimeData.Type.Shape:
					{
						switch (data.shape.type)
						{
							case HairBoundary.RuntimeShape.Type.Capsule: return SdCapsule(p, data.shape.data);
							case HairBoundary.RuntimeShape.Type.Sphere: return SdSphere(p, data.shape.data);
							case HairBoundary.RuntimeShape.Type.Torus: return SdTorus(p, data.shape.data);
							case HairBoundary.RuntimeShape.Type.Cube: return SdCube(p, data.shape.data, Matrix4x4.Inverse(data.xform.matrix.WithoutScale()));
						}
					}
					break;
			}
			return 1e+7f;
		}

		public static float SdDiscrete(in Vector3 p, in HairBoundary.RuntimeSDF sdf) => SdDiscrete(p, sdf.worldToUVW, sdf.sdfTexture as Texture3D);
		public static float SdDiscrete(in Vector3 p, in Matrix4x4 invM, Texture3D sdf)
		{
			float3 uvw = mul(invM, float4(p, 1.0f)).xyz;
			return sdf.GetPixelBilinear(uvw.x, uvw.y, uvw.z).r;
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

		public static float SdCube(in float3 p, in HairBoundary.RuntimeShape.Data cube, in float4x4 invM) => SdCube(p, cube.pA, invM);
		public static float SdCube(float3 p, in float3 extent, in float4x4 invM)
		{
			p = mul(invM, float4(p, 1.0f)).xyz;

			// see: "distance functions" by Inigo Quilez
			// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm

			float3 b = extent;
			float3 q = abs(p) - b;

			return length(max(q, 0.0f)) + min(max(q.x, max(q.y, q.z)), 0.0f);
		}
	}
}
