using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace Unity.DemoTeam.Hair
{
	public struct TriMeshQueries : IDisposable
	{
		public unsafe struct TriMeshBVHContext : IUnsafeBVHContext
		{
			public int triangleCount;
			public int* triangleVertexIndicesPtr;
			public float3* vertexPositionPtr;
			public float3* vertexNormalPtr;
			public float2* vertexUV0Ptr;

			public int GetLeafCount()
			{
				return triangleCount;
			}

			public void BuildLeafData(UnsafeBVH.Leaf* leafPtr, int leafCount)
			{
				for (uint i = 0; i != leafCount; i++)
				{
					var j0 = triangleVertexIndicesPtr[i * 3 + 0];
					var j1 = triangleVertexIndicesPtr[i * 3 + 1];
					var j2 = triangleVertexIndicesPtr[i * 3 + 2];

					var p0 = vertexPositionPtr[j0];
					var p1 = vertexPositionPtr[j1];
					var p2 = vertexPositionPtr[j2];

					leafPtr[i].min = min(p0, min(p1, p2));
					leafPtr[i].max = max(p0, max(p1, p2));
					leafPtr[i].index = i;
				}
			}

			public float DistanceLeafPoint(uint leafIndex, in float3 p)
			{
				var j0 = triangleVertexIndicesPtr[leafIndex * 3 + 0];
				var j1 = triangleVertexIndicesPtr[leafIndex * 3 + 1];
				var j2 = triangleVertexIndicesPtr[leafIndex * 3 + 2];

				var p0 = vertexPositionPtr[j0];
				var p1 = vertexPositionPtr[j1];
				var p2 = vertexPositionPtr[j2];

				return TriMeshQueriesUtility.SqDistTriangle(p0, p1, p2, p);
			}

			public float DistanceLeafTrace(uint leafIndex, in float3 p, in float3 r)
			{
				return float.PositiveInfinity;//TODO
			}
		}

		public TriMeshBuffers triangleMesh;
		public TriMeshBVHContext triangleBVHContext;
		public UnsafeBVH triangleBVH;

		public TriMeshQueries(Mesh.MeshData meshData, Allocator allocator = Allocator.Temp)
		{
			triangleMesh = new TriMeshBuffers(meshData, TriMeshBuffers.Attribute.All, allocator);

			unsafe
			{
				triangleBVHContext = new TriMeshBVHContext
				{
					triangleCount = triangleMesh.triangleVertexCount / 3,
					triangleVertexIndicesPtr = (int*)triangleMesh.triangleVertexIndices.GetUnsafePtr(),
					vertexPositionPtr = (float3*)triangleMesh.vertexPosition.GetUnsafePtr(),
					vertexNormalPtr = (float3*)triangleMesh.vertexNormal.GetUnsafePtr(),
					vertexUV0Ptr = (float2*)triangleMesh.vertexUV0.GetUnsafePtr(),
				};

				triangleBVH = UnsafeBVHBuilder.CreateFromContext(triangleBVHContext, allocator);
			}
		}

		public void Dispose()
		{
			triangleMesh.Dispose();
			triangleBVH.Dispose();
		}

		public uint FindClosestTriangle(in float3 p)
		{
			return UnsafeBVHQueries.FindClosestLeaf(triangleBVHContext, triangleBVH, p);
		}

		public float2 FindClosestTriangleUV(in float3 p)
		{
			var triangleIndex = FindClosestTriangle(p);
			if (triangleIndex != uint.MaxValue)
			{
				unsafe
				{
					var j0 = triangleBVHContext.triangleVertexIndicesPtr[triangleIndex * 3 + 0];
					var j1 = triangleBVHContext.triangleVertexIndicesPtr[triangleIndex * 3 + 1];
					var j2 = triangleBVHContext.triangleVertexIndicesPtr[triangleIndex * 3 + 2];

					var p0 = triangleBVHContext.vertexPositionPtr[j0];
					var p1 = triangleBVHContext.vertexPositionPtr[j1];
					var p2 = triangleBVHContext.vertexPositionPtr[j2];

					var n = cross(p1 - p0, p2 - p0);
					var q = p - n * dot(p - p0, n);
					var u = saturate(TriMeshQueriesUtility.MakeBarycentric(q, p0, p1, p2));

					var u0 = triangleBVHContext.vertexUV0Ptr[j0];
					var u1 = triangleBVHContext.vertexUV0Ptr[j1];
					var u2 = triangleBVHContext.vertexUV0Ptr[j2];

					return TriMeshQueriesUtility.BarycentricInterpolate(u, u0, u1, u2);
				}
			}
			else
			{
				return float2(0.0f, 0.0f);
			}
		}
	}

	public static class TriMeshQueriesUtility
	{
		public static float3 MakeBarycentric(in float3 q, in float3 a, in float3 b, in float3 c)
		{
			// compute (u, v, w) for point q in plane spanned by triangle (a, b, c)
			// https://gamedev.stackexchange.com/a/23745

			var v0 = b - a;
			var v1 = c - a;
			var v2 = q - a;

			var d00 = dot(v0, v0);
			var d01 = dot(v0, v1);
			var d11 = dot(v1, v1);
			var d20 = dot(v2, v0);
			var d21 = dot(v2, v1);

			var denom = d00 * d11 - d01 * d01;
			var v = (d11 * d20 - d01 * d21) / denom;
			var w = (d00 * d21 - d01 * d20) / denom;
			var u = 1.0f - v - w;

			return new float3(u, v, w);
		}

		public static float2 BarycentricInterpolate(in float3 uvw, in float2 a, in float2 b, in float2 c)
		{
			return a * uvw.x + b * uvw.y + c * uvw.z;
		}

		public static float3 BarycentricInterpolate(in float3 uvw, in float3 a, in float3 b, in float3 c)
		{
			return a * uvw.x + b * uvw.y + c * uvw.z;
		}

		public static float SqDistTriangle(in float3 v1, in float3 v2, in float3 v3, in float3 p)
		{
			// see: "distance to triangle" by Inigo Quilez
			// https://www.iquilezles.org/www/articles/triangledistance/triangledistance.htm

			float dot2(float3 v) => dot(v, v);

			// prepare data
			float3 v21 = v2 - v1; float3 p1 = p - v1;
			float3 v32 = v3 - v2; float3 p2 = p - v2;
			float3 v13 = v1 - v3; float3 p3 = p - v3;
			float3 nor = cross(v21, v13);

			// inside/outside test
			if (sign(dot(cross(v21, nor), p1)) +
				sign(dot(cross(v32, nor), p2)) +
				sign(dot(cross(v13, nor), p3)) < 2.0f)
			{
				// 3 edges
				return min(min(
					dot2(v21 * clamp(dot(v21, p1) / dot2(v21), 0.0f, 1.0f) - p1),
					dot2(v32 * clamp(dot(v32, p2) / dot2(v32), 0.0f, 1.0f) - p2)),
					dot2(v13 * clamp(dot(v13, p3) / dot2(v13), 0.0f, 1.0f) - p3));
			}
			else
			{
				// 1 face
				return dot(nor, p1) * dot(nor, p1) / dot2(nor);
			}
		}
	}
}
