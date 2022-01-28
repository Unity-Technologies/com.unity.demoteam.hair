using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace Unity.DemoTeam.Hair
{
	public class ToggleGroupAttribute : PropertyAttribute { }

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(ToggleGroupAttribute))]
	public class ToggleGroupAttributeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			EditorGUILayout.BeginHorizontal();

			var labelText = label.text;
			var labelTooltip = label.tooltip;

			if (TryGetTooltipAttribute(fieldInfo, out labelTooltip))
			{
				label.tooltip = labelTooltip;
			}

			property.boolValue = EditorGUILayout.Toggle(label, property.boolValue, GUILayout.ExpandWidth(false));

			using (new EditorGUI.DisabledScope(!property.boolValue))
			{
				while (property.Next(enterChildren: false))
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
						if (fieldLabelText.StartsWith(labelText))
							fieldLabelText = fieldLabelText.Substring(labelText.Length).TrimStart();

						TryGetTooltipAttribute(field, out var fieldLabelTooltip);

						GUILayout.Space(2.0f);
						GUILayout.Label(new GUIContent(fieldLabelText, fieldLabelTooltip));
					}

					GUILayout.Space(-12.0f);
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
								property.boolValue = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(30.0f));
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

					if (groupItem.withSuffix != null)
					{
						GUILayout.Space(2.0f);
						GUILayout.Label(new GUIContent(groupItem.withSuffix));
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
					if (field.FieldType.IsArray)
					{
						type = field.FieldType.GetElementType();
						// skip the array section of the property path
						// e.g. foo.Array.data[0].bar
						//         '--- skip ---> ###
						for (int i = 0; i != 3; i++)
						{
							start = delim + 1;
							delim = path.IndexOf('.', start);
						}
					}
					else
					{
						type = field.FieldType;
						start = delim + 1;
						delim = path.IndexOf('.', start);
					}
				}
				else
				{
					return null;
				}
			}

			return type.GetField(path.Substring(start));
		}

		static bool TryGetMinAttribute(FieldInfo field, out float min)
		{
			var a = field.GetCustomAttribute<MinAttribute>();
			if (a != null)
			{
				min = a.min;
				return true;
			}
			else
			{
				min = 0.0f;
				return false;
			}
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
