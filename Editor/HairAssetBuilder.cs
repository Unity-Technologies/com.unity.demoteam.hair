//#define VISIBLE_SUBASSETS
#define CLEAR_ALL_SUBASSETS

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if HAS_PACKAGE_UNITY_ALEMBIC
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	public static class HairAssetBuilder
	{
		public static void ClearHairAsset(HairAsset hairAsset)
		{
			// do nothing if already clean
			if (hairAsset.strandGroups == null)
				return;

			// unlink and destroy any sub-assets
#if CLEAR_ALL_SUBASSETS
			var subAssetsPath = AssetDatabase.GetAssetPath(hairAsset);
			var subAssets = AssetDatabase.LoadAllAssetsAtPath(subAssetsPath);
			{
				foreach (var subAsset in subAssets)
				{
					if (subAsset != hairAsset)
					{
						AssetDatabase.RemoveObjectFromAsset(subAsset);
						UnityEngine.Object.DestroyImmediate(subAsset);
					}
				}
			}
#else
			foreach (var strandGroup in hairAsset.strandGroups)
			{
				if (strandGroup.meshAssetRoots != null)
				{
					AssetDatabase.RemoveObjectFromAsset(strandGroup.meshAssetRoots);
					Mesh.DestroyImmediate(strandGroup.meshAssetRoots);
				}

				if (strandGroup.meshAssetLines != null)
				{
					AssetDatabase.RemoveObjectFromAsset(strandGroup.meshAssetLines);
					Mesh.DestroyImmediate(strandGroup.meshAssetLines);
				}

				if (strandGroup.meshAssetStrips != null)
				{
					AssetDatabase.RemoveObjectFromAsset(strandGroup.meshAssetStrips);
					Mesh.DestroyImmediate(strandGroup.meshAssetStrips);
				}
			}
#endif

			// clear
			hairAsset.strandGroups = null;
		}

		public static void BuildHairAsset(HairAsset hairAsset)
		{
			// clean up
			ClearHairAsset(hairAsset);

			// build the data
			switch (hairAsset.settingsBasic.type)
			{
				case HairAsset.Type.Alembic:
					BuildHairAsset(hairAsset, hairAsset.settingsAlembic, hairAsset.settingsBasic.memoryLayout);
					break;

				case HairAsset.Type.Procedural:
					BuildHairAsset(hairAsset, hairAsset.settingsProcedural, hairAsset.settingsBasic.memoryLayout);
					break;
			}

			// filter the data
			if (hairAsset.strandGroups != null)
			{
				ref var strandGroups = ref hairAsset.strandGroups;

				int validCount = 0;

				for (int i = 0; i != strandGroups.Length; i++)
				{
					if (strandGroups[i].strandCount > 0)
						strandGroups[validCount++] = strandGroups[i];
				}

				if (strandGroups.Length > validCount)
					Array.Resize(ref strandGroups, validCount);

				if (strandGroups.Length == 0)
					strandGroups = null;
			}

			// hash the data
			if (hairAsset.strandGroups != null)
			{
				var hash = new Hash128();
				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					hash.Append(hairAsset.strandGroups[i].meshAssetLines.GetInstanceID());
					hash.Append(hairAsset.strandGroups[i].particlePosition);
				}

				hairAsset.checksum = hash.ToString();
			}
			else
			{
				hairAsset.checksum = string.Empty;
			}

			// link sub-assets
			if (hairAsset.strandGroups != null)
			{
				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					AssetDatabase.AddObjectToAsset(hairAsset.strandGroups[i].meshAssetRoots, hairAsset);
					AssetDatabase.AddObjectToAsset(hairAsset.strandGroups[i].meshAssetLines, hairAsset);
					AssetDatabase.AddObjectToAsset(hairAsset.strandGroups[i].meshAssetStrips, hairAsset);

					hairAsset.strandGroups[i].meshAssetRoots.name = "Roots:" + i;
					hairAsset.strandGroups[i].meshAssetLines.name = "X-Lines:" + i;
					hairAsset.strandGroups[i].meshAssetStrips.name = "X-Strips:" + i;
				}
			}

			// dirty the asset
			EditorUtility.SetDirty(hairAsset);

