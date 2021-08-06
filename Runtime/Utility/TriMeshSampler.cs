using System;
using UnityEngine;
using Unity.Collections;

namespace Unity.DemoTeam.Hair
{
	public struct TriMeshSampler : IDisposable
	{
		public TriMeshBuffers triangleMesh;

		public int triangleCount;
		public float triangleAreaSum;
		public NativeArray<float> triangleArea;
		public NativeArray<float> triangleAreaAccu;

		public Unity.Mathematics.Random randSeed;
		public Unity.Mathematics.Random rand;

		public TriMeshSampler(Mesh.MeshData meshData, uint seedIndex, Allocator allocator = Allocator.Temp, uint submeshMask = ~0u)
		{
			triangleMesh = new TriMeshBuffers(meshData, TriMeshBuffers.Attribute.All, allocator, submeshMask);

			triangleCount = triangleMesh.triangleVertexCount / 3;
			triangleAreaSum = 0.0f;

			triangleArea = new NativeArray<float>(triangleCount, allocator);
			triangleAreaAccu = new NativeArray<float>(triangleCount, allocator);

			// calc triangle area + total
			for (int i = 0; i != triangleCount; i++)
			{
				var j0 = triangleMesh.triangleVertexIndices[i * 3 + 0];
				var j1 = triangleMesh.triangleVertexIndices[i * 3 + 1];
				var j2 = triangleMesh.triangleVertexIndices[i * 3 + 2];

				var p0 = triangleMesh.vertexPosition[j0];
				var p1 = triangleMesh.vertexPosition[j1];
				var p2 = triangleMesh.vertexPosition[j2];

				var area = 0.5f * Mathf.Abs(Vector3.Magnitude(Vector3.Cross(p2 - p0, p1 - p0)));

				triangleAreaSum += area;
				triangleArea[i] = area;
			}

			// calc triangle area accumulated until current index
			if (triangleCount > 0)
			{
				triangleAreaAccu[0] = triangleArea[0];
				for (int i = 1; i != triangleCount; i++)
				{
					triangleAreaAccu[i] = triangleAreaAccu[i - 1] + triangleArea[i];
				}
			}

			// initial sequence
			rand = randSeed = Unity.Mathematics.Random.CreateFromIndex(seedIndex);
		}

		public void Dispose()
		{
			triangleMesh.Dispose();

			triangleArea.Dispose();
			triangleAreaAccu.Dispose();
		}

		private void ResetSequence()
		{
			rand = randSeed;
		}

		private int NextTriangleIndex()
		{
			if (triangleCount == 0)
			{
				return -1;
			}
			else
			{
				var searchValue = triangleAreaSum * rand.NextFloat();
				var searchIndex = triangleAreaAccu.BinarySearch(searchValue);

				if (searchIndex >= 0)
					return searchIndex;
				else
					return ~searchIndex;
			}
		}

		public Sample Next()
		{
			var i = NextTriangleIndex();
			if (i == -1)
			{
				return new Sample();
			}

			var s = rand.NextFloat();
			var t = rand.NextFloat();
			if (s + t > 1.0f)
			{
				s = 1.0f - s;
				t = 1.0f - t;
			}

			var j0 = triangleMesh.triangleVertexIndices[i * 3 + 0];
			var j1 = triangleMesh.triangleVertexIndices[i * 3 + 1];
			var j2 = triangleMesh.triangleVertexIndices[i * 3 + 2];

			var p0 = triangleMesh.vertexPosition[j0];
			var p1 = triangleMesh.vertexPosition[j1] - p0;
			var p2 = triangleMesh.vertexPosition[j2] - p0;

			var n0 = triangleMesh.vertexNormal[j0];
			var n1 = triangleMesh.vertexNormal[j1] - n0;
			var n2 = triangleMesh.vertexNormal[j2] - n0;

			var u0 = triangleMesh.vertexUV0[j0];
			var u1 = triangleMesh.vertexUV0[j1] - u0;
			var u2 = triangleMesh.vertexUV0[j2] - u0;

			return new Sample()
			{
				position = p0 + (s * p1) + (t * p2),
				normal = n0 + (s * n1) + (t * n2),
				uv0 = u0 + (s * u1) + (t * u2),
			};
		}

		public struct Sample
		{
			public Vector3 position;
			public Vector3 normal;
			public Vector2 uv0;
		}
	}
}
