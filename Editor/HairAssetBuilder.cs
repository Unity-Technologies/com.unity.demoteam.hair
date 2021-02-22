//#define ENABLE_VISIBLE_SUBASSETS

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if UNITY_FORMATS_ALEMBIC
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
			foreach (var strandGroup in hairAsset.strandGroups)
			{
				if (strandGroup.meshAssetLines != null)
				{
					AssetDatabase.RemoveObjectFromAsset(strandGroup.meshAssetLines);
					Mesh.DestroyImmediate(strandGroup.meshAssetLines);
				}

				if (strandGroup.meshAssetRoots != null)
				{
					AssetDatabase.RemoveObjectFromAsset(strandGroup.meshAssetRoots);
					Mesh.DestroyImmediate(strandGroup.meshAssetRoots);
				}
			}

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
					AssetDatabase.AddObjectToAsset(hairAsset.strandGroups[i].meshAssetLines, hairAsset);
					AssetDatabase.AddObjectToAsset(hairAsset.strandGroups[i].meshAssetRoots, hairAsset);

					hairAsset.strandGroups[i].meshAssetLines.name = "Lines:" + i;
					hairAsset.strandGroups[i].meshAssetRoots.name = "Roots:" + i;
				}
			}

			// dirty the asset
			EditorUtility.SetDirty(hairAsset);

