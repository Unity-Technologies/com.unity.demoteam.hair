using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class PropertyDrawers
	{
		[CustomPropertyDrawer(typeof(LinearRampAttribute))]
		public class LinearRampDrawer : PropertyDrawer
		{
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
			{
				var ramp = base.attribute as LinearRampAttribute;
				{
					HairGUI.LinearRamp(position, label, property, ramp.ranges);
				}
			}
		}
	}
}
