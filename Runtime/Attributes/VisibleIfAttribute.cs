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
	public class VisibleIfAttributeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var visible = base.attribute as VisibleIfAttribute;
			if (visible.EvaluateAt(property))
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
				if (visible.EvaluateAt(property))
				{
					EditorGUI.PropertyField(position, property, label, includeChildren: true);
				}
			}
		}
	}
#endif
}
