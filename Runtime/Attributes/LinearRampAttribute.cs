using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Hair
{
	public class LinearRampAttribute : PropertyAttribute
	{
		public Rect ranges;
		public LinearRampAttribute(float x0, float y0, float x1, float y1)
		{
			this.ranges = new Rect(x0, y0, x1 - x0, y1 - y0);
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(LinearRampAttribute))]
	public class LinearRampAttributeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var ramp = base.attribute as LinearRampAttribute;
			{
				HairEditorGUI.LinearRampProperty(position, label, property, ramp.ranges);
			}
		}
	}
#endif

#if UNITY_EDITOR
	public static partial class HairEditorGUILayout
	{
		public static void LinearRampProperty(GUIContent label, SerializedProperty property, Rect ranges, params GUILayoutOption[] options)
		{
			HairEditorGUI.LinearRampProperty(EditorGUILayout.GetControlRect(options), label, property, ranges);
		}

		public static AnimationCurve LinearRamp(GUIContent label, AnimationCurve curve, Rect ranges, params GUILayoutOption[] options)
		{
			return HairEditorGUI.LinearRamp(EditorGUILayout.GetControlRect(options), label, curve, ranges);
		}
	}

	public static partial class HairEditorGUI
	{
		public static void LinearRampProperty(Rect position, GUIContent label, SerializedProperty property, Rect ranges)
		{
			label = EditorGUI.BeginProperty(position, label, property);
			{
				EditorGUI.BeginChangeCheck();

				var curve = property.animationCurveValue;
				{
					curve = LinearRamp(position, label, curve, ranges);
				}

				if (EditorGUI.EndChangeCheck())
				{
					property.animationCurveValue = curve;
				}
			}
			EditorGUI.EndProperty();
		}

		public static AnimationCurve LinearRamp(Rect position, GUIContent label, AnimationCurve curve, Rect ranges)
		{
			EditorGUI.BeginChangeCheck();

			curve = EditorGUI.CurveField(position, label, curve, Color.white, ranges);

			if (EditorGUI.EndChangeCheck())
			{
				curve = ClampCurve(curve, ranges);
				curve.preWrapMode = curve.postWrapMode = WrapMode.Clamp;

				AnimationUtility.SetKeyLeftTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
				AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
				AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
				AnimationUtility.SetKeyRightTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
			}

			return curve;
		}

		static AnimationCurve ClampCurve(AnimationCurve curve, Rect ranges)
		{
			var xm = ranges.xMin + 0.5f + ranges.width;
			var ym = ranges.yMin + 0.5f * ranges.height;

			switch (curve.length)
			{
				case 0:
					curve.AddKey(ranges.xMin, ym);
					curve.AddKey(ranges.yMin, ym);
					break;

				case 1:
					var q = ClampKey(curve[0], ranges);
					{
						curve.AddKey(q.time > xm ? ranges.xMin : ranges.xMax, q.value);
					}
					break;

				default:
					while (curve.length > 2)
					{
						curve.RemoveKey(1);// throw away interior keys
					}

					curve.MoveKey(0, ClampKey(curve[0], ranges));
					curve.MoveKey(1, ClampKey(curve[1], ranges));
					break;
			}

			return curve;
		}

		static Keyframe ClampKey(in Keyframe key, Rect ranges)
		{
			return new Keyframe(
				Mathf.Clamp(key.time, ranges.xMin, ranges.xMax),
				Mathf.Clamp(key.value, ranges.yMin, ranges.yMax)
			);
		}
	}
#endif
}
