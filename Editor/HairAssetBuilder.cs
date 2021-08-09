//#define VISIBLE_SUBASSETS
#define CLEAR_ALL_SUBASSETS

using System;
using UnityEngine;
using UnityEngine.Profiling;
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
					BuildHairAssetAlembic(hairAsset);
					break;

				case HairAsset.Type.Procedural:
					BuildHairAssetProcedural(hairAsset);
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

					hairAsset.strandGroups[i].meshAssetRoots.name += (":" + i);
					hairAsset.strandGroups[i].meshAssetLines.name += (":" + i);
					hairAsset.strandGroups[i].meshAssetStrips.name += (":" + i);
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

		public static void BuildHairAssetAlembic(HairAsset hairAsset)
		{
#if HAS_PACKAGE_UNITY_ALEMBIC
			// check stream present
			var alembic = hairAsset.settingsAlembic.alembicAsset;
			if (alembic == null)
				return;

			// instantiate to load the data
			var alembicInstantiate = EditorUtility.IsPersistent(hairAsset.settingsAlembic.alembicAsset);
			if (alembicInstantiate)
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
					BuildStrandGroupAlembic(ref hairAsset.strandGroups[i], hairAsset, curveSets[i]);
				}
			}

			// destroy container
			if (alembicInstantiate)
			{
				GameObject.DestroyImmediate(alembic.gameObject);
			}
#else
			return;
#endif
		}

		public static void BuildHairAssetProcedural(HairAsset hairAsset)
		{
			// prep strand groups
			hairAsset.strandGroups = new HairAsset.StrandGroup[1];

			// build strand groups
			for (int i = 0; i != hairAsset.strandGroups.Length; i++)
			{
				BuildStrandGroupProcedural(ref hairAsset.strandGroups[i], hairAsset);
			}
		}

#if HAS_PACKAGE_UNITY_ALEMBIC
		public static void BuildStrandGroupAlembic(ref HairAsset.StrandGroup strandGroup, in HairAsset hairAsset, AlembicCurves curveSet)
		{
			//TODO require/recommend resampling or WARN if not all pairs are equidistant?
			var settings = hairAsset.settingsAlembic;

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
			strandGroup.particleMemoryLayout = hairAsset.settingsBasic.memoryLayout;

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

					strandGroup.rootUV[i] = settings.rootUVConstant;
					strandGroup.rootPosition[i] = p0;
					strandGroup.rootDirection[i] = Vector3.Normalize(p1 - p0);
				}

				if (settings.rootUV == HairAsset.SettingsAlembic.RootUV.ResolveFromMesh)
				{
					if (settings.rootUVMesh != null && settings.rootUVMesh.isReadable)
					{
						using (var meshData = Mesh.AcquireReadOnlyMeshData(settings.rootUVMesh))
						using (var meshQueries = new TriMeshQueries(meshData[0], Allocator.Temp))
						{
							for (int i = 0; i != curveCount; i++)
							{
								strandGroup.rootUV[i] = meshQueries.FindClosestTriangleUV(strandGroup.rootPosition[i]);
							}
						}
					}
				}

				switch (strandGroup.particleMemoryLayout)
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
			FinalizeStrandGroup(ref strandGroup, hairAsset);
		}
