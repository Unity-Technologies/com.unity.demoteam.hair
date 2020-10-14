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
			StrandsSequential,
			StrandsInterleaved,
		}

		[Serializable]
		public struct SettingsBasic
		{
			[Tooltip("Type of generator")]
			public Type type;
			[Tooltip("Material applied to the generated groups")]
			public Material defaultMaterial;
			[Range(0.070f, 100.0f), Tooltip("Strand diameter in millimeters")]
			public float defaultStrandDiameter;
		}

		[Serializable]
		public struct SettingsAlembic
		{
			public AlembicStreamPlayer sourceAsset;
		}

		[Serializable]
		public struct SettingsProcedural
		{
			public const int MAX_STRAND_COUNT = 64000;
			public const int MAX_STRAND_PARTICLE_COUNT = 128;

			public enum Style
			{
				Curtain,
				Brush,
				Cap,
				StratifiedCurtain,
			}

			public Style style;
			[Range(64, MAX_STRAND_COUNT), Tooltip("Number of strands")]
			public int strandCount;
			[Range(3, MAX_STRAND_PARTICLE_COUNT), Tooltip("Number of particles along each strand")]
			public int strandParticleCount;
			[Range(0.001f, 5.0f), Tooltip("Strand length in meters")]
			public float strandLength;
		}

		[Serializable]
		public struct StrandGroup
		{
			public int strandCount;
			public int strandParticleCount;

			public float strandLengthMin;
			public float strandLengthMax;
			public float strandLengthAvg;

			[HideInInspector] public MemoryLayout memoryLayout;

			[HideInInspector] public Vector3[] initialPositions;
			[HideInInspector] public Vector3[] initialRootPositions;
			[HideInInspector] public Vector3[] initialRootDirections;

			[HideInInspector] public Mesh meshAssetLines;
			[HideInInspector] public Mesh meshAssetRoots;
		}

		public SettingsBasic settingsBasic;
		public SettingsAlembic settingsAlembic;
		public SettingsProcedural settingsProcedural;
		public bool autoBuild;

		public StrandGroup[] strandGroups;

		public Hash128 checksum;
	}
}
