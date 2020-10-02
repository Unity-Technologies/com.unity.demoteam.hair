using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Formats.Alembic.Importer;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

[CustomEditor(typeof(GroomAsset))]
public class GroomAssetEditor : Editor
{
	public override void OnInspectorGUI()
	{
		var groom = target as GroomAsset;
		if (groom != null)
		{
			if (GUILayout.Button("Build strand groups"))
			{
				GroomAssetBuilder.BuildGroomAsset(groom);
			}
		}

		base.OnInspectorGUI();
	}
}

[InitializeOnLoad]
public class GroomInstanceCreator
{
	static GroomInstanceCreator()
	{
		//EditorApplication.ongui
	}
}
