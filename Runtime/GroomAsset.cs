using System;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

namespace Unity.DemoTeam.Hair
{
	[CreateAssetMenu]
	[PreferBinarySerialization]
	public class GroomAsset : ScriptableObject
	{
		public enum Type
		{
			Procedural,
			Alembic,
		}

		public enum MemoryLayout
		{
			StrandsInterleaved,
			StrandsSequential,
		}

		[Serializable]
		public struct SettingsBasic
		{
			[Tooltip("Type of generator")]
			public Type type;
			[Tooltip("Material applied to the generated groups")]
			public Material material;
			[Tooltip("Memory layout for the strands")]
			public MemoryLayout memoryLayout;
			[Range(0.070f, 100.0f), Tooltip("Strand diameter in millimeters")]
			public float strandDiameter;
			[Range(0.0f, 9.0f)]
			public float strandParticleContrib;// TODO remove

			public static readonly SettingsBasic defaults = new SettingsBasic()
			{
				type = Type.Procedural,
				memoryLayout = MemoryLayout.StrandsInterleaved,
				strandDiameter = 10.0f,
			};
		}

		[Serializable]
		public struct SettingsAlembic
		{
			public AlembicStreamPlayer sourceAsset;

			public bool resampleCurves;
			[Range(3, HairSim.MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of particles along each strand")]
			public int resampleParticleCount;

			public static readonly SettingsAlembic defaults = new SettingsAlembic()
			{
				resampleCurves = true,
				resampleParticleCount = 32,
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
			[Range(0.001f, 5.0f), Tooltip("Strand length in meters")]
			public float strandLength;

			public static readonly SettingsProcedural defaults = new SettingsProcedural()
			{
				style = Style.Curtain,
				strandCount = 64,
				strandParticleCount = 32,
				strandLength = 0.5f,
			};
		}

		[Serializable]
		public struct StrandGroup
		{
			public int strandCount;
			public int strandParticleCount;

			[HideInInspector] public float strandLengthMin;
			[HideInInspector] public float strandLengthMax;
			[HideInInspector] public float strandLengthAvg;

			[HideInInspector] public MemoryLayout memoryLayout;

			[HideInInspector] public Vector3[] initialPositions;
			[HideInInspector] public Vector3[] initialRootPositions;
			[HideInInspector] public Vector3[] initialRootDirections;
			[HideInInspector] public float[] initialLengths;

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

		public Bounds GetBounds()
		{
			Debug.Assert(strandGroups != null && strandGroups.Length != 0);

			var strandBounds = strandGroups[0].meshAssetRoots.bounds;
			var strandLength = strandGroups[0].strandLengthMax;

			for (int i = 1; i != strandGroups.Length; i++)
			{
				strandBounds.Encapsulate(strandGroups[i].meshAssetRoots.bounds);
				strandLength = Mathf.Max(strandGroups[i].strandLengthMax, strandLength);
			}

			strandBounds.Expand(1.5f * (2.0f * strandLength));

			var extent = strandBounds.extents;
			var extentMax = Mathf.Max(extent.x, extent.y, extent.z);

			return new Bounds(strandBounds.center, Vector3.one * (2.0f * extentMax));
		}

		public Bounds GetBoundsForSquareCells()
		{
			var bounds = GetBounds();
			{
				var nonSquareExtent = bounds.extents;
				var nonSquareExtentMax = Mathf.Max(nonSquareExtent.x, nonSquareExtent.y, nonSquareExtent.z);

				return new Bounds(bounds.center, Vector3.one * (2.0f * nonSquareExtentMax));
			}
		}
	}
}
