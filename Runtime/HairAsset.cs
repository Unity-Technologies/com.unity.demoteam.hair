using System;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using Unity.DemoTeam.Attributes;

namespace Unity.DemoTeam.Hair
{
	[CreateAssetMenu, PreferBinarySerialization]
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

		[Serializable]
		public struct SettingsBasic
		{
			[Tooltip("Type of generator")]
			public Type type;
			[Tooltip("Memory layout for the generated strands")]
			public MemoryLayout memoryLayout;
			[Tooltip("Material applied to the generated strand groups")]
			public Material material;

			public static readonly SettingsBasic defaults = new SettingsBasic()
			{
				type = Type.Procedural,
				memoryLayout = MemoryLayout.Interleaved,
			};
		}

		[Serializable]
		public struct SettingsAlembic
		{
			[Tooltip("Alembic asset containing at least one set of curves")]
			public AlembicStreamPlayer sourceAsset;
			[Tooltip("Resample curves to ensure a specific number of particles along each strand")]
			public bool resampleCurves;
			[Range(3, HairSim.MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of particles along each strand")]
			public int resampleParticleCount;

			public static readonly SettingsAlembic defaults = new SettingsAlembic()
			{
				resampleCurves = true,
				resampleParticleCount = 16,
			};
		}

		[Serializable]
		public struct SettingsProcedural
		{
			public enum Style
			{
				Curtain,
				Brush,
				Cap,
				StratifiedCurtain,
			}

			public Style style;

			[Range(64, HairSim.MAX_STRAND_COUNT), Tooltip("Number of strands")]
			public int strandCount;
			[Range(3, HairSim.MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of particles along each strand")]
			public int strandParticleCount;

			[Range(0.001f, 5.0f), Tooltip("Strand length (in meters)")]
			public float strandLength;
			[ToggleGroup, Tooltip("Enable this to vary strand lengths")]
			public bool strandLengthVariation;
			[ToggleGroupItem, Range(0.0f, 1.0f), Tooltip("Fraction of strand length")]
			public float strandLengthVariationAmount;

			[ToggleGroup, Tooltip("Enable this to curl the strands")]
			public bool strandCurl;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 10.0f), Tooltip("Curl radius (in centimeters)")]
			public float strandCurlRadius;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Curl slope")]
			public float strandCurlSlope;
			[ToggleGroupItem, Tooltip("Relax slope for small radii, to maintain strand length")]
			public bool strandCurlSlopeRelaxed;
			[ToggleGroup, Tooltip("Enable this to vary curls")]
			public bool strandCurlVariation;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Fraction of curl radius")]
			public float strandCurlVariationRadius;
			[ToggleGroupItem(withLabel = true), Range(0.0f, 1.0f), Tooltip("Fraction of curl slope")]
			public float strandCurlVariationSlope;

			public static readonly SettingsProcedural defaults = new SettingsProcedural()
			{
				style = Style.Curtain,

				strandCount = 64,
				strandParticleCount = 32,

				strandLength = 0.25f,
				strandLengthVariation = false,
				strandLengthVariationAmount = 0.2f,

				strandCurl = false,
				strandCurlRadius = 1.0f,
				strandCurlSlope = 0.3f,
				strandCurlSlopeRelaxed = false,
				strandCurlVariation = false,
				strandCurlVariationRadius = 0.1f,
				strandCurlVariationSlope = 0.3f,
			};
		}

		[Serializable]
		public struct StrandGroup
		{
			public int strandCount;
			public int strandParticleCount;

			public float maxStrandLength;
			public float maxParticleInterval;

			[HideInInspector] public float[] rootScale;
			[HideInInspector] public Vector3[] rootPosition;
			[HideInInspector] public Vector3[] rootDirection;

			[HideInInspector] public Vector3[] particlePosition;
			[HideInInspector] public MemoryLayout particleMemoryLayout;

			[HideInInspector] public Mesh meshAssetLines;
			[HideInInspector] public Mesh meshAssetRoots;
		}

		public Material defaultMaterial;

		public SettingsBasic settingsBasic = SettingsBasic.defaults;
		public SettingsAlembic settingsAlembic = SettingsAlembic.defaults;
		public SettingsProcedural settingsProcedural = SettingsProcedural.defaults;

		public StrandGroup[] strandGroups;
		public bool strandGroupsAutoBuild;

		public string checksum;

		public HairSim.SolverSettings settingsSolver = HairSim.SolverSettings.defaults;
		public HairSim.VolumeSettings settingsVolume = HairSim.VolumeSettings.defaults;
	}
}
