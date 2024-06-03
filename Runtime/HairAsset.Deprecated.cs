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
		public partial struct StrandGroup
		{
			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Removed (all topologies built at runtime)", false)]
			public Mesh meshAssetLines { get => meshAssetRoots; }

			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Removed (all topologies built at runtime)", false)]
			public Mesh meshAssetStrips { get => meshAssetRoots; }

			[EditorBrowsable(EditorBrowsableState.Never), HideInInspector]
			[Obsolete("Removed (all topologies built at runtime)", false)]
			public Mesh meshAssetTubes { get => meshAssetRoots; }
		}
	}
}
