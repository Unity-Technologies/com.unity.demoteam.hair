using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Hair
{
	public class LinearRampAttribute : PropertyAttribute
	{
		public float x0;
		public float x1;
		public float y0;
		public float y1;

		public LinearRampAttribute(float x0, float x1, float y0, float y1)
		{
			this.x0 = x0;
			this.x1 = x1;
			this.y0 = y0;
			this.y1 = y1;
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(LinearRampAttribute))]
	public class LinearRampAttributeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			label = EditorGUI.BeginProperty(position, label, property);
			{
				var ramp = base.attribute as LinearRampAttribute;
				var rampRect = new Rect(ramp.x0, ramp.y0, ramp.x1 - ramp.x0, ramp.y1 - ramp.y0);

				EditorGUI.BeginChangeCheck();

				var curve = property.animationCurveValue;
				{
					curve = EditorGUI.CurveField(position, label, curve, Color.green, rampRect);
				}

				if (EditorGUI.EndChangeCheck())
				{
					curve = ClampCurve(curve, ramp);
					curve.preWrapMode = curve.postWrapMode = WrapMode.Clamp;

					AnimationUtility.SetKeyLeftTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyRightTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);

					property.animationCurveValue = curve;
				}
			}
			EditorGUI.EndProperty();
		}

		static AnimationCurve ClampCurve(AnimationCurve curve, LinearRampAttribute ramp)
		{
			var xm = 0.5f * (ramp.x0 + ramp.x1);
			var ym = 0.5f * (ramp.y0 + ramp.y1);

			switch (curve.length)
			{
				case 0:
					curve.AddKey(ramp.x0, ym);
					curve.AddKey(ramp.x1, ym);
					break;

				case 1:
					var q = ClampKey(curve[0], ramp);
					{
						curve.AddKey(q.time > xm ? ramp.x0 : ramp.x1, q.value);
					}
					break;

				default:
					while (curve.length > 2)
					{
						curve.RemoveKey(1);// throw away interior keys
					}

					curve.MoveKey(0, ClampKey(curve[0], ramp));
					curve.MoveKey(1, ClampKey(curve[1], ramp));
					break;
			}

			return curve;
		}

		static Keyframe ClampKey(in Keyframe key, LinearRampAttribute ramp)
		{
			return new Keyframe(
				Mathf.Clamp(key.time, ramp.x0, ramp.x1),
				Mathf.Clamp(key.value, ramp.y0, ramp.y1)
			);
		}
	}
#endif
}
