using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public static class Vector2Extensions
	{
		public static Vector2 Abs(this Vector2 value)
		{
			return new Vector2
			{
				x = Mathf.Abs(value.x),
				y = Mathf.Abs(value.y),
			};
		}

		public static Vector2 CMul(this Vector2 value, in Vector2 other)
		{
			return new Vector2
			{
				x = value.x * other.x,
				y = value.y * other.y,
			};
		}

		public static float CMin(this Vector2 value)
		{
			return Mathf.Min(value.x, value.y);
		}

		public static float CMax(this Vector2 value)
		{
			return Mathf.Max(value.x, value.y);
		}
	}

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

		public static Vector3 CMul(this Vector3 value, in Vector3 other)
		{
			return new Vector3
			{
				x = value.x * other.x,
				y = value.y * other.y,
				z = value.z * other.z,
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

		public static float CSum(this Vector3 value)
		{
			return value.x + value.y + value.z;
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

		public static Vector3 Sign(this Vector3 value)
		{
			return new Vector3
			{
				x = Mathf.Sign(value.x),
				y = Mathf.Sign(value.y),
				z = Mathf.Sign(value.z),
			};
		}

		public static Plane ToPlane(this Vector3 value, float distance)
		{
			return new Plane(value, distance);
		}
	}

	public static class Vector4Extensions
	{
		public static Vector4 Rcp(this Vector4 value)
		{
			return new Vector4
			{
				x = 1.0f / value.x,
				y = 1.0f / value.y,
				z = 1.0f / value.z,
				w = 1.0f / value.w,
			};
		}

		public static Vector4 Sign(this Vector4 value)
		{
			return new Vector4
			{
				x = Mathf.Sign(value.x),
				y = Mathf.Sign(value.y),
				z = Mathf.Sign(value.z),
				w = Mathf.Sign(value.w),
			};
		}

		public static Plane ToPlane(this Vector4 value)
		{
			return new Plane(value, value.w);
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

		public static Bounds WithPadding(this Bounds bounds, float padding)
		{
			return new Bounds(bounds.center, bounds.size + new Vector3(2.0f * padding, 2.0f * padding, 2.0f * padding));
		}

		public static Bounds WithScale(this Bounds bounds, float scale)
		{
			return new Bounds(bounds.center, bounds.size * scale);
		}

		public static Bounds ToSquare(this Bounds bounds)
		{
			return new Bounds(bounds.center, bounds.size.Abs().CMax() * Vector3.one);
		}
	}

	public static class PlaneExtensions
	{
		public static bool IsNaN(this Plane value)
		{
			var n = value.normal;
			return (
				float.IsNaN(n.x) ||
				float.IsNaN(n.y) ||
				float.IsNaN(n.z)
			);
		}

		public static Vector4 ToVector4(this Plane value)
		{
			var n = value.normal;
			var d = value.distance;

			return new Vector4
			{
				x = n.x,
				y = n.y,
				z = n.z,
				w = d,
			};
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
