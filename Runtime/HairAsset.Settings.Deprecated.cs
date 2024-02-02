#pragma warning disable 0282 // ignore undefined ordering

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public partial class HairAsset
	{
		//TODO explicit layout
		public partial struct SettingsResolve
		{
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsResolve.rootUVConstant (UnityUpgradable) -> rootUVFallback", true)]
			public Vector2 rootUVConstant;

			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Renamed SettingsResolve.resampleParticleCount (UnityUpgradable) -> resampleResolution", true)]
			public int resampleParticleCount;
		}
	}
}
