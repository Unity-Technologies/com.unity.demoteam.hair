using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Attributes
{
	public class LineHeaderAttribute : PropertyAttribute
	{
		public string header;
		public LineHeaderAttribute(string header = null)
		{
			this.header = header;
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(LineHeaderAttribute))]
	public class LineHeaderAttributeDrawer : DecoratorDrawer
	{
		const float lineWidth = 2.0f;
		const float paddingTop = 11.0f;
		const float paddingBottom = 4.0f;

		const float headerAlign = 0.05f;
		const float headerMargin = 3.0f;

		public override float GetHeight() => lineWidth + paddingTop + paddingBottom;
		public override void OnGUI(Rect position)
		{
			var attrib = attribute as LineHeaderAttribute;
			if (attrib.header == null)
			{
				position = EditorGUI.IndentedRect(position);
				position.y += paddingTop;
				position.height = lineWidth;

				var color = GUI.color;
				GUI.color = Color.white;
				GUI.Box(position, "");
				GUI.color = color;
			}
			else
			{
				position = EditorGUI.IndentedRect(position);
				position.y += paddingTop;
				position.height = lineWidth;

				var headerSize = EditorStyles.miniLabel.CalcSize(new GUIContent(attrib.header));
				var headerWidth = headerSize.x + 2.0f * headerMargin;
				var spacerWidth = position.width - headerWidth;

				var delimL = position.xMin + headerAlign * spacerWidth;
				var delimR = position.xMax - (1.0f - headerAlign) * spacerWidth;

				var posL = position;
				var posC = position;
				var posR = position;

				posL.xMax = delimL;
				posC.xMin = delimL + headerMargin;
				posC.xMax = delimR - headerMargin;
				posC.y -= 0.6f * headerSize.y;
				posC.height = headerSize.y;
				posR.xMin = delimR;

				var color = GUI.color;
				GUI.color = Color.white;
				GUI.Box(posL, "");
				GUI.color = Color.Lerp(Color.white, Color.black, 0.45f);
				GUI.Box(posC, attrib.header, EditorStyles.miniLabel);
				GUI.color = Color.white;
				GUI.Box(posR, "");
				GUI.color = color;
			}
		}
	}
#endif
}
