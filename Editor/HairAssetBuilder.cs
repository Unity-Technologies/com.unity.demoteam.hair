﻿//#define VISIBLE_SUBASSETS
#define CLEAR_ALL_SUBASSETS

#define LOD_INDEX_INCREASING
#define USE_DERIVED_CACHE

using System;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;

#if HAS_PACKAGE_UNITY_COLLECTIONS_1_0_0_PRE_3
using Unity.Collections.NotBurstCompatible;
#endif

#if HAS_PACKAGE_UNITY_ALEMBIC
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	public static class HairAssetBuilder
	{
		[Flags]
		public enum BuildFlags
		{
			None = 0,
			DisableLinking = 1 << 0,
			DisableProgress = 1 << 1,
		}

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

		public static void BuildHairAsset(HairAsset hairAsset, BuildFlags buildFlags = BuildFlags.None)
		{
			// clean up
			ClearHairAsset(hairAsset);

			// build the data
			using (new LongOperationOutput(enable: buildFlags.HasFlag(BuildFlags.DisableProgress) == false))
			{
				switch (hairAsset.settingsBasic.type)
				{
					case HairAsset.Type.Procedural:
						BuildHairAssetProcedural(hairAsset);
						break;

					case HairAsset.Type.Alembic:
						BuildHairAssetAlembic(hairAsset);
						break;

					case HairAsset.Type.Custom:
						BuildHairAssetCustom(hairAsset);
						break;
				}
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
					if (buildFlags.HasFlag(BuildFlags.DisableLinking) == false)
					{
						AssetDatabase.AddObjectToAsset(hairAsset.strandGroups[i].meshAssetRoots, hairAsset);
					}

					hairAsset.strandGroups[i].meshAssetRoots.name += (":" + i);
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
			var alembicCurveSets = alembic.gameObject.GetComponentsInChildren<AlembicCurves>(includeInactive: true);
			if (alembicCurveSets.Length > 0)
			{
				// prep strand groups
				hairAsset.strandGroups = new HairAsset.StrandGroup[alembicCurveSets.Length];

				// build strand groups
				for (int i = 0, alembicCurveSetIndex = 0; alembicCurveSetIndex < alembicCurveSets.Length; i++)
				{
					BuildStrandGroupAlembic(ref hairAsset.strandGroups[i], hairAsset, alembicCurveSets, ref alembicCurveSetIndex);
				}
			}

			// destroy container
			if (alembicInstantiate)
			{
				GameObject.DestroyImmediate(alembic.gameObject);
			}
#endif
		}

		public static void BuildHairAssetCustom(HairAsset hairAsset)
		{
			// check provider present
			var dataProvider = hairAsset.settingsCustom.dataProvider;
			if (dataProvider == null)
				return;

			if (dataProvider.AcquireCurves(out var curveSet, Allocator.Temp))
			{
				using (curveSet)
				{
					// prep strand groups
					hairAsset.strandGroups = new HairAsset.StrandGroup[1];

					// build strand groups
					for (int i = 0; i != hairAsset.strandGroups.Length; i++)
					{
						BuildStrandGroupResolved(ref hairAsset.strandGroups[0], hairAsset, hairAsset.settingsCustom.settingsResolve, curveSet);
					}
				}
			}
		}

		public static void BuildStrandGroupProcedural(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			ref readonly var settings = ref hairAsset.settingsProcedural;

			// get target counts
			var generatedStrandCount = settings.strandCount;
			var generatedStrandParticleCount = settings.strandParticleCount;

			// prep temporaries
			using (var generatedRoots = new HairAssetProvisional.ProceduralRoots(generatedStrandCount))
			using (var generatedStrands = new HairAssetProvisional.ProceduralStrands(generatedStrandCount, generatedStrandParticleCount))
			{
				// build temporaries
				if (GenerateRoots(generatedRoots, settings))
				{
					GenerateStrands(generatedStrands, generatedRoots, settings, hairAsset.settingsBasic.memoryLayout);
				}
				else
				{
					return;
				}

				// set strand count
				strandGroup.strandCount = generatedStrandCount;
				strandGroup.strandParticleCount = generatedStrandParticleCount;

				// set memory layout
				strandGroup.particleMemoryLayout = hairAsset.settingsBasic.memoryLayout;

				// prep strand buffers
				strandGroup.rootUV = new Vector2[generatedStrandCount];
				strandGroup.rootScale = new Vector4[generatedStrandCount];// completed in FinalizeStrandGroup(..)
				strandGroup.particleFeatures = HairAsset.StrandGroup.ParticleFeatures.Position;
				strandGroup.particlePosition = new Vector3[generatedStrandCount * generatedStrandParticleCount];
				strandGroup.particleTexCoord = null;
				strandGroup.particleDiameter = null;

				// populate strand buffers
				if (generatedStrandCount * generatedStrandParticleCount > 0)
				{
					generatedRoots.rootUV.CopyTo(strandGroup.rootUV);
					generatedStrands.rootScale.CopyTo(strandGroup.rootScale);
					generatedStrands.particlePosition.CopyTo(strandGroup.particlePosition);
				}
			}

			// calc derivative fields, create mesh assets
			FinalizeStrandGroup(ref strandGroup, hairAsset);
		}

		// resolve root scale
		public static unsafe void BuildStrandGroupResolved(ref HairAsset.StrandGroup strandGroup, in HairAsset hairAsset, in HairAsset.SettingsResolve settingsResolve, in HairAssetProvisional.CurveSet curveSet)
		{
			ref readonly var settings = ref settingsResolve;

			// validate input curve set
			if (curveSet.curveCount > curveSet.curveVertexCount.Length)
			{
				Debug.LogWarning("Discarding provided curve set due to incomplete (out of bounds) curve data.");
				return;
			}

			var curveSetInfo = new HairAssetProvisional.CurveSetInfo(curveSet);
			{
				var curveSetIncompleteRoot = (HairAssetProvisional.CurveSet.CurveFeatures)0;
				{
					if (curveSet.curveCount > curveSet.curveDataTexCoord.Length && curveSet.curveFeatures.HasFlag(HairAssetProvisional.CurveSet.CurveFeatures.TexCoord))
						curveSetIncompleteRoot |= HairAssetProvisional.CurveSet.CurveFeatures.TexCoord;
					if (curveSet.curveCount > curveSet.curveDataDiameter.Length && curveSet.curveFeatures.HasFlag(HairAssetProvisional.CurveSet.CurveFeatures.Diameter))
						curveSetIncompleteRoot |= HairAssetProvisional.CurveSet.CurveFeatures.Diameter;
					if (curveSet.curveCount > curveSet.curveDataTapering.Length && curveSet.curveFeatures.HasFlag(HairAssetProvisional.CurveSet.CurveFeatures.Tapering))
						curveSetIncompleteRoot |= HairAssetProvisional.CurveSet.CurveFeatures.Tapering;
				}

				if (curveSetIncompleteRoot != 0)
				{
					Debug.LogWarningFormat("Discarding provided curve set due to incomplete (out of bounds) curve data. ({0})", curveSetIncompleteRoot.ToString());
					return;
				}

				var curveSetIncompleteVertex = (HairAssetProvisional.CurveSet.VertexFeatures)0;
				{
					if (curveSetInfo.sumVertexCount > curveSet.vertexDataPosition.Length)
						curveSetIncompleteVertex |= HairAssetProvisional.CurveSet.VertexFeatures.Position;
					if (curveSetInfo.sumVertexCount > curveSet.vertexDataTexCoord.Length && curveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.TexCoord))
						curveSetIncompleteVertex |= HairAssetProvisional.CurveSet.VertexFeatures.TexCoord;
					if (curveSetInfo.sumVertexCount > curveSet.vertexDataDiameter.Length && curveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.Diameter))
						curveSetIncompleteVertex |= HairAssetProvisional.CurveSet.VertexFeatures.Diameter;
				}

				if (curveSetIncompleteVertex != 0)
				{
					Debug.LogWarningFormat("Discarding provided curve set due to incomplete (out of bounds) vertex data. ({0})", curveSetIncompleteVertex.ToString());
					return;
				}

				if (curveSetInfo.minVertexCount < 2)
				{
					Debug.LogWarning("Discarding provided curve set due to degenerate curves with less than two vertices.");
					return;
				}
			}

			// sanitize input curve data to guarantee uniform vertex count
			var uniformCurveSet = curveSet;
			var uniformVertexCount = curveSetInfo.maxVertexCount;
			var uniformVertexAlloc = false;
			{
				// resample enabled if requested by user
				var resample = settings.resampleCurves;
				var resampleIterations = Mathf.Clamp(settings.resampleQuality, HairAsset.SettingsResolve.MIN_RESAMPLE_QUALITY, HairAsset.SettingsResolve.MAX_RESAMPLE_QUALITY);
				var resampleVertexCount = Mathf.Clamp(settings.resampleResolution, HairAsset.SettingsResolve.MIN_RESAMPLE_RESOLUTION, HairAsset.SettingsResolve.MAX_RESAMPLE_RESOLUTION);

				// resample required if there are curves with varying vertex count
				var resampleRequired = !resample && (curveSetInfo.minVertexCount != curveSetInfo.maxVertexCount);
				if (resampleRequired)
				{
					Debug.LogWarning("Resampling curve set (to maximum vertex count within set) due to curves with varying vertex count.");

					resample = true;
					resampleVertexCount = curveSetInfo.maxVertexCount;
					resampleIterations = 1;
				}

				// resample the data if requested by user or required due to curves with varying vertex count
				if (resample)
				{
					// allocate buffers for resampled data
					uniformCurveSet.vertexDataPosition = new UnsafeList<Vector3>(curveSet.curveCount * resampleVertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
					uniformCurveSet.vertexDataTexCoord = new UnsafeList<Vector2>(curveSet.curveCount * resampleVertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
					uniformCurveSet.vertexDataDiameter = new UnsafeList<float>(curveSet.curveCount * resampleVertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
					uniformVertexCount = resampleVertexCount;
					uniformVertexAlloc = true;

					using (var longOperation = new LongOperationScope("Resampling curves"))
					using (var resampleResolve = new UnsafeList<ResampleBlend>(curveSet.curveCount * resampleVertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
					{
						var dstVertexCount = uniformVertexCount;
						var dstDataPositionPtr = uniformCurveSet.vertexDataPosition.Ptr;
						var dstDataTexCoordPtr = uniformCurveSet.vertexDataTexCoord.Ptr;
						var dstDataDiameterPtr = uniformCurveSet.vertexDataDiameter.Ptr;
						var dstVertexOffset = 0;

						var srcVertexCountPtr = curveSet.curveVertexCount.Ptr;
						var srcDataPositionPtr = curveSet.vertexDataPosition.Ptr;
						var srcDataTexCoordPtr = curveSet.vertexDataTexCoord.Ptr;
						var srcDataDiameterPtr = curveSet.vertexDataDiameter.Ptr;
						var srcVertexOffset = 0;

						var resampleResolvePtr = resampleResolve.Ptr;

						// resample position stream
						for (int i = 0; i != curveSet.curveCount; i++)
						{
							longOperation.UpdateStatus("Resampling", i, curveSet.curveCount);

							var srcVertexCount = srcVertexCountPtr[i];
							{
								Resample(
									srcDataPositionPtr + srcVertexOffset, srcVertexCount,
									dstDataPositionPtr + dstVertexOffset, dstVertexCount,
									resampleResolvePtr + dstVertexOffset, resampleIterations);

								srcVertexOffset += srcVertexCount;
								dstVertexOffset += dstVertexCount;
							}
						}

						// resample additional streams
						if (uniformCurveSet.vertexFeatures > HairAssetProvisional.CurveSet.VertexFeatures.Position)
						{
							var resampleTexCoord = uniformCurveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.TexCoord);
							var resampleDiameter = uniformCurveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.Diameter);

							dstVertexOffset = 0;
							srcVertexOffset = 0;

							for (int i = 0; i != curveSet.curveCount; i++)
							{
								for (int j = 0; j != dstVertexCount; j++)
								{
									var dstVertexIndex = dstVertexOffset + j;

									ref readonly var dstVertexResolve = ref resampleResolvePtr[dstVertexIndex];

									var srcVertexIndex0 = srcVertexOffset + dstVertexResolve.srcIndex0;
									var srcVertexIndex1 = srcVertexOffset + dstVertexResolve.srcIndex1;

									//Debug.Log("curve " + i + " dstVertex " + j + " <- lerp("+ dstVertexResolve.srcIndex0 + ", "+ dstVertexResolve.srcIndex1 + ", "+ dstVertexResolve.srcBlendFrac + ")");

									if (resampleTexCoord)
									{
										dstDataTexCoordPtr[dstVertexIndex] = Vector2.Lerp(
											srcDataTexCoordPtr[srcVertexIndex0],
											srcDataTexCoordPtr[srcVertexIndex1],
											dstVertexResolve.srcBlendFrac);
									}

									if (resampleDiameter)
									{
										dstDataDiameterPtr[dstVertexIndex] = Mathf.Lerp(
											srcDataDiameterPtr[srcVertexIndex0],
											srcDataDiameterPtr[srcVertexIndex1],
											dstVertexResolve.srcBlendFrac);
									}
								}

								srcVertexOffset += srcVertexCountPtr[i];
								dstVertexOffset += dstVertexCount;
							}
						}
					}
				}
			}

			// set strand count
			strandGroup.strandCount = uniformCurveSet.curveCount;
			strandGroup.strandParticleCount = uniformVertexCount;

			// set memory layout
			strandGroup.particleMemoryLayout = hairAsset.settingsBasic.memoryLayout;

			// set strand features
			static HairAsset.StrandGroup.ParticleFeatures GetParticleFeature(HairAsset.SettingsResolve.AdditionalData flag)
			{
				switch (flag)
				{
					case HairAsset.SettingsResolve.AdditionalData.PerVertexUV: return HairAsset.StrandGroup.ParticleFeatures.TexCoord;
					case HairAsset.SettingsResolve.AdditionalData.PerVertexWidth: return HairAsset.StrandGroup.ParticleFeatures.Diameter;
					default: return 0;
				}
			}

			strandGroup.particleFeatures = HairAsset.StrandGroup.ParticleFeatures.Position;
			if (settings.additionalData)
			{
				if (uniformCurveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.TexCoord))
					strandGroup.particleFeatures |= GetParticleFeature(settings.additionalDataMask & HairAsset.SettingsResolve.AdditionalData.PerVertexUV);
				if (uniformCurveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.Diameter))
					strandGroup.particleFeatures |= GetParticleFeature(settings.additionalDataMask & HairAsset.SettingsResolve.AdditionalData.PerVertexWidth);
			}

			// prep strand buffers
			strandGroup.rootUV = new Vector2[uniformCurveSet.curveCount];
			strandGroup.rootScale = new Vector4[uniformCurveSet.curveCount];// completed in FinalizeStrandGroup(..)

			static void PrepareParticleBufferConditional<T>(in HairAsset.StrandGroup dstGroup, ref T[] dstBuffer, bool enabled = true) where T : unmanaged
			{
				dstBuffer = enabled ? (new T[dstGroup.strandCount * dstGroup.strandParticleCount]) : null;
			};

			PrepareParticleBufferConditional(strandGroup, ref strandGroup.particlePosition);
			PrepareParticleBufferConditional(strandGroup, ref strandGroup.particleTexCoord, strandGroup.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.TexCoord));
			PrepareParticleBufferConditional(strandGroup, ref strandGroup.particleDiameter, strandGroup.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.Diameter));

			// build strand buffers
			{
				var uniformCurveDataTexCoordPtr = uniformCurveSet.curveDataTexCoord.Ptr;
				var uniformCurveDataDiameterPtr = uniformCurveSet.curveDataDiameter.Ptr;
				var uniformCurveDataTaperingPtr = uniformCurveSet.curveDataTapering.Ptr;

				var uniformVertexDataPositionPtr = uniformCurveSet.vertexDataPosition.Ptr;
				var uniformVertexDataTexCoordPtr = uniformCurveSet.vertexDataTexCoord.Ptr;
				var uniformVertexDataDiameterPtr = uniformCurveSet.vertexDataDiameter.Ptr;

				// resolve root uv
				switch (settings.rootUV)
				{
					case HairAsset.SettingsResolve.RootUV.ResolveFromMesh:
						{
							if (settings.rootUVMesh != null)
							{
								using (var longOperation = new LongOperationScope("Resolving root UVs"))
								{
									longOperation.UpdateStatus("Building mesh BVH", 0.0f);
#if USE_DERIVED_CACHE
									var meshQueries = DerivedCache.GetOrCreate((Mesh)settings.rootUVMesh,
										meshArg =>
										{
											using (var meshData = Mesh.AcquireReadOnlyMeshData(meshArg))
											{
												return new TriMeshQueries(meshData[0], Allocator.Persistent);
											}
										});
#else
									using (var meshData = Mesh.AcquireReadOnlyMeshData(settings.rootUVMesh))
									using (var meshQueries = new TriMeshQueries(meshData[0], Allocator.Temp))
#endif

									for (int i = 0; i != uniformCurveSet.curveCount; i++)
									{
										longOperation.UpdateStatus("Resolving", i, uniformCurveSet.curveCount);
										{
											ref readonly var rootPos = ref uniformVertexDataPositionPtr[i * uniformVertexCount];
											strandGroup.rootUV[i] = meshQueries.FindClosestTriangleUV(rootPos);
										}
									}
								}
							}
							else
							{
								Debug.LogWarning("Unable to resolve root UVs from mesh, since no mesh was assigned. Using provided uniform fallback.");
								goto case HairAsset.SettingsResolve.RootUV.UseFallback;
							}
						}
						break;

					case HairAsset.SettingsResolve.RootUV.ResolveFromCurves:
						{
							// first try per-curve texcoord
							if (uniformCurveSet.curveFeatures.HasFlag(HairAssetProvisional.CurveSet.CurveFeatures.TexCoord))
							{
								for (int i = 0; i != uniformCurveSet.curveCount; i++)
								{
									strandGroup.rootUV[i] = uniformCurveDataTexCoordPtr[i];
								}
							}
							// else try per-vertex texcoord
							else if (uniformCurveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.TexCoord))
							{
								for (int i = 0; i != uniformCurveSet.curveCount; i++)
								{
									strandGroup.rootUV[i] = uniformVertexDataTexCoordPtr[i * uniformVertexCount];
								}
							}
							// else use fallback
							else
							{
								Debug.LogWarning("Unable to resolve root UVs from curves, since no curve UVs were provided in curve set. Using provided uniform fallback.");
								goto case HairAsset.SettingsResolve.RootUV.UseFallback;
							}
						}
						break;

					case HairAsset.SettingsResolve.RootUV.UseFallback:
						{
							for (int i = 0; i != uniformCurveSet.curveCount; i++)
							{
								strandGroup.rootUV[i] = settings.rootUVFallback;
							}
						}
						break;
				}

				// resolve diameter and tapering
				{
					var tryResolveDiameter = (settings.strandDiameter == HairAsset.SettingsResolve.StrandDiameter.ResolveFromCurves);
					var tryResolveTapering = (settings.tipScale == HairAsset.SettingsResolve.TipScale.ResolveFromCurves);

					// write result per vertex
					var perVertexResult = (tryResolveDiameter || tryResolveTapering) && uniformCurveSet.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.Diameter);
					if (perVertexResult)
					{
						for (int i = 0; i != uniformCurveSet.curveCount; i++)
						{
							var maxDiameter = uniformVertexDataDiameterPtr[i * uniformVertexCount];
							var maxDiameterVertexIndex = 0;
							var maxDiameterVertexIndexTaper = 0;

							for (int j = 1; j != uniformVertexCount; j++)
							{
								var vertexDiameter = uniformVertexDataDiameterPtr[i * uniformVertexCount + j];
								if (vertexDiameter >= maxDiameter)
								{
									maxDiameter = vertexDiameter;
									maxDiameterVertexIndex = j;
								}
								else
								{
									if (maxDiameterVertexIndex == j - 1)
									{
										maxDiameterVertexIndexTaper = j - 1;
									}
								}
							}

							var tipDiameter = uniformVertexDataDiameterPtr[(i + 1) * uniformVertexCount - 1];
							var tipScale = Mathf.Min(tipDiameter, maxDiameter) / maxDiameter;
							var tipScaleOffset = maxDiameterVertexIndexTaper / (float)(uniformVertexCount - 1);

							strandGroup.rootScale[i].y = maxDiameter * uniformCurveSet.unitScaleDiameter;
							strandGroup.rootScale[i].z = tipScaleOffset;
							strandGroup.rootScale[i].w = tipScale;
						}
					}

					// resolve strand diameter
					switch (settings.strandDiameter)
					{
						case HairAsset.SettingsResolve.StrandDiameter.ResolveFromCurves:
							{
								// first try per-vertex diameter (result written already)
								if (perVertexResult)
								{
								}
								// else try per-curve diameter
								else if (uniformCurveSet.curveFeatures.HasFlag(HairAssetProvisional.CurveSet.CurveFeatures.Diameter))
								{
									for (int i = 0; i != uniformCurveSet.curveCount; i++)
									{
										strandGroup.rootScale[i].y = uniformCurveDataDiameterPtr[i] * uniformCurveSet.unitScaleDiameter;
									}
								}
								// else use fallback
								else
								{
									Debug.LogWarning("Unable to resolve strand diameters from curves, since no curve diameters were provided in curve set. Using provided uniform fallback.");
									goto case HairAsset.SettingsResolve.StrandDiameter.UseFallback;
								}
							}
							break;

						case HairAsset.SettingsResolve.StrandDiameter.UseFallback:
							{
								for (int i = 0; i != uniformCurveSet.curveCount; i++)
								{
									strandGroup.rootScale[i].y = settings.strandDiameterFallback * 0.001f;
								}
							}
							break;
					}

					// resolve tapering
					switch (settings.tipScale)
					{
						case HairAsset.SettingsResolve.TipScale.ResolveFromCurves:
							{
								// first try per-vertex tapering (result written already)
								if (perVertexResult)
								{
								}
								// else try per-curve tapering
								else if (uniformCurveSet.curveFeatures.HasFlag(HairAssetProvisional.CurveSet.CurveFeatures.Tapering))
								{
									for (int i = 0; i != uniformCurveSet.curveCount; i++)
									{
										ref readonly var curveTapering = ref uniformCurveDataTaperingPtr[i];
										strandGroup.rootScale[i].z = curveTapering.tipScaleOffset;
										strandGroup.rootScale[i].w = curveTapering.tipScale;
									}
								}
								// else use fallback
								else
								{
									Debug.LogWarning("Unable to resolve tapering from curves, since no curve diameters were provided in curve set. Using provided uniform fallback.");
									goto case HairAsset.SettingsResolve.TipScale.UseFallback;
								}
							}
							break;

						case HairAsset.SettingsResolve.TipScale.UseFallback:
							{
								for (int i = 0; i != uniformCurveSet.curveCount; i++)
								{
									strandGroup.rootScale[i].z = settings.tipScaleFallbackOffset;
									strandGroup.rootScale[i].w = settings.tipScaleFallback;
								}
							}
							break;
					}
				}

				// write particle data
				static void WriteParticleData<T>(in HairAsset.StrandGroup dstGroup, T[] dstBuffer, UnsafeList<T> srcBuffer) where T : unmanaged
				{
					if (dstBuffer == null)
						return;

					switch (dstGroup.particleMemoryLayout)
					{
						case HairAsset.MemoryLayout.Sequential:
							{
								var srcPtr = srcBuffer.Ptr;
								var srcSize = dstGroup.strandCount * dstGroup.strandParticleCount * sizeof(T);

								fixed (T* dstPtr = dstBuffer)
								{
									UnsafeUtility.MemCpy(dstPtr, srcPtr, srcSize);
								}
							}
							break;

						case HairAsset.MemoryLayout.Interleaved:
							{
								var srcBufferPtr = srcBuffer.Ptr;
								var srcStride = sizeof(T);
								var dstStride = sizeof(T) * dstGroup.strandCount;

								fixed (T* dstBufferPtr = dstBuffer)
								{
									for (int i = 0; i != dstGroup.strandCount; i++)
									{
										var srcPtr = srcBufferPtr + i * dstGroup.strandParticleCount;
										var dstPtr = dstBufferPtr + i;

										UnsafeUtility.MemCpyStride(dstPtr, dstStride, srcPtr, srcStride, srcStride, dstGroup.strandParticleCount);
									}
								}
							}
							break;
					}
				}

				WriteParticleData(strandGroup, strandGroup.particlePosition, uniformCurveSet.vertexDataPosition);
				if (strandGroup.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.TexCoord))
					WriteParticleData(strandGroup, strandGroup.particleTexCoord, uniformCurveSet.vertexDataTexCoord);
				if (strandGroup.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.Diameter))
					WriteParticleData(strandGroup, strandGroup.particleDiameter, uniformCurveSet.vertexDataDiameter);

				// scale particle data
				//TODO
				//if (uniformCurveSet.unitScalePosition != 1.0f)
				//	;
				//if (uniformCurveSet.unitScaleDiameter != 0.001f)
				//	;
			}

			// free allocated buffers
			if (uniformVertexAlloc)
			{
				uniformCurveSet.vertexDataPosition.Dispose();
				uniformCurveSet.vertexDataTexCoord.Dispose();
				uniformCurveSet.vertexDataDiameter.Dispose();
			}

			// calc derivative fields, create mesh assets
			FinalizeStrandGroup(ref strandGroup, hairAsset);
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
			public float[] vertexDataDiameter;
			public HairAssetProvisional.CurveSet.VertexFeatures vertexFeatures;
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
				vertexDataDiameter = curveSet.Widths,
				vertexFeatures = HairAssetProvisional.CurveSet.VertexFeatures.Position,
			};

			// find vertex features
			if (info.vertexDataTexCoord != null && info.vertexDataTexCoord.Length > 0)
				info.vertexFeatures |= HairAssetProvisional.CurveSet.VertexFeatures.TexCoord;
			if (info.vertexDataDiameter != null && info.vertexDataDiameter.Length > 0)
				info.vertexFeatures |= HairAssetProvisional.CurveSet.VertexFeatures.Diameter;

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

			// sanitize vertex features
			if (info.vertexFeatures.HasFlag(HairAssetProvisional.CurveSet.VertexFeatures.Diameter))
			{
				var invalidRootDiameter = 0;
				{
					for (int i = 0; i != info.curveCount; i++)
					{
						var j = info.curveVertexOffset[i];
						if (j < info.vertexDataDiameter.Length && info.vertexDataDiameter[j] <= 0.0f)
						{
							invalidRootDiameter++;
						}
					}
				}

				if (invalidRootDiameter > 0)
				{
					Debug.LogWarningFormat("Discarding alembic curve set diameters due to {0} (out of {1}) curves having degenerate root diameter.", invalidRootDiameter, info.curveCount);
					info.vertexFeatures ^= HairAssetProvisional.CurveSet.VertexFeatures.Diameter;
				}
			}

			// done
			return info;
		}

		public static unsafe void BuildStrandGroupAlembic(ref HairAsset.StrandGroup strandGroup, in HairAsset hairAsset, AlembicCurves[] alembicCurveSets, ref int alembicCurveSetIndex)
		{
			ref readonly var settings = ref hairAsset.settingsAlembic;

			var combinedCurveSet = new HairAssetProvisional.CurveSet(Allocator.Temp);

			using (var longOperation = new LongOperationScope("Gathering curves"))
			{
				var combinedCurveVertexCountMin = 0;
				var combinedCurveVertexCountMax = 0;
				var combinedCurveVertexFeatures = HairAssetProvisional.CurveSet.VertexFeatures.Position;

				while (alembicCurveSetIndex < alembicCurveSets.Length)
				{
					// validate alembic curve set
					var alembicCurveSetInfo = PrepareAlembicCurvesInfo(alembicCurveSets[alembicCurveSetIndex++]);
					if (alembicCurveSetInfo.curveVertexCountMin >= 2)
					{
						// first valid set decides feature flags and maximum vertex count and feature mask
						if (combinedCurveSet.curveCount == 0)
						{
							combinedCurveVertexCountMin = alembicCurveSetInfo.curveVertexCountMin;
							combinedCurveVertexCountMax = alembicCurveSetInfo.curveVertexCountMax;
							combinedCurveVertexFeatures = alembicCurveSetInfo.vertexFeatures;
						}

						// secondary valid set (etc.) must conform to maximum vertex count and feature mask
						if (combinedCurveVertexCountMax != alembicCurveSetInfo.curveVertexCountMax ||
							combinedCurveVertexFeatures != alembicCurveSetInfo.vertexFeatures)
						{
							alembicCurveSetIndex--;// decrementing to revisit when building next strand group
							break;
						}

						// append data to combined set
						fixed (Vector3* alembicVertexDataPositionPtr = alembicCurveSetInfo.vertexDataPosition)
						fixed (Vector2* alembicVertexDataTexCoordPtr = alembicCurveSetInfo.vertexDataTexCoord)
						fixed (float* alembicVertexDataDiameterPtr = alembicCurveSetInfo.vertexDataDiameter)
						{
							using (var alembicCurveVertexCount = IntervalLengthsFromOffsetsAndTotalLength(alembicCurveSetInfo.curveVertexOffset, alembicCurveSetInfo.vertexDataPosition.Length, Allocator.Temp))
							{
								combinedCurveSet.curveCount += alembicCurveSetInfo.curveCount;
								combinedCurveSet.curveVertexCount.AddRange(alembicCurveVertexCount.GetUnsafePtr(), alembicCurveVertexCount.Length);
								combinedCurveSet.vertexDataPosition.AddRange(alembicVertexDataPositionPtr, alembicCurveSetInfo.vertexDataPosition.Length);
								combinedCurveSet.vertexDataTexCoord.AddRange(alembicVertexDataTexCoordPtr, alembicCurveSetInfo.vertexDataTexCoord.Length);
								combinedCurveSet.vertexDataDiameter.AddRange(alembicVertexDataDiameterPtr, alembicCurveSetInfo.vertexDataDiameter.Length);
							}
						}

						// finished if not combining
						if (settings.alembicAssetGroups != HairAsset.SettingsAlembic.Groups.Combine)
						{
							break;
						}
					}
					else
					{
						Debug.LogWarningFormat("Skipping alembic curve set (index {0}) due to degenerate curves with less than two vertices.", alembicCurveSetIndex);
					}
				}

				static float SourceUnitToUnitScale(HairAsset.SettingsAlembic.SourceUnit sourceUnit)
				{
					switch (sourceUnit)
					{
						default:
						case HairAsset.SettingsAlembic.SourceUnit.DataInMeters: return 1.0f;
						case HairAsset.SettingsAlembic.SourceUnit.DataInCentimeters: return 0.01f;
						case HairAsset.SettingsAlembic.SourceUnit.DataInMillimeters: return 0.001f;
					}
				}

				combinedCurveSet.vertexFeatures = combinedCurveVertexFeatures;
				combinedCurveSet.unitScalePosition = SourceUnitToUnitScale(settings.alembicScalePositions);
				combinedCurveSet.unitScaleDiameter = SourceUnitToUnitScale(settings.alembicScaleDiameters);
			}

			using (combinedCurveSet)
			{
				BuildStrandGroupResolved(ref strandGroup, hairAsset, settings.settingsResolve, combinedCurveSet);
			}
		}
