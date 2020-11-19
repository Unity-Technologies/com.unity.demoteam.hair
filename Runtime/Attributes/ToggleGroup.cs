using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using Unity.Mathematics;

namespace Unity.DemoTeam.Attributes
{
	public class ToggleGroupItemAttribute : PropertyAttribute
	{
		public bool withLabel;
		public bool allowSceneObjects;
		public ToggleGroupItemAttribute(bool withLabel = false, bool allowSceneObjects = true)
		{
			this.withLabel = withLabel;
			this.allowSceneObjects = allowSceneObjects;
		}
	}

	public class ToggleGroupAttribute : PropertyAttribute { }

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(ToggleGroupItemAttribute))]
	public class ToggleGroupItemAttributeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) { }
	}

	[CustomPropertyDrawer(typeof(ToggleGroupAttribute))]
	public class ToggleGroupAttributeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			EditorGUILayout.BeginHorizontal();

			if (TryGetTooltipAttribute(fieldInfo, out var tooltip))
			{
				label.tooltip = tooltip;
			}

			property.boolValue = EditorGUILayout.Toggle(label, property.boolValue);

			using (new EditorGUI.DisabledGroupScope(!property.boolValue))
			{
				while (property.Next(false))
				{
					var target = property.serializedObject.targetObject;
					if (target == null)
						continue;

					var field = GetFieldByPropertyPath(target.GetType(), property.propertyPath);
					if (field == null)
						continue;

					var groupItem = field.GetCustomAttribute<ToggleGroupItemAttribute>();
					if (groupItem == null)
						break;

					if (groupItem.withLabel)
					{
						var fieldLabelText = property.displayName;
						if (fieldLabelText.StartsWith(label.text))
							fieldLabelText = fieldLabelText.Substring(label.text.Length);

						TryGetTooltipAttribute(field, out var fieldLabelTooltip);

						var fieldLabel = new GUIContent(fieldLabelText, fieldLabelTooltip);
						var fieldLabelPad = 18.0f;// ...
						var fieldLabelWidth = EditorStyles.label.CalcSize(fieldLabel).x + fieldLabelPad;

						GUILayout.Space(-10.0f);
						EditorGUILayout.LabelField(fieldLabel, EditorStyles.label, GUILayout.MaxWidth(fieldLabelWidth));
					}

					GUILayout.Space(-10.0f);
					switch (property.propertyType)
					{
						case SerializedPropertyType.Enum:
							{
								var enumValue = (Enum)Enum.GetValues(field.FieldType).GetValue(property.enumValueIndex);
								var enumValueSelected = EditorGUILayout.EnumPopup(enumValue);
								property.enumValueIndex = Convert.ToInt32(enumValueSelected);
							}
							break;

						case SerializedPropertyType.Float:
							{
								if (TryGetRangeAttribute(field, out var min, out var max))
									property.floatValue = EditorGUILayout.Slider(property.floatValue, min, max);
								else
									property.floatValue = EditorGUILayout.FloatField(property.floatValue);
							}
							break;

						case SerializedPropertyType.Integer:
							{
								if (TryGetRangeAttribute(field, out var min, out var max))
									property.intValue = EditorGUILayout.IntSlider(property.intValue, (int)min, (int)max);
								else
									property.intValue = EditorGUILayout.IntField(property.intValue);
							}
							break;

						case SerializedPropertyType.Boolean:
							{
								property.boolValue = EditorGUILayout.ToggleLeft(property.displayName, property.boolValue);
							}
							break;

						case SerializedPropertyType.ObjectReference:
							{
								property.objectReferenceValue = EditorGUILayout.ObjectField(property.objectReferenceValue, field.FieldType, allowSceneObjects: groupItem.allowSceneObjects);
							}
							break;

						case SerializedPropertyType.LayerMask:
							{
								var concatName = InternalEditorUtility.layers;
								var concatMask = (property.intValue == -1) ? -1 : InternalEditorUtility.LayerMaskToConcatenatedLayersMask(property.intValue);

								concatMask = EditorGUILayout.MaskField(concatMask, concatName);

								if (concatMask == -1)
									property.intValue = -1;
								else
									property.intValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(concatMask);
							}
							break;

						default:
							Debug.Log("unsupported [ToggleGroupItem] " + property.propertyPath + ": " + property.propertyType.ToString());
							break;
					}

					if (!groupItem.withLabel)
					{
						if (TryGetTooltipAttribute(field, out var fieldTooltip))
						{
							GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent(string.Empty, fieldTooltip));
						}
					}
				}
			}

			EditorGUILayout.EndHorizontal();
			EditorGUI.EndProperty();
		}

		static FieldInfo GetFieldByPropertyPath(Type type, string path)
		{
			var start = 0;
			var delim = path.IndexOf('.', start);

			while (delim != -1)
			{
				var field = type.GetField(path.Substring(start, delim - start));
				if (field != null)
				{
					type = field.FieldType;
					start = delim + 1;
					delim = path.IndexOf('.', start);
				}
				else
				{
					return null;
				}
			}

			return type.GetField(path.Substring(start));
		}

		static bool TryGetRangeAttribute(FieldInfo field, out float min, out float max)
		{
			var a = field.GetCustomAttribute<RangeAttribute>();
			if (a != null)
			{
				min = a.min;
				max = a.max;
				return true;
			}
			else
			{
				min = 0.0f;
				max = 0.0f;
				return false;
			}
		}

		static bool TryGetTooltipAttribute(FieldInfo field, out string tooltip)
		{
			var a = field.GetCustomAttribute<TooltipAttribute>();
			if (a != null)
			{
				tooltip = a.tooltip;
				return true;
			}
			else
			{
				tooltip = string.Empty;
				return false;
			}
		}
	}
#endif
}
