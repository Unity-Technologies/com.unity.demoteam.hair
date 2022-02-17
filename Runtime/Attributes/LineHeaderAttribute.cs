using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Hair
{
	public class LineHeaderAttribute : PropertyAttribute
	{
		public string label;
		public LineHeaderAttribute(string label = null)
		{
			this.label = label;
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(LineHeaderAttribute))]
	public class LineHeaderAttributeDrawer : DecoratorDrawer
	{
		public override float GetHeight() => HairEditorGUI.lineHeaderHeight;
		public override void OnGUI(Rect position)
		{
			var header = attribute as LineHeaderAttribute;
			{
				HairEditorGUI.LineHeader(position, header.label);
			}
		}
	}
#endif

#if UNITY_EDITOR
	public static partial class HairEditorGUILayout
	{
		public static void LineHeader(string label)
		{
			HairEditorGUI.LineHeader(EditorGUILayout.GetControlRect(), label);
		}
	}

	public static partial class HairEditorGUI
	{
		const float lineHeight = 2.0f;
		const float lineMarginTop = 11.0f;
		const float lineMarginBottom = 4.0f;

		const float labelAlign = 0.05f;
		const float labelMargin = 3.0f;

		public const float lineHeaderHeight = lineHeight + lineMarginTop + lineMarginBottom;

		public static void LineHeader(Rect position, string label)
		{
			if (label == null)
			{
				position = EditorGUI.IndentedRect(position);
				position.y += lineMarginTop;
				position.height = lineHeight;

				var color = GUI.color;
				GUI.color = Color.white;
				GUI.Box(position, "");
				GUI.color = color;
			}
			else
			{
				position = EditorGUI.IndentedRect(position);
				position.y += lineMarginTop;
				position.height = lineHeight;

				var labelSize = EditorStyles.miniLabel.CalcSize(new GUIContent(label));
				var labelWidth = labelSize.x + 2.0f * labelMargin;
				var spacerWidth = position.width - labelWidth;

				var delimL = position.xMin + labelAlign * spacerWidth;
				var delimR = position.xMax - (1.0f - labelAlign) * spacerWidth;

				var posL = position;
				var posC = position;
				var posR = position;

				posL.xMax = delimL;
				posC.xMin = delimL + labelMargin;
				posC.xMax = delimR - labelMargin;
				posC.y -= 0.6f * labelSize.y;
				posC.height = labelSize.y;
				posR.xMin = delimR;

				var color = GUI.color;
				GUI.color = Color.white;
				GUI.Box(posL, "");
				GUI.color = Color.Lerp(Color.white, Color.black, 0.45f);
				GUI.Box(posC, label, EditorStyles.miniLabel);
				GUI.color = Color.white;
				GUI.Box(posR, "");
				GUI.color = color;
			}
		}
	}
#endif
}
