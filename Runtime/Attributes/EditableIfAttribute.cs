using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Hair
{
	public class EditableIfAttribute : CompareFieldBase
	{
		public EditableIfAttribute(string fieldName, object cmpValue) : base(fieldName, cmpValue) { }
		public EditableIfAttribute(string fieldName, CmpOp cmpOp, object cmpValue) : base(fieldName, cmpOp, cmpValue) { }
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(EditableIfAttribute))]
	public class EditableIfAttributeDrawer : CompareFieldBaseDrawer
	{
		public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
		{
			var enabled = GUI.enabled;
			GUI.enabled = base.Compare(property) && enabled;
			EditorGUI.PropertyField(rect, property, label, true);
			GUI.enabled = enabled;
		}
	}
#endif
}
