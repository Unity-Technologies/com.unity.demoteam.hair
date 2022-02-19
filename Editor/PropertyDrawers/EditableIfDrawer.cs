using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class PropertyDrawers
	{
		[CustomPropertyDrawer(typeof(EditableIfAttribute))]
		public class EditableIfDrawer : PropertyDrawer
		{
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
			{
				var editable = base.attribute as EditableIfAttribute;
				using (new EditorGUI.DisabledScope(ComparePropertyUtility.Evaluate(editable, property) == false))
				{
					EditorGUI.PropertyField(position, property, label, includeChildren: true);
				}
			}
		}
	}
}
