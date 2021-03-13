using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public static class Vector3Ex
	{
		public static Vector3 Abs(this Vector3 value)
		{
			return new Vector3
			{
				x = Mathf.Abs(value.x),
				y = Mathf.Abs(value.y),
				z = Mathf.Abs(value.z),
			};
		}

		public static float ComponentMin(this Vector3 value)
		{
			return Mathf.Min(value.x, value.y, value.z);
		}

		public static float ComponentMax(this Vector3 value)
		{
			return Mathf.Max(value.x, value.y, value.z);
		}
	}
}
