using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Hair
{
	public class VisibleIfAttribute : CompareFieldBase
	{
		public VisibleIfAttribute(string fieldName, object cmpValue) : base(fieldName, cmpValue) { }
		public VisibleIfAttribute(string fieldName, CmpOp cmpOp, object cmpValue) : base(fieldName, cmpOp, cmpValue) { }
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(VisibleIfAttribute))]
	public class VisibleIfAttributeDrawer : CompareFieldBaseDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (base.Compare(property))
				return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
			else
				return -EditorGUIUtility.standardVerticalSpacing;
		}

		public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
		{
			if (rect.height <= 0.0f)
				return;
			if (base.Compare(property))
				EditorGUI.PropertyField(rect, property, label, includeChildren: true);
		}
	}
#endif
}
