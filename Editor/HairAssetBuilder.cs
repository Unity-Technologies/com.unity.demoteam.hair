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
			using (var generatedRoots = new HairAssetProvider.GeneratedRoots(generatedStrandCount))
			using (var generatedStrands = new HairAssetProvider.GeneratedStrands(generatedStrandCount, generatedStrandParticleCount))
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

		static unsafe bool GenerateRoots(in HairAssetProvider.GeneratedRoots roots, in HairAsset.SettingsProcedural settings)
		{
			switch (settings.placement)
			{
				case HairAsset.SettingsProcedural.PlacementType.Primitive:
					return GenerateRootsPrimitive(roots, settings);

				case HairAsset.SettingsProcedural.PlacementType.Custom:
					return (settings.placementCustom)?.GenerateRoots(roots) ?? false;

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
	}
}
