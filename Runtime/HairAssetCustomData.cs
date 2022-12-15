using UnityEngine;
using Unity.Collections;

namespace Unity.DemoTeam.Hair
{
	public abstract class HairAssetCustomData : ScriptableObject
	{
		public abstract bool AcquireCurves(out HairAssetProvisional.CurveSet curveSet, Allocator allocator);
	}
}
