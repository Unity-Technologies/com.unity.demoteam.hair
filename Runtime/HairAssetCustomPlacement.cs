using System;
using System.ComponentModel;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public abstract class HairAssetCustomPlacement : ScriptableObject
	{
		public abstract bool GenerateRoots(in HairAssetProvisional.ProceduralRoots roots);
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Renamed HairAssetProvider (UnityUpgradable) -> HairAssetCustomPlacement", true)]
	public abstract class HairAssetProvider
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Renamed GeneratedRoots (UnityUpgradable) -> HairAssetProvisional.ProceduralRoots", true)]
		public struct GeneratedRoots
		{
			[EditorBrowsable(EditorBrowsableState.Never)]
			[Obsolete("Renamed StrandParameters (UnityUpgradable) -> GeneratedRoots.RootParameters", true)]
			public struct StrandParameters { };
		};
	}
}
