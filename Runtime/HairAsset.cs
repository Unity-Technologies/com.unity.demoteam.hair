using System;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.DemoTeam.Attributes;

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	[CreateAssetMenu(menuName = "Hair/Hair Asset", order = 350), PreferBinarySerialization]
	public class HairAsset : ScriptableObject
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
			[Tooltip("Material applied to the generated strand groups")]
			public Material material;
			[ToggleGroup, Tooltip("Build LOD clusters for the generated strands (to optionally reduce cost of rendering and simulation)")]
			public bool kLODClusters;
			[ToggleGroupItem, Tooltip("Choose how to build base level clusters")]
			public LODClusters kLODClustersProvider;
			[ToggleGroupItem(withLabel = true), Tooltip("Enable subdivision of base level clusters (to generate upper LODs)")]
			public bool kLODClustersPyramid;

			public static readonly SettingsBasic defaults = new SettingsBasic()
			{
				type = Type.Procedural,
				memoryLayout = MemoryLayout.Interleaved,
				material = null,
				kLODClusters = false,
				kLODClustersProvider = LODClusters.Generated,
				kLODClustersPyramid = false,
			};
		}

		[Serializable]
		public struct SettingsAlembic
		{
			public enum RootUV
			{
				Uniform,
				ResolveFromMesh,
				//ResolveFromAttribute,
			}

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
			[Tooltip("Alembic asset containing at least one set of curves")]
			public AlembicStreamPlayer alembicAsset;
#endif

			[LineHeader("UV Resolve")]

			public RootUV rootUV;
			[VisibleIf(nameof(rootUV), RootUV.Uniform)]
			public Vector2 rootUVConstant;
			[VisibleIf(nameof(rootUV), RootUV.ResolveFromMesh)]
			public Mesh rootUVMesh;
			//[VisibleIf(nameof(rootUV), RootUV.ResolveFromAttribute)]
			//public string rootUVAttribute;

			[LineHeader("Processing")]

			[Tooltip("Resample curves to ensure a specific number of equidistant particles along each strand")]
			public bool resampleCurves;
			[Range(3, HairSim.MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of particles along each strand")]
			public int resampleParticleCount;
			[Range(1, 5), Tooltip("Number of resampling iterations")]
			public int resampleQuality;

			public static readonly SettingsAlembic defaults = new SettingsAlembic()
			{
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
				SubmeshIndex0 = 0x1 << 0,
				SubmeshIndex1 = 0x1 << 1,
				SubmeshIndex2 = 0x1 << 2,
				SubmeshIndex3 = 0x1 << 3,
				SubmeshIndex4 = 0x1 << 4,
				SubmeshIndex5 = 0x1 << 5,
				SubmeshIndex6 = 0x1 << 6,
				SubmeshIndex7 = 0x1 << 7,
			}

			public enum CurlSamplingStrategy
			{
				RelaxStrandLength,
				RelaxCurlSlope,
			}

			[LineHeader("Roots")]

			[Tooltip("Placement method")]
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

		// TOPIC: Clumping/LOD
		//
		//		.--- simulated ---.  .------- interpolated --------.
		//		g  g  g  g  g  g  g  f  f  f  f  f  f  f  f  f  f  f
		//		----->-----> ordering ----->----->----->----->----->
		//
		// Per-LOD data
		//		_LODGuideCount -> num. guides
		//		_LODGuideIndex[strandIndex_f] -> strandIndex_g
		//
		// Enable LOD
		//		LOD 0 = all strands / guides from texture / guides from generator (from all strands)
		//		LOD 1 = guides from texture / guides from generator (from LOD 0)
		//		LOD 2 = guides from texture / guides from generator (from LOD 1)
		//		LOD x = guides from generator(LOD x-1)
		//
		// LOD From Texture
		//		Simulated Guide Index Map
		//		[Info: Asset must be set up to resolve root UVs]
		//
		// LOD From Generator
		//		Guide Root Separation

		[Serializable]
		public struct SettingsLODGenerated
		{
			public enum ClusterSelection
			{
				RandomPointsOnMesh,
				RandomPointsInVolume,
			}

			[LineHeader("Base LOD")]

			[Range(0.0f, 1.0f)]
			public float baseLOD;
			public ClusterSelection baseLODClusterSelection;
			[VisibleIf(nameof(baseLODClusterSelection), ClusterSelection.RandomPointsOnMesh)]
			public Mesh baseLODClusterSelectionMesh;
			public int baseLODClusterIterations;

			public static readonly SettingsLODGenerated defaults = new SettingsLODGenerated()
			{
				baseLOD = 0.1f,
				baseLODClusterSelection = ClusterSelection.RandomPointsInVolume,
				baseLODClusterSelectionMesh = null,
				baseLODClusterIterations = 1,
			};
		}

		[Serializable]
		public struct SettingsLODUVMapped
		{
			[LineHeader("Base LOD")]

			[NonReorderable]
			public Texture2D[] baseLODClusterMaps;

			public static readonly SettingsLODUVMapped defaults = new SettingsLODUVMapped()
			{
				baseLODClusterMaps = null,
			};
		}

		[Serializable]
		public struct SettingsLODPyramid
		{
			public enum HighLOD
			{
				AllStrands,
				FractionOfStrands,
			}

			[LineHeader("High LOD Pyramid")]

			[Range(0.0f, 1.0f)]
			public float highLOD;
			[Range(0, 10)]
			public int highLODIntermediateLevels;

			public static readonly SettingsLODPyramid defaults = new SettingsLODPyramid()
			{
				highLOD = 1.0f,
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
			[NonReorderable] public int[] lodGuideCount;// len: lodCount
			[HideInInspector] public int[] lodGuideIndex;// len: lodCount * strandCount
			[HideInInspector] public float[] lodThreshold;

			[HideInInspector] public Mesh meshAssetRoots;
			[HideInInspector] public Mesh meshAssetLines;
			[HideInInspector] public Mesh meshAssetStrips;
		}

		public Material defaultMaterial;

		public SettingsBasic settingsBasic = SettingsBasic.defaults;
		public SettingsAlembic settingsAlembic = SettingsAlembic.defaults;
		public SettingsProcedural settingsProcedural = SettingsProcedural.defaults;
		public SettingsLODGenerated settingsLODGenerated = SettingsLODGenerated.defaults;
		public SettingsLODUVMapped settingsLODUVMapped = SettingsLODUVMapped.defaults;
		public SettingsLODPyramid settingsLODPyramid = SettingsLODPyramid.defaults;

		public StrandGroup[] strandGroups;
		public bool strandGroupsAutoBuild;

		public string checksum;
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