#endif

		public static void BuildStrandGroupProcedural(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var settings = hairAsset.settingsProcedural;

			// get target counts
			var generatedStrandCount = settings.strandCount;
			var generatedStrandParticleCount = settings.strandParticleCount;

			// prep temporaries
			using (var generatedRoots = new HairAssetProvider.GeneratedRoots(generatedStrandCount))
			using (var generatedStrands = new HairAssetProvider.GeneratedStrands(generatedStrandCount, generatedStrandParticleCount))
			{
				// build temporaries
				if (GenerateRoots(generatedRoots, settings))
				{
					GenerateStrands(generatedStrands, generatedRoots, settings, hairAsset.settingsBasic.memoryLayout);
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
				strandGroup.particleMemoryLayout = hairAsset.settingsBasic.memoryLayout;

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
			FinalizeStrandGroup(ref strandGroup, hairAsset);
		}

		static void FinalizeStrandGroup(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var strandCount = strandGroup.strandCount;
			var strandParticleCount = strandGroup.strandParticleCount;

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

						HairAssetUtility.DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandCount, strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

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

			// build lod clusters
			BuildLODClusters(ref strandGroup, hairAsset);

			// build mesh assets
			unsafe
			{
#if VISIBLE_SUBASSETS
				var hideFlags = HideFlags.None;
#else
				var hideFlags = HideFlags.HideInHierarchy;
#endif

				strandGroup.meshAssetRoots = HairInstanceBuilder.CreateMeshRoots(
					hideFlags,
					strandGroup.strandCount,
					strandGroup.rootPosition,
					strandGroup.rootDirection);

				strandGroup.meshAssetLines = HairInstanceBuilder.CreateMeshLines(
					hideFlags,
					strandGroup.particleMemoryLayout,
					strandGroup.strandCount,
					strandGroup.strandParticleCount,
					strandGroup.bounds);

				strandGroup.meshAssetStrips = HairInstanceBuilder.CreateMeshStrips(
					hideFlags,
					strandGroup.particleMemoryLayout,
					strandGroup.strandCount,
					strandGroup.strandParticleCount,
					strandGroup.bounds);
			}
		}


		static void BuildLODClusters(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			if (hairAsset.settingsBasic.kLODClusters)
			{
				switch (hairAsset.settingsBasic.kLODClustersProvider)
				{
					case HairAsset.LODClusters.Generated:
						Debug.LogWarning("not implemented");
						break;//TODO

					case HairAsset.LODClusters.UVMapped:
						BuildLODClustersUVMapped(ref strandGroup, hairAsset);
						break;
				}
			}
		}

		static unsafe void BuildLODClustersUVMapped(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var strandCount = strandGroup.strandCount;
			var strandParticleCount = strandGroup.strandParticleCount;

			// allocate clusters
			var clusterSet = new ClusterSet(0, strandCount, Allocator.Temp);
			var clusterSetDepth = 0;

			// allocate lod guide index
			var lodGuideCount = new NativeList<int>(Allocator.Temp);
			var lodGuideIndex = new NativeList<int>(Allocator.Temp);
			//TODO sensible initial capacity

			//-------------- UVMapped/Generated BASE LOD BEGIN -------------

			// count valid cluster maps
			var clusterMaps = hairAsset.settingsLODUVMapped.baseLODClusterMaps;
			var clusterMapsReadable = 0;
			{
				if (clusterMaps != null)
				{
					foreach (var clusterMap in clusterMaps)
					{
						if (clusterMap != null && clusterMap.isReadable)
							clusterMapsReadable++;
					}
				}

				if (clusterMapsReadable == 0)
				{
					Debug.LogWarning("no readable cluster maps");
				}
			}

			// build clusters from cluster maps
#if true
			using (var clusterLookup = new UnsafeHashMap<uint, int>(strandCount, Allocator.Temp))
#else
			using (var clusterLookup = new UnsafeHashMap<Color, int>(strandCount, Allocator.Temp))
#endif
			using (var strandCluster = new NativeArray<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var strandGuide = new NativeArray<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var strandClusterPtr = (int*)strandCluster.GetUnsafePtr();
				var strandGuidePtr = (int*)strandCluster.GetUnsafePtr();

				foreach (var clusterMap in clusterMaps)
				{
					if (clusterMap == null || clusterMap.isReadable == false)
						continue;

					// find clusters
					var cluster = -1;
					var clusterCount = 0;

					clusterLookup.Clear();

#if true
					using (var clusterMapLabels = new ConnectedComponentLabels(clusterMap, Allocator.Temp))
					{
						Debug.Log("found " + clusterMapLabels.labelCount + " connected components in " + clusterMap.name);

						for (int i = 0; i != strandCount; i++)
						{
							var x = Mathf.RoundToInt(strandGroup.rootUV[i].x * (clusterMap.width - 1));
							var y = Mathf.RoundToInt(strandGroup.rootUV[i].y * (clusterMap.height - 1));

							var clusterLabel = clusterMapLabels.GetPixelLabel(x, y);
							if (clusterLookup.TryGetValue(clusterLabel, out cluster) == false)
							{
								cluster = clusterCount++;
								clusterLookup.Add(clusterLabel, cluster);
							}

							strandClusterPtr[i] = cluster;
						}
					}
#else
					for (int i = 0; i != strandCount; i++)
					{
						var x = Mathf.RoundToInt(strandGroup.rootUV[i].x * (clusterMap.width - 1));
						var y = Mathf.RoundToInt(strandGroup.rootUV[i].y * (clusterMap.height - 1));
						var c = clusterMap.GetPixel(x, y, 0);
						{
							c.a = 0.0f;
						}

						if (clusterLookup.TryGetValue(c, out cluster) == false)
						{
							cluster = clusterCount++;
							clusterLookup.Add(c, cluster);
						}

						strandClusterPtr[i] = cluster;
					}

#endif

					// build clusters
					var nextClusterSet = new ClusterSet(clusterCount, strandCount, Allocator.Temp);
					{
						nextClusterSet.strandCluster.CopyFrom(strandCluster);
						nextClusterSet.SelectGuidesFrom(strandGroup, clusterSetDepth++);
						nextClusterSet.ImportGuidesFrom(clusterSet);
						//TODO handle (reject?) injection of lower granularity
						// e.g. if 3 guides in lower level points to 1 cluster in current
					}

					clusterSet.Dispose();
					clusterSet = nextClusterSet;

					// export strand->guide mapping to lod guide index
					for (int i = 0; i != strandCount; i++)
					{
						strandGuidePtr[i] = clusterSet.clusterGuidePtr[clusterSet.strandClusterPtr[i]];
					}

					lodGuideCount.Add(clusterSet.clusterCount);
					lodGuideIndex.AddRange(strandGuidePtr, strandCount);
				}
			}

			//-------------- UVMapped/Generated BASE LOD END -------------

			// build clusters from subdivision
			//TODO

			// build clusters from all strands
			if (lodGuideCount.Length == 0)
			{
				lodGuideCount.Add(strandCount);
				lodGuideIndex.Resize(strandCount, NativeArrayOptions.UninitializedMemory);

				var lodGuideIndexPtr = (int*)lodGuideIndex.GetUnsafePtr();
				{
					for (int i = 0; i != strandCount; i++)
					{
						lodGuideIndexPtr[i] = i;
					}
				}
			}

			// write lod guide index
			strandGroup.lodCount = lodGuideCount.Length;
#if true
			strandGroup.lodGuideCount = lodGuideCount.ToArray();
			strandGroup.lodGuideIndex = lodGuideIndex.ToArray();
#else
			{
				strandGroup.lodGuideCount = new int[lodGuideCount.Length];
				strandGroup.lodGuideIndex = new int[lodGuideIndex.Length];

				// write highest granularity to LOD 0, next-to-highest granularity to LOD 1, etc.
				fixed (int* lodGuideCountDstBase = strandGroup.lodGuideCount)
				fixed (int* lodGuideIndexDstBase = strandGroup.lodGuideIndex)
				{
					int* lodGuideCountCopyDst = lodGuideCountDstBase;
					int* lodGuideIndexCopyDst = lodGuideIndexDstBase;

					int* lodGuideCountCopySrc = (int*)lodGuideCount.GetUnsafePtr() + (strandGroup.lodCount);
					int* lodGuideIndexCopySrc = (int*)lodGuideIndex.GetUnsafePtr() + (strandGroup.lodCount * strandCount);

					for (int i = 0; i != strandGroup.lodCount; i++)
					{
						lodGuideCountCopySrc -= 1;
						lodGuideIndexCopySrc -= strandCount;

						UnsafeUtility.MemCpy(lodGuideCountCopyDst, lodGuideCountCopySrc, sizeof(int));
						UnsafeUtility.MemCpy(lodGuideIndexCopyDst, lodGuideIndexCopySrc, sizeof(int) * strandCount);

						lodGuideCountCopyDst += 1;
						lodGuideIndexCopyDst += strandCount;
					}
				}
			}
#endif

			// write lod thresholds
			strandGroup.lodThreshold = new float[strandGroup.lodCount];
			{
#if true
				var maxLOD = strandGroup.lodCount - 1;
				var maxLODGuideCount = strandGroup.lodGuideCount[maxLOD];

				strandGroup.lodThreshold[maxLOD] = 1.0f;

				for (int i = 0; i != maxLOD; i++)
				{
					strandGroup.lodThreshold[i] = strandGroup.lodGuideCount[i] / (float)maxLODGuideCount;
				}
#else
				strandGroup.lodThreshold[0] = 1.0f;

				for (int i = 1; i != strandGroup.lodCount; i++)
				{
					strandGroup.lodThreshold[i] = strandGroup.lodGuideCount[i] / (float)strandGroup.lodGuideCount[0];
				}
#endif
			}

			// apply remapping
			using (var remapping = new ClusterRemapping(clusterSet, Allocator.Temp))
			{
				// shuffle strands
				remapping.ApplyShuffle(strandGroup.rootUV, 0, 1, strandCount);
				remapping.ApplyShuffle(strandGroup.rootScale, 0, 1, strandCount);
				remapping.ApplyShuffle(strandGroup.rootPosition, 0, 1, strandCount);
				remapping.ApplyShuffle(strandGroup.rootDirection, 0, 1, strandCount);

				switch (strandGroup.particleMemoryLayout)
				{
					case HairAsset.MemoryLayout.Sequential:
						for (int i = 0; i != strandParticleCount; i++)
						{
							remapping.ApplyShuffle(strandGroup.particlePosition,
								attrOffset: i,
								attrStride: strandParticleCount,
								attrCount: strandCount);
						}
						break;

					case HairAsset.MemoryLayout.Interleaved:
						for (int i = 0; i != strandParticleCount; i++)
						{
							remapping.ApplyShuffle(strandGroup.particlePosition,
								attrOffset: i * strandCount,
								attrStride: 1,
								attrCount: strandCount);
						}
						break;
				}

				for (int i = 0; i != strandGroup.lodCount; i++)
				{
					remapping.ApplyShuffle(strandGroup.lodGuideIndex, i * strandCount, 1, strandCount);
				}

				// remap indices
				remapping.ApplyRemapping(strandGroup.lodGuideIndex);
			}

			// dispose lod guide index
			lodGuideCount.Dispose();
			lodGuideIndex.Dispose();

			// dispose clusters
			clusterSet.Dispose();
		}

		static unsafe bool GenerateRoots(in HairAssetProvider.GeneratedRoots roots, in HairAsset.SettingsProcedural settings)
		{
			switch (settings.placement)
			{
				case HairAsset.SettingsProcedural.PlacementType.Primitive:
					return GenerateRootsPrimitive(roots, settings);

				case HairAsset.SettingsProcedural.PlacementType.Custom:
					return (settings.placementProvider)?.GenerateRoots(roots) ?? false;

				case HairAsset.SettingsProcedural.PlacementType.Mesh:
					return GenerateRootsMesh(roots, settings);

				default:
					return false;
			}
		}

		static unsafe bool GenerateRootsPrimitive(in HairAssetProvider.GeneratedRoots generatedRoots, in HairAsset.SettingsProcedural settings)
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
				rootVar[i] = HairAssetProvider.GeneratedRoots.StrandParameters.defaults;
			}

			return true;// success
		}

		static unsafe bool GenerateRootsMesh(in HairAssetProvider.GeneratedRoots generatedRoots, in HairAsset.SettingsProcedural settings)
		{
			var placementMesh = settings.placementMesh;
			if (placementMesh == null)
				return false;

			generatedRoots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);

			using (var meshData = Mesh.AcquireReadOnlyMeshData(settings.placementMesh))
			using (var meshSampler = new TriMeshSampler(meshData[0], 0, Allocator.Temp, (uint)settings.placementMeshGroups))
			{
				bool IsTextureReadable(Texture2D texture)
				{
					return (texture != null && texture.isReadable);
				}

				if (IsTextureReadable(settings.mappedDensity))
				{
					var density = settings.mappedDensity;
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
				if (IsTextureReadable(settings.mappedDirection))
				{
					for (int i = 0; i != settings.strandCount; i++)
					{
						// assume dxt5nm
						Vector4 packed = settings.mappedDirection.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, mipLevel: 0);
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
				if (IsTextureReadable(settings.mappedParameters))
				{
					var rootVarSampled = (Vector4*)rootVar;

					for (int i = 0; i != settings.strandCount; i++)
					{
						rootVarSampled[i] = (Vector4)settings.mappedParameters.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, mipLevel: 0);
					}
				}
				else
				{
					for (int i = 0; i != settings.strandCount; i++)
					{
						rootVar[i] = HairAssetProvider.GeneratedRoots.StrandParameters.defaults;
					}
				}
			}

			return true;// success
		}

		static unsafe bool GenerateStrands(in HairAssetProvider.GeneratedStrands strands, in HairAssetProvider.GeneratedRoots roots, in HairAsset.SettingsProcedural settings, HairAsset.MemoryLayout memoryLayout)
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

				HairAssetUtility.DeclareStrandIterator(memoryLayout, i, settings.strandCount, settings.strandParticleCount, out var strandParticleBegin, out var strandParticleStride, out var strandParticleEnd);

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

		unsafe struct ClusterSet : IDisposable
		{
			public int clusterCount;
			public NativeArray<int> clusterDepth;
			public NativeArray<int> clusterGuide;
			public NativeArray<int> strandCluster;
			public int strandCount;

			public int* clusterDepthPtr;
			public int* clusterGuidePtr;
			public int* strandClusterPtr;

			public ClusterSet(int clusterCount, int strandCount, Allocator allocator)
			{
				this.clusterCount = clusterCount;
				this.clusterDepth = new NativeArray<int>(clusterCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.clusterGuide = new NativeArray<int>(clusterCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.strandCluster = new NativeArray<int>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.strandCount = strandCount;

				this.clusterDepthPtr = (int*)clusterDepth.GetUnsafePtr();
				this.clusterGuidePtr = (int*)clusterGuide.GetUnsafePtr();
				this.strandClusterPtr = (int*)strandCluster.GetUnsafePtr();
			}

			public void Dispose()
			{
				clusterDepth.Dispose();
				clusterGuide.Dispose();
				strandCluster.Dispose();
			}

			public void SelectGuidesFrom(HairAsset.StrandGroup strandGroup, int selectionDepth)
			{
				using (var clusterCenter = new NativeArray<Vector3>(clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (var clusterWeight = new NativeArray<float>(clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (var clusterScore = new NativeArray<float>(clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var clusterCenterPtr = (Vector3*)clusterCenter.GetUnsafePtr();
					var clusterWeightPtr = (float*)clusterWeight.GetUnsafePtr();
					var clusterScorePtr = (float*)clusterScore.GetUnsafePtr();
					var clusterGuidePtr = (int*)clusterGuide.GetUnsafePtr();

					// calc weighted centers
					for (int i = 0; i != clusterCount; i++)
					{
						clusterCenterPtr[i] = Vector3.zero;
						clusterWeightPtr[i] = 0.0f;
					}

					for (int i = 0; i != strandCount; i++)
					{
						var cluster = strandClusterPtr[i];
						{
							clusterCenterPtr[cluster] += strandGroup.rootPosition[i];
							clusterWeightPtr[cluster] += strandGroup.rootScale[i];
						}
					}

					for (int i = 0; i != clusterCount; i++)
					{
						clusterCenterPtr[i] /= clusterWeightPtr[i];
					}

					// select one guide per cluster based on distance to weighted centers
					for (int i = 0; i != clusterCount; i++)
					{
						clusterScorePtr[i] = float.PositiveInfinity;
						clusterDepthPtr[i] = selectionDepth;
						clusterGuidePtr[i] = -1;
					}

					for (int i = 0; i != strandCount; i++)
					{
						var cluster = strandClusterPtr[i];
						{
							var strandLength = strandGroup.maxParticleInterval * strandGroup.rootScale[i];
							var strandOffset = Vector3.Distance(clusterCenterPtr[cluster], strandGroup.rootPosition[i]);

							//TODO better scoring
							var strandScore = strandOffset;// / strandLength;// smaller score is better
							if (strandScore < clusterScorePtr[cluster])
							{
								clusterGuidePtr[cluster] = i;
								clusterScorePtr[cluster] = strandScore;
							}
						}
					}
				}
			}

			public void ImportGuidesFrom(in ClusterSet existingClusters)
			{
				using (var visitedMask = new NativeArray<bool>(clusterCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					var visitedMaskPtr = (bool*)visitedMask.GetUnsafePtr();

					for (int i = 0; i != existingClusters.clusterCount; i++)
					{
						var transferGuide = existingClusters.clusterGuidePtr[i];
						var transferCluster = strandClusterPtr[transferGuide];

						if (visitedMaskPtr[transferCluster])
						{
							//TODO sensible error message
							Debug.LogError("TODO: sensible error message");
						}
						else
						{
							// copy depth so we can sort clusters by depth first and guide second
							clusterDepthPtr[transferCluster] = existingClusters.clusterDepthPtr[transferCluster];

							// copy guide so existing guides remain as depth increases
							clusterGuidePtr[transferCluster] = transferGuide;

							// mark as visited
							visitedMaskPtr[transferCluster] = true;
						}
					}
				}
			}
		}

		unsafe struct ClusterRemapping : IDisposable
		{
			struct SortableCluster : IComparable<SortableCluster>
			{
				public int depth;
				public int guide;

				public int CompareTo(SortableCluster other)
				{
					if (depth != other.depth)
						return depth.CompareTo(other.depth);
					else
						return guide.CompareTo(other.guide);
				}
			}

			public NativeArray<int> strandRemapSrc;// maps final strand index -> old strand index
			public NativeArray<int> strandRemapDst;// maps old strand index -> final strand index

			public int* strandRemapSrcPtr;
			public int* strandRemapDstPtr;

			public ClusterRemapping(in ClusterSet clusters, Allocator allocator)
			{
				strandRemapSrc = new NativeArray<int>(clusters.strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				strandRemapDst = new NativeArray<int>(clusters.strandCount, allocator, NativeArrayOptions.UninitializedMemory);

				strandRemapSrcPtr = (int*)strandRemapSrc.GetUnsafePtr();
				strandRemapDstPtr = (int*)strandRemapDst.GetUnsafePtr();

				// sort clusters by depth first and guide second
				using (var sortedClusters = new NativeArray<SortableCluster>(clusters.clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var sortedClustersPtr = (SortableCluster*)sortedClusters.GetUnsafePtr();

					for (int i = 0; i != clusters.clusterCount; i++)
					{
						sortedClustersPtr[i].depth = clusters.clusterDepthPtr[i];
						sortedClustersPtr[i].guide = clusters.clusterGuidePtr[i];
					}

					sortedClusters.Sort();

					// write guides from sorted clusters
					for (int i = 0; i != clusters.clusterCount; i++)
					{
						strandRemapSrcPtr[i] = sortedClustersPtr[i].guide;
						strandRemapDstPtr[sortedClustersPtr[i].guide] = i;
					}

					// write non-guide strands
					for (int i = 0, j = clusters.clusterCount; i != clusters.strandCount; i++)
					{
						var guide = clusters.clusterGuidePtr[clusters.strandClusterPtr[i]];
						if (guide != i)
						{
							strandRemapSrcPtr[j] = i;
							strandRemapDstPtr[i] = j++;
						}
					}

					//TODO sort non-guide strands by guide first and index second
				}
			}

			public void Dispose()
			{
				strandRemapSrc.Dispose();
				strandRemapDst.Dispose();
			}

			public void ApplyRemapping(int* indexPtr, int indexCount)
			{
				for (int i = 0; i != indexCount; i++)
				{
					indexPtr[i] = strandRemapDstPtr[indexPtr[i]];
				}
			}

			public void ApplyRemapping(in NativeArray<int> indexBuffer)
			{
				ApplyRemapping((int*)indexBuffer.GetUnsafePtr(), indexBuffer.Length);
			}

			public void ApplyRemapping(int[] indexBuffer)
			{
				fixed (int* indexPtr = indexBuffer)
				{
					ApplyRemapping(indexPtr, indexBuffer.Length);
				}
			}

			public void ApplyShuffleUntyped(byte* attrPtr, int attrSize, int attrStride, int attrCount)
			{
				using (var buffer = new NativeArray<byte>(attrCount * attrSize, Allocator.Temp))
				{
					var bufferPtr = (byte*)buffer.GetUnsafePtr();

					// shuffle into temporary buffer
					for (int i = 0; i != attrCount; i++)
					{
						var copyDstPtr = bufferPtr + attrSize * i;
						var copySrcPtr = attrPtr + attrStride * strandRemapSrcPtr[i];

						UnsafeUtility.MemCpy(copyDstPtr, copySrcPtr, attrSize);
					}

					//for (int i = 0; i < 5; i++)
					//{
					//	Debug.Log("attrPtr[" + i + " -> " + 2 * strandRemapSrcPtr[i] + "] = " + ((float*)attrPtr)[2 * strandRemapSrcPtr[i]]);
					//	Debug.Log("bufferPtr[" + i + "] = " + ((float*)bufferPtr)[i * 2]);
					//}

					// copy back the shuffled data
					UnsafeUtility.MemCpyStride(
						attrPtr,        // dst
						attrStride,     // dst stride
						bufferPtr,      // src
						attrSize,       // src stride
						attrSize,       // size
						attrCount);     // count

					// equiv.
					//for (int i = 0; i != attrCount; i++)
					//{
					//	var attrDstPtr = attrPtr + attrStride * i;
					//	var attrSrcPtr = bufferPtr + attrSize * i;
					//	UnsafeUtility.MemCpy(attrDstPtr, attrSrcPtr, attrSize);
					//}
				}
			}

			public void ApplyShuffle<T>(NativeArray<T> attrBuffer, int attrOffset, int attrStride, int attrCount) where T : struct
			{
				var attrPtr = (byte*)attrBuffer.GetUnsafePtr();
				var attrSize = UnsafeUtility.SizeOf<T>();

				ApplyShuffleUntyped(attrPtr + attrOffset * attrSize, attrSize, attrStride * attrSize, attrCount);
			}

			public void ApplyShuffle<T>(T[] attrBuffer, int attrOffset, int attrStride, int attrCount) where T : struct
			{
				var attrPtr = (byte*)UnsafeUtility.PinGCArrayAndGetDataAddress(attrBuffer, out ulong gcHandle);
				var attrSize = UnsafeUtility.SizeOf<T>();

				//Debug.Log("applyShuffle<" + typeof(T).Name + ">");
				//Debug.Log(" - attrSize " + attrSize);
				//Debug.Log(" - attrStride " + (attrStride * attrSize));
				//Debug.Log(" - attrCount " + attrCount);

				ApplyShuffleUntyped(attrPtr + attrOffset * attrSize, attrSize, attrStride * attrSize, attrCount);

				UnsafeUtility.ReleaseGCObject(gcHandle);
			}
		}

		public unsafe struct ConnectedComponentLabels : IDisposable
		{
			public int dimX;
			public int dimY;

			public NativeArray<uint> labelImage;
			public uint* labelImagePtr;
			public uint labelCount;

			public ConnectedComponentLabels(Texture2D texture, Allocator allocator)
			{
				dimX = texture.width;
				dimY = texture.height;

				labelImage = new NativeArray<uint>(dimX * dimY, allocator, NativeArrayOptions.UninitializedMemory);
				labelImagePtr = (uint*)labelImage.GetUnsafePtr();
				labelCount = 0;

				var texelData = texture.GetRawTextureData<byte>();
				var texelDataPtr = (byte*)texelData.GetUnsafePtr();

				var texelCount = dimX * dimY;
				var texelSize = texelData.Length / texelCount;

				var texelBG = new NativeArray<byte>(texelSize, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var texelBGPtr = (byte*)texelBG.GetUnsafePtr();

				// discover adjacency of matching texels
				Profiler.BeginSample("discover adjacency");
				var texelAdjacency = new UnsafeAdjacency(texelCount, 16 * texelCount, Allocator.Temp);
				{
					bool CompareEquals(byte* ptrA, byte* ptrB, int count)
					{
						for (int i = 0; i != count; i++)
						{
							if (ptrA[i] != ptrB[i])
								return false;
						}

						return true;
					};

					void CompareAndConnect(ref UnsafeAdjacency adjacency, byte* valueBasePtr, int valueSize, int indexA, int indexB)
					{
						if (CompareEquals(valueBasePtr + valueSize * indexA, valueBasePtr + valueSize * indexB, valueSize))
						{
							adjacency.Append(indexA, indexB);
							adjacency.Append(indexB, indexA);
						}
					};

					// a b c
					// d e <-- 'e' is sweep index at (x, y)
					for (int y = 0; y != dimY; y++)
					{
						int ym = (y - 1 + dimY) % dimY;
						int yp = (y + 1) % dimY;

						for (int x = 0; x != dimX; x++)
						{
							int xm = (x - 1 + dimX) % dimX;
							int xp = (x + 1) % dimX;

							int i_a = dimX * ym + xm;
							int i_b = dimX * ym + x;
							int i_c = dimX * ym + xp;
							int i_d = dimX * y + xm;
							int i_e = dimX * y + x;

							// skip background texels
							if (CompareEquals(texelBGPtr, texelDataPtr + texelSize * i_e, texelSize))
								continue;

							// 8-way connected clusters
							CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_a);
							CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_b);
							CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_c);
							CompareAndConnect(ref texelAdjacency, texelDataPtr, texelSize, i_e, i_d);
						}
					}
				}
				Profiler.EndSample();

				// discover connected clusters
				Profiler.BeginSample("discover clusters");
				using (var texelVisitor = new UnsafeBFS(texelCount, Allocator.Temp))
				{
					for (int i = 0; i != texelCount; i++)
					{
						// skip texel if already visited
						if (texelVisitor.visitedPtr[i])
							continue;

						// begin new cluster
						labelCount++;

						// visit texels
						if (texelAdjacency.listsPtr[i].size > 0)
						{
							texelVisitor.Insert(i);

							while (texelVisitor.MoveNext(out int visitedIndex, out int visitedDepth))
							{
								foreach (var adjacentIndex in texelAdjacency[visitedIndex])
								{
									texelVisitor.Insert(adjacentIndex);
								}

								labelImagePtr[visitedIndex] = labelCount;
							}
						}
						else
						{
							texelVisitor.Ignore(i);

							labelImagePtr[i] = labelCount;
						}
					}
				}
				Profiler.EndSample();

				// done
				texelAdjacency.Dispose();
				texelBG.Dispose();
			}

			public uint GetLabelCount()
			{
				return labelCount;
			}

			public uint GetPixelLabel(int x, int y)
			{
				int Wrap(int a, int n)
				{
					int r = a % n;
					if (r < 0)
						return r + n;
					else
						return r;
				}

				x = Wrap(x, dimX);
				y = Wrap(y, dimY);

				return labelImagePtr[x + y * dimX];
			}

			public void Dispose()
			{
				labelImage.Dispose();
			}
		}
	}
}
