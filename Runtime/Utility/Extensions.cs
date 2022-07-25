using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public static class Vector3Extensions
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

		public static float CMin(this Vector3 value)
		{
			return Mathf.Min(Mathf.Min(value.x, value.y), value.z);
		}

		public static float CMax(this Vector3 value)
		{
			return Mathf.Max(Mathf.Max(value.x, value.y), value.z);
		}

		public static Vector3 Rcp(this Vector3 value)
		{
			return new Vector3
			{
				x = 1.0f / value.x,
				y = 1.0f / value.y,
				z = 1.0f / value.z,
			};
		}
	}

	public static class QuaternionExtensions
	{
		public static Vector4 ToVector4(this Quaternion value)
		{
			return new Vector4
			{
				x = value.x,
				y = value.y,
				z = value.z,
				w = value.w,
			};
		}
	}

	public static class BoundsExtensions
	{
		public static Bounds WithTransform(this Bounds bounds, Matrix4x4 transform)
		{
			//TODO revisit Unity.Mathematics

			var aPos = bounds.center;
			var aExt = bounds.extents;

			var p000 = new Vector3(aPos.x - aExt.x, aPos.y - aExt.y, aPos.z - aExt.z);
			var p001 = new Vector3(aPos.x + aExt.x, aPos.y - aExt.y, aPos.z - aExt.z);
			var p010 = new Vector3(aPos.x - aExt.x, aPos.y + aExt.y, aPos.z - aExt.z);
			var p011 = new Vector3(aPos.x + aExt.x, aPos.y + aExt.y, aPos.z - aExt.z);
			var p100 = new Vector3(aPos.x - aExt.x, aPos.y - aExt.y, aPos.z + aExt.z);
			var p101 = new Vector3(aPos.x + aExt.x, aPos.y - aExt.y, aPos.z + aExt.z);
			var p110 = new Vector3(aPos.x - aExt.x, aPos.y + aExt.y, aPos.z + aExt.z);
			var p111 = new Vector3(aPos.x + aExt.x, aPos.y + aExt.y, aPos.z + aExt.z);

			var b = new Bounds(transform.MultiplyPoint3x4(p000), Vector3.zero);
			{
				b.Encapsulate(transform.MultiplyPoint3x4(p001));
				b.Encapsulate(transform.MultiplyPoint3x4(p010));
				b.Encapsulate(transform.MultiplyPoint3x4(p011));
				b.Encapsulate(transform.MultiplyPoint3x4(p100));
				b.Encapsulate(transform.MultiplyPoint3x4(p101));
				b.Encapsulate(transform.MultiplyPoint3x4(p110));
				b.Encapsulate(transform.MultiplyPoint3x4(p111));
			}

			return b;
		}

		public static Bounds WithScale(this Bounds bounds, float scale)
		{
			return new Bounds(bounds.center, bounds.size * scale);
		}
	}

	public static class RectExtensions
	{
		public static Rect ClipLeft(this Rect position, float width)
		{
			return new Rect(position.x + width, position.y, position.width - width, position.height);
		}

		public static Rect ClipLeft(this Rect position, float width, out Rect clipped)
		{
			clipped = new Rect(position.x, position.y, width, position.height);
			return position.ClipLeft(width);
		}
	}
}
