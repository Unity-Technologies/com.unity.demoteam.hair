using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class PropertyDrawers
	{
		[CustomPropertyDrawer(typeof(ToggleGroupItemAttribute))]
		public class ToggleGroupItemDrawer : PropertyDrawer
		{
			public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) { }
		}
	}
}
