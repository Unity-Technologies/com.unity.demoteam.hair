using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class PropertyDrawers
	{
		[CustomPropertyDrawer(typeof(VisibleIfAttribute))]
		public class VisibleIfDrawer : PropertyDrawer
		{
			public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
			{
				var visible = base.attribute as VisibleIfAttribute;
				if (ComparePropertyUtility.Evaluate(visible, property))
				{
					return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
				}
				else
				{
					return -EditorGUIUtility.standardVerticalSpacing;
				}
			}

			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
			{
				if (position.height > 0.0f)
				{
					var visible = base.attribute as VisibleIfAttribute;
					if (ComparePropertyUtility.Evaluate(visible, property))
					{
						EditorGUI.PropertyField(position, property, label, includeChildren: true);
					}
				}
			}
		}
	}
}
