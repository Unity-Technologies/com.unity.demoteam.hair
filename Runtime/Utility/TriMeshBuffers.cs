using System;
using UnityEngine;
using Unity.Collections;

namespace Unity.DemoTeam.Hair
{
	public struct TriMeshBuffers : IDisposable
	{
		[Flags]
		public enum Attribute
		{
			All = Position | Normal | UV0,
			Position = 1 << 0,
			Normal = 1 << 1,
			UV0 = 1 << 2,
		}

		public int vertexCount;
		public Attribute vertexAttributes;

		public NativeArray<Vector3> vertexPosition;
		public NativeArray<Vector3> vertexNormal;
		public NativeArray<Vector2> vertexUV0;

		public int triangleVertexCount;
		public NativeArray<int> triangleVertexIndices;

		public uint submeshMask;

		public TriMeshBuffers(Mesh.MeshData meshData, Attribute meshAttributes = Attribute.All, Allocator allocator = Allocator.Temp, uint submeshMask = ~0u)
		{
			vertexCount = meshData.vertexCount;
			vertexAttributes = meshAttributes;

			vertexPosition = new NativeArray<Vector3>(vertexCount, allocator, NativeArrayOptions.UninitializedMemory);
			vertexNormal = new NativeArray<Vector3>(vertexCount, allocator, NativeArrayOptions.UninitializedMemory);
			vertexUV0 = new NativeArray<Vector2>(vertexCount, allocator, NativeArrayOptions.UninitializedMemory);

			if (vertexAttributes.HasFlag(Attribute.Position))
				meshData.GetVertices(vertexPosition);

			if (vertexAttributes.HasFlag(Attribute.Normal))
				meshData.GetNormals(vertexNormal);

			if (vertexAttributes.HasFlag(Attribute.UV0))
				meshData.GetUVs(0, vertexUV0);

			triangleVertexCount = 0;
			{
				for (int i = 0; i != meshData.subMeshCount; i++)
				{
					if (((1 << i) & submeshMask) == 0)
						continue;

					var submesh = meshData.GetSubMesh(i);
					if (submesh.topology != MeshTopology.Triangles)
						continue;

					triangleVertexCount += submesh.indexCount;
				}
			}

			triangleVertexIndices = new NativeArray<int>(triangleVertexCount, allocator, NativeArrayOptions.UninitializedMemory);
			{
				int writeOffset = 0;

				for (int i = 0; i != meshData.subMeshCount; i++)
				{
					if (((1 << i) & submeshMask) == 0)
						continue;

					var submesh = meshData.GetSubMesh(i);
					if (submesh.topology != MeshTopology.Triangles)
						continue;

					var indexCount = submesh.indexCount;
					if (indexCount == 0)
						continue;

					using (var meshIndices = new NativeArray<int>(indexCount, allocator, NativeArrayOptions.UninitializedMemory))
					{
						meshData.GetIndices(meshIndices, i);
						meshIndices.CopyTo(triangleVertexIndices.GetSubArray(writeOffset, submesh.indexCount));
					}

					writeOffset += indexCount;
				}
			}

			this.submeshMask = submeshMask;
		}

		public void Dispose()
		{
			if (vertexPosition.IsCreated)
				vertexPosition.Dispose();

			if (vertexNormal.IsCreated)
				vertexNormal.Dispose();

			if (vertexUV0.IsCreated)
				vertexUV0.Dispose();

			if (triangleVertexIndices.IsCreated)
				triangleVertexIndices.Dispose();
		}

		public bool HasAttribute(Attribute attribute)
		{
			return vertexAttributes.HasFlag(attribute);
		}
	}
}