#if ENABLE_VISIBLE_SUBASSETS
			// save and re-import to force hierearchy update
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(hairAsset), ImportAssetOptions.ForceUpdate);
#endif
		}

		public static void BuildHairAsset(HairAsset hairAsset, in HairAsset.SettingsAlembic settings, HairAsset.MemoryLayout memoryLayout)
		{
#if UNITY_FORMATS_ALEMBIC
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

			// fetch all curve sets 
			var curveSets = alembic.gameObject.GetComponentsInChildren<AlembicCurves>(true);
			if (curveSets.Length == 0)
				return;

			// prep strand groups in hair asset
			hairAsset.strandGroups = new HairAsset.StrandGroup[curveSets.Length];

			// build strand groups in hair asset
			for (int i = 0; i != hairAsset.strandGroups.Length; i++)
			{
				BuildHairAssetStrandGroup(ref hairAsset.strandGroups[i], settings, curveSets[i], memoryLayout);
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

			// prep strand groups in hair asset
			hairAsset.strandGroups = new HairAsset.StrandGroup[1];

			// build strand groups in hair asset
			for (int i = 0; i != hairAsset.strandGroups.Length; i++)
			{
				BuildHairAssetStrandGroup(ref hairAsset.strandGroups[i], settings, memoryLayout);
			}
		}

#if UNITY_FORMATS_ALEMBIC
		public static void BuildHairAssetStrandGroup(ref HairAsset.StrandGroup strandGroup, in HairAsset.SettingsAlembic settings, AlembicCurves curveSet, HairAsset.MemoryLayout memoryLayout)
		{
			//TODO require resampling if not all curves have same number of points

			// get buffers
			var bufferPos = curveSet.Positions;
			var bufferPosCount = curveSet.CurvePointCount;

			//Debug.Log("curveSet: " + curveSet.name);
			//Debug.Log("bufferPos.Length = " + bufferPos.Length);
			//Debug.Log("bufferPointCount.Length = " + bufferPointCount.Length);

			// get curve counts
			int curveCount = bufferPosCount.Length;
			int curvePointCount = (curveCount == 0) ? 0 : (bufferPos.Length / curveCount);
			int curvePointRemainder = (curveCount == 0) ? 0 : (bufferPos.Length % curveCount);

			Debug.Assert(curveCount > 0);
			Debug.Assert(curvePointCount >= 2);
			Debug.Assert(curvePointRemainder == 0);

			if (curveCount == 0 || curvePointCount < 2 || curvePointRemainder > 0)
			{
				curveCount = 0;
				curvePointCount = 0;
				curvePointRemainder = 0;
			}

			// resample curves
			if (settings.resampleCurves)
			{
				var bufferPosResample = new Vector3[curveCount * settings.resampleParticleCount];

				unsafe
				{
					fixed (Vector3* srcBase = bufferPos)
					fixed (Vector3* dstBase = bufferPosResample)
					{
						var srcOffset = 0;
						var dstOffset = 0;

						for (int i = 0; i != curveCount; i++)
						{
							Resample(srcBase + srcOffset, bufferPosCount[i], dstBase + dstOffset, settings.resampleParticleCount);

							srcOffset += bufferPosCount[i];
							dstOffset += settings.resampleParticleCount;
						}
					}
				}

				bufferPos = bufferPosResample;

				curvePointCount = settings.resampleParticleCount;
				curvePointRemainder = 0;
			}

			// set curve counts
			strandGroup.strandCount = curveCount;
			strandGroup.strandParticleCount = curvePointCount;

			// prep curve buffers
			strandGroup.rootScale = new float[curveCount];
			strandGroup.rootPosition = new Vector3[curveCount];
			strandGroup.rootDirection = new Vector3[curveCount];
			strandGroup.particlePosition = new Vector3[curveCount * curvePointCount];

			// build curve buffers
			bufferPos.CopyTo(strandGroup.particlePosition, 0);

			for (int i = 0; i != curveCount; i++)
			{
				ref var p0 = ref strandGroup.particlePosition[i * curvePointCount];
				ref var p1 = ref strandGroup.particlePosition[i * curvePointCount + 1];

				strandGroup.rootPosition[i] = p0;
				strandGroup.rootDirection[i] = Vector3.Normalize(p1 - p0);
			}

			// apply memory layout
			switch (memoryLayout)
			{
				case HairAsset.MemoryLayout.Sequential:
					{
						// do nothing
					}
					break;

				case HairAsset.MemoryLayout.Interleaved:
					unsafe
					{
						using (var src = new NativeArray<Vector3>(strandGroup.particlePosition, Allocator.Temp))
						{
							var srcBase = (Vector3*)src.GetUnsafePtr();
							var srcStride = sizeof(Vector3);
							var dstStride = sizeof(Vector3) * strandGroup.strandCount;

							fixed (Vector3* dstBase = strandGroup.particlePosition)
							{
								for (int i = 0; i != curveCount; i++)
								{
									var srcPtr = srcBase + i * strandGroup.strandParticleCount;
									var dstPtr = dstBase + i;

									UnsafeUtility.MemCpyStride(dstPtr, dstStride, srcPtr, srcStride, srcStride, strandGroup.strandParticleCount);
								}
							}
						}
					}
					break;
			}

			strandGroup.particleMemoryLayout = memoryLayout;

			// calc derivative fields, create mesh assets
			FinalizeStrandGroup(ref strandGroup);
		}
#endif

		public static void BuildHairAssetStrandGroup(ref HairAsset.StrandGroup strandGroup, in HairAsset.SettingsProcedural settings, HairAsset.MemoryLayout memoryLayout)
		{
			// calc curve counts
			int curveCount = settings.strandCount;
			int curvePointCount = settings.strandParticleCount;
			int curvePointRemainder = 0;

			Debug.Assert(curveCount > 0);
			Debug.Assert(curvePointCount >= 2);
			Debug.Assert(curvePointRemainder == 0);

			if (curveCount == 0 || curvePointCount < 2 || curvePointRemainder > 0)
			{
				curveCount = 0;
				curvePointCount = 0;
				curvePointRemainder = 0;
			}

			// set curve counts
			strandGroup.strandCount = curveCount;
			strandGroup.strandParticleCount = curvePointCount;

			// prep curve buffers
			strandGroup.rootScale = new float[curveCount];
			strandGroup.rootPosition = new Vector3[curveCount];
			strandGroup.rootDirection = new Vector3[curveCount];
			strandGroup.particlePosition = new Vector3[curveCount * curvePointCount];

			// build curve buffers
			using (var procRoots = GenerateRoots(settings))
			using (var procStrands = GenerateStrands(settings, procRoots, memoryLayout))
			{
				procRoots.rootPosition.CopyTo(strandGroup.rootPosition);
				procRoots.rootDirection.CopyTo(strandGroup.rootDirection);
				procStrands.particlePosition.CopyTo(strandGroup.particlePosition);
			}

			// apply memory layout
			strandGroup.particleMemoryLayout = memoryLayout;

			// calc derivative fields, create mesh assets
			FinalizeStrandGroup(ref strandGroup);
		}

		static void FinalizeStrandGroup(ref HairAsset.StrandGroup strandGroup)
		{
			// get curve counts
			var curveCount = strandGroup.strandCount;
			var curvePointCount = strandGroup.strandParticleCount;

			// examine strands
			using (var strandLength = new NativeArray<float>(curveCount, Allocator.Temp))
			{
				unsafe
				{
					// calc piece-wise lengths
					var strandLengthPtr = (float*)strandLength.GetUnsafePtr();

					for (int i = 0; i != curveCount; i++)
					{
						var accuLength = 0.0f;

						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandGroup.strandCount, strandGroup.strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

						for (int j = strandParticleBegin + strandParticleStride; j != strandParticleEnd; j += strandParticleStride)
						{
							ref var p0 = ref strandGroup.particlePosition[j - strandParticleStride];
							ref var p1 = ref strandGroup.particlePosition[j];

							accuLength += Vector3.Distance(p0, p1);
						}

						strandLengthPtr[i] = accuLength;
					}

					// find maximum strand length
					strandGroup.maxStrandLength = 0.0f;

					for (int i = 0; i != curveCount; i++)
					{
						strandGroup.maxStrandLength = Mathf.Max(strandGroup.maxStrandLength, strandLengthPtr[i]);
					}

					// calc scale factors within group
					for (int i = 0; i != curveCount; i++)
					{
						strandGroup.rootScale[i] = strandLengthPtr[i] / strandGroup.maxStrandLength;
					}

					// find maximum particle interval
					strandGroup.maxParticleInterval = strandGroup.maxStrandLength / (strandGroup.strandParticleCount - 1);
				}
			}

			// prep lines mesh
			strandGroup.meshAssetLines = new Mesh();
			strandGroup.meshAssetLines.indexFormat = (curveCount * curvePointCount > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16;
#if !ENABLE_VISIBLE_SUBASSETS
			strandGroup.meshAssetLines.hideFlags |= HideFlags.HideInHierarchy;
#endif

			// build lines mesh
			var wireStrandLineCount = curvePointCount - 1;
			var wireStrandPointCount = wireStrandLineCount * 2;

			using (var particleTangent = new NativeArray<Vector3>(curveCount * curvePointCount, Allocator.Temp))
			using (var particleUV = new NativeArray<Vector2>(curveCount * curvePointCount, Allocator.Temp))
			using (var indices = new NativeArray<int>(curveCount * wireStrandPointCount, Allocator.Temp))
			{
				unsafe
				{
					var particleTangentPtr = (Vector3*)particleTangent.GetUnsafePtr();
					var particleUVPtr = (Vector2*)particleUV.GetUnsafePtr();
					var indicesPtr = (int*)indices.GetUnsafePtr();

					// write index data
					switch (strandGroup.particleMemoryLayout)
					{
						//TODO profile this again, first look indicated slower
						//case HairAsset.MemoryLayout.Interleaved:
						//	for (int j = 0; j != wireStrandLineCount; j++)
						//	{
						//		for (int i = 0; i != curveCount; i++)
						//		{
						//			*(indicesPtr++) = j * curveCount + i;
						//			*(indicesPtr++) = j * curveCount + i + curveCount;
						//		}
						//	}
						//	break;

						default:
							for (int i = 0; i != curveCount; i++)
							{
								DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandGroup.strandCount, strandGroup.strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

								for (int j = 0; j != wireStrandLineCount; j++)
								{
									*(indicesPtr++) = strandParticleBegin + strandParticleStride * j;
									*(indicesPtr++) = strandParticleBegin + strandParticleStride * (j + 1);
								}
							}
							break;
					}

					// write tangents
					for (int i = 0; i != curveCount; i++)
					{
						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandGroup.strandCount, strandGroup.strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

						for (int j = strandParticleBegin; j != strandParticleEnd - strandParticleStride; j += strandParticleStride)
						{
							ref var p0 = ref strandGroup.particlePosition[j];
							ref var p1 = ref strandGroup.particlePosition[j + strandParticleStride];

							particleTangentPtr[j] = Vector3.Normalize(p1 - p0);
						}

						particleTangentPtr[strandParticleEnd - strandParticleStride] = particleTangentPtr[strandParticleEnd - 2 * strandParticleStride];
					}

					// write uvs
					for (int i = 0; i != curveCount; i++)
					{
						DeclareStrandIterator(strandGroup.particleMemoryLayout, i, strandGroup.strandCount, strandGroup.strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

						for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
						{
							particleUVPtr[j] = new Vector2(j, i);// particle index + strand index
						}
					}
				}

				strandGroup.meshAssetLines.SetVertices(strandGroup.particlePosition);
				strandGroup.meshAssetLines.SetNormals(particleTangent);
				strandGroup.meshAssetLines.SetUVs(0, particleUV);
				strandGroup.meshAssetLines.SetIndices(indices, MeshTopology.Lines, 0);
			}

			// prep roots mesh
			strandGroup.meshAssetRoots = new Mesh();
#if !ENABLE_VISIBLE_SUBASSETS
			strandGroup.meshAssetRoots.hideFlags |= HideFlags.HideInHierarchy;
#endif

			// build roots mesh
			using (var indices = new NativeArray<int>(curveCount, Allocator.Temp))
			{
				unsafe
				{
					var indicesPtr = (int*)indices.GetUnsafePtr();

					// write index data
					for (int i = 0; i != curveCount; i++)
					{
						*(indicesPtr++) = i;
					}
				}

				strandGroup.meshAssetRoots.SetVertices(strandGroup.rootPosition);
				strandGroup.meshAssetRoots.SetNormals(strandGroup.rootDirection);
				strandGroup.meshAssetRoots.SetIndices(indices, MeshTopology.Points, 0);
			}
		}

		public struct GeneratedRoots : IDisposable
		{
			public struct Params4
			{
				public float normalizedStrandLength;
				public float normalizedStrandDiameter;
				public float normalizedCurlRadius;
				public float normalizedCurlSlope;
			}

			public int strandCount;
			public NativeArray<Vector3> rootPosition;
			public NativeArray<Vector3> rootDirection;
			public NativeArray<Vector2> rootTexCoord;
			public NativeArray<Params4> rootParameters;// R,G,B,A == Strand length, Strand diameter, Curl radius, Curl slope

			public GeneratedRoots(int strandCount)
			{
				this.strandCount = strandCount;
				rootPosition = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
				rootDirection = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
				rootTexCoord = new NativeArray<Vector2>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
				rootParameters = new NativeArray<Params4>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			}

			public void Dispose()
			{
				rootPosition.Dispose();
				rootDirection.Dispose();
				rootTexCoord.Dispose();
				rootParameters.Dispose();
			}
		}

		public struct GeneratedStrands : IDisposable
		{
			public int strandCount;
			public int strandParticleCount;
			public NativeArray<Vector3> particlePosition;

			public GeneratedStrands(int strandCount, int strandParticleCount)
			{
				this.strandCount = strandCount;
				this.strandParticleCount = strandParticleCount;
				particlePosition = new NativeArray<Vector3>(strandCount * strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			}

			public void Dispose()
			{
				particlePosition.Dispose();
			}
		}

		public static GeneratedRoots GenerateRoots(in HairAsset.SettingsProcedural settings)
		{
			var procRoots = new GeneratedRoots(settings.strandCount);

			unsafe
			{
				var rootPos = (Vector3*)procRoots.rootPosition.GetUnsafePtr();
				var rootDir = (Vector3*)procRoots.rootDirection.GetUnsafePtr();
				var rootUV0 = (Vector2*)procRoots.rootTexCoord.GetUnsafePtr();
				var rootVar = (Vector4*)procRoots.rootParameters.GetUnsafePtr();

				if (settings.placement == HairAsset.SettingsProcedural.PlacementType.Primitive)
				{
					switch (settings.placementPrimitive)
					{
						case HairAsset.SettingsProcedural.PrimitiveType.Curtain:
							{
								var strandSpan = 1.0f;
								var strandInterval = strandSpan / (settings.strandCount - 1);

								var startPos = (-0.5f * strandSpan) * Vector3.right;
								var startDir = Vector3.down;

								for (int i = 0; i != settings.strandCount; i++)
								{
									rootPos[i] = startPos + i * strandInterval * Vector3.right;
									rootDir[i] = startDir;
									rootUV0[i] = Vector2.zero;
								}
							}
							break;

						case HairAsset.SettingsProcedural.PrimitiveType.StratifiedCurtain:
							{
								var strandSpan = 1.0f;
								var strandInterval = strandSpan / settings.strandCount;
								var strandIntervalNoise = 0.5f;

								var xorshift = new Unity.Mathematics.Random(257);

								for (int i = 0; i != settings.strandCount; i++)
								{
									var localRnd = strandIntervalNoise * xorshift.NextFloat2(-1.0f, 1.0f);
									var localPos = -0.5f * strandSpan + (i + 0.5f + 0.5f * localRnd.x) * strandInterval;

									rootPos[i] = new Vector3(localPos, 0.0f, 0.5f * localRnd.y * strandInterval);
									rootDir[i] = Vector3.down;
									rootUV0[i] = Vector2.zero;
								}
							}
							break;

						case HairAsset.SettingsProcedural.PrimitiveType.Brush:
							{
								var localExt = 0.5f * Vector3.one;
								var localMin = new Vector2(-localExt.x, -localExt.z);
								var localMax = new Vector2(localExt.x, localExt.z);

								var xorshift = new Unity.Mathematics.Random(257);

								for (int i = 0; i != settings.strandCount; i++)
								{
									var localPos = xorshift.NextFloat2(localMin, localMax);

									rootPos[i] = new Vector3(localPos.x, 0.0f, localPos.y);
									rootDir[i] = Vector3.down;
									rootUV0[i] = Vector2.zero;
								}
							}
							break;

						case HairAsset.SettingsProcedural.PrimitiveType.Cap:
							{
								var localExt = 0.5f;
								var xorshift = new Unity.Mathematics.Random(257);

								for (int i = 0; i != settings.strandCount; i++)
								{
									var localDir = xorshift.NextFloat3Direction();
									if (localDir.y < 0.0f)
										localDir.y = -localDir.y;

									rootPos[i] = (Vector3)localDir * localExt;
									rootDir[i] = (Vector3)localDir;
									rootUV0[i] = Vector2.zero;
								}
							}
							break;
					}

					for (int i = 0; i != settings.strandCount; i++)
					{
						rootVar[i] = Vector4.one;
					}
				}
				else if (settings.placement == HairAsset.SettingsProcedural.PlacementType.Mesh)
				{
					using (var meshData = Mesh.AcquireReadOnlyMeshData(settings.placementMesh))
					using (var meshSampler = new TriMeshSampler(meshData[0], 0, Allocator.Temp))
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
								var sampleDensity = density.GetPixelBilinear(sample.uv0.x, sample.uv0.y);
								var sampleIteration = 0;// safety

								while (sampleDensity.r < densityThreshold.NextFloat() && sampleIteration++ < 200)
								{
									sample = meshSampler.Next();
									sampleDensity = density.GetPixelBilinear(sample.uv0.x, sample.uv0.y, 0);
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
								rootDir[i] = Vector3.Normalize((Vector4)settings.paintedDirection.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, 0));
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
							for (int i = 0; i != settings.strandCount; i++)
							{
								rootVar[i] = (Vector4)settings.paintedParameters.GetPixelBilinear(rootUV0[i].x, rootUV0[i].y, 0);
							}
						}
						else
						{
							for (int i = 0; i != settings.strandCount; i++)
							{
								rootVar[i] = Vector4.one;
							}
						}
					}
				}
			}

			return procRoots;
		}

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

			public TriMeshBuffers(Mesh.MeshData meshData, Attribute meshAttributes = Attribute.All, Allocator allocator = Allocator.Temp)
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
			}

			public bool HasAttribute(Attribute attribute)
			{
				return vertexAttributes.HasFlag(attribute);
			}

			public void Dispose()
			{
				if (vertexPosition.IsCreated)
					vertexPosition.Dispose();

				if (vertexNormal.IsCreated)
					vertexNormal.Dispose();

				if (vertexUV0.IsCreated)
					vertexUV0.Dispose();
			}
		}

		public struct TriMeshSampler : IDisposable
		{
			public TriMeshBuffers triangleMesh;

			public int triangleCount;
			public float triangleAreaSum;
			public NativeArray<float> triangleArea;
			public NativeArray<float> triangleAreaAccu;

			public Unity.Mathematics.Random randSeed;
			public Unity.Mathematics.Random rand;

			public TriMeshSampler(Mesh.MeshData meshData, uint seedIndex, Allocator allocator = Allocator.Temp)
			{
				triangleMesh = new TriMeshBuffers(meshData, TriMeshBuffers.Attribute.All, allocator);

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

					if (searchIndex > 0)
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

				var	p0 = triangleMesh.vertexPosition[j0];
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

		public static GeneratedStrands GenerateStrands(in HairAsset.SettingsProcedural settings, in GeneratedRoots procRoots, HairAsset.MemoryLayout memoryLayout)
		{
			var procStrands = new GeneratedStrands(settings.strandCount, settings.strandParticleCount);

			unsafe
			{
				var pos = (Vector3*)procStrands.particlePosition.GetUnsafePtr();

				var rootPos = (Vector3*)procRoots.rootPosition.GetUnsafePtr();
				var rootDir = (Vector3*)procRoots.rootDirection.GetUnsafePtr();
				var rootVar = (GeneratedRoots.Params4*)procRoots.rootParameters.GetUnsafePtr();

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

							pos[j] = curPos +
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
			}

			return procStrands;
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

		public static unsafe void Resample(Vector3* srcPos, int srcCount, Vector3* dstPos, int dstCount)
		{
			var length = 0.0f;

			for (int i = 1; i != srcCount; i++)
			{
				length += Vector3.Distance(srcPos[i], srcPos[i - 1]);
			}

			dstPos[0] = srcPos[0];

			var dstPosPrev = dstPos[0];
			var dstSpacing = length / (dstCount - 1);
			var dstSpacingSq = dstSpacing * dstSpacing;

			var srcIndex = 1;
			var dstIndex = 1;

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

			// just extrapolate the tail, if there is a tail
			if (dstIndex < dstCount)
			{
				var n = Vector3.Normalize(srcPos[srcCount - 1] - dstPosPrev);

				while (dstIndex < dstCount)
				{
					dstPos[dstIndex] = dstPosPrev + n * dstSpacing;
					dstPosPrev = dstPos[dstIndex++];
				}
			}
		}
	}
}
