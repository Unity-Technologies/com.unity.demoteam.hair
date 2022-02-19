using System;
using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairGUILayout
	{
		public struct PropertyRectScope : IDisposable
		{
			public readonly Rect position;
			public readonly GUIContent label;

			public PropertyRectScope(SerializedProperty property, params GUILayoutOption[] options) : this(property, GUIContent.none, options) { }
			public PropertyRectScope(SerializedProperty property, string label, params GUILayoutOption[] options) : this(property, new GUIContent(label), options) { }
			public PropertyRectScope(SerializedProperty property, GUIContent label, params GUILayoutOption[] options)
			{
				this.position = EditorGUILayout.GetControlRect(label != null && label != GUIContent.none, options);
				this.label = EditorGUI.BeginProperty(position, label, property);
			}

			public PropertyRectScope(GUIContent label, SerializedProperty property, float fixedWidth, GUIStyle style)
			{
				this.position = GUILayoutUtility.GetRect(fixedWidth, EditorGUIUtility.singleLineHeight, style, GUILayout.Width(fixedWidth));
				this.label = EditorGUI.BeginProperty(position, label, property);
			}

			public PropertyRectScope(GUIContent label, SerializedProperty property, float minWidth, float maxWidth, GUIStyle style)
			{
				this.position = GUILayoutUtility.GetRect(minWidth, maxWidth, EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight, style);
				this.label = EditorGUI.BeginProperty(position, label, property);
			}

			public void Dispose()
			{
				EditorGUI.EndProperty();
			}
		}
	}
}
