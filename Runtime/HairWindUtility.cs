using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public struct HairWindProxy : ISpatialComponentProxy<HairWind, HairWind.RuntimeData>
	{
		public bool TryGetData(HairWind component, ref HairWind.RuntimeData data)
			=> HairWind.TryGetData(component, ref data);
		public bool TryGetComponentData(Component component, ref HairWind.RuntimeData data)
			=> HairWind.TryGetComponentData(component, ref data);

		public int ResolveDataHandle(in HairWind.RuntimeData data)
			=> data.xform.handle;
		public float ResolveDataDistance(in HairWind.RuntimeData data, in Vector3 p)
			=> HairWindUtility.SdWind(p, data);
	}

	public static class HairWindUtility
	{
		//-----------------
		// signed distance

		public static float SdWind(in Vector3 p, in HairWind.RuntimeData data)
		{
			switch (data.type)
			{
				case HairWind.RuntimeData.Type.Directional:
					return 0.0f;

				case HairWind.RuntimeData.Type.Spherical:
					return Vector3.Distance(p, data.emitter.p);

				case HairWind.RuntimeData.Type.Turbine:
					return Vector3.Distance(p, data.emitter.p + data.emitter.n * data.emitter.t0);
			}
			return 1e+7f;
		}
	}
}
