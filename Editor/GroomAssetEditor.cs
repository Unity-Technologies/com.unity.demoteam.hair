using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	[CustomEditor(typeof(GroomAsset))]
	public class GroomAssetEditor : Editor
	{
		Editor meshPreview;

		SerializedProperty type;
		SerializedProperty settingsBasic;
		SerializedProperty settingsAlembic;
		SerializedProperty settingsProcedural;
		SerializedProperty strandGroups;
		SerializedProperty autoBuild;

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
			settingsBasic = serializedObject.FindProperty("settingsBasic");
			settingsAlembic = serializedObject.FindProperty("settingsAlembic");
			settingsProcedural = serializedObject.FindProperty("settingsProcedural");

			type = settingsBasic.FindPropertyRelative("type");

			strandGroups = serializedObject.FindProperty("strandGroups");

			autoBuild = serializedObject.FindProperty("autoBuild");
		}

		public override void OnInspectorGUI()
		{
			var groom = target as GroomAsset;
			if (groom == null)
				return;

			EditorGUILayout.LabelField("Importer", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				EditorGUI.BeginChangeCheck();
				{
					StructPropertyFieldsWithHeader(settingsBasic);

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
				}

				bool settingsChanged = EditorGUI.EndChangeCheck();

				EditorGUILayout.Space();
				EditorGUILayout.BeginHorizontal();
				{
					if (GUILayout.Button("Build strand groups") || (settingsChanged && autoBuild.boolValue))
					{
						GroomAssetBuilder.ClearGroomAsset(groom);
						serializedObject.ApplyModifiedPropertiesWithoutUndo();
						GroomAssetBuilder.BuildGroomAsset(groom);
						serializedObject.Update();

						if (meshPreview != null)
						{
							DestroyImmediate(meshPreview);
							meshPreview = null;
						}
					}

					autoBuild.boolValue = EditorGUILayout.ToggleLeft("Auto", autoBuild.boolValue, GUILayout.Width(50.0f));
				}
				EditorGUILayout.EndHorizontal();
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

				if (meshPreview != null)
				{
					DestroyImmediate(meshPreview);
					meshPreview = null;
				}
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

				EditorGUILayout.LabelField("Totals", EditorStyles.miniBoldLabel);
				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUILayout.IntField("Total groups", groom.strandGroups.Length, EditorStyles.label);
					EditorGUILayout.IntField("Total strands", numStrands, EditorStyles.label);
					EditorGUILayout.IntField("Total particles", numParticles, EditorStyles.label);
				}

				using (new EditorGUI.DisabledGroupScope(true))
				{
					for (int i = 0; i != strandGroups.arraySize; i++)
					{
						EditorGUILayout.Space();
						StructPropertyFieldsWithHeader(strandGroups.GetArrayElementAtIndex(i), "Group " + i);
					}
				}

				EditorGUILayout.Space();

				if (meshPreview == null)
				{
					var meshPreviewTargets = new Mesh[groom.strandGroups.Length];

					for (int i = 0; i != meshPreviewTargets.Length; i++)
					{
						meshPreviewTargets[i] = groom.strandGroups[i].meshAssetLines;
					}

					meshPreview = CreateEditor(meshPreviewTargets);
				}

				meshPreview.DrawPreview(GUILayoutUtility.GetRect(200, 200));
			}
		}
	}
}
