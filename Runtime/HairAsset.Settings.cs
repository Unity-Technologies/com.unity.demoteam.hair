using System;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_2021_2_OR_NEWER
using UnityEngine.Search;
#endif

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	public partial class HairAsset
	{
		public enum Type
		{
			Procedural	= 0,
			Alembic		= 1,
			Custom		= 2,
		}

		public enum MemoryLayout
		{
			Interleaved	= 0,
			Sequential	= 1,
		}

		public enum StrandClusterMode
		{
			Roots		= 0,
			Strands		= 1,
			Strands3pt	= 2,
		}

		[Serializable]
		public struct SettingsBasic
		{
			[Tooltip("Type of generator")]
			public Type type;
			[Tooltip("Memory layout for the generated strands")]
			public MemoryLayout memoryLayout;
			[ToggleGroup, Tooltip("Build hierarchical LOD clusters for the generated strands (allows reducing cost of simulation and rendering)")]
			public bool kLODClusters;
			[ToggleGroupItem(withLabel = true), Tooltip("Choose how the generated strands are clustered (where Roots == by 3-D root positions, Strands == by n-D strand positions, Strands 3pt == by 9-D quantized strand positions)")]
			public StrandClusterMode kLODClustersClustering;

			public static readonly SettingsBasic defaults = new SettingsBasic()
			{
				type = Type.Procedural,
				memoryLayout = MemoryLayout.Interleaved,
				kLODClusters = true,
				kLODClustersClustering = StrandClusterMode.Strands,
			};
		}

		[Serializable]
		public struct SettingsProcedural
		{
			public enum PlacementMode
			{
				Primitive	= 0,
				Custom		= 1,
				Mesh		= 2,
			}

			public enum PrimitiveType
			{
				Curtain				= 0,
				Brush				= 1,
				Cap					= 2,
				StratifiedCurtain	= 3,
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
				RelaxStrandLength	= 0,
				RelaxCurlSlope		= 1,
			}

			[LineHeader("Roots")]

			[Tooltip("Strand placement method")]
			public PlacementMode placement;
			[VisibleIf(nameof(placement), PlacementMode.Primitive), Tooltip("Place strands using builtin primitive generator")]
			public PrimitiveType placementPrimitive;
			[VisibleIf(nameof(placement), PlacementMode.Custom), Tooltip("Place strands using specified custom generator")]
			public HairAssetCustomPlacement placementProvider;
			[VisibleIf(nameof(placement), PlacementMode.Mesh), Tooltip("Place strands on specified triangle mesh")]
			public Mesh placementMesh;
			[VisibleIf(nameof(placement), PlacementMode.Mesh), Tooltip("Included submesh indices")]
			public SubmeshMask placementMeshGroups;
			[VisibleIf(nameof(placement), PlacementMode.Mesh), Tooltip("Place strands on mesh according to specified density map (where 0 == Empty region, 1 == Fully populated region)")]
			public Texture2D mappedDensity;
			[VisibleIf(nameof(placement), PlacementMode.Mesh), Tooltip("Obtain strand direction from specified object-space direction map")]
			public Texture2D mappedDirection;
			[VisibleIf(nameof(placement), PlacementMode.Mesh), Tooltip("Obtain normalized strand parameters from specified 4-channel mask map (where R,G,B,A == Strand length, Strand diameter, Curl radius, Curl slope)")]
			public Texture2D mappedParameters;

			//[ToggleGroup, Tooltip("Initial seed")]
			//public bool seed;
			//[ToggleGroupItem, Min(1)]
			//public uint seedValue;

			[LineHeader("Quantity")]

			[Range(HairSim.MIN_STRAND_COUNT, HairSim.MAX_STRAND_COUNT), Tooltip("Number of strands")]
			public int strandCount;
			[Range(HairSim.MIN_STRAND_PARTICLE_COUNT, HairSim.MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of equidistant particles along each strand")]
			public int strandParticleCount;

			[LineHeader("Proportions")]

			[Range(0.001f, 5.0f), Tooltip("Strand length (in meters)")]
			public float strandLength;
			[ToggleGroup, Tooltip("Enable this to vary the strand lengths")]
			public bool strandLengthVariation;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Amount of variation as fraction of strand length")]
			public float strandLengthVariationAmount;
			[Range(0.01f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
			public float strandDiameter;
			[ToggleGroup, Tooltip("Enable this to vary the strand diameters")]
			public bool strandDiameterVariation;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Amount of variation as fraction of strand diameter")]
			public float strandDiameterVariationAmount;
			[Range(0.0f, 1.0f)]
			public float tipScale;
			[Range(0.0f, 1.0f)]
			public float tipScaleOffset;

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
				placement = PlacementMode.Primitive,
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
				strandDiameter = SharedDefaults.defaultStrandDiameter,
				strandDiameterVariation = false,
				strandDiameterVariationAmount = 0.2f,
				tipScale = SharedDefaults.defaultTipScale,
				tipScaleOffset = SharedDefaults.defaultTipScaleOffset,

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
		public struct SettingsAlembic
		{
			public enum Groups
			{
				Combine		= 0,
				Preserve	= 1,
			}

			//static float SourceUnitToUnitScale(HairAsset.SettingsAlembic.SourceUnit sourceUnit, float sourceUnitFallback = 1.0f)
			//{
			//	switch (sourceUnit)
			//	{
			//		case HairAsset.SettingsAlembic.SourceUnit.DataInMeters: return 1.0f;
			//		case HairAsset.SettingsAlembic.SourceUnit.DataInCentimeters: return 0.01f;
			//		case HairAsset.SettingsAlembic.SourceUnit.DataInMillimeters: return 0.001f;
			//		default: return sourceUnitFallback;
			//	}
			//}
			//public enum SourceUnit
			//{
			//	DataInMeters,
			//	DataInCentimeters,
			//	DataInMillimeters,
			//}

			[LineHeader("Curves")]

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
			[Tooltip("Alembic asset containing at least one set of curves")]
#if UNITY_2021_2_OR_NEWER
			[SearchContext("p: ext:abc t:AlembicStreamPlayer", "asset",
				SearchViewFlags.CompactView |
				SearchViewFlags.HideSearchBar |
				SearchViewFlags.DisableInspectorPreview |
				SearchViewFlags.DisableSavedSearchQuery)]
#endif
			public AlembicStreamPlayer alembicAsset;
#endif
			[Tooltip("Whether to combine or preserve subsequent sets of curves with same vertex count")]
			public Groups alembicAssetGroups;

			[VisibleIf(false)] public SettingsResolve settingsResolve;

			public static readonly SettingsAlembic defaults = new SettingsAlembic()
			{
#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
				alembicAsset = null,
#endif
				alembicAssetGroups = Groups.Combine,

				settingsResolve = SettingsResolve.defaults,
			};
		}

		[Serializable]
		public struct SettingsCustom
		{
			[LineHeader("Curves")]

			public HairAssetCustomData dataProvider;

			[VisibleIf(false)] public SettingsResolve settingsResolve;

			public static readonly SettingsCustom defaults = new SettingsCustom()
			{
				dataProvider = null,

				settingsResolve = SettingsResolve.defaults,
			};
		}

		[Serializable]
		public partial struct SettingsResolve
		{
			public const int MIN_RESAMPLE_RESOLUTION = HairSim.MIN_STRAND_PARTICLE_COUNT;
			public const int MAX_RESAMPLE_RESOLUTION = HairSim.MAX_STRAND_PARTICLE_COUNT;
			public const int MIN_RESAMPLE_QUALITY = 1;
			public const int MAX_RESAMPLE_QUALITY = 5;

			public enum StrandDiameter
			{
				ResolveFromCurves	= 0,
				UseFallback			= 1,
			}

			public enum RootUV
			{
				ResolveFromMesh		= 0,
				ResolveFromCurves	= 1,
				UseFallback			= 2,
			}

			[Flags]
			public enum TransferAttributes
			{
				None			= 0,
				PerVertexUV		= 1 << 1,
				PerVertexWidth	= 1 << 0,
				All				= PerVertexUV | PerVertexWidth,
			}

			[LineHeader("Processing")]

			[Tooltip("Resample curves to ensure a specific number of equidistant particles along each strand")]
			public bool resampleCurves;
			[Range(MIN_RESAMPLE_RESOLUTION, MAX_RESAMPLE_RESOLUTION), Tooltip("Number of equidistant particles along each strand")]
			public int resampleResolution;
			[Range(MIN_RESAMPLE_QUALITY, MAX_RESAMPLE_QUALITY), Tooltip("Number of resampling iterations")]
			public int resampleQuality;

			[LineHeader("UV Resolve")]

			public RootUV rootUV;
			[VisibleIf(nameof(rootUV), RootUV.ResolveFromMesh)]
			public Mesh rootUVMesh;
			public Vector2 rootUVFallback;

			[LineHeader("Proportions")]

			public StrandDiameter strandDiameter;
			[VisibleIf(nameof(strandDiameter), StrandDiameter.ResolveFromCurves), Min(0.0f)]
			public float strandDiameterScale;
			[Range(0.01f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
			public float strandDiameterFallback;
			[Range(0.0f, 1.0f)]
			public float tipScaleFallback;
			[Range(0.0f, 1.0f)]
			public float tipScaleFallbackOffset;

			[LineHeader("Material")]

			[ToggleGroup]
			public bool exportAttributes;
			[ToggleGroupItem]
			public TransferAttributes exportAttributesMask;

			public static readonly SettingsResolve defaults = new SettingsResolve()
			{
				resampleCurves = true,
				resampleResolution = 16,
				resampleQuality = 3,

				rootUV = RootUV.UseFallback,
				rootUVMesh = null,
				rootUVFallback = Vector2.zero,

				strandDiameter = StrandDiameter.ResolveFromCurves,
				strandDiameterScale = 0.01f,
				strandDiameterFallback = SharedDefaults.defaultStrandDiameter,
				tipScaleFallback = SharedDefaults.defaultTipScale,
				tipScaleFallbackOffset = SharedDefaults.defaultTipScaleOffset,

				exportAttributes = false,
				exportAttributesMask = TransferAttributes.All,
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
				Generated	= 0,
				UVMapped	= 1,
			}

			public enum BaseLODClusterMapFormat
			{
				OneClusterPerColor			= 0,
				OneClusterPerVisualCluster	= 1,
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
				Automatic	= 0,
				Manual		= 1,
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
					baseLODClusterQuantity = 0.001f,
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

		public static class SharedDefaults
		{
			public static readonly float defaultStrandDiameter = 1.0f;
			public static readonly float defaultTipScaleOffset = 0.8f;
			public static readonly float defaultTipScale = 0.1f;
		}
	}
}
