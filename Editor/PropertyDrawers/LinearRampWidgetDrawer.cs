using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class PropertyDrawers
	{
		[CustomPropertyDrawer(typeof(LinearRampWidgetAttribute))]
		public class LinearRampWidgetDrawer : PropertyDrawer
		{
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
			{
				if (property.propertyType == SerializedPropertyType.Vector2)
				{
					var ramp = base.attribute as LinearRampWidgetAttribute;

					EditorGUI.BeginProperty(position, label, property);
					position = EditorGUI.PrefixLabel(position, label);

					//NOTE: EditorGUIUtility.fieldWidth
					property.vector2Value = HairGUI.LinearRampWidget(position, property.vector2Value, new Vector2(ramp.min, ramp.max), ramp.style);

					EditorGUI.EndProperty();
				}
				else
				{
					base.OnGUI(position, property, label);
				}
			}
		}
	}
}
