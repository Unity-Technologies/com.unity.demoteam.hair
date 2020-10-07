using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GroomAsset))]
public class GroomAssetEditor : Editor
{
	Editor meshPreview;

	SerializedProperty type;
	SerializedProperty settings;
	SerializedProperty settingsAlembic;
	SerializedProperty settingsProcedural;
	SerializedProperty strandGroups;

	void StructPropertyFields(SerializedProperty settings)
	{
		if (settings.hasChildren)
		{
			var itSiblings = settings.Copy();
			var itChildren = settings.Copy();

			itSiblings.Next(enterChildren: false);

			if (itChildren.NextVisible(enterChildren: true))
			{
				EditorGUILayout.PropertyField(itChildren, includeChildren: true);

				while (itChildren.NextVisible(enterChildren: false))
				{
					if (SerializedProperty.EqualContents(itSiblings, itChildren))
						break;

					EditorGUILayout.PropertyField(itChildren, includeChildren: true);
				}
			}
		}
	}

	void StructPropertyFieldsWithHeader(SerializedProperty settings, string label)
	{
		EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
		using (new EditorGUI.IndentLevelScope())
		{
			StructPropertyFields(settings);
		}
	}

	void StructPropertyFieldsWithHeader(SerializedProperty settings)
	{
		StructPropertyFieldsWithHeader(settings, settings.displayName);
	}

	private void OnEnable()
	{
		settings = serializedObject.FindProperty("settings");
		settingsAlembic = serializedObject.FindProperty("settingsAlembic");
		settingsProcedural = serializedObject.FindProperty("settingsProcedural");

		type = settings.FindPropertyRelative("type");

		strandGroups = serializedObject.FindProperty("strandGroups");
	}

	public override void OnInspectorGUI()
	{
		var groom = target as GroomAsset;
		if (groom == null)
			return;

		EditorGUILayout.LabelField("Importer", EditorStyles.centeredGreyMiniLabel);
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		{
			StructPropertyFieldsWithHeader(settings);

			EditorGUILayout.Space();

			switch ((GroomAsset.Type)type.enumValueIndex)
			{
				case GroomAsset.Type.Alembic:
					StructPropertyFieldsWithHeader(settingsAlembic);
					break;
				case GroomAsset.Type.Procedural:
					StructPropertyFieldsWithHeader(settingsProcedural);
					break;
			}

			EditorGUILayout.Space();

			if (GUILayout.Button("Build strand groups"))
			{
				GroomAssetBuilder.ClearGroomAsset(groom);
				serializedObject.ApplyModifiedPropertiesWithoutUndo();
				GroomAssetBuilder.BuildGroomAsset(groom);
				serializedObject.Update();

				meshPreview = null;
			}
		}
		EditorGUILayout.EndVertical();
		EditorGUILayout.Space();

		EditorGUILayout.LabelField("Strand groups", EditorStyles.centeredGreyMiniLabel);
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		{
			DrawGroups(groom);
		}
		EditorGUILayout.EndVertical();
	}

	public void DrawGroups(GroomAsset groom)
	{
		if (groom.strandGroups == null || groom.strandGroups.Length == 0)
		{
			EditorGUILayout.LabelField("None");

			meshPreview = null;
		}
		else
		{
			int numStrands = 0;
			int numParticles = 0;

			for (int i = 0; i != groom.strandGroups.Length; i++)
			{
				numStrands += groom.strandGroups[i].strandCount;
				numParticles += groom.strandGroups[i].strandCount * groom.strandGroups[i].strandParticleCount;
			}

			EditorGUILayout.TextField("Checksum", groom.checksum.ToString(), EditorStyles.label);
			EditorGUILayout.IntField("Total groups", groom.strandGroups.Length, EditorStyles.label);
			EditorGUILayout.IntField("Total strands", numStrands, EditorStyles.label);
			EditorGUILayout.IntField("Total particles", numParticles, EditorStyles.label);

			for (int i = 0; i != strandGroups.arraySize; i++)
			{
				EditorGUILayout.Space();
				StructPropertyFieldsWithHeader(strandGroups.GetArrayElementAtIndex(i), "Group " + i);
			}

			EditorGUILayout.Space();

			if (meshPreview == null)
			{
				var meshPreviewTargets = new Mesh[groom.strandGroups.Length];

				for (int i = 0; i != meshPreviewTargets.Length; i++)
					meshPreviewTargets[i] = groom.strandGroups[i].meshAssetLines;

				meshPreview = CreateEditor(meshPreviewTargets);
			}

			meshPreview.DrawPreview(GUILayoutUtility.GetRect(150, 150));
		}
	}
}
