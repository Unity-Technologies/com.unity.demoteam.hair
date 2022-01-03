using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace Unity.DemoTeam.Hair
{
	public class ToggleGroupItemAttribute : PropertyAttribute
	{
		public bool withLabel;
		public string withSuffix;
		public bool allowSceneObjects;

		public ToggleGroupItemAttribute(bool withLabel = false, string withSuffix = null, bool allowSceneObjects = true)
		{
			this.withLabel = withLabel;
			this.withSuffix = withSuffix;
			this.allowSceneObjects = allowSceneObjects;
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(ToggleGroupItemAttribute))]
	public class ToggleGroupItemAttributeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) { }
	}
#endif
}
