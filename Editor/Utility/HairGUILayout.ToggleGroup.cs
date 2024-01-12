using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairGUILayout
	{
		const float widthSlider = 100.0f;
		const float widthSpacing = 5.0f;
		const float widthToggle = 14.0f;

		public static void ToggleGroup(FieldInfo fieldInfo, SerializedProperty property, GUIContent label)
		{
			var ev = Event.current;
			var evClick = (ev.type == EventType.MouseDown) || (ev.type == EventType.MouseUp);
			var evClickRight = evClick && (ev.button == 1);

			var groupVisible = fieldInfo.GetCustomAttribute<VisibleIfAttribute>();
			if (groupVisible != null)
			{
				if (ComparePropertyUtility.Evaluate(groupVisible, property) == false)
				{
					return;
				}
			}

			EditorGUILayout.BeginHorizontal();

			var groupLabelPrefix = label.text + " ";
			{
				if (TryGetTooltipAttribute(fieldInfo, out var tooltip))
				{
					label.tooltip = tooltip;
				}
			}

			using (var groupScope = new PropertyRectScope(label, property, EditorGUIUtility.labelWidth + widthToggle + 3.0f, EditorStyles.toggle))
			{
				var toggle = EditorGUI.Toggle(groupScope.position, groupScope.label, property.boolValue);
				if (toggle != property.boolValue)
					property.boolValue = toggle;
			}

			using (new EditorGUI.DisabledScope(property.boolValue == false))
			{
				var storedIndentLevel = EditorGUI.indentLevel;
				EditorGUI.indentLevel = 0;

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

					var groupItem_withTooltip = TryGetTooltipAttribute(field, out var groupItemTooltip);
					if (groupItem.withLabel)
					{
						var groupItemLabel = property.displayName;
						if (groupItemLabel.StartsWith(groupLabelPrefix))
							groupItemLabel = groupItemLabel.Substring(groupLabelPrefix.Length);

						label = new GUIContent(groupItemLabel, groupItemTooltip);
					}
					else
					{
						label = GUIContent.none;
					}

					EditorGUIUtility.labelWidth = GUI.skin.label.CalcSize(label).x;

					var reserveFixed = false;
					var reserveStyle = EditorStyles.layerMaskField;

					var reserveLabel = widthSpacing + EditorGUIUtility.labelWidth;
					var reserveField = widthSpacing + EditorGUIUtility.fieldWidth;
					var reserveExtra = 0.0f;

					var fieldHasRange = false;
					var fieldHasRangeMin = 0.0f;
					var fieldHasRangeMax = 0.0f;
					var fieldHasRenderingLayerMask = false;

					switch (property.propertyType)
					{
						case SerializedPropertyType.Boolean:
							reserveFixed = true;
							reserveField = widthSpacing + widthToggle + (groupItem.withLabel ? -3.0f : 0.0f);
							break;

						case SerializedPropertyType.Integer:
						case SerializedPropertyType.Float:
							if (TryGetRangeAttribute(field, out fieldHasRangeMin, out fieldHasRangeMax))
							{
								fieldHasRange = true;
								reserveExtra = widthSpacing + widthSlider;
							}
							else if (TryGetRenderingLayerMaskAttribute(field))
							{
								fieldHasRenderingLayerMask = true;
							}
							break;
					}

					PropertyRectScope propertyScope;
					{
						var reserveMin = reserveField + (groupItem.withLabel ? reserveLabel : 0.0f);
						var reserveMax = reserveField + reserveLabel + reserveExtra;

						if (reserveFixed)
							propertyScope = new PropertyRectScope(label, property, reserveMin, reserveStyle);
						else
							propertyScope = new PropertyRectScope(label, property, reserveMin, reserveMax, reserveStyle);
					}

					using (propertyScope)
					{
						var position = propertyScope.position.ClipLeft(widthSpacing);
						label = propertyScope.label;

						switch (property.propertyType)
						{
							case SerializedPropertyType.Enum:
								{
									EditorGUI.BeginChangeCheck();

									var enumValueArray = Enum.GetValues(field.FieldType);
									var enumValueIndex = property.enumValueIndex;
									if (enumValueIndex == -1 || enumValueIndex >= enumValueArray.Length)
										enumValueIndex = 0;

									var enumValue = (Enum)enumValueArray.GetValue(enumValueIndex);
									{
										enumValue = EditorGUI.EnumPopup(position, label, enumValue, EditorStyles.popup);
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.enumValueIndex = Array.IndexOf(enumValueArray, enumValue);
									}
								}
								break;

							case SerializedPropertyType.AnimationCurve:
								{
									EditorGUI.BeginChangeCheck();

									var curve = property.animationCurveValue;
									{
										using (new EditorGUI.DisabledScope(evClickRight))
										{
											if (TryGetLinearRampAttribute(field, out var ranges))
												curve = HairGUI.LinearRamp(position, label, curve, ranges);
											else
												curve = EditorGUI.CurveField(position, label, curve);
										}
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.animationCurveValue = curve;
									}
								}
								break;

							case SerializedPropertyType.Float:
								{
									EditorGUI.BeginChangeCheck();

									var value = property.floatValue;
									{
										using (new EditorGUI.DisabledScope(evClickRight))
										{
											if (fieldHasRange)
												value = EditorGUI.Slider(position, label, value, fieldHasRangeMin, fieldHasRangeMax);
											else
												value = EditorGUI.FloatField(position, label, value);
										}
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.floatValue = value;
									}
								}
								break;

							case SerializedPropertyType.Integer:
								{
									EditorGUI.BeginChangeCheck();

									var value = property.intValue;
									{
										using (new EditorGUI.DisabledScope(evClickRight))
										{
											if (fieldHasRange)
												value = EditorGUI.IntSlider(position, label, value, (int)fieldHasRangeMin, (int)fieldHasRangeMax);
											else if (fieldHasRenderingLayerMask)
												value = HairGUI.RenderingLayerMask(position, label, value);
											else
												value = EditorGUI.IntField(position, label, value);
										}
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.intValue = value;
									}
								}
								break;

							case SerializedPropertyType.Boolean:
								{
									EditorGUI.BeginChangeCheck();

									var toggle = property.boolValue;
									{
										toggle = EditorGUI.Toggle(position, label, toggle);
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.boolValue = toggle;
									}
								}
								break;

							case SerializedPropertyType.Vector2:
								{
									EditorGUI.BeginChangeCheck();

									var value = property.vector2Value;
									{
										using (new EditorGUI.DisabledScope(evClickRight))
										{
											value = EditorGUI.Vector2Field(position, label, value);
										}
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.vector2Value = value;
									}
								}
								break;

							case SerializedPropertyType.Vector3:
								{
									EditorGUI.BeginChangeCheck();

									var value = property.vector3Value;
									{
										using (new EditorGUI.DisabledScope(evClickRight))
										{
											value = EditorGUI.Vector3Field(position, label, value);
										}
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.vector3Value = value;
									}
								}
								break;

							case SerializedPropertyType.ObjectReference:
								{
									EditorGUI.BeginChangeCheck();

									var objectReference = property.objectReferenceValue;
									{
										objectReference = EditorGUI.ObjectField(position, label, objectReference, field.FieldType, allowSceneObjects: groupItem.allowSceneObjects);
									}

									if (EditorGUI.EndChangeCheck())
									{
										property.objectReferenceValue = objectReference;
									}
								}
								break;

							case SerializedPropertyType.LayerMask:
								{
									EditorGUI.BeginChangeCheck();

									var concatName = InternalEditorUtility.layers;
									var concatMask = (property.intValue == -1) ? -1 : InternalEditorUtility.LayerMaskToConcatenatedLayersMask(property.intValue);
									{
										concatMask = EditorGUI.MaskField(position, label, concatMask, concatName);
									}

									if (EditorGUI.EndChangeCheck())
									{
										if (concatMask == -1)
											property.intValue = -1;
										else
											property.intValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(concatMask);
									}
								}
								break;

							default:
								Debug.Log("ToggleGroupItem: unsupported property type " + property.propertyType.ToString() + " (at " + property.propertyPath + ")");
								break;
						}

						if (groupItem.withLabel == false && groupItem_withTooltip)
						{
							GUI.Label(position, new GUIContent(string.Empty, groupItemTooltip));
						}
					}

					if (groupItem.withSuffix != null)
					{
						var suffix = new GUIContent(groupItem.withSuffix);
						var suffixWidth = GUI.skin.label.CalcSize(suffix).x;
						var suffixPosition = GUILayoutUtility.GetRect(suffixWidth, EditorGUIUtility.singleLineHeight, reserveStyle, GUILayout.Width(suffixWidth));
						GUI.Label(suffixPosition, suffix);
					}
				}

				EditorGUIUtility.labelWidth = 0;// reset to default
				EditorGUI.indentLevel = storedIndentLevel;
			}

			EditorGUILayout.EndHorizontal();
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

		static bool TryGetAttribute<T>(FieldInfo field, out T attribute) where T : PropertyAttribute
		{
			var a = field.GetCustomAttribute<T>();
			if (a != null)
			{
				attribute = a;
				return true;
			}
			else
			{
				attribute = null;
				return false;
			}
		}

		static bool TryGetMinAttribute(FieldInfo field, out float min)
		{
			if (TryGetAttribute<MinAttribute>(field, out var a))
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
			if (TryGetAttribute<RangeAttribute>(field, out var a))
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
			if (TryGetAttribute<TooltipAttribute>(field, out var a))
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

		static bool TryGetLinearRampAttribute(FieldInfo field, out Rect ranges)
		{
			if (TryGetAttribute<LinearRampAttribute>(field, out var a))
			{
				ranges = a.ranges;
				return true;
			}
			else
			{
				ranges = Rect.zero;
				return false;
			}
		}

		static bool TryGetRenderingLayerMaskAttribute(FieldInfo field)
		{
			return TryGetAttribute<RenderingLayerMaskAttribute>(field, out var a);
		}
	}
}