#if VISIBLE_SUBASSETS
			// save and re-import to force hierearchy update
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(hairAsset), ImportAssetOptions.ForceUpdate);
#endif
		}

		public static void BuildHairAsset(HairAsset hairAsset, in HairAsset.SettingsAlembic settings, HairAsset.MemoryLayout memoryLayout)
		{
#if HAS_PACKAGE_UNITY_ALEMBIC
			// check stream present
			var alembic = settings.alembicAsset;
			if (alembic == null)
				return;

			// instantiate to load the data
			var alembicInstantiated = EditorUtility.IsPersistent(settings.alembicAsset);
			if (alembicInstantiated)
			{
				alembic = GameObject.Instantiate(alembic);
				alembic.gameObject.hideFlags |= HideFlags.DontSave;
			}

			// fetch all alembic curve sets 
			var curveSets = alembic.gameObject.GetComponentsInChildren<AlembicCurves>(includeInactive: true);
			if (curveSets.Length > 0)
			{
				// prep strand groups
				hairAsset.strandGroups = new HairAsset.StrandGroup[curveSets.Length];

				// build strand groups
				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					BuildHairAssetStrandGroup(ref hairAsset.strandGroups[i], settings, curveSets[i], memoryLayout);
				}
			}

			// destroy container
			if (alembicInstantiated)
			{
				GameObject.DestroyImmediate(alembic.gameObject);
			}
#else
			return;
#endif
		}

		public static void BuildHairAsset(HairAsset hairAsset, in HairAsset.SettingsProcedural settings, HairAsset.MemoryLayout memoryLayout)
		{
			// check placement target present
			var placementMesh = settings.placementMesh;
			if (placementMesh == null && settings.placement == HairAsset.SettingsProcedural.PlacementType.Mesh)
				return;

			// prep strand groups
			hairAsset.strandGroups = new HairAsset.StrandGroup[1];

			// build strand groups
			for (int i = 0; i != hairAsset.strandGroups.Length; i++)
			{
				BuildHairAssetStrandGroup(ref hairAsset.strandGroups[i], settings, memoryLayout);
			}
		}

