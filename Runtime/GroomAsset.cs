using System;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

[CreateAssetMenu]
[PreferBinarySerialization]
public class GroomAsset : ScriptableObject
{
	#region Import settings

	public enum Type
	{
		Alembic,
		Procedural,
	}

	[Serializable]
	public struct SettingsAlembic
	{
		public AlembicStreamPlayer sourceAsset;

		[Range(0.070f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
		public float strandDiameter;
	}

	[Serializable]
	public struct SettingsProcedural
	{
		public const int MAX_STRANDS = 64000;
		public const int MAX_STRAND_PARTICLES = 128;

		public enum Style
		{
			Curtain,
			Brush,
			Cap,
			StratifiedCurtain,
		}

		public Style style;
		[Range(64, MAX_STRANDS)]
		public int strandCount;
		[Range(3, MAX_STRAND_PARTICLES)]
		public int strandParticleCount;
		[Range(0.001f, 5.0f), Tooltip("Strand length (in meters)")]
		public float strandLength;
		[Range(0.070f, 100.0f), Tooltip("Strand diameter (in millimeters)")]
		public float strandDiameter;
	}

	public Type type;
	public SettingsAlembic settingsAlembic;
	public SettingsProcedural settingsProcedural;

	#endregion Import settings

	#region Generated strands

	[Serializable]
	public struct StrandGroup
	{
		public enum MemoryLayout
		{
			StrandsSequential,
			StrandsInterleaved,
		}

		[HideInInspector] public MemoryLayout memoryLayout;

		public int strandCount;
		public int strandParticleCount;
		//public float strandDiameter;
		//public float strandLength;
		//public float strandParticleContrib;

		[HideInInspector] public Vector3[] initialPositions;
		[HideInInspector] public Vector3[] initialRootPositions;
		[HideInInspector] public Vector3[] initialRootDirections;

		public Mesh meshAssetLines;
		public Mesh meshAssetRoots;
	}

	public StrandGroup[] strandGroups;
	
	#endregion Generated strands
}
