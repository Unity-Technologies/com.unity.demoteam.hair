using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Formats.Alembic.Importer;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public static class GroomAssetBuilder
	{
		public static void ClearGroomAsset(GroomAsset groom)
		{
			// do nothing if already clean
			if (groom.strandGroups == null)
				return;

			// unlink and destroy any sub-assets
			foreach (var strandGroup in groom.strandGroups)
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
			groom.strandGroups = null;
		}

		public static void BuildGroomAsset(GroomAsset groom)
		{
			// clean up
			ClearGroomAsset(groom);

			// build the data
			switch (groom.settingsBasic.type)
			{
				case GroomAsset.Type.Alembic:
					BuildGroomAsset(groom, groom.settingsAlembic, groom.settingsBasic.memoryLayout);
					break;

				case GroomAsset.Type.Procedural:
					BuildGroomAsset(groom, groom.settingsProcedural, groom.settingsBasic.memoryLayout);
					break;
			}

			// hash the data
			if (groom.strandGroups != null)
			{
				var hash = new Hash128();
				for (int i = 0; i != groom.strandGroups.Length; i++)
				{
					hash.Append(groom.strandGroups[i].meshAssetLines.GetInstanceID());
					hash.Append(groom.strandGroups[i].initialPosition);
				}

				groom.checksum = hash.ToString();
			}
			else
			{
				groom.checksum = string.Empty;
			}

			// link sub-assets
			if (groom.strandGroups != null)
			{
				for (int i = 0; i != groom.strandGroups.Length; i++)
				{
					AssetDatabase.AddObjectToAsset(groom.strandGroups[i].meshAssetLines, groom);
					AssetDatabase.AddObjectToAsset(groom.strandGroups[i].meshAssetRoots, groom);

					groom.strandGroups[i].meshAssetLines.name = "Lines:" + i;
					groom.strandGroups[i].meshAssetRoots.name = "Roots:" + i;
				}
			}

			// save the asset
			EditorUtility.SetDirty(groom);
			AssetDatabase.SaveAssets();
		}

		public static void BuildGroomAsset(GroomAsset groom, in GroomAsset.SettingsAlembic settings, GroomAsset.MemoryLayout memoryLayout)
		{
			// check stream present
			var alembic = settings.sourceAsset;
			if (alembic == null)
				return;

			// fetch all curve sets 
			var curveSets = alembic.gameObject.GetComponentsInChildren<AlembicCurves>(true);
			if (curveSets.Length == 0)
				return;

			// force-load the data
			alembic.LoadFromFile(alembic.PathToAbc);

			// prep strand groups in groom asset
			groom.strandGroups = new GroomAsset.StrandGroup[curveSets.Length];

			// build strand groups in groom asset
			for (int i = 0; i != groom.strandGroups.Length; i++)
			{
				BuildGroomAssetStrandGroup(ref groom.strandGroups[i], settings, curveSets[i], memoryLayout);
			}
		}

		public static void BuildGroomAsset(GroomAsset groom, in GroomAsset.SettingsProcedural settings, GroomAsset.MemoryLayout memoryLayout)
		{
			// prep strand groups in groom asset
			groom.strandGroups = new GroomAsset.StrandGroup[1];

			// build strand groups in groom asset
			for (int i = 0; i != groom.strandGroups.Length; i++)
			{
				BuildGroomAssetStrandGroup(ref groom.strandGroups[i], settings, memoryLayout);
			}
		}

		public static void BuildGroomAssetStrandGroup(ref GroomAsset.StrandGroup strandGroup, in GroomAsset.SettingsAlembic settings, AlembicCurves curveSet, GroomAsset.MemoryLayout memoryLayout)
		{
			// get buffers
			var bufferPos = curveSet.Positions;
			var bufferPosOffset = curveSet.PositionsOffsetBuffer;

			Debug.Log("curveSet: " + curveSet.name);
			Debug.Log("bufferPos.Count = " + bufferPos.Count);
			Debug.Log("bufferPosOffset.Count = " + bufferPosOffset.Count);

			// get curve counts
			int curveCount = bufferPosOffset.Count;
			int curvePointCount = (curveCount == 0) ? 0 : (bufferPos.Count / curveCount);
			int curvePointRemainder = (curveCount == 0) ? 0 : (bufferPos.Count % curveCount);

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
			strandGroup.initialLength = new float[curveCount];
			strandGroup.initialPosition = new Vector3[curveCount * curvePointCount];
			strandGroup.initialRootPosition = new Vector3[curveCount];
			strandGroup.initialRootDirection = new Vector3[curveCount];

			// build curve buffers
			bufferPos.CopyTo(strandGroup.initialPosition);

			for (int i = 0; i != curveCount; i++)
			{
				ref var p0 = ref strandGroup.initialPosition[i * curvePointCount];
				ref var p1 = ref strandGroup.initialPosition[i * curvePointCount + 1];

				strandGroup.initialRootPosition[i] = p0;
				strandGroup.initialRootDirection[i] = Vector3.Normalize(p1 - p0);
			}

			// apply memory layout
			switch (memoryLayout)
			{
				case GroomAsset.MemoryLayout.Sequential:
					{
						// do nothing
					}
					break;

				case GroomAsset.MemoryLayout.Interleaved:
					unsafe
					{
						using (var src = new NativeArray<Vector3>(strandGroup.initialPosition, Allocator.Temp))
						{
							var srcBase = (Vector3*)src.GetUnsafePtr();
							var srcStride = sizeof(Vector3);
							var dstStride = sizeof(Vector3) * strandGroup.strandCount;

							fixed (Vector3* dstBase = strandGroup.initialPosition)
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

			strandGroup.memoryLayout = memoryLayout;

			// build derivative mesh assets
			BuildGroomAssetStrandGroupMeshAssets(ref strandGroup);
		}

		public static void BuildGroomAssetStrandGroup(ref GroomAsset.StrandGroup strandGroup, in GroomAsset.SettingsProcedural settings, GroomAsset.MemoryLayout memoryLayout)
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
			strandGroup.initialLength = new float[curveCount];
			strandGroup.initialPosition = new Vector3[curveCount * curvePointCount];
			strandGroup.initialRootPosition = new Vector3[curveCount];
			strandGroup.initialRootDirection = new Vector3[curveCount];

			// build curve buffers
			using (var tmpRoots = GenerateRoots(settings))
			using (var tmpStrands = GenerateStrands(settings, tmpRoots, memoryLayout))
			{
				tmpStrands.position.CopyTo(strandGroup.initialPosition);
				tmpRoots.rootPosition.CopyTo(strandGroup.initialRootPosition);
				tmpRoots.rootDirection.CopyTo(strandGroup.initialRootDirection);
			}

			// apply memory layout
			strandGroup.memoryLayout = memoryLayout;

			// build derivative mesh assets
			BuildGroomAssetStrandGroupMeshAssets(ref strandGroup);
		}

		public static void BuildGroomAssetStrandGroupMeshAssets(ref GroomAsset.StrandGroup strandGroup)
		{
			// get curve counts
			var curveCount = strandGroup.strandCount;
			var curvePointCount = strandGroup.strandParticleCount;

			// find min/max/avg strand length
			strandGroup.strandLengthAvg = 0.0f;
			strandGroup.strandLengthMin = float.PositiveInfinity;
			strandGroup.strandLengthMax = float.NegativeInfinity;
			{
				for (int i = 0; i != curveCount; i++)
				{
					float strandLength = 0.0f;

					DeclareStrandIterator(strandGroup.memoryLayout, i, strandGroup.strandCount, strandGroup.strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

					for (int j = strandParticleBegin + strandParticleStride; j != strandParticleEnd; j += strandParticleStride)
					{
						ref var p0 = ref strandGroup.initialPosition[j - strandParticleStride];
						ref var p1 = ref strandGroup.initialPosition[j];

						strandLength += Vector3.Distance(p0, p1);
					}

					strandGroup.strandLengthAvg += strandLength;
					strandGroup.strandLengthMin = Mathf.Min(strandLength, strandGroup.strandLengthMin);
					strandGroup.strandLengthMax = Mathf.Max(strandLength, strandGroup.strandLengthMax);

					strandGroup.initialLength[i] = strandLength;
				}

				strandGroup.strandLengthAvg /= curveCount;
			}

			// prep lines mesh
			strandGroup.meshAssetLines = new Mesh();
			strandGroup.meshAssetLines.indexFormat = (curveCount * curvePointCount > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16;

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

					// write index buffer
					for (int i = 0; i != curveCount; i++)
					{
						DeclareStrandIterator(strandGroup.memoryLayout, i, strandGroup.strandCount, strandGroup.strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

						for (int j = 0; j != wireStrandLineCount; j++)
						{
							*(indicesPtr++) = strandParticleBegin + strandParticleStride * j;
							*(indicesPtr++) = strandParticleBegin + strandParticleStride * (j + 1);
						}
					}

					// write tangents
					for (int i = 0; i != curveCount; i++)
					{
						DeclareStrandIterator(strandGroup.memoryLayout, i, strandGroup.strandCount, strandGroup.strandParticleCount, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

						for (int j = strandParticleBegin; j != strandParticleEnd - strandParticleStride; j += strandParticleStride)
						{
							ref var p0 = ref strandGroup.initialPosition[j];
							ref var p1 = ref strandGroup.initialPosition[j + strandParticleStride];

							particleTangentPtr[j] = Vector3.Normalize(p1 - p0);
						}

						particleTangentPtr[strandParticleEnd - strandParticleStride] = particleTangentPtr[strandParticleEnd - 2 * strandParticleStride];
					}

					// write uvs
					for (int i = 0, k = 0; i != curveCount; i++)
					{
						for (int j = 0; j != curvePointCount; j++)
						{
							*(particleUVPtr++) = new Vector2(k++, i);// strand particle index + strand index
						}
					}
				}

				strandGroup.meshAssetLines.SetVertices(strandGroup.initialPosition);
				strandGroup.meshAssetLines.SetNormals(particleTangent);
				strandGroup.meshAssetLines.SetUVs(0, particleUV);
				strandGroup.meshAssetLines.SetIndices(indices, MeshTopology.Lines, 0);
			}

			// prep roots mesh
			strandGroup.meshAssetRoots = new Mesh();

			// build roots mesh
			strandGroup.meshAssetRoots.SetVertices(strandGroup.initialRootPosition);
			strandGroup.meshAssetRoots.SetNormals(strandGroup.initialRootDirection);
		}

		public struct IntermediateRoots : IDisposable
		{
			public NativeArray<Vector3> rootPosition;
			public NativeArray<Vector3> rootDirection;

			public IntermediateRoots(int strandCount)
			{
				rootPosition = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
				rootDirection = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			}

			public void Dispose()
			{
				rootPosition.Dispose();
				rootDirection.Dispose();
			}
		}

		public struct IntermediateStrands : IDisposable
		{
			public int strandCount;
			public int strandParticleCount;
			public NativeArray<Vector3> position;

			public IntermediateStrands(int strandCount, int strandParticleCount)
			{
				this.strandCount = strandCount;
				this.strandParticleCount = strandParticleCount;
				position = new NativeArray<Vector3>(strandCount * strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			}

			public void Dispose()
			{
				position.Dispose();
			}
		}

		public static IntermediateRoots GenerateRoots(in GroomAsset.SettingsProcedural settings)
		{
			var tmpRoots = new IntermediateRoots(settings.strandCount);

			unsafe
			{
				var rootPos = (Vector3*)tmpRoots.rootPosition.GetUnsafePtr();
				var rootDir = (Vector3*)tmpRoots.rootDirection.GetUnsafePtr();

				switch (settings.style)
				{
					case GroomAsset.SettingsProcedural.Style.Curtain:
						{
							var strandSpan = 1.0f;
							var strandInterval = strandSpan / (settings.strandCount - 1);

							var startPos = (-0.5f * strandSpan) * Vector3.right;
							var startDir = Vector3.down;

							for (int i = 0; i != settings.strandCount; i++)
							{
								rootPos[i] = startPos + i * strandInterval * Vector3.right;
								rootDir[i] = startDir;
							}
						}
						break;

					case GroomAsset.SettingsProcedural.Style.StratifiedCurtain:
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
							}
						}
						break;

					case GroomAsset.SettingsProcedural.Style.Brush:
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
							}
						}
						break;

					case GroomAsset.SettingsProcedural.Style.Cap:
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
							}
						}
						break;
				}
			}

			return tmpRoots;
		}

		public static IntermediateStrands GenerateStrands(in GroomAsset.SettingsProcedural settings, in IntermediateRoots tmpRoots, GroomAsset.MemoryLayout memoryLayout)
		{
			var tmpStrands = new IntermediateStrands(settings.strandCount, settings.strandParticleCount);

			unsafe
			{
				var pos = (Vector3*)tmpStrands.position.GetUnsafePtr();
				var rootPos = (Vector3*)tmpRoots.rootPosition.GetUnsafePtr();
				var rootDir = (Vector3*)tmpRoots.rootDirection.GetUnsafePtr();

				float strandParticleInterval = settings.strandLength / (settings.strandParticleCount - 1);

				for (int i = 0; i != settings.strandCount; i++)
				{
					var curPos = rootPos[i];
					var curDir = rootDir[i];

					DeclareStrandIterator(memoryLayout, i, settings.strandCount, settings.strandParticleCount, out var strandParticleBegin, out var strandParticleStride, out var strandParticleEnd);

					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						pos[j] = curPos;
						curPos += strandParticleInterval * curDir;
					}
				}
			}

			return tmpStrands;
		}

		public static void DeclareStrandIterator(GroomAsset.MemoryLayout memoryLayout, int strandIndex, int strandCount, int strandParticleCount,
			out int strandParticleBegin,
			out int strandParticleStride,
			out int strandParticleEnd)
		{
			switch (memoryLayout)
			{
				default:
				case GroomAsset.MemoryLayout.Sequential:
					strandParticleBegin = strandIndex * strandParticleCount;
					strandParticleStride = 1;
					break;

				case GroomAsset.MemoryLayout.Interleaved:
					strandParticleBegin = strandIndex;
					strandParticleStride = strandCount;
					break;
			}

			strandParticleEnd = strandParticleBegin + strandParticleStride * strandParticleCount;
		}
	}
}
