using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

#if HAS_PACKAGE_UNITY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
#endif

namespace Unity.DemoTeam.Hair
{
	public static class HairInstanceBuilder
	{
		public static void ClearHairInstance(HairInstance hairInstance)
		{
			var strandGroupInstances = hairInstance.strandGroupInstances;
			if (strandGroupInstances != null)
			{
				for (int i = 0; i != strandGroupInstances.Length; i++)
				{
					ref readonly var strandGroupInstance = ref strandGroupInstances[i];

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
					if (strandGroupInstance.sceneObjects.rootMeshAttachment != null)
						strandGroupInstance.sceneObjects.rootMeshAttachment.Detach(false);
#endif

					CoreUtils.Destroy(strandGroupInstance.sceneObjects.groupContainer);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.materialInstance);
#if !UNITY_2021_2_OR_NEWER
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstance);
#endif
				}
			}

			hairInstance.strandGroupInstances = null;
			hairInstance.strandGroupChecksums = null;

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(hairInstance);
#endif
		}

		public static void BuildHairInstance(HairInstance hairInstance, HairInstance.GroupProvider[] strandGroupProviders, HideFlags hideFlags = HideFlags.NotEditable)
		{
			ClearHairInstance(hairInstance);

			// prep strand group instances
			var strandGroupInstanceCount = 0;
			var strandGroupChecksumCount = 0;

			if (strandGroupProviders != null)
			{
				for (int i = 0; i != strandGroupProviders.Length; i++)
				{
					var hairAsset = strandGroupProviders[i].hairAsset;
					if (hairAsset == null || hairAsset.checksum == "")
						continue;

					var strandGroups = hairAsset.strandGroups;
					if (strandGroups == null)
						continue;

					strandGroupInstanceCount += hairAsset.strandGroups.Length;
					strandGroupChecksumCount++;
				}
			}

			if (strandGroupInstanceCount == 0)
				return;

			hairInstance.strandGroupInstances = new HairInstance.GroupInstance[strandGroupInstanceCount];
			hairInstance.strandGroupChecksums = new string[strandGroupChecksumCount];

			// build strand group instances
			for (int i = 0, writeIndexInstance = 0, writeIndexChecksum = 0; i != strandGroupProviders.Length; i++)
			{
				var hairAsset = strandGroupProviders[i].hairAsset;
				if (hairAsset == null || hairAsset.checksum == "")
					continue;

				var strandGroups = hairAsset.strandGroups;
				if (strandGroups == null)
					continue;

				for (int j = 0; j != strandGroups.Length; j++)
				{
					ref var strandGroupInstance = ref hairInstance.strandGroupInstances[writeIndexInstance++];

					strandGroupInstance.groupAssetReference.hairAsset = hairAsset;
					strandGroupInstance.groupAssetReference.hairAssetGroupIndex = j;
					strandGroupInstance.settingsIndex = -1;

					var flatIndex = writeIndexInstance - 1;

					// create scene object for group
					strandGroupInstance.sceneObjects.groupContainer = CreateContainer("Group:" + flatIndex, hairInstance.gameObject, hideFlags);

					// create scene objects for root mesh
					strandGroupInstance.sceneObjects.rootMeshContainer = CreateContainer("Roots:" + flatIndex, strandGroupInstance.sceneObjects.groupContainer, hideFlags);
					{
						strandGroupInstance.sceneObjects.rootMeshFilter = CreateComponent<MeshFilter>(strandGroupInstance.sceneObjects.rootMeshContainer, hideFlags);
						strandGroupInstance.sceneObjects.rootMeshFilter.sharedMesh = strandGroups[j].meshAssetRoots;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
						strandGroupInstance.sceneObjects.rootMeshAttachment = CreateComponent<SkinAttachment>(strandGroupInstance.sceneObjects.rootMeshContainer, hideFlags);
						strandGroupInstance.sceneObjects.rootMeshAttachment.attachmentType = SkinAttachment.AttachmentType.Mesh;
						strandGroupInstance.sceneObjects.rootMeshAttachment.forceRecalculateBounds = true;
#endif
					}

					// create scene objects for strand mesh
					strandGroupInstance.sceneObjects.strandMeshContainer = CreateContainer("Strands:" + flatIndex, strandGroupInstance.sceneObjects.groupContainer, hideFlags);
					{
						strandGroupInstance.sceneObjects.strandMeshFilter = CreateComponent<MeshFilter>(strandGroupInstance.sceneObjects.strandMeshContainer, hideFlags);
						strandGroupInstance.sceneObjects.strandMeshRenderer = CreateComponent<MeshRenderer>(strandGroupInstance.sceneObjects.strandMeshContainer, hideFlags);

#if HAS_PACKAGE_UNITY_HDRP_15_0_2
						strandGroupInstance.sceneObjects.strandMeshRendererHDRP = CreateComponent<HDAdditionalMeshRendererSettings>(strandGroupInstance.sceneObjects.strandMeshContainer, hideFlags);
#endif
					}
				}

				hairInstance.strandGroupChecksums[writeIndexChecksum++] = hairAsset.checksum;
			}

#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(hairInstance);
#endif
		}

		//--------
		// meshes

		public const MeshUpdateFlags MESH_UPDATE_UNCHECKED = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds;

		public static unsafe void BuildMeshRoots(Mesh meshRoots, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, Vector3[] particlePosition)
		{
			var indexFormat = IndexFormat.UInt32;
			var indexStride = sizeof(uint);
			var indexCount = strandCount;

			using (var rootMeshPosition = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var rootMeshTangent = new NativeArray<Vector4>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var rootMeshNormal = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var indices = new NativeArray<byte>(indexCount * indexStride, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var rootMeshPositionPtr = (Vector3*)rootMeshPosition.GetUnsafePtr();
				var rootMeshTangentPtr = (Vector4*)rootMeshTangent.GetUnsafePtr();
				var rootMeshNormalPtr = (Vector3*)rootMeshNormal.GetUnsafePtr();
				var indicesPtr = (uint*)indices.GetUnsafePtr();

				// write attributes
				for (int i = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, strandCount, strandParticleCount, i,
						out var strandParticleBegin,
						out var strandParticleStride,
						out var strandParticleEnd);

					ref readonly var p0 = ref particlePosition[strandParticleBegin + strandParticleStride * 0];
					ref readonly var p1 = ref particlePosition[strandParticleBegin + strandParticleStride * 1];

					var localRootDir = Vector3.Normalize(p1 - p0);
					var localRootFrame = Quaternion.FromToRotation(Vector3.up, localRootDir);
					var localRootPerp = localRootFrame * Vector3.right;

					*(rootMeshPositionPtr++) = p0;
					*(rootMeshTangentPtr++) = new Vector4(localRootPerp.x, localRootPerp.y, localRootPerp.z, 1.0f);
					*(rootMeshNormalPtr++) = localRootDir;
				}

				// write indices
				for (uint i = 0; i != strandCount; i++)
				{
					*(indicesPtr++) = i;
				}

				if (strandCount <= UInt16.MaxValue)
				{
					var indicesPtrRead = (uint*)indices.GetUnsafePtr();
					var indicesPtrWrite = (ushort*)indices.GetUnsafePtr();

					for (uint i = 0; i != strandCount; i++)
					{
						*(indicesPtrWrite++) = (ushort)(*(indicesPtrRead++));
					}

					indexFormat = IndexFormat.UInt16;
					indexStride = sizeof(ushort);
				}

				// apply to mesh
				var meshVertexCount = strandCount;
				var meshUpdateFlags = MESH_UPDATE_UNCHECKED;
				{
					meshRoots.SetVertexBufferParams(meshVertexCount,
						new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, dimension: 3, stream: 0),
						new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3, stream: 1),
						new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, dimension: 4, stream: 2));
					{
						meshRoots.SetVertexBufferData(rootMeshPosition, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
						meshRoots.SetVertexBufferData(rootMeshNormal, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);
						meshRoots.SetVertexBufferData(rootMeshTangent, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 2, meshUpdateFlags);
					}

					meshRoots.SetIndexBufferParams(indexCount, indexFormat);
					{
						switch (indexFormat)
						{
							case IndexFormat.UInt16: meshRoots.SetIndexBufferData(indices.Reinterpret<byte, ushort>(), dataStart: 0, meshBufferStart: 0, indexCount, meshUpdateFlags); break;
							case IndexFormat.UInt32: meshRoots.SetIndexBufferData(indices.Reinterpret<byte, uint>(), dataStart: 0, meshBufferStart: 0, indexCount, meshUpdateFlags); break;
						}
					}

					meshRoots.subMeshCount = 1;
					meshRoots.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Points) { vertexCount = meshVertexCount }, meshUpdateFlags);
					meshRoots.RecalculateBounds();
				}
			}
		}

		struct RenderMeshData : IDisposable
		{
			public NativeArray<byte> vertices;
			public NativeArray<byte> indices;

			public VertexAttributeFormat vertexFormat;
			public int vertexStride;
			public int vertexCount;

			public IndexFormat indexFormat;
			public int indexStride;
			public int indexCount;

			public void Dispose()
			{
				vertices.Dispose();
				indices.Dispose();
			}
		}

		static unsafe RenderMeshData CreateRenderMeshData(int strandCount, int strandParticleCount, int strandParticleVertexCount, int indexCount, Allocator allocator)
		{
			if (strandParticleCount > ushort.MaxValue)
			{
				strandParticleCount = ushort.MaxValue;
				Debug.LogWarning("Creating renderer data with truncated number of particles per strand (input exceeds maximum of 65535 particles per strand).");
			}

			RenderMeshData data;
			{
				var reqBitsStrandIndex = 32 - math.lzcnt(strandCount);
				var reqBitsVertexIndex = 32 - math.lzcnt(strandParticleCount);
				var reqBitsVertexFacet = 32 - math.lzcnt(strandParticleVertexCount - 1);

				data.vertexCount = strandCount * strandParticleCount * strandParticleVertexCount;
				data.vertexFormat = VertexAttributeFormat.UNorm16;
				data.vertexStride = 4 * sizeof(ushort);
				{
					if (reqBitsStrandIndex + reqBitsVertexFacet <= 24 && reqBitsVertexIndex <= 8)
					{
						data.vertexFormat = VertexAttributeFormat.UNorm8;
						data.vertexStride = 4 * sizeof(byte);
					}
				}

				data.indexCount = indexCount;
				data.indexFormat = IndexFormat.UInt32;
				data.indexStride = sizeof(uint);

				data.vertices = new NativeArray<byte>(data.vertexCount * data.vertexStride, allocator, NativeArrayOptions.UninitializedMemory);
				{
					switch (data.vertexFormat)
					{
						case VertexAttributeFormat.UNorm8:
							//	24-n bit strand index (i)
							//	0..8 bit vertex facet (k)
							//	   8 bit vertex index (j)
							//
							//	[ iiii iiii | iiii iiii | iiii kkkk | jjjj jjjj ]
							//	  ----w----   ----z----   ----y----   ----x----
							{
								var lshStrandIndex = 8 + reqBitsVertexFacet;
								var lshVertexFacet = 8;

								var verticesPtr = (uint*)data.vertices.GetUnsafePtr();

								for (uint i = 0; i != strandCount; i++)
								{
									for (uint j = 0; j != strandParticleCount; j++)
									{
										for (uint k = 0; k != strandParticleVertexCount; k++)
										{
											*(verticesPtr++) =
												(i << lshStrandIndex) |
												(k << lshVertexFacet) |
												(j);
										}
									}
								}
							}
							break;

						case VertexAttributeFormat.UNorm16:
							//	 48-n bit strand index (i)
							//	   16 bit vertex index (k)
							//	0..16 bit vertex facet (j)
							//
							//	[ iiii iiii iiii iiii | iiii iiii iiii iiii | iiii iiii ffff ffff | jjjj jjjj jjjj jjjj ]
							//	  ---------w---------   ---------z---------   ---------y---------   ---------x---------
							{
								var lshStrandIndex = 16 + reqBitsVertexFacet;
								var lshVertexFacet = 16;

								var verticesPtr = (ulong*)data.vertices.GetUnsafePtr();

								for (uint i = 0; i != strandCount; i++)
								{
									for (uint j = 0; j != strandParticleCount; j++)
									{
										for (uint k = 0; k != strandParticleVertexCount; k++)
										{
											*(verticesPtr++) =
												((ulong)i << lshStrandIndex) |
												((ulong)k << lshVertexFacet) |
												((ulong)j);
										}
									}
								}
							}
							break;
					}
				}

				data.indices = new NativeArray<byte>(data.indexCount * data.indexStride, allocator, NativeArrayOptions.UninitializedMemory);
				{
					// note: indices are written by caller
				}
			}

			return data;
		}

		static unsafe void ApplyRenderMeshData(Mesh mesh, MeshTopology topology, RenderMeshData data, in Bounds bounds)
		{
			/*
			if (topology == MeshTopology.Triangles)
			{
				var _DecodeStrandIndex = 1u;
				var _DecodeVertexComponentValue = (1 << 16) - 1;
				var _DecodeVertexComponentWidth = 32 - math.lzcnt(_DecodeVertexComponentValue);
				var _DecodeVertexCount = 3;
				var _DecodeVertexWidth = _DecodeVertexCount > 0 ? 32 - math.lzcnt(_DecodeVertexCount - 1) : 0;
				var _DecodeVertexValue = math.ceilpow2(_DecodeVertexCount);

				Debug.Log("BEGIN strandIndex " + _DecodeStrandIndex + " vertexCount " + _DecodeVertexCount + " vertexWidth " + _DecodeVertexWidth);
				for (uint k = 0; k != _DecodeVertexCount; k++)
				{
					uint pack = (_DecodeStrandIndex << _DecodeVertexWidth) | k;
					//Debug.Log(Convert.ToString(enc, 2).PadLeft(8, '0'));

					float4 packedID = 0;
					packedID.y = (float)pack / _DecodeVertexComponentValue;

					uint4 unpack = (uint4)math.round(packedID * _DecodeVertexComponentValue);
					var decodedStrandIndex = unpack.y >> _DecodeVertexWidth;
					var decodedVertexFacet = unpack.y & ((1 << _DecodeVertexWidth) - 1);
					var decodedTubularU = math.frac(packedID.y * ((float)_DecodeVertexComponentValue / (1 << _DecodeVertexWidth))) * ((1 << _DecodeVertexWidth) / (float)_DecodeVertexCount);
					Debug.Log("k " + k + " ... decodedStrandIndex " + decodedStrandIndex + " decodedVertexFacet " + decodedVertexFacet + " decodedTubularU " + decodedTubularU);
				}
			}
			*/

			if (data.vertexCount <= UInt16.MaxValue)
			{
				var indicesPtrRead = (uint*)data.indices.GetUnsafePtr();
				var indicesPtrWrite = (ushort*)data.indices.GetUnsafePtr();

				for (uint i = 0; i != data.indexCount; i++)
				{
					*(indicesPtrWrite++) = (ushort)(*(indicesPtrRead++));
				}

				data.indexFormat = IndexFormat.UInt16;
				data.indexStride = sizeof(ushort);
			}

			var meshUpdateFlags = MESH_UPDATE_UNCHECKED;
			{
				mesh.SetVertexBufferParams(data.vertexCount, new VertexAttributeDescriptor(VertexAttribute.TexCoord0, data.vertexFormat, dimension: 4, stream: 0));
				{
					switch (data.vertexFormat)
					{
						case VertexAttributeFormat.UNorm8: mesh.SetVertexBufferData(data.vertices.Reinterpret<byte, uint>(), dataStart: 0, meshBufferStart: 0, data.vertexCount, stream: 0, meshUpdateFlags); break;
						case VertexAttributeFormat.UNorm16: mesh.SetVertexBufferData(data.vertices.Reinterpret<byte, ulong>(), dataStart: 0, meshBufferStart: 0, data.vertexCount, stream: 0, meshUpdateFlags); break;
					}
				}

				mesh.SetIndexBufferParams(data.indexCount, data.indexFormat);
				{
					switch (data.indexFormat)
					{
						case IndexFormat.UInt16: mesh.SetIndexBufferData(data.indices.Reinterpret<byte, ushort>(), dataStart: 0, meshBufferStart: 0, data.indexCount, meshUpdateFlags); break;
						case IndexFormat.UInt32: mesh.SetIndexBufferData(data.indices.Reinterpret<byte, uint>(), dataStart: 0, meshBufferStart: 0, data.indexCount, meshUpdateFlags); break;
					}
				}

				mesh.subMeshCount = 1;
				mesh.SetSubMesh(0, new SubMeshDescriptor(0, data.indexCount, topology) { vertexCount = data.vertexCount, bounds = bounds }, meshUpdateFlags);
				mesh.bounds = bounds;
			}
		}

		public static unsafe void BuildRenderMeshLines(Mesh meshLines, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var perLineVertices = strandParticleCount;
			var perLineSegments = perLineVertices - 1;
			var perLineIndices = perLineSegments * 2;

			using (var data = CreateRenderMeshData(strandCount, strandParticleCount, strandParticleVertexCount: 1, indexCount: strandCount * perLineIndices, Allocator.Temp))
			{
				// write indices
				{
					var indicesPtr = (uint*)data.indices.GetUnsafePtr();

					for (uint i = 0, segmentBase = 0; i != strandCount; i++, segmentBase++)
					{
						for (uint j = 0; j != perLineSegments; j++, segmentBase++)
						{
							*(indicesPtr++) = segmentBase;
							*(indicesPtr++) = segmentBase + 1;
						}
					}
				}

				// apply to mesh
				ApplyRenderMeshData(meshLines, MeshTopology.Lines, data, bounds);
			}
		}

		public static unsafe void BuildRenderMeshStrips(Mesh meshStrips, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var perStripVertices = strandParticleCount * 2;
			var perStripSegments = strandParticleCount - 1;
			var perStripTriangles = perStripSegments * 2;
			var perStripIndices = perStripTriangles * 3;

			using (var data = CreateRenderMeshData(strandCount, strandParticleCount, strandParticleVertexCount : 2, indexCount: strandCount * perStripIndices, Allocator.Temp))
			{
				// write indices
				{
					var indicesPtr = (uint*)data.indices.GetUnsafePtr();

					for (uint i = 0, segmentBase = 0; i != strandCount; i++, segmentBase += 2)
					{
						for (uint j = 0; j != perStripSegments; j++, segmentBase += 2)
						{
							//  :  .   :
							//  |,     |
							//  4------5
							//  |    ,´|
							//  |  ,´  |      etc.
							//  |,´    |    
							//  2------3    12----13
							//  |    ,´|    |    ,´|
							//  |  ,´  |    |  ,´  |
							//  |,´    |    |,´    |
							//  0------1    10----11
							//  .
							//  |
							//  '--- segmentBase

							// indices for first triangle
							*(indicesPtr++) = segmentBase + 0;
							*(indicesPtr++) = segmentBase + 3;
							*(indicesPtr++) = segmentBase + 1;

							// indices for second triangle
							*(indicesPtr++) = segmentBase + 0;
							*(indicesPtr++) = segmentBase + 2;
							*(indicesPtr++) = segmentBase + 3;
						}
					}
				}

				// apply to mesh
				ApplyRenderMeshData(meshStrips, MeshTopology.Triangles, data, bounds);
			}
		}

		public static unsafe void BuildRenderMeshTubes(Mesh meshTubes, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			const int numSides = 4;
			
			var perTubeVertices = strandParticleCount * numSides;
			var perTubeSegments = strandParticleCount - 1;
			var perTubeTriangles = (perTubeSegments * numSides * 2) + 4;
			var perTubeIndices = perTubeTriangles * 3;
			
			using (var data = CreateRenderMeshData(strandCount, strandParticleCount, strandParticleVertexCount: numSides, indexCount: strandCount * perTubeIndices, Allocator.Temp))
			{
				void CreateTriangle(ref uint* indicesPtr, uint offset, uint i0, uint i1, uint i2)
				{
					*(indicesPtr++) = offset + i0;
					*(indicesPtr++) = offset + i1;
					*(indicesPtr++) = offset + i2;
				}

				// write indices
				{
					var indicesPtr = (uint*)data.indices.GetUnsafePtr();

					for (uint i = 0, segmentBase = 0; i != strandCount; i++, segmentBase += 4)
					{
						// end cap a
						{
							CreateTriangle(ref indicesPtr, segmentBase, 0, 2, 3);
							CreateTriangle(ref indicesPtr, segmentBase, 0, 1, 2);
						}

						for (uint j = 0; j != perTubeSegments; j++, segmentBase += 4)
						{
							//     :      :
							//     7------6
							//   ,´:    ,´|        4------5------6------7------4
							//  4------5  |        |    ,´|    ,´|    ,´|    ,´|
							//  |  :   |  |   =>   |  ,´  |  ,´  |  ,´  |  ,´  |
							//  |  3 - |- 2        |,´    |,´    |,´    |,´    |
							//  |,´    |,´         0------1------2------3------0
							//  0------1
							//  .
							//  |
							//  '--- segmentBase

							// side a
							{
								CreateTriangle(ref indicesPtr, segmentBase, 0, 5, 1);
								CreateTriangle(ref indicesPtr, segmentBase, 0, 4, 5);
							}

							// side b
							{
								CreateTriangle(ref indicesPtr, segmentBase, 1, 6, 2);
								CreateTriangle(ref indicesPtr, segmentBase, 1, 5, 6);
							}

							// side c
							{
								CreateTriangle(ref indicesPtr, segmentBase, 2, 7, 3);
								CreateTriangle(ref indicesPtr, segmentBase, 2, 6, 7);
							}

							// side d
							{
								CreateTriangle(ref indicesPtr, segmentBase, 3, 4, 0);
								CreateTriangle(ref indicesPtr, segmentBase, 3, 7, 4);
							}
						}

						// end cap b
						{
							CreateTriangle(ref indicesPtr, segmentBase, 0, 2, 1);
							CreateTriangle(ref indicesPtr, segmentBase, 0, 3, 2);
						}
					}
				}

				// apply to mesh
				ApplyRenderMeshData(meshTubes, MeshTopology.Triangles, data, bounds);
			}
		}

		public static Mesh CreateMeshRoots(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, Vector3[] particlePosition)
		{
			var meshRoots = new Mesh();
			{
				meshRoots.hideFlags = hideFlags;
				meshRoots.name = "Roots";
				BuildMeshRoots(meshRoots, memoryLayout, strandCount, strandParticleCount, particlePosition);
			}
			return meshRoots;
		}

		public static Mesh CreateMeshRootsIfNull(ref Mesh meshRoots, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, Vector3[] particlePosition)
		{
			if (meshRoots == null)
				meshRoots = CreateMeshRoots(hideFlags, memoryLayout, strandCount, strandParticleCount, particlePosition);

			return meshRoots;
		}

		public delegate Mesh FnCreateRenderMesh(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds);
		public delegate void FnCreateRenderMeshIfNull(ref Mesh mesh, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds);

		public static Mesh CreateRenderMeshLines(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var meshLines = new Mesh();
			{
				meshLines.hideFlags = hideFlags;
				meshLines.name = "X-Lines";
				BuildRenderMeshLines(meshLines, memoryLayout, strandCount, strandParticleCount, bounds);
			}
			return meshLines;
		}

		public static Mesh CreateRenderMeshLinesIfNull(ref Mesh meshLines, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			if (meshLines == null)
				meshLines = CreateRenderMeshLines(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);

			return meshLines;
		}

		public static Mesh CreateRenderMeshStrips(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var meshStrips = new Mesh();
			{
				meshStrips.hideFlags = hideFlags;
				meshStrips.name = "X-Strips";
				BuildRenderMeshStrips(meshStrips, memoryLayout, strandCount, strandParticleCount, bounds);
			}
			return meshStrips;
		}

		public static Mesh CreateRenderMeshStripsIfNull(ref Mesh meshStrips, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			if (meshStrips == null)
				meshStrips = CreateRenderMeshStrips(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);

			return meshStrips;
		}
		
		public static Mesh CreateRenderMeshTubes(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var meshTubes = new Mesh();
			{
				meshTubes.hideFlags = hideFlags;
				meshTubes.name = "X-Tubes";
				BuildRenderMeshTubes(meshTubes, memoryLayout, strandCount, strandParticleCount, bounds);
			}
			return meshTubes;
		}

		public static Mesh CreateRenderMeshTubesIfNull(ref Mesh meshTubes, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			if (meshTubes == null)
				meshTubes = CreateRenderMeshTubes(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);

			return meshTubes;
		}

		public static Mesh CreateMeshInstance(Mesh original, HideFlags hideFlags)
		{
			var instance = Mesh.Instantiate(original);
			{
				instance.name = original.name + "(Instance)";
				instance.hideFlags = hideFlags;
			}
			return instance;
		}

		public static Mesh CreateMeshInstanceIfNull(ref Mesh instance, Mesh original, HideFlags hideFlags)
		{
			if (instance == null)
				instance = CreateMeshInstance(original, hideFlags);

			return instance;
		}

		//------------
		// containers

		public static GameObject CreateContainer(string name, GameObject parentContainer, HideFlags hideFlags)
		{
			var container = new GameObject(name);
			{
				container.transform.SetParent(parentContainer.transform, worldPositionStays: false);
				container.hideFlags = hideFlags;
			}
			return container;
		}

		public static T CreateComponent<T>(GameObject container, HideFlags hideFlags) where T : Component
		{
			var component = container.AddComponent<T>();
			{
				component.hideFlags = hideFlags;
			}
			return component;
		}

		public static void CreateComponentIfNull<T>(ref T component, GameObject container, HideFlags hideFlags) where T : Component
		{
			if (component == null)
				component = CreateComponent<T>(container, hideFlags);
		}
	}
}
