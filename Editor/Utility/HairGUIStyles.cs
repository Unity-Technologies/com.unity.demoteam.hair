using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairGUIStyles
	{
		public static readonly GUIStyle settingsBox;
		public static readonly GUIStyle settingsHeader;
		public static readonly GUIStyle settingsFoldout;

		public static readonly GUIStyle statusBox;
		public static readonly GUIStyle statusBoxVertical;

		public static readonly GUIContent miniButtonIconEdit;
		public static readonly GUIContent miniButtonIconAdd;
		public static readonly GUIContent miniButtonIconSub;

		public static readonly GUIContent miniButtonIconPlay;
		public static readonly GUIContent miniButtonIconPause;
		public static readonly GUIContent miniButtonIconRewind;

		static HairGUIStyles()
		{
			settingsBox = new GUIStyle(EditorStyles.helpBox);

			settingsHeader = new GUIStyle(EditorStyles.toolbar);
			//settingsHeader.normal.background = Texture2D.redTexture;

			settingsFoldout = new GUIStyle(EditorStyles.foldout);
			settingsFoldout.padding.left += 1;
			settingsFoldout.margin.left += 9;
			settingsFoldout.margin.top += 1;
			settingsFoldout.font = EditorStyles.miniBoldLabel.font;
			settingsFoldout.fontSize = EditorStyles.miniBoldLabel.fontSize;
			settingsFoldout.fontStyle = EditorStyles.miniBoldLabel.fontStyle;

			statusBox = new GUIStyle(GUI.skin.box);
			statusBox.normal.textColor = statusBox.hover.textColor;

			statusBoxVertical = new GUIStyle(EditorStyles.textField);
			statusBoxVertical.font = EditorStyles.miniFont;
			statusBoxVertical.fontSize = 8;
			statusBoxVertical.wordWrap = true;

			miniButtonIconEdit = EditorGUIUtility.IconContent("editicon.sml");
			miniButtonIconAdd = new GUIContent("+");// EditorGUIUtility.IconContent("Toolbar Plus");
			miniButtonIconSub = new GUIContent("-");// EditorGUIUtility.IconContent("Toolbar Minus");

			miniButtonIconPlay = EditorGUIUtility.IconContent("Animation.Play");
			miniButtonIconPause = EditorGUIUtility.IconContent("PauseButton");
			miniButtonIconRewind = EditorGUIUtility.IconContent("Animation.PrevKey");
		}
	}
}
