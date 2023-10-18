using System;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_2021_2_OR_NEWER
using UnityEngine.Search;
#endif
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	[CreateAssetMenu(menuName = "Hair/Hair Asset", order = 350), PreferBinarySerialization]
	public partial class HairAsset : ScriptableObject, ISerializationCallbackReceiver
	{
		[Serializable]
		public struct StrandGroup
		{
			[Flags]
			public enum ParticleFeatures
			{
				Position = 1 << 0,
				TexCoord = 1 << 1,
				Diameter = 1 << 2,
			}

			public int strandCount;
			public int strandParticleCount;

			public float sumStrandLength;
			public float maxStrandLength;
			public float maxStrandDiameter;
			public float avgStrandDiameter;

			[HideInInspector] public Bounds bounds;

			[HideInInspector] public Vector2[] rootUV;
			[HideInInspector] public Vector4[] rootScale;

			[HideInInspector] public MemoryLayout particleMemoryLayout;
			[HideInInspector] public ParticleFeatures particleFeatures;
			[HideInInspector] public Vector3[] particlePosition;
			[HideInInspector] public Vector2[] particleTexCoord;
			[HideInInspector] public float[] particleDiameter;

			public int lodCount;

			[NonReorderable] public int[] lodGuideCount;	// n: lod index -> num. guides
			[HideInInspector] public int[] lodGuideIndex;	// i: lod index * strand count + strand index -> guide index
			[HideInInspector] public float[] lodGuideCarry;	// f: lod index * strand count + strand index -> guide carry
			[HideInInspector] public float[] lodGuideReach;	// f: lod index * strand count + strand index -> guide reach (approximate cluster extent)
			[HideInInspector] public float[] lodThreshold;	// f: lod index -> relative guide count [0..1]

			[HideInInspector] public Mesh meshAssetRoots;
			[HideInInspector] public Mesh meshAssetLines;
			[HideInInspector] public Mesh meshAssetStrips;
			[HideInInspector] public Mesh meshAssetTubes;

			public int version;
			public const int VERSION = 2;
		}

		public SettingsBasic settingsBasic = SettingsBasic.defaults;
		public SettingsCustom settingsCustom = SettingsCustom.defaults;
		public SettingsAlembic settingsAlembic = SettingsAlembic.defaults;
		public SettingsProcedural settingsProcedural = SettingsProcedural.defaults;
		public SettingsLODClusters settingsLODClusters = SettingsLODClusters.defaults;

		public StrandGroup[] strandGroups;
		public bool strandGroupsAutoBuild;

		public string checksum;

		void ISerializationCallbackReceiver.OnBeforeSerialize() { }
		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			if (strandGroups == null)
				return;

			bool PerformVersionBump(ref StrandGroup strandGroup)
			{
				switch (strandGroup.version)
				{
					// 0->1: add LOD data
					case 0:
						{
							strandGroup.lodCount = 1;
							strandGroup.lodGuideCount = new int[1] { strandGroup.strandCount };
							strandGroup.lodGuideIndex = new int[strandGroup.strandCount];
							strandGroup.lodThreshold = new float[1] { 1.0f };

							for (int i = 0; i != strandGroup.strandCount; i++)
							{
								strandGroup.lodGuideIndex[i] = i;
							}
						}
						strandGroup.version = 1;
						return true;

					// 1->2: add LOD guide carry
					case 1:
						{
							strandGroup.lodGuideCarry = new float[strandGroup.lodGuideIndex.Length];

							for (int i = 0; i != strandGroup.lodGuideCarry.Length; i++)
							{
								strandGroup.lodGuideCarry[i] = 1.0f;
							}
						}
						strandGroup.version = 2;
						return true;

					// 2->3: add LOD guide reach
					case 77:
						/*
						unsafe
						{
							strandGroup.lodGuideReach = new float[strandGroup.lodGuideCarry.Length];

							using (var sumCenter = new UnsafeList<Vector3>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
							using (var sumWeight = new UnsafeList<float>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
							{
								var sumCenterPtr = sumCenter.Ptr;
								var sumWeightPtr = sumWeight.Ptr;

								for (int lodIndex = 0; lodIndex != strandGroup.lodCount; lodIndex++)
								{
									var lodOffset = lodIndex * strandGroup.strandCount;

									// calc centers
									for (int i = 0; i != strandGroup.strandCount; i++)
									{
										sumCenterPtr[i] = Vector3.zero;
										sumWeightPtr[i] = 0.0f;
									}

									for (int i = 0; i != strandGroup.strandCount; i++)
									{
										HairAssetUtility.DeclareStrandIterator(strandGroup, i, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

										var j = strandGroup.lodGuideIndex[lodOffset + i];
										{
											sumCenterPtr[j] += strandGroup.rootScale[i] * strandGroup.particlePosition[strandParticleBegin];
											sumWeightPtr[j] += strandGroup.rootScale[i];
										}
									}

									for (int i = 0; i != strandGroup.strandCount; i++)
									{
										sumCenterPtr[i] /= sumWeightPtr[i];
									}

									// calc radii
									for (int i = 0; i != strandGroup.strandCount; i++)
									{
										HairAssetUtility.DeclareStrandIterator(strandGroup, i, out int strandParticleBegin, out int strandParticleStride, out int strandParticleEnd);

										var j = strandGroup.lodGuideIndex[lodOffset + i];
										{
											var dd = Vector3.SqrMagnitude(strandGroup.particlePosition[strandParticleBegin] - sumCenterPtr[j]);
											if (dd > strandGroup.lodGuideReach[lodOffset + j])
											{
												strandGroup.lodGuideReach[lodOffset + j] = dd;
											}
										}
									}

									for (int i = 0; i != strandGroup.strandCount; i++)
									{
										if (strandGroup.lodGuideIndex[lodOffset + i] == i)
											strandGroup.lodGuideReach[lodOffset + i] = Mathf.Sqrt(strandGroup.lodGuideReach[lodOffset + i]);
									}

									//Debug.Log("lod " + lodIndex + ", first guide radius = " + strandGroup.lodGuideRadii[lodIndex * strandGroup.strandCount]);
								}
							}
						}
						//strandGroup.version = 3;
						return false;
						*/

					// done
					default:
						return false;
				}
			}

			for (int i = 0; i != strandGroups.Length; i++)
			{
				while (PerformVersionBump(ref strandGroups[i])) { };
			}
		}
	}

	public static class HairAssetProvisional
	{
		public struct ProceduralRoots : IDisposable
		{
			public struct RootParameters
			{
				public float normalizedStrandLength;
				public float normalizedStrandDiameter;
				public float normalizedCurlRadius;
				public float normalizedCurlSlope;

				public static readonly RootParameters defaults = new RootParameters()
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
			public NativeArray<RootParameters> rootParameters;// R,G,B,A == Strand length, Strand diameter, Curl radius, Curl slope

			public ProceduralRoots(int strandCount, Allocator allocator = Allocator.Temp)
			{
				this.strandCount = strandCount;
				this.rootUV = new NativeArray<Vector2>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.rootPosition = new NativeArray<Vector3>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.rootDirection = new NativeArray<Vector3>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.rootParameters = new NativeArray<RootParameters>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
			}

			public unsafe void GetUnsafePtrs(out Vector3* rootPositionPtr, out Vector3* rootDirectionPtr, out Vector2* rootUVPtr, out RootParameters* rootParametersPtr)
			{
				rootUVPtr = (Vector2*)rootUV.GetUnsafePtr();
				rootPositionPtr = (Vector3*)rootPosition.GetUnsafePtr();
				rootDirectionPtr = (Vector3*)rootDirection.GetUnsafePtr();
				rootParametersPtr = (RootParameters*)rootParameters.GetUnsafePtr();
			}

			public void Dispose()
			{
				rootUV.Dispose();
				rootPosition.Dispose();
				rootDirection.Dispose();
				rootParameters.Dispose();
			}
		}

		public struct ProceduralStrands : IDisposable
		{
			public int strandCount;
			public int strandParticleCount;
			public NativeArray<Vector4> rootScale;
			public NativeArray<Vector3> particlePosition;

			public ProceduralStrands(int strandCount, int strandParticleCount, Allocator allocator = Allocator.Temp)
			{
				this.strandCount = strandCount;
				this.strandParticleCount = strandParticleCount;
				this.rootScale = new NativeArray<Vector4>(strandCount, allocator, NativeArrayOptions.UninitializedMemory);
				this.particlePosition = new NativeArray<Vector3>(strandCount * strandParticleCount, allocator, NativeArrayOptions.UninitializedMemory);
			}

			public unsafe void GetUnsafePtrs(out Vector4* rootScalePtr, out Vector3* particlePositionPtr)
			{
				rootScalePtr = (Vector4*)rootScale.GetUnsafePtr();
				particlePositionPtr = (Vector3*)particlePosition.GetUnsafePtr();
			}

			public void Dispose()
			{
				rootScale.Dispose();
				particlePosition.Dispose();
			}
		}

		public struct CurveSet : IDisposable
		{
			[Flags]
			public enum CurveFeatures
			{
				TexCoord = 1 << 0,
				Diameter = 1 << 1,
				Tapering = 1 << 2,
			}

			[Flags]
			public enum VertexFeatures
			{
				Position = 1 << 0,
				TexCoord = 1 << 1,
				Diameter = 1 << 2,
			}

			public struct Tip
			{
				public float tipScaleOffset;	// tip offset (where along curve to begin tapering)
				public float tipScale;			// tip scale
			}

			public int curveCount;							// number of curves in set
			public UnsafeList<int> curveVertexCount;		// i: curve index -> number of vertices in curve
			public UnsafeList<Vector2> curveDataTexCoord;	// i: curve index -> curve root texcoord
			public UnsafeList<float> curveDataDiameter;		// i: curve index -> curve root diameter
			public UnsafeList<Tip> curveDataTapering;		// i: curve index -> curve tapering
			public CurveFeatures curveFeatures;				// m: curve feature flags
			public UnsafeList<Vector3> vertexDataPosition;	// j: vertex index -> curve vertex position
			public UnsafeList<Vector2> vertexDataTexCoord;	// j: vertex index -> curve vertex texcoord
			public UnsafeList<float> vertexDataDiameter;	// j: vertex index -> curve vertex diameter
			public VertexFeatures vertexFeatures;			// m: vertex feature flags
			public float unitScalePosition;					// scale of position data in meters
			public float unitScaleDiameter;					// scale of diameter data in meters

			// vertex data must be laid out sequentially, e.g. for two curves a and b with 4 and 2 vertices respectively:
			//		curveVertexCount	[ 4 2 ]
			//		vertexData*			[ a a a a b b ]
			//
			// vertex data offset for the i'th curve is then given by:
			//		j <- sum( curveVertexCount, 0..i-1 )
			//
			// vertex feature flags are used during processing:
			//		- vertex position is expected (stream required, flag not tested)
			//		- vertex texcoord is optional and is used to export material attribute and/or resolve root uv
			//		- vertex diameter is optional and is used to export material attribute and/or resolve strand diameter and tapering
			//
			// curve feature flags are also used during processing:
			//		- curve texcoord is optional and is used to resolve root uv (takes precedence over vertex texcoord)
			//		- curve diameter is optional and is used to resolve strand diameter (unless vertex diameter is present)
			//		- curve tapering is optional and is used to resolve strand tapering (unless vertex diameter is present)

			public CurveSet(Allocator allocator) : this(0, 0, allocator) { }
			public CurveSet(int initialCurveCapacity, int initialVertexCapacity, Allocator allocator)
			{
				this.curveCount = 0;
				this.curveVertexCount = new UnsafeList<int>(initialCurveCapacity, allocator, NativeArrayOptions.UninitializedMemory);
				this.curveDataTexCoord = new UnsafeList<Vector2>(initialCurveCapacity, allocator, NativeArrayOptions.UninitializedMemory);
				this.curveDataDiameter = new UnsafeList<float>(initialCurveCapacity, allocator, NativeArrayOptions.UninitializedMemory);
				this.curveDataTapering = new UnsafeList<Tip>(initialCurveCapacity, allocator, NativeArrayOptions.UninitializedMemory);
				this.curveFeatures = (CurveFeatures)0;
				this.vertexDataPosition = new UnsafeList<Vector3>(initialVertexCapacity, allocator, NativeArrayOptions.UninitializedMemory);
				this.vertexDataTexCoord = new UnsafeList<Vector2>(initialVertexCapacity, allocator, NativeArrayOptions.UninitializedMemory);
				this.vertexDataDiameter = new UnsafeList<float>(initialVertexCapacity, allocator, NativeArrayOptions.UninitializedMemory);
				this.vertexFeatures = VertexFeatures.Position;
				this.unitScalePosition = 1.0f;
				this.unitScaleDiameter = 1.0f;
			}

			public void Dispose()
			{
				curveVertexCount.Dispose();
				curveDataTexCoord.Dispose();
				curveDataDiameter.Dispose();
				curveDataTapering.Dispose();
				vertexDataPosition.Dispose();
				vertexDataTexCoord.Dispose();
				vertexDataDiameter.Dispose();
			}
		}

		public struct CurveSetInfo
		{
			public int minVertexCount;
			public int maxVertexCount;
			public int sumVertexCount;

			public CurveSetInfo(in CurveSet curveSet)
			{
				if (curveSet.curveCount > 0)
				{
					unsafe
					{
						var curveVertexCountPtr = (int*)curveSet.curveVertexCount.Ptr;

						minVertexCount = curveVertexCountPtr[0];
						maxVertexCount = curveVertexCountPtr[0];
						sumVertexCount = curveVertexCountPtr[0];

						for (int i = 1; i != curveSet.curveCount; i++)
						{
							minVertexCount = Mathf.Min(minVertexCount, curveVertexCountPtr[i]);
							maxVertexCount = Mathf.Max(maxVertexCount, curveVertexCountPtr[i]);
							sumVertexCount += curveVertexCountPtr[i];
						}
					}
				}
				else
				{
					minVertexCount = 0;
					maxVertexCount = 0;
					sumVertexCount = 0;
				}
			}
		}
	}

	public static class HairAssetUtility
	{
		public static void DeclareStrandIterator(HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount, int strandIndex,
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

		public static void DeclareStrandIterator(in HairAsset.StrandGroup strandGroup, int strandIndex,
			out int strandParticleBegin,
			out int strandParticleStride,
			out int strandParticleEnd)
		{
			DeclareStrandIterator(strandGroup.particleMemoryLayout, strandGroup.strandCount, strandGroup.strandParticleCount, strandIndex,
				out strandParticleBegin,
				out strandParticleStride,
				out strandParticleEnd);
		}

		public static void DeclareParticleStride(HairAsset.MemoryLayout memoryLayout, int strandCount, int strandParticleCount,
			out int strandParticleOffset,
			out int strandParticleStride)
		{
			switch (memoryLayout)
			{
				default:
				case HairAsset.MemoryLayout.Sequential:
					strandParticleOffset = strandParticleCount;
					strandParticleStride = 1;
					break;

				case HairAsset.MemoryLayout.Interleaved:
					strandParticleOffset = 1;
					strandParticleStride = strandCount;
					break;
			}
		}

		public static void DeclareParticleStride(in HairAsset.StrandGroup strandGroup, out int strandParticleOffset, out int strandParticleStride)
		{
			DeclareParticleStride(strandGroup.particleMemoryLayout, strandGroup.strandCount, strandGroup.strandParticleCount,
				out strandParticleOffset,
				out strandParticleStride);
		}
	}
}