#endif

		static void FinalizeStrandGroup(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var strandCount = strandGroup.strandCount;
			if (strandCount == 0)
				return;

			var strandParticleCount = strandGroup.strandParticleCount;
			if (strandParticleCount == 0)
				return;

			// finalize strand properties
			using (var longOperation = new LongOperationScope("Finalizing strands"))
			{
				// calc strand lengths
				for (int i = 0; i != strandCount; i++)
				{
					longOperation.UpdateStatus("Measuring", i, strandCount);

					HairAssetUtility.DeclareStrandIterator(strandGroup, i, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					ref var strandParams = ref strandGroup.rootScale[i];
					{
						for (int j = strandParticleBegin + strandParticleStride; j != strandParticleEnd; j += strandParticleStride)
						{
							ref var p0 = ref strandGroup.particlePosition[j - strandParticleStride];
							ref var p1 = ref strandGroup.particlePosition[j];

							strandParams.x += Vector3.Distance(p0, p1);
						}
					}
				}

				// ensure non-zero strand parameters
				{
					var strandParamsEps = 1e-10f * Vector4.one;
					{
						for (int i = 0; i != strandCount; i++)
						{
							strandGroup.rootScale[i] = Vector4.Max(strandGroup.rootScale[i], strandParamsEps);
						}
					}
				}

				// calc maximum/average strand parameters within group (x: strand length, y: strand diameter, z: tip scale offset, w: tip scale)
				strandGroup.strandLengthTotal = 0.0f;
				strandGroup.strandParamsMax = Vector4.zero;
				strandGroup.strandParamsAvg = Vector4.zero;
				{
					for (int i = 0; i != strandCount; i++)
					{
						ref readonly var strandParams = ref strandGroup.rootScale[i];
						{
							strandGroup.strandLengthTotal += strandParams.x;
							strandGroup.strandParamsMax = Vector4.Max(strandGroup.strandParamsMax, strandParams);
							strandGroup.strandParamsAvg += strandParams.x * strandParams;
						}
					}

					strandGroup.strandParamsAvg /= strandGroup.strandLengthTotal;
				}

				// calc normalized strand parameters wrt. group maximum (x: strand length, y: strand diameter, z: tip scale offset, w: tip scale)
				for (int i = 0; i != strandCount; i++)
				{
					strandGroup.rootScale[i].Scale(strandGroup.strandParamsMax.Rcp());
				}

				// calc bounds
				if (strandGroup.particlePosition.Length > 0)
				{
					var boundsMin = strandGroup.particlePosition[0];
					var boundsMax = boundsMin;
					var boundsPad = strandGroup.strandParamsMax.y * Vector3.one;

					for (int i = 1, n = strandGroup.particlePosition.Length; i != n; i++)
					{
						longOperation.UpdateStatus("Computing bounds", i, n);

						boundsMin = Vector3.Min(boundsMin, strandGroup.particlePosition[i]);
						boundsMax = Vector3.Max(boundsMax, strandGroup.particlePosition[i]);
					}

					strandGroup.bounds = new Bounds(0.5f * (boundsMin + boundsMax), boundsMax - boundsMin + boundsPad);
				}
				else
				{
					strandGroup.bounds = new Bounds(Vector3.zero, Vector3.zero);
				}
			}

			// build lod clusters
			BuildLODClusters(ref strandGroup, hairAsset);

			// build mesh assets
			using (var longOperation = new LongOperationScope("Preparing mesh assets"))
			{
				unsafe
				{
#if VISIBLE_SUBASSETS
					var hideFlags = HideFlags.None;
#else
					var hideFlags = HideFlags.HideInHierarchy;
#endif

					strandGroup.meshAssetRoots = HairInstanceBuilder.CreateMeshRoots(
						hideFlags,
						strandGroup.particleMemoryLayout,
						strandGroup.strandCount,
						strandGroup.strandParticleCount,
						strandGroup.particlePosition);
				}
			}
		}

		// STRATEGY 1: solve base lod first, and then split towards high lod <--- EQ. divisive aproach
		//	PROS:
		//		- fully informed base lod 
		//		- also splits can be locally refined
		//	CONS:
		//		- costly to refine every split
		//		- splitting might be complicated
		//	SPLIT:
		//		a. sort clusters by number of strands descending
		//		b. split largest cluster in two
		//		c. refine the split
		//
		// ------ ------
		//
		// STRATEGY 2: merge towards base lod
		//	PROS:
		//		- only high lod needs to be refined
		//	CONS: 
		//		- only high lod can be refined
		//		- base lod not fully informed
		//	MERGE:
		//		a. sort clusters by number of strands ascending
		//		b. merge smallest cluster to one with nD closest mean
		//		c. recompute closest mean
		//
		// ------ ------
		//
		// INITIALIZE (3D):
		//	1. guess N root cluster centers
		//		a. sample volume
		//		b. sample mesh
		//		c. mis?
		//	2. build initial clusters by root distance
		//
		// REFINE (nD):
		//	3. k-means iteration in higher dimensional space
		//		a. translate cluster set into nD
		//		b. then cluster by kmeans in nD
		//
		// SPLIT (3D):
		//	4. sort clusters by number of strands
		//	5. split largest cluster
		//		a. longest axis / pca?
		//		b. 
		//
		static void BuildLODClusters(ref HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			var strandCount = strandGroup.strandCount;
			var strandParticleCount = strandGroup.strandParticleCount;

			// prepare lod chain
			var lodCapacity = 0;
			var lodChain = new LODChain(lodCapacity, strandGroup, hairAsset.settingsBasic.kLODClustersClustering, hairAsset.settingsLODClusters.clusterVoid, Allocator.Temp);

			// build lod chain
			using (StrandClusterUtility.BindStrandData(ref lodChain.clusterSet, strandGroup, Allocator.Temp))
			{
				if (hairAsset.settingsBasic.kLODClusters)
				{
					switch (hairAsset.settingsLODClusters.baseLOD.baseLOD)
					{
						case HairAsset.SettingsLODClusters.BaseLODMode.Generated:
							BuildBaseLODGenerated(ref lodChain, strandGroup, hairAsset);
							break;

						case HairAsset.SettingsLODClusters.BaseLODMode.UVMapped:
							BuildBaseLODUVMapped(ref lodChain, strandGroup, hairAsset);
							break;
					}

					if (hairAsset.settingsLODClusters.highLOD.highLOD)
					{
						switch (hairAsset.settingsLODClusters.highLOD.highLODMode)
						{
							case HairAsset.SettingsLODClusters.HighLODMode.Automatic:
								BuildHighLODAutomatic(ref lodChain, strandGroup, hairAsset);
								break;

							case HairAsset.SettingsLODClusters.HighLODMode.Manual:
								BuildHighLODManual(ref lodChain, strandGroup, hairAsset);
								break;
						}
					}
				}

				if (lodChain.lodCount == 0 || lodChain.lodGuideCount[lodChain.lodCount - 1] < strandCount)
				{
					BuildHighLODAllStrands(ref lodChain, strandGroup, hairAsset);
				}
			}

			// export lod clusters
			strandGroup.lodCount = lodChain.lodCount;
#if LOD_INDEX_INCREASING
#if HAS_PACKAGE_UNITY_COLLECTIONS_1_0_0_PRE_3
			strandGroup.lodGuideCount = lodChain.lodGuideCount.ToArrayNBC();
			strandGroup.lodGuideIndex = lodChain.lodGuideIndex.ToArrayNBC();
			strandGroup.lodGuideCarry = lodChain.lodGuideCarry.ToArrayNBC();
			strandGroup.lodGuideReach = lodChain.lodGuideReach.ToArrayNBC();
#else
			strandGroup.lodGuideCount = lodChain.lodGuideCount.ToArray();
			strandGroup.lodGuideIndex = lodChain.lodGuideIndex.ToArray();
			strandGroup.lodGuideCarry = lodChain.lodGuideCarry.ToArray();
			strandGroup.lodGuideReach = lodChain.lodGuideReach.ToArray();
#endif
#else
			strandGroup.lodGuideCount = new int[lodChain.lodGuideCount.Length];
			strandGroup.lodGuideIndex = new int[lodChain.lodGuideIndex.Length];
			strandGroup.lodGuideCarry = new float[lodChain.lodGuideCarry.Length];
			strandGroup.lodGuideReach = new float[lodChain.lodGuideReach.Length];

			unsafe
			{
				// write highest granularity to LOD 0, next-to-highest granularity to LOD 1, etc.
				fixed (int* lodGuideCountDstBase = strandGroup.lodGuideCount)
				fixed (int* lodGuideIndexDstBase = strandGroup.lodGuideIndex)
				fixed (float* lodGuideCarryDstBase = strandGroup.lodGuideCarry)
				fixed (float* lodGuideReachDstBase = strandGroup.lodGuideReach)
				{
					var lodGuideCountCopyDst = lodGuideCountDstBase;
					var lodGuideIndexCopyDst = lodGuideIndexDstBase;
					var lodGuideCarryCopyDst = lodGuideCarryDstBase;
					var lodGuideReachCopyDst = lodGuideReachDstBase;

					var lodGuideCountCopySrc = (int*)lodChain.lodGuideCount.GetUnsafePtr() + (strandGroup.lodCount);
					var lodGuideIndexCopySrc = (int*)lodChain.lodGuideIndex.GetUnsafePtr() + (strandGroup.lodCount * strandCount);
					var lodGuideCarryCopySrc = (float*)lodChain.lodGuideCarry.GetUnsafePtr() + (strandGroup.lodCount * strandCount);
					var lodGuideReachCopySrc = (float*)lodChain.lodGuideReach.GetUnsafePtr() + (strandGroup.lodCount * strandCount);

					for (int i = 0; i != strandGroup.lodCount; i++)
					{
						lodGuideCountCopySrc -= 1;
						lodGuideIndexCopySrc -= strandCount;
						lodGuideCarryCopySrc -= strandCount;
						lodGuideReachCopySrc -= strandCount;

						UnsafeUtility.MemCpy(lodGuideCountCopyDst, lodGuideCountCopySrc, sizeof(int));
						UnsafeUtility.MemCpy(lodGuideIndexCopyDst, lodGuideIndexCopySrc, sizeof(int) * strandCount);
						UnsafeUtility.MemCpy(lodGuideCarryCopyDst, lodGuideCarryCopySrc, sizeof(float) * strandCount);
						UnsafeUtility.MemCpy(lodGuideReachCopyDst, lodGuideReachCopySrc, sizeof(float) * strandCount);

						lodGuideCountCopyDst += 1;
						lodGuideIndexCopyDst += strandCount;
						lodGuideCarryCopyDst += strandCount;
						lodGuideReachCopyDst += strandCount;
					}
				}
			}
#endif

			// export lod thresholds
			strandGroup.lodThreshold = new float[strandGroup.lodCount];
			{
				for (int i = 0; i != strandGroup.lodCount; i++)
				{
					strandGroup.lodThreshold[i] = strandGroup.lodGuideCount[i] / (float)strandGroup.strandCount;
				}
			}

			// build remapping tables from final cluster set
			var remappingRequired = (lodChain.lodCount > 1 || lodChain.clusterSet.dataDesc.clusterCount < strandCount);
			if (remappingRequired)
			{
				using (var remapping = new StrandClusterRemapping(lodChain.clusterSet, Allocator.Temp))
				{
					// apply to strands
					remapping.ApplyShuffle(strandGroup.rootUV, 0, 1, strandCount);
					remapping.ApplyShuffle(strandGroup.rootScale, 0, 1, strandCount);

					HairAssetUtility.DeclareParticleStride(strandGroup, out var strandParticleOffset, out var strandParticleStride);
					//	.Sequential
					//		strandParticleOffset = strandParticleCount;
					//		strandParticleStride = 1;
					//	.Interleaved
					//		strandParticleOffset = 1;
					//		strandParticleStride = strandCount;

					for (int i = 0; i != strandParticleCount; i++)
					{
						remapping.ApplyShuffle(strandGroup.particlePosition,
							attrOffset: strandParticleStride * i,
							attrStride: strandParticleOffset,
							attrCount: strandCount);
					}

					if (strandGroup.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.TexCoord))
					{
						for (int i = 0; i != strandParticleCount; i++)
						{
							remapping.ApplyShuffle(strandGroup.particleTexCoord,
								attrOffset: strandParticleStride * i,
								attrStride: strandParticleOffset,
								attrCount: strandCount);
						}
					}

					if (strandGroup.particleFeatures.HasFlag(HairAsset.StrandGroup.ParticleFeatures.Diameter))
					{
						for (int i = 0; i != strandParticleCount; i++)
						{
							remapping.ApplyShuffle(strandGroup.particleDiameter,
								attrOffset: strandParticleStride * i,
								attrStride: strandParticleOffset,
								attrCount: strandCount);
						}
					}

					/* old particle shuffle, for reference
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
					*/

					for (int i = 0; i != strandGroup.lodCount; i++)
					{
						remapping.ApplyShuffle(strandGroup.lodGuideIndex, i * strandCount, 1, strandCount);
						remapping.ApplyShuffle(strandGroup.lodGuideCarry, i * strandCount, 1, strandCount);
						remapping.ApplyShuffle(strandGroup.lodGuideReach, i * strandCount, 1, strandCount);
					}

					// apply to indices (since strands have now moved)
					remapping.ApplyRemapping(strandGroup.lodGuideIndex);
				}
			}

			// dispose lod chain
			lodChain.Dispose();
		}

		static unsafe void BuildBaseLODGenerated(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			ref readonly var settingsClusters = ref hairAsset.settingsLODClusters;
			ref readonly var settings = ref settingsClusters.baseLODParamsGenerated;

			var clusterCount = Mathf.RoundToInt(strandGroup.strandCount * settings.baseLODClusterQuantity);
			if (clusterCount > strandGroup.strandCount)
				clusterCount = strandGroup.strandCount;
			if (clusterCount < 1)
				clusterCount = 1;

			if (lodChain.clusterSet.ExpandProcedural(clusterCount, settingsClusters.clusterAllocation, settingsClusters.clusterAllocationOrder, settingsClusters.clusterRefinement ? settingsClusters.clusterRefinementIterations : 0))
			{
				lodChain.clusterSet.Commit();
				lodChain.Increment();
			}
		}

		static unsafe void BuildBaseLODUVMapped(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			ref readonly var settingsClusters = ref hairAsset.settingsLODClusters;
			ref readonly var settings = ref hairAsset.settingsLODClusters.baseLODParamsUVMapped;

			var strandCount = strandGroup.strandCount;

			// early out if no readable cluster maps
			var clusterMaps = settings.baseLODClusterMaps;
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
#if HAS_PACKAGE_UNITY_COLLECTIONS_1_3_0
			using (var clusterLookupColor = new UnsafeParallelHashMap<Color, int>(strandCount, Allocator.Temp))
			using (var clusterLookupLabel = new UnsafeParallelHashMap<uint, int>(strandCount, Allocator.Temp))
#else
			using (var clusterLookupColor = new UnsafeHashMap<Color, int>(strandCount, Allocator.Temp))
			using (var clusterLookupLabel = new UnsafeHashMap<uint, int>(strandCount, Allocator.Temp))
#endif
			using (var strandCluster = new UnsafeList<int>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
			{
				var strandClusterPtr = strandCluster.Ptr;

				foreach (var clusterMap in clusterMaps)
				{
					if (clusterMap == null || clusterMap.isReadable == false)
						continue;

					// build strand->cluster
					var clusterCount = 0;

					switch (settings.baseLODClusterFormat)
					{
						case HairAsset.SettingsLODClusters.BaseLODClusterMapFormat.OneClusterPerColor:
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

						case HairAsset.SettingsLODClusters.BaseLODClusterMapFormat.OneClusterPerVisualCluster:
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

					// expand existing set
					if (lodChain.clusterSet.ExpandPreassigned(clusterCount, strandCluster))
					{
						if (settingsClusters.clusterRefinement)
						{
							lodChain.clusterSet.Refine(settingsClusters.clusterRefinementIterations);
						}

						lodChain.clusterSet.Commit();
						lodChain.Increment();
					}
				}
			}
		}

		static unsafe void BuildHighLODAutomatic(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			ref readonly var settingsClusters = ref hairAsset.settingsLODClusters;
			ref readonly var settings = ref hairAsset.settingsLODClusters.highLODParamsAutomatic;

			var clusterCountMin = lodChain.clusterSet.dataDesc.clusterCount;
			if (clusterCountMin == 0)
				return;

			var clusterCountMax = Mathf.RoundToInt(settings.highLODClusterQuantity * strandGroup.strandCount);
			if (clusterCountMax > strandGroup.strandCount)
				clusterCountMax = strandGroup.strandCount;
			if (clusterCountMax < 1)
				clusterCountMax = 1;

			var logS = Mathf.Log(settings.highLODClusterExpansion);
			var logR = Mathf.Log((float)clusterCountMax / (float)clusterCountMin);
			var step = logR / logS;

			var incrementCount = Mathf.CeilToInt(step);
			if (incrementCount > 0)
			{
				for (int i = 0; i != incrementCount; i++)
				{
					//     .---steps---.
					// 0---A---B---C---1
					// min           max

					var t = (i + 1) / (float)incrementCount;
					var s = clusterCountMin * Mathf.Pow(settings.highLODClusterExpansion, t * step);
					
					var clusterCount = Mathf.RoundToInt(s);
					if (clusterCount < lodChain.strandCount)
					{
						if (lodChain.clusterSet.ExpandProcedural(clusterCount, settingsClusters.clusterAllocation, settingsClusters.clusterAllocationOrder, settingsClusters.clusterRefinement ? settingsClusters.clusterRefinementIterations : 0))
						{
							lodChain.clusterSet.Commit();
							lodChain.Increment();
						}
					}
					else
					{
						BuildHighLODAllStrands(ref lodChain, strandGroup, hairAsset);
						break;
					}
				}
			}
		}

		static unsafe void BuildHighLODManual(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			ref readonly var settingsClusters = ref hairAsset.settingsLODClusters;
			ref readonly var settings = ref settingsClusters.highLODParamsManual;

			for (int i = 0; i != settings.highLODClusterQuantities.Length; i++)
			{
				var clusterCount = Mathf.RoundToInt(strandGroup.strandCount * settings.highLODClusterQuantities[i]);
				if (clusterCount > strandGroup.strandCount)
					clusterCount = strandGroup.strandCount;
				if (clusterCount < 1)
					clusterCount = 1;

				if (clusterCount < lodChain.strandCount)
				{
					if (lodChain.clusterSet.ExpandProcedural(clusterCount, settingsClusters.clusterAllocation, settingsClusters.clusterAllocationOrder, settingsClusters.clusterRefinement ? settingsClusters.clusterRefinementIterations : 0))
					{
						lodChain.clusterSet.Commit();
						lodChain.Increment();
					}
				}
				else
				{
					BuildHighLODAllStrands(ref lodChain, strandGroup, hairAsset);
					break;
				}
			}
		}

		static unsafe void BuildHighLODAllStrands(ref LODChain lodChain, in HairAsset.StrandGroup strandGroup, HairAsset hairAsset)
		{
			using (var strandIndices = StrandClusterContext.AllocateRange(0, strandGroup.strandCount, Allocator.Temp))
			{
				if (lodChain.clusterSet.ExpandPreassigned(strandGroup.strandCount, strandIndices))
				{
					lodChain.clusterSet.Commit();
					lodChain.Increment();
				}
			}
		}

		static unsafe bool GenerateRoots(in HairAssetProvisional.ProceduralRoots roots, in HairAsset.SettingsProcedural settings)
		{
			switch (settings.placement)
			{
				case HairAsset.SettingsProcedural.PlacementMode.Primitive:
					return GenerateRootsPrimitive(roots, settings);

				case HairAsset.SettingsProcedural.PlacementMode.Custom:
					return (settings.placementProvider)?.GenerateRoots(roots) ?? false;

				case HairAsset.SettingsProcedural.PlacementMode.Mesh:
					return GenerateRootsMesh(roots, settings);

				default:
					return false;
			}
		}

		static unsafe bool GenerateRootsPrimitive(in HairAssetProvisional.ProceduralRoots roots, in HairAsset.SettingsProcedural settings)
		{
			roots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);

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
				rootVar[i] = HairAssetProvisional.ProceduralRoots.RootParameters.defaults;
			}

			return true;// success
		}

		static unsafe bool GenerateRootsMesh(in HairAssetProvisional.ProceduralRoots roots, in HairAsset.SettingsProcedural settings)
		{
			var placementMesh = settings.placementMesh;
			if (placementMesh == null)
				return false;

			roots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);

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
					switch (settings.mappedDirection.format)
					{
						case TextureFormat.DXT5:
							{
								for (int i = 0; i != settings.strandCount; i++)
								{
									Vector4 dxt5nm = settings.mappedDirection.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, mipLevel: 0);
									{
										dxt5nm.x *= dxt5nm.w;
									}
									Vector3 n;
									{
										n.x = dxt5nm.x * 2.0f - 1.0f;
										n.y = dxt5nm.y * 2.0f - 1.0f;
										n.z = Mathf.Sqrt(1.0f - Mathf.Clamp01(n.x * n.x + n.y * n.y));
									}
									rootDir[i] = Vector3.Normalize(n);
								}
							}
							break;

						default:
							{
								for (int i = 0; i != settings.strandCount; i++)
								{
									Vector4 xyz = settings.mappedDirection.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, mipLevel: 0);
									Vector3 n;
									{
										n.x = 2.0f * xyz.x - 1.0f;
										n.y = 2.0f * xyz.y - 1.0f;
										n.z = 2.0f * xyz.z - 1.0f;
									}
									rootDir[i] = Vector3.Normalize(n);
								}
							}
							break;
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
						rootVar[i] = HairAssetProvisional.ProceduralRoots.RootParameters.defaults;
					}
				}
			}

			return true;// success
		}

		static unsafe bool GenerateStrands(in HairAssetProvisional.ProceduralStrands strands, in HairAssetProvisional.ProceduralRoots roots, in HairAsset.SettingsProcedural settings, HairAsset.MemoryLayout memoryLayout)
		{
			var particleInterval = settings.strandLength / (settings.strandParticleCount - 1);
			var particleIntervalVariation = settings.strandLengthVariation ? settings.strandLengthVariationAmount : 0.0f;
			var particleIntervalRandSeq = new Unity.Mathematics.Random(257);
			var particleDiameter = settings.strandDiameter * 0.001f;
			var particleDiameterVariation = settings.strandDiameterVariation ? settings.strandDiameterVariationAmount : 0.0f;
			var particleDiameterRandSeq = new Unity.Mathematics.Random(3432);

			var tipScaleVariation = settings.tipScaleVariation ? settings.tipScaleVariationAmount : 0.0f;
			var tipScaleRandSeq = new Unity.Mathematics.Random(7546);
			var tipScaleOffsetVariation = settings.tipScaleOffsetVariation ? settings.tipScaleOffsetVariationAmount : 0.0f;
			var tipScaleOffsetRandSeq = new Unity.Mathematics.Random(9277);

			var curlRadiusVariation = settings.curlVariation ? settings.curlVariationRadius : 0.0f;
			var curlRadiusRandSeq = new Unity.Mathematics.Random(709);
			var curlSlopeVariation = settings.curlVariation ? settings.curlVariationSlope : 0.0f;
			var curlSlopeRandSeq = new Unity.Mathematics.Random(1171);
			var curlPlaneRandSeq = new Unity.Mathematics.Random(457);

			static float NextScale(ref Unity.Mathematics.Random randSeq, float variation)
			{
				if (variation > 0.0f)
					return Mathf.Lerp(1.0f, randSeq.NextFloat(), variation);
				else
					return 1.0f;
			}

			static Vector3 NextVectorInPlane(ref Mathematics.Random randSeq, in Vector3 n)
			{
				Vector3 r;
				{
					do r = Vector3.ProjectOnPlane(randSeq.NextFloat3Direction(), n);
					while (Vector3.SqrMagnitude(r) < 1e-5f);
				}
				return r;
			};

			using (var longOperation = new LongOperationScope("Generating strands"))
			{
				roots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);
				strands.GetUnsafePtrs(out var rootScalePtr, out var particlePositionPtr);

				for (int i = 0; i != settings.strandCount; i++)
				{
					longOperation.UpdateStatus("Strand", i, settings.strandCount);

					// resolve root diameter and tapering
					rootScalePtr[i].x = 0.0f;// completed in FinalizeStrandGroup(..)
					rootScalePtr[i].y = rootVar[i].normalizedStrandDiameter * particleDiameter * NextScale(ref particleDiameterRandSeq, particleDiameterVariation);
					rootScalePtr[i].z = settings.tipScaleOffset * NextScale(ref tipScaleOffsetRandSeq, tipScaleOffsetVariation);
					rootScalePtr[i].w = settings.tipScale * NextScale(ref tipScaleRandSeq, tipScaleVariation);

					// resolve particle positions
					var step = rootVar[i].normalizedStrandLength * particleInterval * NextScale(ref particleIntervalRandSeq, particleIntervalVariation);
					var curPos = rootPos[i];
					var curDir = rootDir[i];

					HairAssetUtility.DeclareStrandIterator(memoryLayout, settings.strandCount, settings.strandParticleCount, i, out var strandParticleBegin, out var strandParticleStride, out var strandParticleEnd);

					if (settings.curl)
					{
						var curPlaneU = Vector3.Normalize(NextVectorInPlane(ref curlPlaneRandSeq, curDir));
						var curPlaneV = Vector3.Cross(curPlaneU, curDir);

						var targetRadius = rootVar[i].normalizedCurlRadius * settings.curlRadius * 0.01f * NextScale(ref curlRadiusRandSeq, curlRadiusVariation);
						var targetSlope = rootVar[i].normalizedCurlSlope * settings.curlSlope * NextScale(ref curlSlopeRandSeq, curlSlopeVariation);

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

							particlePositionPtr[j] =
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
							particlePositionPtr[j] = curPos;
							curPos += step * curDir;
						}
					}
				}
			}

			return true;// success
		}

		struct ResampleBlend
		{
			public int srcIndex0;
			public int srcIndex1;
			public float srcBlendFrac;
		}

		static unsafe void Resample(Vector3* srcPos, int srcCount, Vector3* dstPos, int dstCount, ResampleBlend* dstResolve, int iterations)
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

			ResampleWithHint(srcPos, srcCount, out var srcIndex, dstPos, dstCount, out var dstIndex, dstSpacing, dstResolve);

			// run a couple of iterations
			for (int i = 0; i != iterations; i++)
			{
				if (dstIndex < dstCount || srcIndex < srcCount)
				{
					var remainder = Vector3.Distance(srcPos[srcCount - 1], dstPos[dstIndex - 1]);

					dstLength = (dstIndex - 1) * dstSpacing + remainder;
					dstSpacing = dstLength / (dstCount - 1);

					ResampleWithHint(srcPos, srcCount, out srcIndex, dstPos, dstCount, out dstIndex, dstSpacing, dstResolve);
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
					// extrapolated blend weights are a continuation of the last source vertex
					dstResolve[dstIndex].srcIndex0 = srcCount - 1;
					dstResolve[dstIndex].srcIndex1 = srcCount - 1;
					dstResolve[dstIndex].srcBlendFrac = 0.0f;

					dstPos[dstIndex] = dstPosPrev + dstDirTail * dstSpacing;
					dstPosPrev = dstPos[dstIndex++];
				}
			}
		}

		static unsafe void ResampleWithHint(Vector3* srcPos, int srcCount, out int srcIndex, Vector3* dstPos, int dstCount, out int dstIndex, float dstSpacing, ResampleBlend* dstResolve)
		{
			dstPos[0] = srcPos[0];

			dstResolve[0].srcIndex0 = 0;
			dstResolve[0].srcIndex1 = 0;
			dstResolve[0].srcBlendFrac = 0.0f;

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

					if (bb > float.Epsilon)
						dstPos[dstIndex] = p + n * Mathf.Sqrt(bb);
					else
						dstPos[dstIndex] = p;

					dstResolve[dstIndex].srcIndex0 = srcIndex - 1;
					dstResolve[dstIndex].srcIndex1 = srcIndex;
					dstResolve[dstIndex].srcBlendFrac = Vector3.Dot(n, dstPos[dstIndex] - srcPos[srcIndex - 1]);

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
			public LongOperationScope longOperation;

			public UnsafeClusterSet clusterSet;

			public int lodCount;
			public NativeList<int> lodGuideCount;
			public NativeList<int> lodGuideIndex;
			public NativeList<float> lodGuideCarry;
			public NativeList<float> lodGuideReach;

			public int strandCount;
			public UnsafeList<int> strandGuide;
			public UnsafeList<float> strandCarry;
			public UnsafeList<float> strandReach;

			public LODChain(int lodCapacity, in HairAsset.StrandGroup strandGroup, HairAsset.StrandClusterMode strandClusterMode, ClusterVoid clusterVoid, Allocator allocator)
			{
				this.longOperation = new LongOperationScope("Building clusters");
				this.longOperation.UpdateStatus("Level 1 / ?", 0.0f);

				this.clusterSet = StrandClusterUtility.CreateEmptySet(0, clusterVoid, strandGroup, strandClusterMode, Allocator.Temp);
					
				this.lodCount = 0;
				this.lodGuideCount = new NativeList<int>(lodCapacity, allocator);
				this.lodGuideIndex = new NativeList<int>(lodCapacity * strandGroup.strandCount, allocator);
				this.lodGuideCarry = new NativeList<float>(lodCapacity * strandGroup.strandCount, allocator);
				this.lodGuideReach = new NativeList<float>(lodCapacity * strandGroup.strandCount, allocator);

				this.strandCount = strandGroup.strandCount;
				this.strandGuide = new UnsafeList<int>(strandGroup.strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.strandCarry = new UnsafeList<float>(strandGroup.strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.strandReach = new UnsafeList<float>(strandGroup.strandCount, allocator, NativeArrayOptions.UninitializedMemory);
			}

			public void Dispose()
			{
				longOperation.Dispose();

				clusterSet.Dispose();

				lodGuideCount.Dispose();
				lodGuideIndex.Dispose();
				lodGuideCarry.Dispose();
				lodGuideReach.Dispose();

				strandGuide.Dispose();
				strandCarry.Dispose();
				strandReach.Dispose();
			}

			public bool Increment()
			{
				var clusterCountPrev = (lodCount == 0) ? 0 : lodGuideCount[lodGuideCount.Length - 1];
				if (clusterCountPrev < clusterSet.dataDesc.clusterCount)
				{
					var strandGuidePtr = strandGuide.Ptr;
					var strandCarryPtr = strandCarry.Ptr;
					var strandReachPtr = strandReach.Ptr;

					for (int i = 0; i != strandCount; i++)
					{
						int k = clusterSet.dataDesc.sampleClusterPtr[i];
						{
							strandGuidePtr[i] = clusterSet.dataDesc.clusterGuidePtr[k];
							strandCarryPtr[i] = clusterSet.dataDesc.clusterCarryPtr[k];
							strandReachPtr[i] = clusterSet.dataDesc.clusterReachPtr[k];

							// non-primary strands carry nothing
							if (strandGuidePtr[i] != i)
							{
								strandCarryPtr[i] = 0.0f;
							}
						}
					}

					lodCount++;
					lodGuideCount.Add(clusterSet.dataDesc.clusterCount);
					lodGuideIndex.AddRange(strandGuidePtr, strandCount);
					lodGuideCarry.AddRange(strandCarryPtr, strandCount);
					lodGuideReach.AddRange(strandReachPtr, strandCount);

					longOperation.UpdateStatus("Level " + lodCount + " / ?", 0.0f);
					return true;
				}
				else
				{
					return false;
				}
			}

			/* old preassigned method, kept for reference
			public bool TryAppend(ClusterSet nextClusters)
			{
				if (nextClusters.dataDesc.clusterCount > clusterSet.dataDesc.clusterCount)
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
							int k = clusterSet.dataDesc.sampleClusterPtr[i];
							{
								strandGuidePtr[i] = clusterSet.dataDesc.clusterGuidePtr[k];
								strandCarryPtr[i] = clusterSet.dataDesc.clusterCarryPtr[k];
							}
						}

						lodCount++;
						lodGuideCount.Add(clusterSet.dataDesc.clusterCount);
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
			*/
		}

		public unsafe struct StrandClusterUtility
		{
			public static UnsafeClusterSet CreateEmptySet(int clusterCapacity, ClusterVoid clusterEmpty, in HairAsset.StrandGroup strandGroup, HairAsset.StrandClusterMode strandClusterMode, Allocator allocator)
			{
				HairAssetUtility.DeclareParticleStride(strandGroup, out var strandParticleOffset, out var strandParticleStride);

				var sampleCount = strandGroup.strandCount;
				var samplePositionOffset = strandParticleOffset;
				var samplePositionStride = strandParticleStride;
				var samplePositionCount = strandGroup.strandParticleCount;
				{
					switch (strandClusterMode)
					{
						case HairAsset.StrandClusterMode.Roots: samplePositionCount = 1; break;
						case HairAsset.StrandClusterMode.Strands: break;
						case HairAsset.StrandClusterMode.Strands3pt: samplePositionStride *= (samplePositionCount / 2); samplePositionCount = 3; break;
					}
				}

				return new UnsafeClusterSet(clusterCapacity, clusterEmpty, sampleCount, samplePositionOffset, samplePositionStride, samplePositionCount, allocator);
			}

			public static StrandClusterContext BindStrandData(ref UnsafeClusterSet clusterSet, in HairAsset.StrandGroup strandGroup, Allocator allocator)
			{
				return new StrandClusterContext(ref clusterSet, strandGroup, allocator);
			}
		}

		public unsafe struct StrandClusterContext : IDisposable
		{
			ulong gchSamplePosition;
			UnsafeList<int> sampleIndices;
			UnsafeList<float> sampleWeights;

			public StrandClusterContext(ref UnsafeClusterSet clusterSet, in HairAsset.StrandGroup strandGroup, Allocator allocator)
			{
				this.sampleIndices = AllocateRange(0, strandGroup.strandCount, Allocator.Temp);
				this.sampleWeights = new UnsafeList<float>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
				{
					fixed (Vector4* rootScalePtr = strandGroup.rootScale)
					{
						var sampleWeightsPtr = sampleWeights.Ptr;
						for (int i = 0; i != strandGroup.strandCount; i++)
						{
							ref readonly var L = ref rootScalePtr[i].x;// normalized length
							ref readonly var H = ref rootScalePtr[i].y;// normalized diameter
							sampleWeightsPtr[i] = L * H * H;// weigh strands by volume
						}
					}
				}

				clusterSet.dataDesc.samplePositionPtr = (Vector3*)UnsafeUtility.PinGCArrayAndGetDataAddress(strandGroup.particlePosition, out this.gchSamplePosition);
				clusterSet.dataDesc.sampleResolvePtr = sampleIndices.Ptr;
				clusterSet.dataDesc.sampleWeightPtr = sampleWeights.Ptr;
			}

			public void Dispose()
			{
				UnsafeUtility.ReleaseGCObject(gchSamplePosition);
				sampleIndices.Dispose();
				sampleWeights.Dispose();
			}

			public static UnsafeList<int> AllocateRange(int offset, int length, Allocator allocator)
			{
				var range = new UnsafeList<int>(length, allocator, NativeArrayOptions.UninitializedMemory);
				var rangePtr = range.Ptr;
				{
					for (int i = 0; i != length; i++)
					{
						rangePtr[i] = i + offset;
					}
				}
				return range;
			}
		}

		unsafe struct StrandClusterRemapping : IDisposable
		{
			public UnsafeList<int> strandRemapSrc;// maps final strand index -> old strand index
			public UnsafeList<int> strandRemapDst;// maps old strand index -> final strand index

			public int* strandRemapSrcPtr;
			public int* strandRemapDstPtr;

			public StrandClusterRemapping(in UnsafeClusterSet clusterSet, Allocator allocator)
			{
				strandRemapSrc = new UnsafeList<int>(clusterSet.dataDesc.sampleCount, allocator, NativeArrayOptions.UninitializedMemory);
				strandRemapDst = new UnsafeList<int>(clusterSet.dataDesc.sampleCount, allocator, NativeArrayOptions.UninitializedMemory);

				strandRemapSrcPtr = strandRemapSrc.Ptr;
				strandRemapDstPtr = strandRemapDst.Ptr;

				// sort clusters by depth first and guide second
				using (var sortedClusters = new UnsafeList<ulong>(clusterSet.dataDesc.clusterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
				{
					var sortedClustersPtr = sortedClusters.Ptr;

					for (int k = 0; k != clusterSet.dataDesc.clusterCount; k++)
					{
						var sortDepth = (ulong)clusterSet.dataDesc.clusterDepthPtr[k] << 32;
						var sortGuide = (ulong)clusterSet.dataDesc.clusterGuidePtr[k];
						{
							sortedClustersPtr[k] = sortDepth | sortGuide;
						}
					}

					NativeSortExtension.Sort(sortedClustersPtr, clusterSet.dataDesc.clusterCount);

					// write remapping table for guides from sorted clusters
					for (int k = 0; k != clusterSet.dataDesc.clusterCount; k++)
					{
						var guide = (int)(sortedClustersPtr[k] & 0xffffffffuL);
						{
							strandRemapSrcPtr[k] = guide;
							strandRemapDstPtr[guide] = k;
						}
					}
				}

				var remainingCount = clusterSet.dataDesc.sampleCount - clusterSet.dataDesc.clusterCount;
				if (remainingCount > 0)
				{
					// sort remaining (non-guide) strands by remapped guide first and index second
					using (var sortedRemaining = new UnsafeList<ulong>(remainingCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
					{
						var sortedRemainingPtr = sortedRemaining.Ptr;

						for (int i = 0, j = 0; i != clusterSet.dataDesc.sampleCount; i++)
						{
							var guide = clusterSet.dataDesc.clusterGuidePtr[clusterSet.dataDesc.sampleClusterPtr[i]];
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
						NativeSortExtension.Sort(sortedRemainingPtr, remainingCount);

						// write remapping table for remaining (non-guide) strands
						for (int i = 0, j = clusterSet.dataDesc.clusterCount; i != remainingCount; i++, j++)
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
				using (var buffer = new NativeArray<byte>(attrCount * attrSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
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

					// copy back reference
					//
					//	for (int i = 0; i != attrCount; i++)
					//	{
					//		var attrDstPtr = attrPtr + attrStride * i;
					//		var attrSrcPtr = bufferPtr + attrSize * i;
					//		UnsafeUtility.MemCpy(attrDstPtr, attrSrcPtr, attrSize);
					//	}
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
