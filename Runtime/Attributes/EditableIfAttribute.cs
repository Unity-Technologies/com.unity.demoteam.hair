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
	public class EditableIfAttributeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var editable = base.attribute as EditableIfAttribute;

			if (GUI.enabled && editable.EvaluateAt(property) == false)
			{
				GUI.enabled = false;
				EditorGUI.PropertyField(position, property, label, includeChildren: true);
				GUI.enabled = true;
			}
			else
			{
				EditorGUI.PropertyField(position, property, label, includeChildren: true);
			}
		}
	}
#endif
}
