using System;
using UnityEngine;
using UnityEngine.Serialization;

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	[CreateAssetMenu(menuName = "Hair/Hair Asset", order = 350), PreferBinarySerialization]
	public class HairAsset : ScriptableObject, ISerializationCallbackReceiver
	{
		public enum Type
		{
			Procedural,
			Alembic,
		}

		public enum MemoryLayout
		{
			Interleaved,
			Sequential,
		}

		public enum StrandClusterMode
		{
			Roots,
			Strands,
			Strands3pt,
		}

		[Serializable]
		public struct SettingsBasic
		{
			[Tooltip("Type of generator")]
			public Type type;
			[Tooltip("Memory layout for the generated strands")]
			public MemoryLayout memoryLayout;
			[ToggleGroup, Tooltip("Build LOD clusters for the generated strands (allows optionally reducing cost of rendering and/or simulation)")]
			public bool kLODClusters;
			[ToggleGroupItem(withLabel = true), Tooltip("Choose how the generated strands are clustered (where Roots == by 3-D root positions, Strands == by n-D strand positions, Strands 3pt == by 9-D quantized strand positions)")]
			public StrandClusterMode kLODClustersClustering;

			public static readonly SettingsBasic defaults = new SettingsBasic()
			{
				type = Type.Procedural,
				memoryLayout = MemoryLayout.Interleaved,
				kLODClusters = false,
				kLODClustersClustering = StrandClusterMode.Strands,
			};
		}

		[Serializable]
		public struct SettingsAlembic
		{
			public enum Groups
			{
				Combine,
				Preserve,
			}

			public enum RootUV
			{
				Uniform,
				ResolveFromMesh,
				//ResolveFromCurveUV,
				//ResolveFromCurveAttribute,
			}

			[LineHeader("Curves")]

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
			[Tooltip("Alembic asset containing at least one set of curves")]
			public AlembicStreamPlayer alembicAsset;
#endif
			[Tooltip("Whether to combine or preserve subsequent sets of curves with same vertex count")]
			public Groups alembicAssetGroups;

			[LineHeader("UV Resolve")]

			public RootUV rootUV;
			[VisibleIf(nameof(rootUV), RootUV.Uniform)]
			public Vector2 rootUVConstant;
			[VisibleIf(nameof(rootUV), RootUV.ResolveFromMesh)]
			public Mesh rootUVMesh;
			//[VisibleIf(nameof(rootUV), RootUV.ResolveFromCurveAttribute)]
			//public string rootUVAttribute;

			[LineHeader("Processing")]

			[Tooltip("Resample curves to ensure a specific number of equidistant particles along each strand")]
			public bool resampleCurves;

			[Range(3, HairSim.MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of equidistant particles along each strand")]
			public int resampleParticleCount;
			[Range(1, 5), Tooltip("Number of resampling iterations")]
			public int resampleQuality;

			public static readonly SettingsAlembic defaults = new SettingsAlembic()
			{
#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
				alembicAsset = null,
#endif
				alembicAssetGroups = Groups.Combine,

				rootUV = RootUV.Uniform,
				rootUVConstant = Vector2.zero,
				rootUVMesh = null,

				resampleCurves = true,
				resampleParticleCount = 16,
				resampleQuality = 1,
			};
		}

		[Serializable]
		public struct SettingsProcedural
		{
			public enum PlacementType
			{
				Primitive,
				Custom,
				Mesh,
			}

			public enum PrimitiveType
			{
				Curtain,
				Brush,
				Cap,
				StratifiedCurtain,
			}

			[Flags]
			public enum SubmeshMask
			{
				Submesh0 = 1 << 0,
				Submesh1 = 1 << 1,
				Submesh2 = 1 << 2,
				Submesh3 = 1 << 3,
				Submesh4 = 1 << 4,
				Submesh5 = 1 << 5,
				Submesh6 = 1 << 6,
				Submesh7 = 1 << 7,
			}

			public enum CurlSamplingStrategy
			{
				RelaxStrandLength,
				RelaxCurlSlope,
			}

			[LineHeader("Roots")]

			[Tooltip("Root placement method")]
			public PlacementType placement;
			[VisibleIf(nameof(placement), PlacementType.Primitive), Tooltip("Place strands using builtin primitive generator")]
			public PrimitiveType placementPrimitive;
			[VisibleIf(nameof(placement), PlacementType.Custom), Tooltip("Place strands using specified custom generator"), FormerlySerializedAs("placementGenerator")]
			public HairAssetProvider placementProvider;
			[VisibleIf(nameof(placement), PlacementType.Mesh), Tooltip("Place strands on specified triangle mesh")]
			public Mesh placementMesh;
			[VisibleIf(nameof(placement), PlacementType.Mesh), Tooltip("Included submesh indices"), FormerlySerializedAs("placementMeshInclude")]
			public SubmeshMask placementMeshGroups;
			[VisibleIf(nameof(placement), PlacementType.Mesh), Tooltip("Place strands on mesh according to specified density map (where 0 == Empty region, 1 == Fully populated region)"), FormerlySerializedAs("placementDensity")]
			public Texture2D mappedDensity;
			[VisibleIf(nameof(placement), PlacementType.Mesh), Tooltip("Obtain strand direction from specified object-space normal map"), FormerlySerializedAs("paintedDirection")]
			public Texture2D mappedDirection;
			[VisibleIf(nameof(placement), PlacementType.Mesh), Tooltip("Obtain normalized strand parameters from specified 4-channel mask map (where R,G,B,A == Strand length, Strand diameter, Curl radius, Curl slope)"), FormerlySerializedAs("paintedParameters")]
			public Texture2D mappedParameters;

			//[ToggleGroup, Tooltip("Initial seed")]
			//public bool seed;
			//[ToggleGroupItem, Min(1)]
			//public uint seedValue;

			[LineHeader("Strands")]

			[Range(64, HairSim.MAX_STRAND_COUNT), Tooltip("Number of strands")]
			public int strandCount;
			[Range(3, HairSim.MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of equidistant particles along each strand")]
			public int strandParticleCount;
			[Range(0.001f, 5.0f), Tooltip("Strand length (in meters)")]
			public float strandLength;
			[ToggleGroup, Tooltip("Enable this to vary the strand lengths")]
			public bool strandLengthVariation;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Amount of variation as fraction of strand length")]
			public float strandLengthVariationAmount;

			[LineHeader("Curls")]

			[ToggleGroup, Tooltip("Enable this to curl the strands")]
			public bool curl;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 10.0f), Tooltip("Curl radius (in centimeters)")]
			public float curlRadius;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Curl slope")]
			public float curlSlope;
			[ToggleGroup, Tooltip("Enable this to vary the curls")]
			public bool curlVariation;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Amount of variation as fraction of curl radius")]
			public float curlVariationRadius;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Amount of variation as fraction of curl slope")]
			public float curlVariationSlope;
			[Tooltip("Choose which parameter to relax if the curls become undersampled (due to a combination of particle count, strand length, curl radius and slope)")]
			public CurlSamplingStrategy curlSamplingStrategy;

			public static readonly SettingsProcedural defaults = new SettingsProcedural()
			{
				placement = PlacementType.Primitive,
				placementPrimitive = PrimitiveType.Curtain,
				placementProvider = null,
				placementMesh = null,
				placementMeshGroups = (SubmeshMask)(-1),
				mappedDensity = null,
				mappedDirection = null,
				mappedParameters = null,
				//seed = false,
				//seedValue = 1,

				strandCount = 64,
				strandParticleCount = 32,

				strandLength = 0.25f,
				strandLengthVariation = false,
				strandLengthVariationAmount = 0.2f,

				curl = false,
				curlRadius = 1.0f,
				curlSlope = 0.3f,
				curlVariation = false,
				curlVariationRadius = 0.1f,
				curlVariationSlope = 0.3f,
				curlSamplingStrategy = CurlSamplingStrategy.RelaxStrandLength,
			};
		}

		[Serializable]
		public struct SettingsLODClusters
		{
			[LineHeader("Clustering")]

			//[Min(1), Tooltip("Initial seed (evolved with set)")]
			//public int initialSeed;
			[Tooltip("Cluster policy to apply to empty clusters")]
			public ClusterVoid clusterVoid;
			[Tooltip("Cluster allocation policy (where Global == Allocate and iterate within full set, Split Global == Allocate within select cluster but iterate within full set, Split Branching == Allocate and iterate solely within split cluster)")]
			public ClusterAllocationPolicy clusterAllocation;
			[VisibleIf(nameof(clusterAllocation), CompareOp.Neq, ClusterAllocationPolicy.Global), Tooltip("Cluster allocation order for split-type policies (decides the order in which existing clusters are selected to be split into smaller clusters)")]
			public ClusterAllocationOrder clusterAllocationOrder;
			[ToggleGroup, Tooltip("Enable cluster refinement by k-means iteration")]
			public bool clusterRefinement;
			[ToggleGroupItem, Range(1, 200), Tooltip("Number of k-means iterations (upper bound, may finish earlier)")]
			public int clusterRefinementIterations;

			public enum BaseLODMode
			{
				Generated,
				UVMapped,
			}

			public enum BaseLODClusterMapFormat
			{
				OneClusterPerColor,
				OneClusterPerVisualCluster,
			}

			[Serializable]
			public struct BaseLOD
			{
				[LineHeader("Base LOD")]

				[Tooltip("Choose build path for the lower level clusters (where Generated == Intialize witk k-means++, UV Mapped == Import from cluster maps")]
				public BaseLODMode baseLOD;
			}

			[Serializable]
			public struct BaseLODParamsGenerated
			{
				[Range(0.0f, 1.0f), Tooltip("Number of clusters as fraction of all strands")]
				public float baseLODClusterQuantity;
			}

			[Serializable]
			public struct BaseLODParamsUVMapped
			{
				[Tooltip("Cluster map format (controls how specified cluster maps are interpreted)")]
				public BaseLODClusterMapFormat baseLODClusterFormat;
				[NonReorderable, Tooltip("Cluster map chain (higher indices must provide increasing level of detail)")]
				public Texture2D[] baseLODClusterMaps;
			}

			public enum HighLODMode
			{
				Automatic,
				Manual,
			}

			[Serializable]
			public struct HighLOD
			{
				[LineHeader("High LOD")]

				[ToggleGroup, Tooltip("Enable upper level clusters (will be built using lower level clusters as basis)")]
				public bool highLOD;
				[ToggleGroupItem, Tooltip("Choose build path for the upper level clusters")]
				public HighLODMode highLODMode;
			}

			[Serializable]
			public struct HighLODParamsAutomatic
			{
				[Range(0.0f, 1.0f), Tooltip("Number of clusters as fraction of all strands")]
				public float highLODClusterQuantity;
				[Range(1.2f, 8.0f), Tooltip("Upper bound on multiplier for number of clusters per level incremenent")]
				public float highLODClusterExpansion;
			}

			[Serializable]
			public struct HighLODParamsManual
			{
				[Range(0.0f, 1.0f), NonReorderable, Tooltip("Numbers of clusters as fractions of all strands")]
				public float[] highLODClusterQuantities;
			}

			[VisibleIf(false)] public BaseLOD baseLOD;
			[VisibleIf(false)] public BaseLODParamsGenerated baseLODParamsGenerated;
			[VisibleIf(false)] public BaseLODParamsUVMapped baseLODParamsUVMapped;

			[VisibleIf(false)] public HighLOD highLOD;
			[VisibleIf(false)] public HighLODParamsAutomatic highLODParamsAutomatic;
			[VisibleIf(false)] public HighLODParamsManual highLODParamsManual;

			public static readonly SettingsLODClusters defaults = new SettingsLODClusters()
			{
				//initialSeed = 7,
				clusterVoid = ClusterVoid.Preserve,
				clusterAllocation = ClusterAllocationPolicy.SplitBranching,
				clusterAllocationOrder = ClusterAllocationOrder.ByHighestError,
				clusterRefinement = true,
				clusterRefinementIterations = 100,

				baseLOD = new BaseLOD
				{
					baseLOD = BaseLODMode.Generated,
				},
				baseLODParamsGenerated = new BaseLODParamsGenerated
				{
					baseLODClusterQuantity = 0.0f,
				},
				baseLODParamsUVMapped = new BaseLODParamsUVMapped
				{
					baseLODClusterFormat = BaseLODClusterMapFormat.OneClusterPerColor,
				},

				highLOD = new HighLOD
				{
					highLOD = true,
				},
				highLODParamsAutomatic = new HighLODParamsAutomatic
				{
					highLODClusterQuantity = 1.0f,
					highLODClusterExpansion = 2.0f,
				},
				highLODParamsManual = new HighLODParamsManual
				{
					highLODClusterQuantities = new float[]
					{
						0.25f,
						0.5f,
						0.75f,
						1.0f,
					},
				},
			};
		}

		[Serializable]
		public struct StrandGroup
		{
			public int strandCount;
			public int strandParticleCount;

			public float maxStrandLength;
			public float maxParticleInterval;

			public float totalLength;

			[HideInInspector] public Bounds bounds;

			[HideInInspector] public Vector2[] rootUV;
			[HideInInspector] public float[] rootScale;
			[HideInInspector] public Vector3[] rootPosition;
			[HideInInspector] public Vector3[] rootDirection;

			[HideInInspector] public Vector3[] particlePosition;
			[HideInInspector] public MemoryLayout particleMemoryLayout;

			public int lodCount;
			[NonReorderable] public int[] lodGuideCount;	// n: lod index -> num. guides
			[HideInInspector] public int[] lodGuideIndex;	// i: lod index * strand count + strand index -> guide index
			[HideInInspector] public float[] lodGuideCarry;	// f: lod index * strand count + strand index -> guide carry
			[HideInInspector] public float[] lodThreshold;	// f: lod index -> relative guide count [0..1] (to maximum lod in group)

			[HideInInspector] public Mesh meshAssetRoots;
			[HideInInspector] public Mesh meshAssetLines;
			[HideInInspector] public Mesh meshAssetStrips;

			public int version;
			public const int VERSION = 2;
		}

		public SettingsBasic settingsBasic = SettingsBasic.defaults;
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
