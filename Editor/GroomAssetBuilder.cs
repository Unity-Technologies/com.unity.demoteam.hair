using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Formats.Alembic.Importer;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

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
					BuildGroomAsset(groom, groom.settingsAlembic);
					break;

				case GroomAsset.Type.Procedural:
					BuildGroomAsset(groom, groom.settingsProcedural);
					break;
			}

			// hash the data
			if (groom.strandGroups != null)
			{
				var hash = new Hash128();
				for (int i = 0; i != groom.strandGroups.Length; i++)
				{
					hash.Append(groom.strandGroups[i].meshAssetLines.GetInstanceID());
					hash.Append(groom.strandGroups[i].initialPositions);
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

		public static void BuildGroomAsset(GroomAsset groom, in GroomAsset.SettingsAlembic settings)
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
			for (int i = 0; i != curveSets.Length; i++)
			{
				BuildGroomAssetStrandGroup(ref groom.strandGroups[i], settings, curveSets[i]);
			}
		}

		public static void BuildGroomAssetStrandGroup(ref GroomAsset.StrandGroup strandGroup, in GroomAsset.SettingsAlembic settings, AlembicCurves curveSet)
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
			strandGroup.initialPositions = new Vector3[curveCount * curvePointCount];
			strandGroup.initialRootPositions = new Vector3[curveCount];
			strandGroup.initialRootDirections = new Vector3[curveCount];

			// build curve buffers
			bufferPos.CopyTo(strandGroup.initialPositions);

			for (int i = 0; i != curveCount; i++)
			{
				ref var p0 = ref strandGroup.initialPositions[i * curvePointCount];
				ref var p1 = ref strandGroup.initialPositions[i * curvePointCount + 1];

				strandGroup.initialRootPositions[i] = p0;
				strandGroup.initialRootDirections[i] = Vector3.Normalize(p1 - p0);
			}

			// build derivative mesh assets
			BuildGroomAssetStrandGroupMeshAssets(ref strandGroup);
		}

		public static void BuildGroomAsset(GroomAsset groom, in GroomAsset.SettingsProcedural settings)
		{
			// prep strand groups in groom asset
			groom.strandGroups = new GroomAsset.StrandGroup[1];

			// build strand groups in groom asset
			BuildGroomAssetStrandGroup(ref groom.strandGroups[0], settings);
		}

		public static void BuildGroomAssetStrandGroup(ref GroomAsset.StrandGroup strandGroup, in GroomAsset.SettingsProcedural settings)
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
			strandGroup.initialPositions = new Vector3[curveCount * curvePointCount];
			strandGroup.initialRootPositions = new Vector3[curveCount];
			strandGroup.initialRootDirections = new Vector3[curveCount];

			// build curve buffers
			using (var tempRoots = GenerateRoots(settings))
			using (var tempStrands = GenerateStrands(settings, tempRoots))
			{
				tempStrands.positions.CopyTo(strandGroup.initialPositions);
				tempRoots.rootPositions.CopyTo(strandGroup.initialRootPositions);
				tempRoots.rootDirections.CopyTo(strandGroup.initialRootDirections);
			}

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

					for (int j = 1; j != curvePointCount; j++)
					{
						ref var p0 = ref strandGroup.initialPositions[i * curvePointCount + j - 1];
						ref var p1 = ref strandGroup.initialPositions[i * curvePointCount + j];

						strandLength += Vector3.Distance(p0, p1);
					}

					strandGroup.strandLengthAvg += strandLength;
					strandGroup.strandLengthMin = Mathf.Min(strandLength, strandGroup.strandLengthMin);
					strandGroup.strandLengthMax = Mathf.Max(strandLength, strandGroup.strandLengthMax);
				}

				strandGroup.strandLengthAvg /= curveCount;
			}

			// prep debug mesh
			strandGroup.meshAssetLines = new Mesh();
			strandGroup.meshAssetLines.indexFormat = (curveCount * curvePointCount > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16;

			// build debug mesh
			var wireStrandLineCount = curvePointCount - 1;
			var wireStrandPointCount = wireStrandLineCount * 2;

			using (var wireStrandTangents = new NativeArray<Vector3>(curveCount * curvePointCount, Allocator.Temp))
			using (var wireStrandIndices = new NativeArray<int>(curveCount * wireStrandPointCount, Allocator.Temp))
			{
				unsafe
				{
					var wireTangentPtr = (Vector3*)wireStrandTangents.GetUnsafePtr();
					var wireIndexPtr = (int*)wireStrandIndices.GetUnsafePtr();

					for (int i = 0; i != curveCount; i++)
					{
						for (int j = 0; j != wireStrandLineCount; j++)
						{
							*(wireIndexPtr++) = i * curvePointCount + j;
							*(wireIndexPtr++) = i * curvePointCount + j + 1;
						}
					}

					for (int i = 0; i != curveCount; i++)
					{
						for (int j = 0; j != curvePointCount - 1; j++)
						{
							ref var p0 = ref strandGroup.initialPositions[i * curvePointCount + j];
							ref var p1 = ref strandGroup.initialPositions[i * curvePointCount + j + 1];

							*(wireTangentPtr++) = Vector3.Normalize(p1 - p0);
						}

						*(wireTangentPtr++) = *(wireTangentPtr - 1);
					}
				}

				strandGroup.meshAssetLines.SetVertices(strandGroup.initialPositions);
				strandGroup.meshAssetLines.SetNormals(wireStrandTangents);
				strandGroup.meshAssetLines.SetIndices(wireStrandIndices, MeshTopology.Lines, 0);
			}

			// prep roots mesh
			strandGroup.meshAssetRoots = new Mesh();

			// build roots mesh
			strandGroup.meshAssetRoots.SetVertices(strandGroup.initialRootPositions);
			strandGroup.meshAssetRoots.SetNormals(strandGroup.initialRootDirections);
		}


		//-----------------------------------

		public struct IntermediateRoots : IDisposable
		{
			public NativeArray<Vector3> rootPositions;
			public NativeArray<Vector3> rootDirections;

			public IntermediateRoots(int strandCount)
			{
				rootPositions = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
				rootDirections = new NativeArray<Vector3>(strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			}

			public void Dispose()
			{
				rootPositions.Dispose();
				rootDirections.Dispose();
			}
		}

		public struct IntermediateStrands : IDisposable
		{
			public int strandCount;
			public int strandParticleCount;
			public NativeArray<Vector3> positions;

			public IntermediateStrands(int strandCount, int strandParticleCount)
			{
				this.strandCount = strandCount;
				this.strandParticleCount = strandParticleCount;
				positions = new NativeArray<Vector3>(strandCount * strandParticleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			}

			public void Dispose()
			{
				positions.Dispose();
			}
		}

		public static IntermediateRoots GenerateRoots(in GroomAsset.SettingsProcedural settings)
		{
			var tempRoots = new IntermediateRoots(settings.strandCount);

			unsafe
			{
				var rootPos = (Vector3*)tempRoots.rootPositions.GetUnsafePtr();
				var rootDir = (Vector3*)tempRoots.rootDirections.GetUnsafePtr();

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

			return tempRoots;
		}

		public static IntermediateStrands GenerateStrands(in GroomAsset.SettingsProcedural settings, in IntermediateRoots tempRoots)
		{
			var tempStrands = new IntermediateStrands(settings.strandCount, settings.strandParticleCount);

			unsafe
			{
				var pos = (Vector3*)tempStrands.positions.GetUnsafePtr();
				var rootPos = (Vector3*)tempRoots.rootPositions.GetUnsafePtr();
				var rootDir = (Vector3*)tempRoots.rootDirections.GetUnsafePtr();

				float strandParticleInterval = settings.strandLength / (settings.strandParticleCount - 1);

				for (int i = 0; i != settings.strandCount; i++)
				{
					var curPos = rootPos[i];
					var curDir = rootDir[i];

#if LAYOUT_INTERLEAVED
					int strandParticleBegin = i;
					int strandParticleStride = settings.strandCount;
#else
					int strandParticleBegin = i * settings.strandParticleCount;
					int strandParticleStride = 1;
#endif
					int strandParticleEnd = strandParticleBegin + strandParticleStride * settings.strandParticleCount;

					for (int j = strandParticleBegin; j != strandParticleEnd; j += strandParticleStride)
					{
						pos[j] = curPos;
						curPos += strandParticleInterval * curDir;
					}
				}
			}

			return tempStrands;
		}
	}
}
