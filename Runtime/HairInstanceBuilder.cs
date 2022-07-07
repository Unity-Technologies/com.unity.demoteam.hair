using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

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
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceLines);
					CoreUtils.Destroy(strandGroupInstance.sceneObjects.meshInstanceStrips);
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

		public static unsafe void BuildMeshRoots(Mesh meshRoots, int strandCount, Vector3[] rootPosition, Vector3[] rootDirection)
		{
			using (var indices = new NativeArray<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var indicesPtr = (int*)indices.GetUnsafePtr();

				// write indices
				for (int i = 0; i != strandCount; i++)
				{
					*(indicesPtr++) = i;
				}

				// apply to mesh
				var meshVertexCount = strandCount;
				var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
				{
					meshRoots.SetVertexBufferParams(meshVertexCount,
						new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, dimension: 3, stream: 0),
						new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3, stream: 1)
					);

					meshRoots.SetVertexBufferData(rootPosition, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
					meshRoots.SetVertexBufferData(rootDirection, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

					meshRoots.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
					meshRoots.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
					meshRoots.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Points), meshUpdateFlags);
					meshRoots.RecalculateBounds();
				}
			}
		}

		public static unsafe void BuildMeshLines(Mesh meshLines, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var perLineVertices = strandParticleCount;
			var perLineSegments = perLineVertices - 1;
			var perLineIndices = perLineSegments * 2;

			var unormU0 = (uint)(UInt16.MaxValue * 0.5f);
			var unormVk = UInt16.MaxValue / (float)perLineSegments;

			using (var vertexID = new NativeArray<float>(strandCount * perLineVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var vertexUV = new NativeArray<uint>(strandCount * perLineVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var indices = new NativeArray<int>(strandCount * perLineIndices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var vertexIDPtr = (float*)vertexID.GetUnsafePtr();
				var vertexUVPtr = (uint*)vertexUV.GetUnsafePtr();
				var indicesPtr = (int*)indices.GetUnsafePtr();

				// write vertex ID
				for (int i = 0, k = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, strandCount, strandParticleCount, i, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						*(vertexIDPtr++) = k++;// vertexID
					}
				}

				// write vertex UV
				for (int i = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, strandCount, strandParticleCount, i, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin, k = 0; j != strandParticleEnd; j += strandParticleStride, k++)
					{
						var unormV = (uint)(unormVk * k);
						{
							*(vertexUVPtr++) = (unormV << 16) | unormU0;// texCoord
						}
					}
				}

				// write indices
				for (int i = 0, segmentBase = 0; i != strandCount; i++, segmentBase++)
				{
					for (int j = 0; j != perLineSegments; j++, segmentBase++)
					{
						*(indicesPtr++) = segmentBase;
						*(indicesPtr++) = segmentBase + 1;
					}
				}

				// apply to mesh
				var meshVertexCount = strandCount * perLineVertices;
				var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
				{
					meshLines.SetVertexBufferParams(meshVertexCount,
						new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 1, stream: 0),// vertexID
						new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm16, dimension: 2, stream: 1) // vertexUV
					);

					meshLines.SetVertexBufferData(vertexID, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
					meshLines.SetVertexBufferData(vertexUV, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

					meshLines.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
					meshLines.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
					meshLines.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Lines), meshUpdateFlags);
					meshLines.bounds = bounds;
				}
			}
		}

		public static unsafe void BuildMeshStrips(Mesh meshStrips, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var perStripVertices = 2 * strandParticleCount;
			var perStripSegments = strandParticleCount - 1;
			var perStripTriangles = 2 * perStripSegments;
			var perStripsIndices = perStripTriangles * 3;

			var unormU0 = (uint)(UInt16.MaxValue * 0.0f);
			var unormU1 = (uint)(UInt16.MaxValue * 1.0f);
			var unormVs = UInt16.MaxValue / (float)perStripSegments;

			using (var vertexID = new NativeArray<float>(strandCount * perStripVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var vertexUV = new NativeArray<uint>(strandCount * perStripVertices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var indices = new NativeArray<int>(strandCount * perStripsIndices, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var vertexIDPtr = (float*)vertexID.GetUnsafePtr();
				var vertexUVPtr = (uint*)vertexUV.GetUnsafePtr();
				var indicesPtr = (int*)indices.GetUnsafePtr();

				// write vertex ID
				for (int i = 0, k = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, strandCount, strandParticleCount, i, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						// two vertices per particle
						*(vertexIDPtr++) = k++;// vertexID
						*(vertexIDPtr++) = k++;// ...
					}
				}

				// write vertex UV
				for (int i = 0; i != strandCount; i++)
				{
					HairAssetUtility.DeclareStrandIterator(memoryLayout, strandCount, strandParticleCount, i, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin, k = 0; j != strandParticleEnd; j += strandParticleStride, k++)
					{
						var unormV = (uint)(unormVs * k);
						{
							// two vertices per particle
							*(vertexUVPtr++) = (unormV << 16) | unormU0;// texCoord
							*(vertexUVPtr++) = (unormV << 16) | unormU1;// ...
						}
					}
				}

				// write indices
				for (int i = 0, segmentBase = 0; i != strandCount; i++, segmentBase += 2)
				{
					for (int j = 0; j != perStripSegments; j++, segmentBase += 2)
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
						*(indicesPtr++) = segmentBase + 1;
						*(indicesPtr++) = segmentBase + 3;

						// indices for second triangle
						*(indicesPtr++) = segmentBase + 0;
						*(indicesPtr++) = segmentBase + 3;
						*(indicesPtr++) = segmentBase + 2;
					}
				}

				// apply to mesh asset
				var meshVertexCount = strandCount * perStripVertices;
				var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
				{
					meshStrips.SetVertexBufferParams(meshVertexCount,
						new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 1, stream: 0),// vertexID
						new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm16, dimension: 2, stream: 1) // vertexUV
					);

					meshStrips.SetVertexBufferData(vertexID, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
					meshStrips.SetVertexBufferData(vertexUV, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

					meshStrips.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
					meshStrips.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
					meshStrips.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles), meshUpdateFlags);
					meshStrips.bounds = bounds;
				}
			}
		}

		public static Mesh CreateMeshRoots(HideFlags hideFlags, int strandCount, Vector3[] rootPosition, Vector3[] rootDirection)
		{
			var meshRoots = new Mesh();
			{
				meshRoots.hideFlags = hideFlags;
				meshRoots.name = "Roots";
				BuildMeshRoots(meshRoots, strandCount, rootPosition, rootDirection);
			}
			return meshRoots;
		}

		public static void CreateMeshRootsIfNull(ref Mesh meshRoots, HideFlags hideFlags, int strandCount, Vector3[] rootPosition, Vector3[] rootDirection)
		{
			if (meshRoots == null)
				meshRoots = CreateMeshRoots(hideFlags, strandCount, rootPosition, rootDirection);
		}

		public static Mesh CreateMeshLines(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var meshLines = new Mesh();
			{
				meshLines.hideFlags = hideFlags;
				meshLines.name = "X-Lines";
				BuildMeshLines(meshLines, memoryLayout, strandCount, strandParticleCount, bounds);
			}
			return meshLines;
		}

		public static void CreateMeshLinesIfNull(ref Mesh meshLines, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			if (meshLines == null)
				meshLines = CreateMeshLines(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);
		}

		public static Mesh CreateMeshStrips(HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			var meshStrips = new Mesh();
			{
				meshStrips.hideFlags = hideFlags;
				meshStrips.name = "X-Strips";
				BuildMeshStrips(meshStrips, memoryLayout, strandCount, strandParticleCount, bounds);
			}
			return meshStrips;
		}

		public static void CreateMeshStripsIfNull(ref Mesh meshStrips, HideFlags hideFlags, HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, in Bounds bounds)
		{
			if (meshStrips == null)
				meshStrips = CreateMeshStrips(hideFlags, memoryLayout, strandCount, strandParticleCount, bounds);
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

		public static void CreateMeshInstanceIfNull(ref Mesh instance, Mesh original, HideFlags hideFlags)
		{
			if (instance == null)
				instance = CreateMeshInstance(original, hideFlags);
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
