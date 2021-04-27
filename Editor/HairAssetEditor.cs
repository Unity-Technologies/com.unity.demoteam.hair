using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	using static HairGUILayout;

	[CustomEditor(typeof(HairAsset))]
	public class HairAssetEditor : Editor
	{
		PreviewRenderUtility previewRenderer;

		Material previewMaterial;
		Vector2 previewAngle;
		float previewZoom;

		ComputeBuffer[] previewBuffers;
		string previewBuffersChecksum;

		SerializedProperty _settingsBasic;
		SerializedProperty _settingsBasic_type;
		SerializedProperty _settingsBasic_material;
		SerializedProperty _settingsAlembic;
		SerializedProperty _settingsProcedural;

		SerializedProperty _strandGroups;
		SerializedProperty _strandGroupsAutoBuild;

		void OnEnable()
		{
			previewMaterial = new Material((target as HairAsset).defaultMaterial);//TODO change this to a RP agnostic default
			previewMaterial.hideFlags = HideFlags.HideAndDontSave;
			previewAngle = Vector2.zero;
			previewZoom = 0.0f;

			previewRenderer = new PreviewRenderUtility();
			previewRenderer.camera.backgroundColor = Color.black;// Color.Lerp(Color.black, Color.grey, 0.5f);
			previewRenderer.camera.nearClipPlane = 0.001f;
			previewRenderer.camera.farClipPlane = 50.0f;
			previewRenderer.camera.fieldOfView = 50.0f;
			previewRenderer.camera.transform.position = Vector3.zero;
			previewRenderer.camera.transform.LookAt(Vector3.forward, Vector3.up);

			previewRenderer.lights[0].transform.SetParent(previewRenderer.camera.transform, worldPositionStays: false);
			previewRenderer.lights[0].transform.localPosition = Vector3.up;
			previewRenderer.lights[0].intensity = 1.5f;

			for (int i = 1; i != previewRenderer.lights.Length; i++)
			{
				previewRenderer.lights[i].enabled = false;
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
			ReleasePreviewBuffers();

			if (previewRenderer != null)
			{
				previewRenderer.Cleanup();
				previewRenderer = null;
			}

			if (previewMaterial != null)
			{
				Material.DestroyImmediate(previewMaterial);
				previewMaterial = null;
			}
		}

		public override bool UseDefaultMargins()
		{
			return false;
		}

		public override void OnInspectorGUI()
		{
			var hairAsset = target as HairAsset;
			if (hairAsset == null)
				return;

			EditorGUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
			{
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
			EditorGUILayout.EndVertical();
		}

		static StructValidation ValidationGUIAlembic(object userData)
		{
#if HAS_PACKAGE_UNITY_ALEMBIC
			return StructValidation.Pass;
#else
			EditorGUILayout.HelpBox("Alembic settings require package 'com.unity.formats.alembic' >= 2.2.0-exp.2", MessageType.Warning, wide: true);
			return StructValidation.Inaccessible;
#endif
		}

		static StructValidation ValidationGUIProcedural(object userData)
		{
			void WarnIfMissingReadable(Texture2D texture, string label)
			{
				if (texture != null && texture.isReadable == false)
				{
					EditorGUILayout.HelpBox(string.Format("Configuration warning: '{0}' map will be ignored as the asset is not marked 'Read/Write'.", label), MessageType.Warning, wide: true);
				}
			}

			var hairAsset = userData as HairAsset;
			if (hairAsset.settingsProcedural.placement == HairAsset.SettingsProcedural.PlacementType.Mesh)
			{
				WarnIfMissingReadable(hairAsset.settingsProcedural.placementDensity, "Placement Density");
				WarnIfMissingReadable(hairAsset.settingsProcedural.paintedDirection, "Painted Direction");
				WarnIfMissingReadable(hairAsset.settingsProcedural.paintedParameters, "Painted Parameters");
			}

			return StructValidation.Pass;
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
						StructPropertyFieldsWithHeader(_settingsAlembic, ValidationGUIAlembic);
						break;
					case HairAsset.Type.Procedural:
						StructPropertyFieldsWithHeader(_settingsProcedural, ValidationGUIProcedural, hairAsset);
						break;
				}
			}

			bool settingsChanged = EditorGUI.EndChangeCheck();

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			{
				var buildNow = GUILayout.Button("Build strand groups");
				var buildAuto = EditorGUILayout.ToggleLeft("Auto", _strandGroupsAutoBuild.boolValue, GUILayout.Width(50.0f));
				var buildAutoDown = buildAuto && (_strandGroupsAutoBuild.boolValue == false);

				_strandGroupsAutoBuild.boolValue = buildAuto;

				if (buildNow || (buildAuto && settingsChanged) || buildAutoDown)
				{
					HairAssetBuilder.ClearHairAsset(hairAsset);
					serializedObject.ApplyModifiedPropertiesWithoutUndo();
					HairAssetBuilder.BuildHairAsset(hairAsset);
					serializedObject.Update();
				}
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

				if (previewBuffersChecksum != hairAsset.checksum)
				{
					InitializePreviewBuffers(hairAsset);
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
								{
									rect.xMin += 1;
									rect.yMin += 1;
									rect.xMax -= 1;
									rect.yMax -= 1;
								}

								// apply zoom
								var e = Event.current;
								if (e.alt && e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
								{
									previewZoom -= 0.05f * e.delta.y;
									previewZoom = Mathf.Clamp01(previewZoom);
									e.Use();
								}

								// apply rotation
								var drag = Vector2.zero;
								if (Drag2D(ref drag, rect))
								{
									drag *= Vector2.one;
								}

								// draw preview
								var meshLines = hairAsset.strandGroups[i].meshAssetLines;
								var meshCenter = hairAsset.strandGroups[i].bounds.center;
								var meshRadius = hairAsset.strandGroups[i].bounds.extents.magnitude * Mathf.Lerp(1.0f, 0.5f, previewZoom);

								var cameraDistance = meshRadius / Mathf.Sin(0.5f * Mathf.Deg2Rad * previewRenderer.cameraFieldOfView);
								var cameraTransform = previewRenderer.camera.transform;
								{
									cameraTransform.Rotate(drag.y, drag.x, 0.0f, Space.Self);
									cameraTransform.position = meshCenter - cameraDistance * cameraTransform.forward;
								}

								var sourceMaterial = _settingsBasic_material.objectReferenceValue as Material;
								if (sourceMaterial == null)
									sourceMaterial = hairAsset.defaultMaterial;

								if (previewMaterial.shader != sourceMaterial.shader)
									previewMaterial.shader = sourceMaterial.shader;

								previewMaterial.CopyPropertiesFromMaterial(sourceMaterial);

								previewMaterial.SetBuffer("_ParticlePosition", previewBuffers[i]);
								previewMaterial.SetInt("_StrandCount", hairAsset.strandGroups[i].strandCount);
								previewMaterial.SetInt("_StrandParticleCount", hairAsset.strandGroups[i].strandParticleCount);
								previewMaterial.SetFloat("_StrandDiameter", 0.01f);
								previewMaterial.SetFloat("_StrandScale", 1.0f);

								if (hairAsset.strandGroups[i].particleMemoryLayout == HairAsset.MemoryLayout.Interleaved)
									previewMaterial.EnableKeyword("LAYOUT_INTERLEAVED");
								else
									previewMaterial.DisableKeyword("LAYOUT_INTERLEAVED");

								previewMaterial.EnableKeyword("HAIR_VERTEX_DYNAMIC");
								previewMaterial.EnableKeyword("HAIR_VERTEX_PREVIEW");

								previewRenderer.BeginPreview(rect, GUIStyle.none);
								previewRenderer.DrawMesh(meshLines, Matrix4x4.identity, previewMaterial, subMeshIndex: 0);
								previewRenderer.Render(true, true);
								previewRenderer.EndAndDrawPreview(rect);
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

		void ReleasePreviewBuffers()
		{
			if (previewBuffers != null)
			{
				for (int i = 0; i != previewBuffers.Length; i++)
				{
					if (previewBuffers[i] != null)
						previewBuffers[i].Release();
				}
			}

			previewBuffers = null;
			previewBuffersChecksum = string.Empty;
		}

		void InitializePreviewBuffers(HairAsset hairAsset)
		{
			ReleasePreviewBuffers();

			if (hairAsset == null || hairAsset.strandGroups == null)
				return;

			unsafe
			{
				previewBuffers = new ComputeBuffer[hairAsset.strandGroups.Length];

				for (int i = 0; i != previewBuffers.Length; i++)
				{
					ref var assetData = ref hairAsset.strandGroups[i].particlePosition;

					using (var previewData = new NativeArray<Vector4>(assetData.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
					{
						unsafe
						{
							fixed (void* sourcePtr = assetData)
							{
								UnsafeUtility.MemCpyStride(previewData.GetUnsafePtr(), sizeof(Vector4), sourcePtr, sizeof(Vector3), sizeof(Vector3), assetData.Length);
							}
						}

						previewBuffers[i] = new ComputeBuffer(assetData.Length, sizeof(Vector4), ComputeBufferType.Default);
						previewBuffers[i].name = "PreviewBuffer:" + i;
						previewBuffers[i].SetData(previewData);
					}
				}

				previewBuffersChecksum = hairAsset.checksum;
			}
		}

		static bool Drag2D(ref Vector2 delta, Rect rect)
		{
			int i = GUIUtility.GetControlID("HairAssetEditor.Drag2D".GetHashCode(), FocusType.Passive);
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
						if (GUIUtility.hotControl == 0)
							GUIUtility.hotControl = i;

						if (GUIUtility.hotControl == i)
						{
							e.Use();
							EditorGUIUtility.SetWantsMouseJumping(1);
							return true;// dragging
						}
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
