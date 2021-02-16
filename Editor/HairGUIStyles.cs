using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static class HairGUIStyles
	{
		public static readonly GUIStyle settingsBox;

		public static readonly GUIStyle statusBox;
		public static readonly GUIStyle statusBoxVertical;

		static HairGUIStyles()
		{
			settingsBox = new GUIStyle(EditorStyles.helpBox);

			statusBox = new GUIStyle(GUI.skin.box);
			statusBox.normal.textColor = statusBox.hover.textColor;

			statusBoxVertical = new GUIStyle(EditorStyles.textField);
			statusBoxVertical.font = EditorStyles.miniFont;
			statusBoxVertical.fontSize = 8;
			statusBoxVertical.wordWrap = true;
		}
	}
}