#if HAS_PACKAGE_UNITY_ALEMBIC
		public static void BuildHairAssetStrandGroup(ref HairAsset.StrandGroup strandGroup, in HairAsset.SettingsAlembic settings, AlembicCurves curveSet, HairAsset.MemoryLayout memoryLayout)
		{
			//TODO require/recommend resampling or WARN if not all pairs are equidistant?

			// get curve data
			var curveVertexDataPosition = curveSet.Positions;
			var curveVertexOffsets = curveSet.CurveOffsets;

			var curveCount = curveVertexOffsets.Length;
			var curveVertexCount = -1;

			// validate curve data, conditionally apply resampling
			using (var curveVertexCountBuffer = new NativeArray<int>(curveCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var curveVertexCountMin = -1;
				var curveVertexCountMax = -1;

				unsafe
				{
					int* curveVertexCountPtr = (int*)curveVertexCountBuffer.GetUnsafePtr();

					// check min-max vertex count
					if (curveCount > 0)
					{
						for (int i = 1; i != curveCount; i++)
						{
							curveVertexCountPtr[i - 1] = curveVertexOffsets[i] - curveVertexOffsets[i - 1];
						}

						curveVertexCountPtr[curveCount - 1] = curveVertexDataPosition.Length - curveVertexOffsets[curveCount - 1];
						curveVertexCountMin = curveVertexCountPtr[0];
						curveVertexCountMax = curveVertexCountPtr[0];

						for (int i = 1; i != curveCount; i++)
						{
							Mathf.Min(curveVertexCountMin, curveVertexCountPtr[i]);
							Mathf.Max(curveVertexCountMax, curveVertexCountPtr[i]);
						}
					}

					// skip group if it has obviously degenerate curves
					if (curveVertexCountMin < 2)
					{
						Debug.LogWarningFormat("Hair Importer: skipping strand group due to degenerate curves with less than two vertices.");

						curveCount = 0;
						curveVertexCount = 0;
						curveVertexCountMin = 0;
						curveVertexCountMax = 0;
					}

					// resample curves if selected
					var resampling = settings.resampleCurves;
					var resamplingCount = settings.resampleParticleCount;
					var resamplingQuality = settings.resampleQuality;

					// resample always if there are curves with varying vertex count
					var resamplingRequired = !settings.resampleCurves && (curveVertexCountMin != curveVertexCountMax);
					if (resamplingRequired)
					{
						Debug.LogWarningFormat("Hair Importer: resampling strand group (to maximum vertex count within group) due to curves with varying vertex count.");

						resampling = true;
						resamplingCount = curveVertexCountMax;
						resamplingQuality = 1;
					}

					// apply resampling if selected or required
					if (resampling)
					{
						var curveVertexPositionsResampled = new Vector3[curveCount * resamplingCount];

						unsafe
						{
							fixed (Vector3* srcBasePtr = curveVertexDataPosition)
							fixed (Vector3* dstBasePtr = curveVertexPositionsResampled)
							{
								var dstOffset = 0;
								var dstCount = resamplingCount;

								for (int i = 0; i != curveCount; i++)
								{
									var srcOffset = curveVertexOffsets[i];
									var srcCount = curveVertexCountPtr[i];

									Resample(srcBasePtr + srcOffset, srcCount, dstBasePtr + dstOffset, dstCount, resamplingQuality);

									dstOffset += dstCount;
								}
							}
						}

						curveVertexDataPosition = curveVertexPositionsResampled;
						curveVertexCount = resamplingCount;
					}
					else
					{
						curveVertexCount = curveVertexCountMin;
					}
				}
			}

			// set strand counts
			strandGroup.strandCount = curveCount;
			strandGroup.strandParticleCount = curveVertexCount;

			// set memory layout
			strandGroup.particleMemoryLayout = memoryLayout;

			// prep strand buffers
			strandGroup.rootUV = new Vector2[curveCount];
			strandGroup.rootScale = new float[curveCount];// written in FinalizeStrandGroup(..)
			strandGroup.rootPosition = new Vector3[curveCount];
			strandGroup.rootDirection = new Vector3[curveCount];
			strandGroup.particlePosition = new Vector3[curveCount * curveVertexCount];

			// build strand buffers
			{
				for (int i = 0; i != curveCount; i++)
				{
					ref var p0 = ref curveVertexDataPosition[i * curveVertexCount];
					ref var p1 = ref curveVertexDataPosition[i * curveVertexCount + 1];

					strandGroup.rootUV[i] = Vector2.zero;//TODO alembic root UV's
					strandGroup.rootPosition[i] = p0;
					strandGroup.rootDirection[i] = Vector3.Normalize(p1 - p0);
				}

				switch (memoryLayout)
				{
					case HairAsset.MemoryLayout.Sequential:
						{
							curveVertexDataPosition.CopyTo(strandGroup.particlePosition, 0);
						}
						break;

					case HairAsset.MemoryLayout.Interleaved:
						unsafe
						{
							var srcStride = sizeof(Vector3);
							var dstStride = sizeof(Vector3) * strandGroup.strandCount;

							fixed (Vector3* srcBasePtr = curveVertexDataPosition)
							fixed (Vector3* dstBasePtr = strandGroup.particlePosition)
							{
								for (int i = 0; i != curveCount; i++)
								{
									var srcPtr = srcBasePtr + i * curveVertexCount;
									var dstPtr = dstBasePtr + i;

									UnsafeUtility.MemCpyStride(dstPtr, dstStride, srcPtr, srcStride, srcStride, curveVertexCount);
								}
							}
						}
						break;
				}
			}

			// calc derivative fields, create mesh assets
			FinalizeStrandGroup(ref strandGroup);
		}
#endif

		public static void BuildHairAssetStrandGroup(ref HairAsset.StrandGroup strandGroup, in HairAsset.SettingsProcedural settings, HairAsset.MemoryLayout memoryLayout)
		{
			// get target counts
			var generatedStrandCount = settings.strandCount;
			var generatedStrandParticleCount = settings.strandParticleCount;

			// prep temporaries
			using (var generatedRoots = new GeneratedRoots(generatedStrandCount))
			using (var generatedStrands = new GeneratedStrands(generatedStrandCount, generatedStrandParticleCount))
			{
				// build temporaries
				if (GenerateRoots(generatedRoots, settings))
				{
					GenerateStrands(generatedStrands, generatedRoots, settings, memoryLayout);
				}
				else
				{
					generatedStrandCount = 0;
					generatedStrandParticleCount = 0;
				}

				// set strand counts
				strandGroup.strandCount = generatedStrandCount;
				strandGroup.strandParticleCount = generatedStrandParticleCount;

				// set memory layout
				strandGroup.particleMemoryLayout = memoryLayout;

				// prep strand buffers
				strandGroup.rootUV = new Vector2[generatedStrandCount];
				strandGroup.rootScale = new float[generatedStrandCount];// written in FinalizeStrandGroup(..)
				strandGroup.rootPosition = new Vector3[generatedStrandCount];
				strandGroup.rootDirection = new Vector3[generatedStrandCount];
				strandGroup.particlePosition = new Vector3[generatedStrandCount * generatedStrandParticleCount];

				// populate strand buffers
				if (generatedStrandCount * generatedStrandParticleCount > 0)
				{
					generatedRoots.rootUV.CopyTo(strandGroup.rootUV);
					generatedRoots.rootPosition.CopyTo(strandGroup.rootPosition);
					generatedRoots.rootDirection.CopyTo(strandGroup.rootDirection);
					generatedStrands.particlePosition.CopyTo(strandGroup.particlePosition);
				}
			}

			// calc derivative fields, create mesh assets
			FinalizeStrandGroup(ref strandGroup);
		}

		static void FinalizeStrandGroup(ref HairAsset.StrandGroup strandGroup)
		{
			// get strand counts
			ref var strandCount = ref strandGroup.strandCount;
			ref var strandParticleCount = ref strandGroup.strandParticleCount;

			// finalize strand properties
			using (var strandLengths = new NativeArray<float>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				unsafe
				{
					// calc individual lengths
					var strandLengthsPtr = (float*)strandLengths.GetUnsafePtr();

					for (int i = 0; i != strandCount; i++)
					{
						var accuLength = 0.0f;

						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

						for (int j = strandParticleBegin + strandParticleStride; j != strandParticleEnd; j += strandParticleStride)
						{
							ref var p0 = ref strandGroup.particlePosition[j - strandParticleStride];
							ref var p1 = ref strandGroup.particlePosition[j];

							accuLength += Vector3.Distance(p0, p1);
						}

						strandLengthsPtr[i] = accuLength;
					}

					// find maximum strand length
					strandGroup.maxStrandLength = 0.0f;

					for (int i = 0; i != strandCount; i++)
					{
						strandGroup.maxStrandLength = Mathf.Max(strandGroup.maxStrandLength, strandLengthsPtr[i]);
					}

					// calc maximum particle interval
					strandGroup.maxParticleInterval = strandGroup.maxStrandLength / (strandGroup.strandParticleCount - 1);

					// calc scale factors within group
					for (int i = 0; i != strandCount; i++)
					{
						strandGroup.rootScale[i] = strandLengthsPtr[i] / strandGroup.maxStrandLength;
					}

					// find bounds
					if (strandGroup.particlePosition.Length > 0)
					{
						var boundsMin = strandGroup.particlePosition[0];
						var boundsMax = boundsMin;

						for (int i = 1; i != strandGroup.particlePosition.Length; i++)
						{
							boundsMin = Vector3.Min(boundsMin, strandGroup.particlePosition[i]);
							boundsMax = Vector3.Max(boundsMax, strandGroup.particlePosition[i]);
						}

						strandGroup.bounds = new Bounds(0.5f * (boundsMin + boundsMax), boundsMax - boundsMin);
					}
					else
					{
						strandGroup.bounds = new Bounds(Vector3.zero, Vector3.zero);
					}
				}
			}

			// build roots mesh
			unsafe
			{
				strandGroup.meshAssetRoots = new Mesh();
#if !VISIBLE_SUBASSETS
				strandGroup.meshAssetRoots.hideFlags |= HideFlags.HideInHierarchy;
#endif

				using (var indices = new NativeArray<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var indicesPtr = (int*)indices.GetUnsafePtr();

					// write indices
					for (int i = 0; i != strandCount; i++)
					{
						*(indicesPtr++) = i;
					}

					// apply to mesh
					var meshAsset = strandGroup.meshAssetRoots;
					var meshVertexCount = strandCount;
					var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
					{
						meshAsset.SetVertexBufferParams(meshVertexCount,
							new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, dimension: 3, stream: 0),
							new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, dimension: 3, stream: 1)
						);

						meshAsset.SetVertexBufferData(strandGroup.rootPosition, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
						meshAsset.SetVertexBufferData(strandGroup.rootDirection, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

						meshAsset.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
						meshAsset.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
						meshAsset.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Points), meshUpdateFlags);
						meshAsset.RecalculateBounds();
					}
				}
			}

			// build lines mesh
			unsafe
			{
				strandGroup.meshAssetLines = new Mesh();
#if !VISIBLE_SUBASSETS
				strandGroup.meshAssetLines.hideFlags |= HideFlags.HideInHierarchy;
#endif

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
						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

						for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
						{
							*(vertexIDPtr++) = k++;// vertexID
						}
					}

					// write vertex UV
					for (int i = 0; i != strandCount; i++)
					{
						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

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
					var meshAsset = strandGroup.meshAssetLines;
					var meshVertexCount = strandCount * perLineVertices;
					var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
					{
						meshAsset.SetVertexBufferParams(meshVertexCount,
							new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 1, stream: 0),// vertexID
							new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm16, dimension: 2, stream: 1) // vertexUV
						);

						meshAsset.SetVertexBufferData(vertexID, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
						meshAsset.SetVertexBufferData(vertexUV, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

						meshAsset.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
						meshAsset.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
						meshAsset.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Lines), meshUpdateFlags);
						meshAsset.bounds = strandGroup.bounds;
					}
				}
			}

			// build strips mesh
			unsafe
			{
				strandGroup.meshAssetStrips = new Mesh();
#if !VISIBLE_SUBASSETS
				strandGroup.meshAssetStrips.hideFlags |= HideFlags.HideInHierarchy;
#endif

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
						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

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
						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

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
					var meshAsset = strandGroup.meshAssetStrips;
					var meshVertexCount = strandCount * perStripVertices;
					var meshUpdateFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
					{
						meshAsset.SetVertexBufferParams(meshVertexCount,
							new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, dimension: 1, stream: 0),// vertexID
							new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm16, dimension: 2, stream: 1) // vertexUV
						);

						meshAsset.SetVertexBufferData(vertexID, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 0, meshUpdateFlags);
						meshAsset.SetVertexBufferData(vertexUV, dataStart: 0, meshBufferStart: 0, meshVertexCount, stream: 1, meshUpdateFlags);

						meshAsset.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
						meshAsset.SetIndexBufferData(indices, dataStart: 0, meshBufferStart: 0, indices.Length, meshUpdateFlags);
						meshAsset.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles), meshUpdateFlags);
						meshAsset.bounds = strandGroup.bounds;
					}
				}
			}
		}

		public struct GeneratedRoots : IDisposable
		{
			public struct StrandParameters
			{
				public float normalizedStrandLength;
				public float normalizedStrandDiameter;
				public float normalizedCurlRadius;
				public float normalizedCurlSlope;

				public static readonly StrandParameters defaults = new StrandParameters()
				{
					normalizedStrandLength = 1.0f,
					normalizedStrandDiameter = 1.0f,
					normalizedCurlRadius = 1.0f,
					normalizedCurlSlope = 1.0f,
				};
			}

			public int strandCount;
			public NativeArray<Vector2> rootUV;
			public NativeArray<Vector3> rootPosition;
			public NativeArray<Vector3> rootDirection;
			public NativeArray<StrandParameters> rootParameters;// R,G,B,A == Strand length, Strand diameter, Curl radius, Curl slope

			public GeneratedRoots(int strandCount, Allocator allocator = Allocator.Temp)
			{
				this.strandCount = strandCount;
				rootUV = new NativeArray<Vector2>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				rootPosition = new NativeArray<Vector3>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				rootDirection = new NativeArray<Vector3>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				rootParameters = new NativeArray<StrandParameters>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
			}

			public unsafe void GetUnsafePtrs(out Vector3* rootPositionPtr, out Vector3* rootDirectionPtr, out Vector2* rootUVPtr, out StrandParameters* rootParametersPtr)
			{
				rootUVPtr = (Vector2*)rootUV.GetUnsafePtr();
				rootPositionPtr = (Vector3*)rootPosition.GetUnsafePtr();
				rootDirectionPtr = (Vector3*)rootDirection.GetUnsafePtr();
				rootParametersPtr = (StrandParameters*)rootParameters.GetUnsafePtr();
			}

			public void Dispose()
			{
				rootUV.Dispose();
				rootPosition.Dispose();
				rootDirection.Dispose();
				rootParameters.Dispose();
			}
		}

		public struct GeneratedStrands : IDisposable
		{
			public int strandCount;
			public int strandParticleCount;
			public NativeArray<Vector3> particlePosition;

			public GeneratedStrands(int strandCount, int strandParticleCount, Allocator allocator = Allocator.Temp)
			{
				this.strandCount = strandCount;
				this.strandParticleCount = strandParticleCount;
				particlePosition = new NativeArray<Vector3>(strandCount * strandParticleCount, allocator, NativeArrayOptions.UninitializedMemory);
			}

			public unsafe void GetUnsafePtrs(out Vector3* particlePositionPtr)
			{
				particlePositionPtr = (Vector3*)particlePosition.GetUnsafePtr();
			}

			public void Dispose()
			{
				particlePosition.Dispose();
			}
		}

		public interface IRootGenerator
		{
			bool GenerateRoots(in GeneratedRoots roots);
		}

		static unsafe bool GenerateRoots(in GeneratedRoots roots, in HairAsset.SettingsProcedural settings)
		{
			switch (settings.placement)
			{
				case HairAsset.SettingsProcedural.PlacementType.Primitive:
					return GenerateRootsPrimitive(roots, settings);

				case HairAsset.SettingsProcedural.PlacementType.Custom:
					return (settings.placementGenerator as IRootGenerator)?.GenerateRoots(roots) ?? false;

				case HairAsset.SettingsProcedural.PlacementType.Mesh:
					return GenerateRootsMesh(roots, settings);

				default:
					return false;
			}
		}

		static unsafe bool GenerateRootsPrimitive(in GeneratedRoots generatedRoots, in HairAsset.SettingsProcedural settings)
		{
			generatedRoots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);

			var randSeq = new Unity.Mathematics.Random(257);

			switch (settings.placementPrimitive)
			{
				case HairAsset.SettingsProcedural.PrimitiveType.Curtain:
					{
						var step = 1.0f / (settings.strandCount - 1);

						var localDim = new Vector3(1.0f, 0.0f, 0.0f);
						var localDir = Vector3.down;

						for (int i = 0; i != settings.strandCount; i++)
						{
							var uv = new Vector2(i * step, 0.5f);

							rootPos[i] = new Vector3(localDim.x * (uv.x - 0.5f), 0.0f, 0.0f);
							rootDir[i] = localDir;
							rootUV0[i] = uv;
						}
					}
					break;

				case HairAsset.SettingsProcedural.PrimitiveType.StratifiedCurtain:
					{
						var step = 1.0f / settings.strandCount;

						var localDim = new Vector3(1.0f, 0.0f, step);
						var localDir = Vector3.down;

						for (int i = 0; i != settings.strandCount; i++)
						{
							var uvCell = randSeq.NextFloat2(0.0f, 1.0f);
							var uv = new Vector2((i + uvCell.x) * step, uvCell.y);

							rootPos[i] = new Vector3(localDim.x * (uv.x - 0.5f), 0.0f, localDim.z * (uv.y - 0.5f));
							rootDir[i] = localDir;
							rootUV0[i] = uv;
						}
					}
					break;

				case HairAsset.SettingsProcedural.PrimitiveType.Brush:
					{
						var localDim = new Vector3(1.0f, 0.0f, 1.0f);
						var localDir = Vector3.down;

						for (int i = 0; i != settings.strandCount; i++)
						{
							var uv = randSeq.NextFloat2(0.0f, 1.0f);

							rootPos[i] = new Vector3(localDim.x * (uv.x - 0.5f), 0.0f, localDim.z * (uv.y - 0.5f));
							rootDir[i] = localDir;
							rootUV0[i] = uv;
						}
					}
					break;

				case HairAsset.SettingsProcedural.PrimitiveType.Cap:
					{
						for (int i = 0; i != settings.strandCount; i++)
						{
							var localDir = randSeq.NextFloat3Direction();
							if (localDir.y < 0.0f)
								localDir.y = -localDir.y;

							rootPos[i] = new Vector3(localDir.x * 0.5f, localDir.y * 0.5f, localDir.z * 0.5f);
							rootDir[i] = localDir;
							rootUV0[i] = new Vector2(localDir.x * 0.5f + 0.5f, localDir.z * 0.5f + 0.5f);
						}
					}
					break;
			}

			for (int i = 0; i != settings.strandCount; i++)
			{
				rootVar[i] = GeneratedRoots.StrandParameters.defaults;
			}

			return true;// success
		}

		static unsafe bool GenerateRootsMesh(in GeneratedRoots generatedRoots, in HairAsset.SettingsProcedural settings)
		{
			generatedRoots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);

			using (var meshData = Mesh.AcquireReadOnlyMeshData(settings.placementMesh))
			using (var meshSampler = new TriMeshSampler(meshData[0], 0, Allocator.Temp, (uint)settings.placementMeshInclude))
			{
				bool IsTextureReadable(Texture2D texture)
				{
					return (texture != null && texture.isReadable);
				}

				if (IsTextureReadable(settings.placementDensity))
				{
					var density = settings.placementDensity;
					var densityThreshold = new Unity.Mathematics.Random(257);

					for (int i = 0; i != settings.strandCount; i++)
					{
						var sample = meshSampler.Next();
						var sampleDensity = density.GetPixelBilinear(sample.uv0.x, sample.uv0.y, mipLevel: 0);
						var sampleIteration = 0;// safety

						while (sampleDensity.r < densityThreshold.NextFloat() && sampleIteration++ < 200)
						{
							sample = meshSampler.Next();
							sampleDensity = density.GetPixelBilinear(sample.uv0.x, sample.uv0.y, mipLevel: 0);
						}

						rootPos[i] = sample.position;
						rootDir[i] = sample.normal;
						rootUV0[i] = sample.uv0;
					}
				}
				else
				{
					for (int i = 0; i != settings.strandCount; i++)
					{
						var sample = meshSampler.Next();
						rootPos[i] = sample.position;
						rootDir[i] = sample.normal;
						rootUV0[i] = sample.uv0;
					}
				}

				// apply painted direction
				if (IsTextureReadable(settings.paintedDirection))
				{
					for (int i = 0; i != settings.strandCount; i++)
					{
						// assume dxt5nm
						Vector4 packed = settings.paintedDirection.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, mipLevel: 0);
						{
							packed.x *= packed.w;
						}
						Vector3 n;
						{
							n.x = packed.x * 2.0f - 1.0f;
							n.y = packed.y * 2.0f - 1.0f;
							n.z = Mathf.Sqrt(1.0f - Mathf.Clamp01(n.x * n.x + n.y * n.y));
						}
						rootDir[i] = Vector3.Normalize(n);
					}
				}
				else
				{
					for (int i = 0; i != settings.strandCount; i++)
					{
						rootDir[i] = Vector3.Normalize(rootDir[i]);
					}
				}

				// apply painted parameters
				if (IsTextureReadable(settings.paintedParameters))
				{
					var rootVarSampled = (Vector4*)rootVar;

					for (int i = 0; i != settings.strandCount; i++)
					{
						rootVarSampled[i] = (Vector4)settings.paintedParameters.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, mipLevel: 0);
					}
				}
				else
				{
					for (int i = 0; i != settings.strandCount; i++)
					{
						rootVar[i] = GeneratedRoots.StrandParameters.defaults;
					}
				}
			}

			return true;// success
		}

		static unsafe bool GenerateStrands(in GeneratedStrands strands, in GeneratedRoots roots, in HairAsset.SettingsProcedural settings, HairAsset.MemoryLayout memoryLayout)
		{
			roots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);

			var pos = (Vector3*)strands.particlePosition.GetUnsafePtr();

			var particleInterval = settings.strandLength / (settings.strandParticleCount - 1);
			var particleIntervalVariation = settings.strandLengthVariation ? settings.strandLengthVariationAmount : 0.0f;

			var curlRadiusVariation = settings.curlVariation ? settings.curlVariationRadius : 0.0f;
			var curlSlopeVariation = settings.curlVariation ? settings.curlVariationSlope : 0.0f;

			var randSeqParticleInterval = new Unity.Mathematics.Random(257);
			var randSeqCurlPlaneUV = new Unity.Mathematics.Random(457);
			var randSeqCurlRadius = new Unity.Mathematics.Random(709);
			var randSeqCurlSlope = new Unity.Mathematics.Random(1171);

			for (int i = 0; i != settings.strandCount; i++)
			{
				var step = rootVar[i].normalizedStrandLength * particleInterval * Mathf.Lerp(1.0f, randSeqParticleInterval.NextFloat(), particleIntervalVariation);

				var curPos = rootPos[i];
				var curDir = rootDir[i];

				DeclareStrandIterator(memoryLayout, i, settings.strandCount, settings.strandParticleCount, out var strandParticleBegin, out var strandParticleStride, out var strandParticleEnd);

				if (settings.curl)
				{
					Vector3 NextVectorInPlane(ref Mathematics.Random randSeq, in Vector3 n)
					{
						Vector3 r;
						{
							do r = Vector3.ProjectOnPlane(randSeq.NextFloat3Direction(), n);
							while (Vector3.SqrMagnitude(r) < 1e-5f);
						}
						return r;
					};

					var curPlaneU = Vector3.Normalize(NextVectorInPlane(ref randSeqCurlPlaneUV, curDir));
					var curPlaneV = Vector3.Cross(curPlaneU, curDir);

					var targetRadius = rootVar[i].normalizedCurlRadius * settings.curlRadius * 0.01f * Mathf.Lerp(1.0f, randSeqCurlRadius.NextFloat(), curlRadiusVariation);
					var targetSlope = rootVar[i].normalizedCurlSlope * settings.curlSlope * Mathf.Lerp(1.0f, randSeqCurlSlope.NextFloat(), curlSlopeVariation);

					var stepPlane = step * Mathf.Cos(0.5f * Mathf.PI * targetSlope);
					if (stepPlane > 1.0f * targetRadius)
						stepPlane = 1.0f * targetRadius;

					var stepSlope = 0.0f;
					{
						switch (settings.curlSamplingStrategy)
						{
							// maintain slope
							case HairAsset.SettingsProcedural.CurlSamplingStrategy.RelaxStrandLength:
								stepSlope = step * Mathf.Sin(0.5f * Mathf.PI * targetSlope);
								break;

							// maintain strand length
							case HairAsset.SettingsProcedural.CurlSamplingStrategy.RelaxCurlSlope:
								stepSlope = Mathf.Sqrt(step * step - stepPlane * stepPlane);
								break;
						}
					}

					var a = (stepPlane > 0.0f) ? 2.0f * Mathf.Asin(stepPlane / (2.0f * targetRadius)) : 0.0f;
					var t = 0;

					curPos -= curPlaneU * targetRadius;

					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						var du = targetRadius * Mathf.Cos(t * a);
						var dv = targetRadius * Mathf.Sin(t * a);
						var dn = stepSlope * t;

						pos[j] =
							curPos +
							du * curPlaneU +
							dv * curPlaneV +
							dn * curDir;

						t++;
					}
				}
				else
				{
					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						pos[j] = curPos;
						curPos += step * curDir;
					}
				}
			}

			return true;// success
		}

		public static void DeclareStrandIterator(HairAsset.MemoryLayout memoryLayout, int strandIndex, int strandCount, int strandParticleCount,
			out int strandParticleBegin,
			out int strandParticleStride,
			out int strandParticleEnd)
		{
			switch (memoryLayout)
			{
				default:
				case HairAsset.MemoryLayout.Sequential:
					strandParticleBegin = strandIndex * strandParticleCount;
					strandParticleStride = 1;
					break;

				case HairAsset.MemoryLayout.Interleaved:
					strandParticleBegin = strandIndex;
					strandParticleStride = strandCount;
					break;
			}

			strandParticleEnd = strandParticleBegin + strandParticleStride * strandParticleCount;
		}

		public static unsafe void Resample(Vector3* srcPos, int srcCount, Vector3* dstPos, int dstCount, int iterations)
		{
			var length = 0.0f;

			for (int i = 1; i != srcCount; i++)
			{
				length += Vector3.Distance(srcPos[i], srcPos[i - 1]);
			}

			var dstLength = length;
			var dstSpacing = dstLength / (dstCount - 1);

			ResampleWithHint(srcPos, srcCount, out var srcIndex, dstPos, dstCount, out var dstIndex, dstSpacing);

			// run a couple of iterations
			for (int i = 0; i != iterations; i++)
			{
				if (dstIndex < dstCount || srcIndex < srcCount)
				{
					var remainder = Vector3.Distance(srcPos[srcCount - 1], dstPos[dstIndex - 1]);

					dstLength = (dstIndex - 1) * dstSpacing + remainder;
					dstSpacing = dstLength / (dstCount - 1);

					ResampleWithHint(srcPos, srcCount, out srcIndex, dstPos, dstCount, out dstIndex, dstSpacing);
				}
				else
				{
					break;
				}
			}

			// extrapolate tail for vertices that remain to be placed
			if (dstIndex < dstCount)
			{
				var dstPosPrev = dstPos[dstIndex - 1];
				var dstDirTail = Vector3.Normalize(srcPos[srcCount - 1] - dstPosPrev);

				while (dstIndex < dstCount)
				{
					dstPos[dstIndex] = dstPosPrev + dstDirTail * dstSpacing;
					dstPosPrev = dstPos[dstIndex++];
				}
			}
		}

		public static unsafe void ResampleWithHint(Vector3* srcPos, int srcCount, out int srcIndex, Vector3* dstPos, int dstCount, out int dstIndex, float dstSpacing)
		{
			dstPos[0] = srcPos[0];

			dstIndex = 1;
			srcIndex = 1;

			var dstPosPrev = dstPos[0];
			var dstSpacingSq = dstSpacing * dstSpacing;

			while (srcIndex < srcCount && dstIndex < dstCount)
			{
				var r = srcPos[srcIndex] - dstPosPrev;
				var rNormSq = Vector3.SqrMagnitude(r);
				if (rNormSq >= dstSpacingSq)
				{
					// find point on line between [srcIndex] and [srcIndex-1]
					var n = Vector3.Normalize(srcPos[srcIndex] - srcPos[srcIndex - 1]);
					var p = srcPos[srcIndex] - n * Vector3.Dot(n, r);

					// b = sqrt(cc - aa) 
					var aa = Vector3.SqrMagnitude(dstPosPrev - p);
					var bb = dstSpacingSq - aa;

					dstPos[dstIndex] = p + n * Mathf.Sqrt(bb);
					dstPosPrev = dstPos[dstIndex++];
				}
				else
				{
					srcIndex++;
				}
			}
		}
	}
}
