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

		public enum LODClusters
		{
			Generated,
			UVMapped,
		}

		[Serializable]
		public struct SettingsBasic
		{
			[Tooltip("Type of generator")]
			public Type type;
			[Tooltip("Memory layout for the generated strands")]
			public MemoryLayout memoryLayout;
			[ToggleGroup, Tooltip("Build LOD clusters for the generated strands (to optionally reduce cost of rendering and simulation)")]
			public bool kLODClusters;
			[ToggleGroupItem, Tooltip("Choose how to build base level clusters")]
			public LODClusters kLODClustersProvider;
			[ToggleGroupItem(withLabel = true), Tooltip("Enable subdivision of base level clusters (to generate upper LODs)")]
			public bool kLODClustersHighLOD;

			public static readonly SettingsBasic defaults = new SettingsBasic()
			{
				type = Type.Procedural,
				memoryLayout = MemoryLayout.Interleaved,
				kLODClusters = false,
				kLODClustersProvider = LODClusters.Generated,
				kLODClustersHighLOD = false,
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

			//[ToggleGroup, Tooltip("Randomization seed")]
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
		public struct SettingsLODGenerated
		{
			public enum ClusterSelection
			{
				RandomPointsOnMesh,
				RandomPointsInVolume,
			}

			[LineHeader("Base LOD")]

			[Range(0.0f, 1.0f), Tooltip("Number of clusters as fraction of all strands")]
			public float baseLODClusterQuantity;
			[Tooltip("Cluster initialization method")]
			public ClusterSelection baseLODClusterInitialization;
			[VisibleIf(nameof(baseLODClusterInitialization), ClusterSelection.RandomPointsOnMesh), Tooltip("Cluster initialization mesh")]
			public Mesh baseLODClusterInitializationMesh;
			[Tooltip("Cluster initialization seed")]
			public uint baseLODClusterInitializationSeed;
			[Tooltip("Number of k-means iterations to apply to initial set of clusters")]
			public int baseLODClusterIterations;

			public static readonly SettingsLODGenerated defaults = new SettingsLODGenerated()
			{
				baseLODClusterQuantity = 0.1f,
				baseLODClusterInitialization = ClusterSelection.RandomPointsInVolume,
				baseLODClusterInitializationMesh = null,
				baseLODClusterInitializationSeed = 7,
				baseLODClusterIterations = 1,
			};
		}

		[Serializable]
		public struct SettingsLODUVMapped
		{
			public enum ClusterMapFormat
			{
				OneClusterPerColor,
				OneClusterPerVisualCluster,
			}

			[LineHeader("Base LOD")]

			[Tooltip("Cluster map format (controls how specified cluster maps are interpreted)")]
			public ClusterMapFormat baseLODClusterMapFormat;
			//[VisibleIf(nameof(baseLODClusterMapFormat), ClusterMapFormat.OneClusterPerColor)]
			//public ClusterMapEncoding baseLODClusterMapEncoding;
			[NonReorderable, Tooltip("Cluster map chain (higher indices must provide increasing levels of detail)")]
			public Texture2D[] baseLODClusterMapChain;

			public static readonly SettingsLODUVMapped defaults = new SettingsLODUVMapped()
			{
				baseLODClusterMapFormat = ClusterMapFormat.OneClusterPerColor,
				baseLODClusterMapChain = null,
			};
		}

		[Serializable]
		public struct SettingsLODPyramid
		{
			[LineHeader("High LOD")]

			[Range(0.0f, 1.0f), Tooltip("Number of clusters as fraction of all strands counting from highest base LOD")]
			public float highLODClusterQuantity;
			[Range(0, 10), Tooltip("Number of intermediate levels that will be generated between highest base LOD and high LOD")]
			public int highLODIntermediateLevels;

			public static readonly SettingsLODPyramid defaults = new SettingsLODPyramid()
			{
				highLODClusterQuantity = 1.0f,
				highLODIntermediateLevels = 0,
			};
		}

		[Serializable]
		public struct StrandGroup
		{
			public int strandCount;
			public int strandParticleCount;

			public float maxStrandLength;
			public float maxParticleInterval;

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
		public SettingsLODGenerated settingsLODGenerated = SettingsLODGenerated.defaults;
		public SettingsLODUVMapped settingsLODUVMapped = SettingsLODUVMapped.defaults;
		public SettingsLODPyramid settingsLODPyramid = SettingsLODPyramid.defaults;

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

							strandGroup.version = 1;
						}
						return true;

					// 1->2: add LOD guide carry
					case 1:
						{
							strandGroup.lodGuideCarry = new float[strandGroup.lodGuideIndex.Length];

							for (int i = 0; i != strandGroup.lodGuideCarry.Length; i++)
							{
								strandGroup.lodGuideCarry[i] = 1.0f;
							}

							strandGroup.version = 2;
						}
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
	}
}
