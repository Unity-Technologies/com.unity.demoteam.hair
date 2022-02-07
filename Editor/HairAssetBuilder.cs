//#define VISIBLE_SUBASSETS
#define CLEAR_ALL_SUBASSETS

#define LOD_INDEX_INCREASING
//#define USE_DERIVED_CACHE

using System;
using UnityEngine;
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
					hash.Append(hairAsset.strandGroups[i].meshAssetRoots.GetInstanceID());
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
				for (int i = 0, curveSetIndex = 0; i != hairAsset.strandGroups.Length; i++)
				{
					if (curveSetIndex < curveSets.Length)
					{
						BuildStrandGroupAlembic(ref hairAsset.strandGroups[i], hairAsset, curveSets, ref curveSetIndex);
					}
					else
					{
						break;
					}
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
		static unsafe NativeArray<int> IntervalLengthsFromOffsetsAndTotalLength(int* intervalOffsetPtr, int intervalCount, int totalLength, Allocator allocator)
		{
			var intervalLength = new NativeArray<int>(intervalCount, allocator, NativeArrayOptions.UninitializedMemory);
			var intervalLengthPtr = (int*)intervalLength.GetUnsafePtr();
			{
				for (int i = 1; i != intervalCount; i++)
				{
					intervalLengthPtr[i - 1] = intervalOffsetPtr[i] - intervalOffsetPtr[i - 1];
				}

				intervalLengthPtr[intervalCount - 1] = totalLength - intervalOffsetPtr[intervalCount - 1];
			}

			return intervalLength;
		}

		static unsafe NativeArray<int> IntervalLengthsFromOffsetsAndTotalLength(int[] intervalOffset, int totalLength, Allocator allocator)
		{
			fixed (int* intervalOffsetPtr = intervalOffset)
			{
				return IntervalLengthsFromOffsetsAndTotalLength(intervalOffsetPtr, intervalOffset.Length, totalLength, allocator);
			}
		}

		struct AlembicCurvesInfo
		{
			public int curveCount;
			public int[] curveVertexOffset;
			public int curveVertexCountMin;
			public int curveVertexCountMax;
			public Vector3[] vertexDataPosition;
			public Vector2[] vertexDataTexCoord;
		}

		static unsafe AlembicCurvesInfo PrepareAlembicCurvesInfo(AlembicCurves curveSet)
		{
			// prepare struct
			var info = new AlembicCurvesInfo
			{
				curveCount = curveSet.CurveOffsets.Length,
				curveVertexOffset = curveSet.CurveOffsets,
				curveVertexCountMin = 0,
				curveVertexCountMax = 0,
				vertexDataPosition = curveSet.Positions,
				vertexDataTexCoord = curveSet.UVs,
			};

			// find min-max vertex count
			if (info.curveCount > 0)
			{
				using (var curveVertexCount = IntervalLengthsFromOffsetsAndTotalLength(info.curveVertexOffset, info.vertexDataPosition.Length, Allocator.Temp))
				{
					var curveVertexCountPtr = (int*)curveVertexCount.GetUnsafePtr();

					info.curveVertexCountMin = curveVertexCountPtr[0];
					info.curveVertexCountMax = curveVertexCountPtr[0];

					for (int i = 1; i != info.curveCount; i++)
					{
						Mathf.Min(info.curveVertexCountMin, curveVertexCountPtr[i]);
						Mathf.Max(info.curveVertexCountMax, curveVertexCountPtr[i]);
					}
				}
			}

			// done
			return info;
		}

		public static unsafe void BuildStrandGroupAlembic(ref HairAsset.StrandGroup strandGroup, in HairAsset hairAsset, AlembicCurves[] curveSets, ref int curveSetIndex)
		{
			var settings = hairAsset.settingsAlembic;

			// gather data from curve sets
			var combinedCurveCount = 0;
			var combinedCurveVertexCount = 0;
			var combinedCurveVertexOffset = new UnsafeList<int>(0, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			var combinedVertexDataPosition = new UnsafeList<Vector3>(0, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			{
				var combinedCurveVertexCountMin = 0;
				var combinedCurveVertexCountMax = 0;

				// validate primary curve set
				var primaryInfo = PrepareAlembicCurvesInfo(curveSets[curveSetIndex++]);
				if (primaryInfo.curveVertexCountMin >= 2)
				{
					// append data from primary curve set
					combinedCurveCount = primaryInfo.curveCount;
					combinedCurveVertexCountMin = primaryInfo.curveVertexCountMin;
					combinedCurveVertexCountMax = primaryInfo.curveVertexCountMax;

					fixed (int* primaryCurveVertexOffsetPtr = primaryInfo.curveVertexOffset)
					fixed (Vector3* primaryVertexDataPositionPtr = primaryInfo.vertexDataPosition)
					{
						combinedCurveVertexOffset.AddRange(primaryCurveVertexOffsetPtr, primaryInfo.curveVertexOffset.Length);
						combinedVertexDataPosition.AddRange(primaryVertexDataPositionPtr, primaryInfo.vertexDataPosition.Length);
					}

					// optionally append data from subsequent curve sets that have same maximum vertex count
					var combineSets = (settings.alembicAssetGroups == HairAsset.SettingsAlembic.Groups.Combine);
					if (combineSets)
					{
						for (; curveSetIndex != curveSets.Length; curveSetIndex++)
						{
							// validate subsequent curve set
							var secondaryInfo = PrepareAlembicCurvesInfo(curveSets[curveSetIndex]);
							if (secondaryInfo.curveVertexCountMin >= 2 && secondaryInfo.curveVertexCountMax == combinedCurveVertexCountMax)
							{
								var prevCombinedCurveCount = combinedCurveCount;
								var prevCombinedVertexCount = combinedVertexDataPosition.Length;

								// append data from subsequent curve set
								combinedCurveVertexCountMin = Mathf.Min(secondaryInfo.curveVertexCountMin, combinedCurveVertexCountMin);
								combinedCurveVertexCountMax = Mathf.Max(secondaryInfo.curveVertexCountMax, combinedCurveVertexCountMax);

								fixed (int* secondaryCurveVertexOffsetPtr = secondaryInfo.curveVertexOffset)
								fixed (Vector3* secondaryVertexDataPositionPtr = secondaryInfo.vertexDataPosition)
								{
									combinedCurveVertexOffset.AddRange(secondaryCurveVertexOffsetPtr, secondaryInfo.curveVertexOffset.Length);
									combinedVertexDataPosition.AddRange(secondaryVertexDataPositionPtr, secondaryInfo.vertexDataPosition.Length);
								}

								// update offsets in appended part of combined set
								if (prevCombinedVertexCount > 0)
								{
									var appendedVertexOffsetPtr = combinedCurveVertexOffset.Ptr + prevCombinedCurveCount;

									for (int j = 0; j != secondaryInfo.curveCount; j++)
									{
										appendedVertexOffsetPtr[j] += prevCombinedVertexCount;
									}
								}

								// update number of curves in combined set
								combinedCurveCount += secondaryInfo.curveCount;
							}
							else
							{
								// stop appending if degenerate or non-matching set was encountered
								break;
							}
						}
					}
				}
				else
				{
					Debug.LogWarningFormat("Skipping curve set (index {0}) due to degenerate curves with less than two vertices.", curveSetIndex);
				}

				// optionally resample the data
				{
					var resample = settings.resampleCurves;
					var resampleIterations = settings.resampleQuality;
					var resampleVertexCount = settings.resampleParticleCount;

					// resampling required if there are curves with varying vertex count
					var resampleRequired = !resample && (combinedCurveVertexCountMin != combinedCurveVertexCountMax);
					if (resampleRequired)
					{
						Debug.LogWarningFormat("Resampling strand group (to maximum vertex count within group) due to curves with varying vertex count.");

						resample = true;
						resampleVertexCount = combinedCurveVertexCountMax;
						resampleIterations = 1;
					}

					// resample the data if requested by user or required by varying vertex count
					if (resample)
					{
						var resampledVertexDataPosition = new UnsafeList<Vector3>(combinedCurveCount * resampleVertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
						{
							using (var srcVertexCount = IntervalLengthsFromOffsetsAndTotalLength(combinedCurveVertexOffset.Ptr, combinedCurveCount, combinedVertexDataPosition.Length, Allocator.Temp))
							{
								var srcVertexCountPtr = (int*)srcVertexCount.GetUnsafePtr();
								var srcVertexOffsetPtr = combinedCurveVertexOffset.Ptr;

								var dstVertexCount = resampleVertexCount;
								var dstVertexOffset = 0;

								var srcBasePtr = combinedVertexDataPosition.Ptr;
								var dstBasePtr = resampledVertexDataPosition.Ptr;

								for (int i = 0; i != combinedCurveCount; i++)
								{
									var srcCount = srcVertexCountPtr[i];
									var srcOffset = srcVertexOffsetPtr[i];

									Resample(srcBasePtr + srcOffset, srcCount, dstBasePtr + dstVertexOffset, dstVertexCount, resampleIterations);

									dstVertexOffset += dstVertexCount;
								}
							}
						}

						combinedCurveVertexCountMin = resampleVertexCount;
						combinedCurveVertexCountMax = resampleVertexCount;

						combinedVertexDataPosition.Dispose();
						combinedVertexDataPosition = resampledVertexDataPosition;
					}
				}

				// uniform vertex count
				combinedCurveVertexCount = combinedCurveVertexCountMin;
			}

			// set strand count
			strandGroup.strandCount = combinedCurveCount;
			strandGroup.strandParticleCount = combinedCurveVertexCount;

			// set memory layout
			strandGroup.particleMemoryLayout = hairAsset.settingsBasic.memoryLayout;

			// prep strand buffers
			strandGroup.rootUV = new Vector2[combinedCurveCount];
			strandGroup.rootScale = new float[combinedCurveCount];// written in FinalizeStrandGroup(..)
			strandGroup.rootPosition = new Vector3[combinedCurveCount];
			strandGroup.rootDirection = new Vector3[combinedCurveCount];
			strandGroup.particlePosition = new Vector3[combinedCurveCount * combinedCurveVertexCount];

			// build strand buffers
			{
				var combinedVertexDataPositionPtr = combinedVertexDataPosition.Ptr;

				for (int i = 0; i != combinedCurveCount; i++)
				{
					ref var p0 = ref combinedVertexDataPositionPtr[i * combinedCurveVertexCount];
					ref var p1 = ref combinedVertexDataPositionPtr[i * combinedCurveVertexCount + 1];

					strandGroup.rootUV[i] = settings.rootUVConstant;
					strandGroup.rootPosition[i] = p0;
					strandGroup.rootDirection[i] = Vector3.Normalize(p1 - p0);
				}

				switch (settings.rootUV)
				{
					case HairAsset.SettingsAlembic.RootUV.ResolveFromMesh:
						if (settings.rootUVMesh != null)
						{
#if USE_DERIVED_CACHE
							var meshQueries = DerivedCache.GetOrCreate(settings.rootUVMesh,
								meshArg =>
								{
									using (var meshData = Mesh.AcquireReadOnlyMeshData(meshArg))
									{
										return new TriMeshQueries(meshData[0], Allocator.Persistent);
									}
								}
							);
#else
							using (var meshData = Mesh.AcquireReadOnlyMeshData(settings.rootUVMesh))
							using (var meshQueries = new TriMeshQueries(meshData[0], Allocator.Temp))
#endif
							{
								for (int i = 0; i != combinedCurveCount; i++)
								{
									strandGroup.rootUV[i] = meshQueries.FindClosestTriangleUV(strandGroup.rootPosition[i]);
								}
							}
						}
						break;
				}

				switch (strandGroup.particleMemoryLayout)
				{
					case HairAsset.MemoryLayout.Sequential:
						{
							fixed (Vector3* dstPtr = strandGroup.particlePosition)
							{
								var srcBasePtr = combinedVertexDataPosition.Ptr;
								var srcLength = combinedVertexDataPosition.Length * sizeof(Vector3);

								UnsafeUtility.MemCpy(dstPtr, srcBasePtr, srcLength);
							}
						}
						break;

					case HairAsset.MemoryLayout.Interleaved:
						unsafe
						{
							var srcBasePtr = combinedVertexDataPosition.Ptr;
							var srcStride = sizeof(Vector3);
							var dstStride = sizeof(Vector3) * strandGroup.strandCount;

							fixed (Vector3* dstBasePtr = strandGroup.particlePosition)
							{
								for (int i = 0; i != combinedCurveCount; i++)
								{
									var srcPtr = srcBasePtr + i * combinedCurveVertexCount;
									var dstPtr = dstBasePtr + i;

									UnsafeUtility.MemCpyStride(dstPtr, dstStride, srcPtr, srcStride, srcStride, combinedCurveVertexCount);
								}
							}
						}
						break;
				}
			}

			// dispose data from curve sets
			combinedCurveVertexOffset.Dispose();
			combinedVertexDataPosition.Dispose();

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
			if (strandCount == 0)
				return;

			var strandParticleCount = strandGroup.strandParticleCount;
			if (strandParticleCount == 0)
				return;

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
					strandGroup.totalLength = 0.0f;
					strandGroup.maxStrandLength = 0.0f;

					for (int i = 0; i != strandCount; i++)
					{
						strandGroup.totalLength += strandLengthsPtr[i];
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

			// append version
			strandGroup.version = HairAsset.StrandGroup.VERSION;
		}

		static void BuildLODClusters(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var strandCount = strandGroup.strandCount;
			var strandParticleCount = strandGroup.strandParticleCount;

			// prepare lod chain
			var lodCapacity = 1;
			var lodChain = new LODChain(lodCapacity, strandCount, Allocator.Temp);

			// build lod clusters
			if (hairAsset.settingsBasic.kLODClusters)
			{
				switch (hairAsset.settingsBasic.kLODClustersProvider)
				{
					case HairAsset.LODClusters.Generated:
						BuildLODChainGenerated(ref lodChain, strandGroup, hairAsset);
						break;

					case HairAsset.LODClusters.UVMapped:
						BuildLODChainUVMapped(ref lodChain, strandGroup, hairAsset);
						break;
				}

				if (hairAsset.settingsBasic.kLODClustersHighLOD)
				{
					BuildLODChainSubdivided(ref lodChain, strandGroup, hairAsset);
				}
			}

			if (lodChain.lodCount == 0)
			{
				BuildLODChainAllStrands(ref lodChain, strandGroup, hairAsset);
			}

			// export lod chain
			strandGroup.lodCount = lodChain.lodCount;
#if LOD_INDEX_INCREASING
			strandGroup.lodGuideCount = lodChain.lodGuideCount.ToArray();
			strandGroup.lodGuideIndex = lodChain.lodGuideIndex.ToArray();
			strandGroup.lodGuideCarry = lodChain.lodGuideCarry.ToArray();
#else
			strandGroup.lodGuideCount = new int[lodChain.lodGuideCount.Length];
			strandGroup.lodGuideIndex = new int[lodChain.lodGuideIndex.Length];
			strandGroup.lodGuideCarry = new float[lodChain.lodGuideCarry.Length];

			unsafe
			{
				// write highest granularity to LOD 0, next-to-highest granularity to LOD 1, etc.
				fixed (int* lodGuideCountDstBase = strandGroup.lodGuideCount)
				fixed (int* lodGuideIndexDstBase = strandGroup.lodGuideIndex)
				fixed (float* lodGuideCarryDstBase = strandGroup.lodGuideCarry)
				{
					var lodGuideCountCopyDst = lodGuideCountDstBase;
					var lodGuideIndexCopyDst = lodGuideIndexDstBase;
					var lodGuideCarryCopyDst = lodGuideCarryDstBase;

					var lodGuideCountCopySrc = (int*)lodChain.lodGuideCount.GetUnsafePtr() + (strandGroup.lodCount);
					var lodGuideIndexCopySrc = (int*)lodChain.lodGuideIndex.GetUnsafePtr() + (strandGroup.lodCount * strandCount);
					var lodGuideCarryCopySrc = (float*)lodChain.lodGuideCarry.GetUnsafePtr() + (strandGroup.lodCount * strandCount);

					for (int i = 0; i != strandGroup.lodCount; i++)
					{
						lodGuideCountCopySrc -= 1;
						lodGuideIndexCopySrc -= strandCount;
						lodGuideCarryCopySrc -= strandCount;

						UnsafeUtility.MemCpy(lodGuideCountCopyDst, lodGuideCountCopySrc, sizeof(int));
						UnsafeUtility.MemCpy(lodGuideIndexCopyDst, lodGuideIndexCopySrc, sizeof(int) * strandCount);
						UnsafeUtility.MemCpy(lodGuideCarryCopyDst, lodGuideCarryCopySrc, sizeof(float) * strandCount);

						lodGuideCountCopyDst += 1;
						lodGuideIndexCopyDst += strandCount;
						lodGuideCarryCopyDst += strandCount;
					}
				}
			}
#endif

			// export lod thresholds
			strandGroup.lodThreshold = new float[strandGroup.lodCount];
			{
#if LOD_INDEX_INCREASING
				var maxLOD = strandGroup.lodCount - 1;
				var maxLODGuideCount = strandGroup.lodGuideCount[maxLOD];

				strandGroup.lodThreshold[maxLOD] = 1.0f;

				float Ramp(float x) => 2.0f * x - x * x;//TODO remove or adjust

				for (int i = 0; i != maxLOD; i++)
				{
					strandGroup.lodThreshold[i] = Ramp(strandGroup.lodGuideCount[i] / (float)maxLODGuideCount);
				}
#else
				strandGroup.lodThreshold[0] = 1.0f;

				for (int i = 1; i != strandGroup.lodCount; i++)
				{
					strandGroup.lodThreshold[i] = strandGroup.lodGuideCount[i] / (float)strandGroup.lodGuideCount[0];
				}
#endif
			}

			// build remapping tables from final cluster set
			var remappingRequired = (lodChain.lodCount > 1 || lodChain.clusterSet.clusterCount < strandCount);
			if (remappingRequired)
			{
				using (var remapping = new ClusterRemapping(lodChain.clusterSet, Allocator.Temp))
				{
					// apply to strands
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
						remapping.ApplyShuffle(strandGroup.lodGuideCarry, i * strandCount, 1, strandCount);
					}

					// apply to indices (since strands have now moved)
					remapping.ApplyRemapping(strandGroup.lodGuideIndex);
				}
			}

			// dispose lod chain
			lodChain.Dispose();
		}

		static unsafe void BuildLODChainGenerated(ref LODChain lodData, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			//TODO procedural lod chain
			Debug.LogError("TODO procedural lod chain");
		}

		static unsafe void BuildLODChainUVMapped(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var strandCount = strandGroup.strandCount;

			// early out if no readable cluster maps
			var clusterMaps = hairAsset.settingsLODUVMapped.baseLODClusterMapChain;
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
					return;
			}

			// add levels from cluster maps
			using (var clusterLookupColor = new UnsafeHashMap<Color, int>(strandCount, Allocator.Temp))
			using (var clusterLookupLabel = new UnsafeHashMap<uint, int>(strandCount, Allocator.Temp))
			using (var strandCluster = new NativeArray<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			using (var strandGuide = new NativeArray<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var strandClusterPtr = (int*)strandCluster.GetUnsafePtr();
				var strandGuidePtr = (int*)strandCluster.GetUnsafePtr();

				foreach (var clusterMap in clusterMaps)
				{
					if (clusterMap == null || clusterMap.isReadable == false)
						continue;

					// build strand->cluster
					var clusterCount = 0;

					switch (hairAsset.settingsLODUVMapped.baseLODClusterMapFormat)
					{
						case HairAsset.SettingsLODUVMapped.ClusterMapFormat.OneClusterPerColor:
							{
								clusterLookupColor.Clear();

								for (int i = 0; i != strandCount; i++)
								{
									var x = Mathf.RoundToInt(strandGroup.rootUV[i].x * (clusterMap.width - 1));
									var y = Mathf.RoundToInt(strandGroup.rootUV[i].y * (clusterMap.height - 1));

									var clusterColor = clusterMap.GetPixel(x, y, 0);
									if (clusterLookupColor.TryGetValue(clusterColor, out var cluster) == false)
									{
										cluster = clusterCount++;
										clusterLookupColor.Add(clusterColor, cluster);
									}

									strandClusterPtr[i] = cluster;
								}
							}
							break;

						case HairAsset.SettingsLODUVMapped.ClusterMapFormat.OneClusterPerVisualCluster:
							{
								clusterLookupLabel.Clear();

#if USE_DERIVED_CACHE
								var clusterMapLabels = DerivedCache.GetOrCreate(clusterMap,
									clusterMapArg =>
									{
										return new Texture2DLabels(clusterMapArg, clusterMapArg.wrapMode, Allocator.Persistent);
									}
								);
#else
								using (var clusterMapLabels = new Texture2DLabels(clusterMap, clusterMap.wrapMode, Allocator.Temp))
#endif
								{
									for (int i = 0; i != strandCount; i++)
									{
										var x = Mathf.RoundToInt(strandGroup.rootUV[i].x * (clusterMap.width - 1));
										var y = Mathf.RoundToInt(strandGroup.rootUV[i].y * (clusterMap.height - 1));

										var clusterLabel = clusterMapLabels.GetLabel(x, y);
										if (clusterLookupLabel.TryGetValue(clusterLabel, out var cluster) == false)
										{
											cluster = clusterCount++;
											clusterLookupLabel.Add(clusterLabel, cluster);
										}

										strandClusterPtr[i] = cluster;
									}
								}
							}
							break;
					}

					// build cluster set
					var clusterSet = new ClusterSet(clusterCount, strandCount, Allocator.Temp);
					{
						clusterSet.InitializeFromStrandCluster(strandGroup, strandCluster, lodChain.lodCount);
					}

					// append to chain
					if (lodChain.TryAppend(clusterSet) == false)
					{
						Debug.LogWarningFormat("Skipped '{0}' while building LOD chain.", clusterMap.name, clusterMap);
					}
				}
			}
		}

		static unsafe void BuildLODChainSubdivided(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			if (lodChain.clusterSet.clusterCount == 0)
				return;

			var clusterCountMin = lodChain.clusterSet.clusterCount;
			var clusterCountMax = lodChain.strandCount;

			var subdivisionCount = hairAsset.settingsLODPyramid.highLODIntermediateLevels + 1;
			if (subdivisionCount > 0)
			{
				var tmin = 0.0f;
				var tmax = hairAsset.settingsLODPyramid.highLODClusterQuantity;

				for (int i = 0; i != subdivisionCount; i++)
				{
					//     .---steps---.
					// 0---A---B---C---1
					// min           max

					var t = Mathf.Lerp(tmin, tmax, (i + 1.0f) / subdivisionCount);
					var s = Mathf.Lerp(clusterCountMin, clusterCountMax, t * t);//TODO try also 2t-t^2

					var clusterCount = Mathf.RoundToInt(s);
					if (clusterCount > lodChain.clusterSet.clusterCount)
					{
						if (clusterCount < lodChain.strandCount)
						{
							//TODO not implemented
							Debug.LogError("TODO procedural lod chain");
						}
						else
						{
							BuildLODChainAllStrands(ref lodChain, strandGroup, hairAsset);
							break;
						}
					}
				}
			}
		}

		static unsafe void BuildLODChainAllStrands(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var clusterCount = strandGroup.strandCount;
			var clusterSet = new ClusterSet(clusterCount, strandGroup.strandCount, Allocator.Temp);

			for (int i = 0; i != clusterCount; i++)
			{
				clusterSet.clusterDepthPtr[i] = lodChain.lodCount;
				clusterSet.clusterGuidePtr[i] = i;
				clusterSet.clusterCarryPtr[i] = 1.0f;
				clusterSet.strandClusterPtr[i] = i;
			}

			lodChain.TryAppend(clusterSet);
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

		static unsafe void Resample(Vector3* srcPos, int srcCount, Vector3* dstPos, int dstCount, int iterations)
		{
			var length = 0.0f;
			{
				for (int i = 1; i != srcCount; i++)
				{
					length += Vector3.Distance(srcPos[i], srcPos[i - 1]);
				}
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

		static unsafe void ResampleWithHint(Vector3* srcPos, int srcCount, out int srcIndex, Vector3* dstPos, int dstCount, out int dstIndex, float dstSpacing)
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

		unsafe struct LODChain : IDisposable
		{
			public ClusterSet clusterSet;

			public int lodCount;
			public NativeList<int> lodGuideCount;
			public NativeList<int> lodGuideIndex;
			public NativeList<float> lodGuideCarry;

			public int strandCount;
			public NativeArray<int> strandGuide;
			public NativeArray<float> strandCarry;
			public int* strandGuidePtr;
			public float* strandCarryPtr;

			public LODChain(int lodCapacity, int strandCount, Allocator allocator)
			{
				this.clusterSet = new ClusterSet(0, strandCount, allocator);

				this.lodCount = 0;
				this.lodGuideCount = new NativeList<int>(lodCapacity, allocator);
				this.lodGuideIndex = new NativeList<int>(lodCapacity * strandCount, allocator);
				this.lodGuideCarry = new NativeList<float>(lodCapacity * strandCount, allocator);

				this.strandCount = strandCount;
				this.strandGuide = new NativeArray<int>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.strandGuidePtr = (int*)strandGuide.GetUnsafePtr();
				this.strandCarry = new NativeArray<float>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.strandCarryPtr = (float*)strandCarry.GetUnsafePtr();
			}

			public void Dispose()
			{
				clusterSet.Dispose();
				lodGuideCount.Dispose();
				lodGuideIndex.Dispose();
				lodGuideCarry.Dispose();
				strandGuide.Dispose();
				strandCarry.Dispose();
			}

			public bool TryAppend(ClusterSet nextClusters)
			{
				if (nextClusters.clusterCount > clusterSet.clusterCount)
				{
					// inject guides from previous set in chain
					if (nextClusters.InjectGuidesFrom(clusterSet))
					{
						// replace current set
						clusterSet.Dispose();
						clusterSet = nextClusters;

						// update lod guide index
						for (int i = 0; i != strandCount; i++)
						{
							int cluster = clusterSet.strandClusterPtr[i];
							{
								strandGuidePtr[i] = clusterSet.clusterGuidePtr[cluster];
								strandCarryPtr[i] = clusterSet.clusterCarryPtr[cluster];
							}
						}

						lodCount++;
						lodGuideCount.Add(clusterSet.clusterCount);
						lodGuideIndex.AddRange(strandGuidePtr, strandCount);
						lodGuideCarry.AddRange(strandCarryPtr, strandCount);

						// succcess
						return true;
					}
					else// injection will fail if e.g. 3 guides in lower level all point to 1 cluster in current
					{
						Debug.LogWarningFormat("Failed to append to LOD chain at depth {0}. At least one cluster in specified set overlaps more than one guide from previous set in chain.", lodCount);
					}
				}
				else
				{
					Debug.LogWarningFormat("Failed to append to LOD chain at depth {0}. Number of clusters in specified set does not exceed that of previous set in chain.", lodCount);
				}

				// failed to append
				return false;
			}
		}

		unsafe struct ClusterSet : IDisposable
		{
			public int clusterCount;
			public NativeArray<int> clusterDepth;
			public NativeArray<int> clusterGuide;
			public NativeArray<float> clusterCarry;

			public int strandCount;
			public NativeArray<int> strandCluster;

			public int* clusterDepthPtr;
			public int* clusterGuidePtr;
			public float* clusterCarryPtr;
			public int* strandClusterPtr;

			public ClusterSet(int clusterCount, int strandCount, Allocator allocator)
			{
				this.clusterCount = clusterCount;
				this.clusterDepth = new NativeArray<int>(clusterCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.clusterGuide = new NativeArray<int>(clusterCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.clusterCarry = new NativeArray<float>(clusterCount, allocator, NativeArrayOptions.UninitializedMemory);

				this.strandCount = strandCount;
				this.strandCluster = new NativeArray<int>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);

				this.clusterDepthPtr = (int*)clusterDepth.GetUnsafePtr();
				this.clusterGuidePtr = (int*)clusterGuide.GetUnsafePtr();
				this.clusterCarryPtr = (float*)clusterCarry.GetUnsafePtr();
				this.strandClusterPtr = (int*)strandCluster.GetUnsafePtr();
			}

			public void Dispose()
			{
				clusterDepth.Dispose();
				clusterGuide.Dispose();
				clusterCarry.Dispose();
				strandCluster.Dispose();
			}

			public void InitializeFromStrandCluster(in HairAsset.StrandGroup strandGroup, in NativeArray<int> strandCluster, int strandClusterDepth)
			{
				this.strandCluster.CopyFrom(strandCluster);

				using (var clusterCenter = new NativeArray<Vector3>(clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (var clusterWeight = new NativeArray<float>(clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				using (var clusterScore = new NativeArray<float>(clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var clusterCenterPtr = (Vector3*)clusterCenter.GetUnsafePtr();
					var clusterWeightPtr = (float*)clusterWeight.GetUnsafePtr();
					var clusterScorePtr = (float*)clusterScore.GetUnsafePtr();

					// find weighted cluster centers
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
						clusterDepthPtr[i] = strandClusterDepth;
						clusterGuidePtr[i] = -1;
					}

					for (int i = 0; i != strandCount; i++)
					{
						var cluster = strandClusterPtr[i];
						{
							var strandLength = strandGroup.maxParticleInterval * strandGroup.rootScale[i];
							var strandOffset = Vector3.Distance(clusterCenterPtr[cluster], strandGroup.rootPosition[i]);

							//TODO better scoring
							var strandScore = strandOffset / strandLength;// smaller score is better
							if (strandScore < clusterScorePtr[cluster])
							{
								clusterGuidePtr[cluster] = i;
								clusterScorePtr[cluster] = strandScore;
							}
						}
					}

					// find cluster carry (weight of all strands in cluster relative to weight of guide)
					for (int i = 0; i != clusterCount; i++)
					{
						clusterCarryPtr[i] = clusterWeightPtr[i] / strandGroup.rootScale[clusterGuidePtr[i]];
					}
				}
			}

			public bool InjectGuidesFrom(in ClusterSet existingClusters)
			{
				using (var clusterUpdated = new NativeArray<bool>(clusterCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					var clusterUpdatedPtr = (bool*)clusterUpdated.GetUnsafePtr();
					var clusterUpdatedCount = 0;

					// loop over existing clusters
					for (int i = 0; i != existingClusters.clusterCount; i++)
					{
						// get existing guide and depth
						var existingGuide = existingClusters.clusterGuidePtr[i];
						var existingDepth = existingClusters.clusterDepthPtr[i];

						// get current cluster for existing guide
						var cluster = strandClusterPtr[existingGuide];

						// update current cluster if not already updated
						if (clusterUpdatedPtr[cluster] == false)
						{
							// transfer guide and depth so we can sort clusters by depth first and guide second
							clusterGuidePtr[cluster] = existingGuide;
							clusterDepthPtr[cluster] = existingDepth;

							// mark as updated
							clusterUpdatedPtr[cluster] = true;
							clusterUpdatedCount++;
						}
					}

					// success if all existing clusters were transferred
					return (clusterUpdatedCount == existingClusters.clusterCount);
				}
			}
		}

		unsafe struct ClusterRemapping : IDisposable
		{
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
				using (var sortedClusters = new NativeArray<ulong>(clusters.clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var sortedClustersPtr = (ulong*)sortedClusters.GetUnsafePtr();

					for (int i = 0; i != clusters.clusterCount; i++)
					{
						var sortDepth = (ulong)clusters.clusterDepthPtr[i] << 32;
						var sortGuide = (ulong)clusters.clusterGuidePtr[i];
						{
							sortedClustersPtr[i] = sortDepth | sortGuide;
						}
					}

					sortedClusters.Sort();

					// write remapping table for guides from sorted clusters
					for (int i = 0; i != clusters.clusterCount; i++)
					{
						var guide = (int)(sortedClustersPtr[i] & 0xffffffffuL);
						{
							strandRemapSrcPtr[i] = guide;
							strandRemapDstPtr[guide] = i;
						}
					}
				}

				var remainingCount = clusters.strandCount - clusters.clusterCount;
				if (remainingCount > 0)
				{
					// sort remaining (non-guide) strands by remapped guide first and index second
					using (var sortedRemaining = new NativeArray<ulong>(remainingCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
					{
						var sortedRemainingPtr = (ulong*)sortedRemaining.GetUnsafePtr();

						for (int i = 0, j = 0; i != clusters.strandCount; i++)
						{
							var guide = clusters.clusterGuidePtr[clusters.strandClusterPtr[i]];
							if (guide != i)
							{
								var sortGuide = (ulong)strandRemapDstPtr[guide] << 32;
								var sortIndex = (ulong)i;
								{
									sortedRemainingPtr[j++] = sortGuide | sortIndex;
								}
							}
						}

						//TODO maybe make it configurable whether/how to sort this data?
						sortedRemaining.Sort();

						// write remapping table for remaining (non-guide) strands
						for (int i = 0, j = clusters.clusterCount; i != remainingCount; i++, j++)
						{
							var index = (int)(sortedRemainingPtr[i] & 0xffffffffuL);
							{
								strandRemapSrcPtr[j] = index;
								strandRemapDstPtr[index] = j;
							}
						}
					}
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

					// copy back the shuffled data
					UnsafeUtility.MemCpyStride(
						attrPtr,        // dst
						attrStride,     // dst stride
						bufferPtr,      // src
						attrSize,       // src stride
						attrSize,       // size
						attrCount);     // count

					// copy back equiv.
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

				ApplyShuffleUntyped(attrPtr + attrOffset * attrSize, attrSize, attrStride * attrSize, attrCount);

				UnsafeUtility.ReleaseGCObject(gcHandle);
			}
		}
	}
}
