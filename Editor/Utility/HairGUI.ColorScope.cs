using System;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairGUI
	{
		public enum ColorType
		{
			Color,
			ContentColor,
			BackgroundColor,
			None,
		}

		static Color GetColor(ColorType colorType)
		{
			switch (colorType)
			{
				default:
				case ColorType.Color: return GUI.color;
				case ColorType.ContentColor: return GUI.contentColor;
				case ColorType.BackgroundColor: return GUI.backgroundColor;
			}
		}

		static void SetColor(ColorType colorType, in Color color)
		{
			switch (colorType)
			{
				default:
				case ColorType.Color: GUI.color = color; break;
				case ColorType.ContentColor: GUI.contentColor = color; break;
				case ColorType.BackgroundColor: GUI.backgroundColor = color; break;
				case ColorType.None: break;
			}
		}

		public struct ColorScope : IDisposable
		{
			public Color restoreColor;
			public ColorType restoreColorType;

			public ColorScope(Color color, ColorType colorType = ColorType.Color)
			{
				this.restoreColor = GetColor(colorType);
				this.restoreColorType = colorType;
				SetColor(colorType, color);
			}

			public void Dispose()
			{
				SetColor(restoreColorType, restoreColor);
			}
		}
	}
}
