using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	using static HairGUILayout;

	[CustomEditor(typeof(HairAsset))]
	public class HairAssetEditor : Editor
	{
		static Material s_previewMat;

		PreviewRenderUtility previewUtil;
		MaterialPropertyBlock previewUtilMPB;
		Vector2 previewDrag;
		float previewZoom;

		SerializedProperty _settingsBasic;
		SerializedProperty _settingsBasic_type;
		SerializedProperty _settingsBasic_material;
		SerializedProperty _settingsAlembic;
		SerializedProperty _settingsProcedural;

		SerializedProperty _strandGroups;
		SerializedProperty _strandGroupsAutoBuild;

		void OnEnable()
		{
			if (previewUtil != null)
				previewUtil.Cleanup();

			previewUtil = new PreviewRenderUtility();
			previewUtilMPB = new MaterialPropertyBlock();
			previewDrag = Vector2.zero;

			previewUtil.camera.backgroundColor = Color.black;// Color.Lerp(Color.black, Color.grey, 0.5f);
			previewUtil.camera.nearClipPlane = 0.001f;
			previewUtil.camera.farClipPlane = 50.0f;
			previewUtil.camera.fieldOfView = 50.0f;
			previewUtil.camera.transform.position = Vector3.zero;
			previewUtil.camera.transform.LookAt(Vector3.forward, Vector3.up);

			previewUtil.lights[0].transform.position = Vector3.zero + Vector3.up;
			previewUtil.lights[0].intensity = 4.0f;

			for (int i = 1; i != previewUtil.lights.Length; i++)
			{
				previewUtil.lights[i].enabled = false;
			}

			_settingsBasic = serializedObject.FindProperty("settingsBasic");
			_settingsBasic_type = _settingsBasic.FindPropertyRelative("type");
			_settingsBasic_material = _settingsBasic.FindPropertyRelative("material");
			_settingsAlembic = serializedObject.FindProperty("settingsAlembic");
			_settingsProcedural = serializedObject.FindProperty("settingsProcedural");

			_strandGroups = serializedObject.FindProperty("strandGroups");
			_strandGroupsAutoBuild = serializedObject.FindProperty("strandGroupsAutoBuild");
		}

		void OnDisable()
		{
			previewUtil.Cleanup();
		}

		public override void OnInspectorGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			EditorGUILayout.LabelField("Importer", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
			{
				DrawImporterGUI();
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Strand Groups", EditorStyles.centeredGreyMiniLabel);
			EditorGUILayout.BeginVertical(HairGUIStyles.settingsBox);
			{
				DrawStrandGroupsGUI();
			}
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField(hairAsset.checksum, EditorStyles.centeredGreyMiniLabel);
		}

		public void DrawImporterGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			EditorGUI.BeginChangeCheck();
			{
				StructPropertyFieldsWithHeader(_settingsBasic);

				if (_settingsBasic_material.objectReferenceValue == null)
					_settingsBasic_material.objectReferenceValue = hairAsset.defaultMaterial;

				EditorGUILayout.Space();

				switch ((HairAsset.Type)_settingsBasic_type.enumValueIndex)
				{
					case HairAsset.Type.Alembic:
						StructPropertyFieldsWithHeader(_settingsAlembic);
						break;
					case HairAsset.Type.Procedural:
						StructPropertyFieldsWithHeader(_settingsProcedural);
						break;
				}
			}

			bool settingsChanged = EditorGUI.EndChangeCheck();

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			{
				if (GUILayout.Button("Build strand groups") || (settingsChanged && _strandGroupsAutoBuild.boolValue))
				{
					HairAssetBuilder.ClearHairAsset(hairAsset);
					serializedObject.ApplyModifiedPropertiesWithoutUndo();
					HairAssetBuilder.BuildHairAsset(hairAsset);
					serializedObject.Update();
				}

				_strandGroupsAutoBuild.boolValue = EditorGUILayout.ToggleLeft("Auto", _strandGroupsAutoBuild.boolValue, GUILayout.Width(50.0f));
			}
			EditorGUILayout.EndHorizontal();
		}

		public void DrawStrandGroupsGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			if (hairAsset.strandGroups == null || hairAsset.strandGroups.Length == 0)
			{
				EditorGUILayout.LabelField("None");
			}
			else
			{
				int numStrands = 0;
				int numParticles = 0;

				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					numStrands += hairAsset.strandGroups[i].strandCount;
					numParticles += hairAsset.strandGroups[i].strandCount * hairAsset.strandGroups[i].strandParticleCount;
				}

				EditorGUILayout.LabelField("Summary", EditorStyles.miniBoldLabel);
				using (new EditorGUI.IndentLevelScope())
				using (new EditorGUI.DisabledScope(true))
				{
					EditorGUILayout.IntField("Groups", hairAsset.strandGroups.Length);
					EditorGUILayout.IntField("Total strands", numStrands);
					EditorGUILayout.IntField("Total particles", numParticles);
				}

				for (int i = 0; i != hairAsset.strandGroups.Length; i++)
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Group:" + i, EditorStyles.miniBoldLabel);
					using (new EditorGUI.IndentLevelScope())
					{
						EditorGUILayout.BeginVertical();
						{
							var rect = GUILayoutUtility.GetAspectRect(16.0f / 9.0f, EditorStyles.helpBox, GUILayout.MaxHeight(400.0f));
							if (rect.width >= 20.0f)
							{
								rect = EditorGUI.IndentedRect(rect);

								GUI.Box(rect, Texture2D.blackTexture, EditorStyles.textField);

								rect.xMin += 1;
								rect.yMin += 1;
								rect.xMax -= 1;
								rect.yMax -= 1;

								var e = Event.current;
								if (e.alt && e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
								{
									previewZoom -= 0.05f * e.delta.y;
									previewZoom = Mathf.Clamp01(previewZoom);
									e.Use();

									GUI.changed = true;
								}

								if (Drag2D(ref previewDrag, rect))
								{
									GUI.changed = true;
								}

								var meshRoots = hairAsset.strandGroups[i].meshAssetRoots;
								var meshLines = hairAsset.strandGroups[i].meshAssetLines;
								var meshCenter = meshLines.bounds.center;
								var meshRadius = meshLines.bounds.extents.magnitude * Mathf.Lerp(1.0f, 0.5f, previewZoom);

								var modelDistance = meshRadius / Mathf.Sin(0.5f * Mathf.Deg2Rad * previewUtil.cameraFieldOfView);
								var modelRotation = Quaternion.Euler(-previewDrag.y, 0.0f, 0.0f) * Quaternion.Euler(0.0f, -previewDrag.x, 0.0f);
								var modelMatrix = Matrix4x4.TRS(modelDistance * Vector3.forward, modelRotation, Vector3.one) * Matrix4x4.Translate(-meshCenter);

								var material = _settingsBasic_material.objectReferenceValue as Material;
								if (material == null)
									material = hairAsset.defaultMaterial;

								previewUtilMPB.SetInt("_StrandCount", hairAsset.strandGroups[i].strandCount);

								previewUtil.BeginPreview(rect, GUIStyle.none);
								previewUtil.DrawMesh(meshLines, modelMatrix, material, 0, previewUtilMPB);
								previewUtil.Render(true, true);
								previewUtil.EndAndDrawPreview(rect);
							}
						}
						EditorGUILayout.EndVertical();

						using (new EditorGUI.DisabledScope(true))
						{
							StructPropertyFields(_strandGroups.GetArrayElementAtIndex(i));
						}
					}
				}

				EditorGUILayout.Space();
				using (new EditorGUI.DisabledScope(true))
				{
					if (GUILayout.Button("Save changes"))
					{
						//TODO
					}
				}
			}
		}

		static bool Drag2D(ref Vector2 delta, Rect rect)
		{
			int i = GUIUtility.GetControlID("HairAsset.Drag2D".GetHashCode(), FocusType.Passive);
			var e = Event.current;

			switch (e.GetTypeForControl(i))
			{
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == i)
					{
						delta += e.delta;
						e.Use();
						return true;// dragging
					}
					break;

				case EventType.MouseDown:
					if (rect.Contains(e.mousePosition))
					{
						GUIUtility.hotControl = i;
						e.Use();
						EditorGUIUtility.SetWantsMouseJumping(1);
						return true;// dragging
					}
					break;

				case EventType.MouseUp:
					if (GUIUtility.hotControl == i)
						GUIUtility.hotControl = 0;

					EditorGUIUtility.SetWantsMouseJumping(0);
					break;
			}

			return false;// not dragging
		}
	}
}
