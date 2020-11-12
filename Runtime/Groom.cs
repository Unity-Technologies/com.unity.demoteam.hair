using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.DemoTeam.Attributes;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Unity.DemoTeam.Hair
{
	public class LineHeaderAttribute : PropertyAttribute { }

	[CustomPropertyDrawer(typeof(LineHeaderAttribute))]
	public class LineHeaderAttributeDrawer : DecoratorDrawer
	{
		public override float GetHeight() => 6.0f;
		public override void OnGUI(Rect position)
		{
			position.yMin += 3.0f;
			position.yMax -= 2.0f;
			EditorGUI.LabelField(position, "", GUI.skin.button);
		}
	}

	public class ToggleGroupItemAttribute : PropertyAttribute
	{
		public bool withLabel;
		public ToggleGroupItemAttribute(bool withLabel = false)
		{
			this.withLabel = withLabel;
		}
	}

	public class ToggleGroupAttribute : PropertyAttribute { }

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(ToggleGroupItemAttribute))]
	public class ToggleGroupItemAttributeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) { }
	}

	[CustomPropertyDrawer(typeof(ToggleGroupAttribute))]
	public class ToggleGroupAttributeDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => -EditorGUIUtility.standardVerticalSpacing;
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			EditorGUILayout.BeginHorizontal();

			if (TryGetTooltipAttribute(fieldInfo, out var tooltip))
			{
				label.tooltip = tooltip;
			}

			property.boolValue = EditorGUILayout.Toggle(label, property.boolValue);

			using (new EditorGUI.DisabledGroupScope(!property.boolValue))
			{
				while (property.Next(false))
				{
					var target = property.serializedObject.targetObject;
					if (target == null)
						continue;

					var field = GetFieldByPropertyPath(target.GetType(), property.propertyPath);
					if (field == null)
						continue;

					var groupItem = field.GetCustomAttribute<ToggleGroupItemAttribute>();
					if (groupItem == null)
						break;

					if (groupItem.withLabel)
					{
						var fieldLabelText = property.displayName;
						if (fieldLabelText.StartsWith(label.text))
							fieldLabelText = fieldLabelText.Substring(label.text.Length);

						TryGetTooltipAttribute(field, out var fieldLabelTooltip);

						var fieldLabel = new GUIContent(fieldLabelText, fieldLabelTooltip);
						var fieldLabelPad = 18.0f;// ...
						var fieldLabelWidth = EditorStyles.label.CalcSize(fieldLabel).x + fieldLabelPad;

						EditorGUILayout.LabelField(fieldLabel, GUILayout.Width(fieldLabelWidth));
					}

					switch (property.propertyType)
					{
						case SerializedPropertyType.Enum:
							{
								var enumValue = (Enum)Enum.GetValues(field.FieldType).GetValue(property.enumValueIndex);
								var enumValueSelected = EditorGUILayout.EnumPopup(enumValue);
								property.enumValueIndex = Convert.ToInt32(enumValueSelected);
							}
							break;

						case SerializedPropertyType.Float:
							{
								if (TryGetRangeAttribute(field, out var min, out var max))
									property.floatValue = EditorGUILayout.Slider(property.floatValue, min, max);
								else
									property.floatValue = EditorGUILayout.FloatField(property.floatValue);
							}
							break;

						case SerializedPropertyType.Integer:
							{
								if (TryGetRangeAttribute(field, out var min, out var max))
									property.intValue = EditorGUILayout.IntSlider(property.intValue, (int)min, (int)max);
								else
									property.intValue = EditorGUILayout.IntField(property.intValue);
							}
							break;

						case SerializedPropertyType.Boolean:
							{
								property.boolValue = EditorGUILayout.ToggleLeft(property.displayName, property.boolValue);
							}
							break;
					}

					if (!groupItem.withLabel)
					{
						if (TryGetTooltipAttribute(field, out var fieldTooltip))
						{
							GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent(string.Empty, fieldTooltip));
						}
					}
				}
			}

			EditorGUILayout.EndHorizontal();
			EditorGUI.EndProperty();
		}

		static FieldInfo GetFieldByPropertyPath(Type type, string path)
		{
			var start = 0;
			var delim = path.IndexOf('.', start);

			while (delim != -1)
			{
				var field = type.GetField(path.Substring(start, delim - start));
				if (field != null)
				{
					type = field.FieldType;
					start = delim + 1;
					delim = path.IndexOf('.', start);
				}
				else
				{
					return null;
				}
			}

			return type.GetField(path.Substring(start));
		}

		static bool TryGetRangeAttribute(FieldInfo field, out float min, out float max)
		{
			var a = field.GetCustomAttribute<RangeAttribute>();
			if (a != null)
			{
				min = a.min;
				max = a.max;
				return true;
			}
			else
			{
				min = 0.0f;
				max = 0.0f;
				return false;
			}
		}

		static bool TryGetTooltipAttribute(FieldInfo field, out string tooltip)
		{
			var a = field.GetCustomAttribute<TooltipAttribute>();
			if (a != null)
			{
				tooltip = a.tooltip;
				return true;
			}
			else
			{
				tooltip = string.Empty;
				return false;
			}
		}
	}
#endif

	[ExecuteAlways, SelectionBase]
	public class Groom : MonoBehaviour
	{
		public static HashSet<Groom> s_instances = new HashSet<Groom>();

		[Serializable]
		public struct GroomContainer
		{
			public GameObject group;

			public MeshFilter rootFilter;
			//public SkinAttachment rootAttachment;

			public MeshFilter lineFilter;
			public MeshRenderer lineRenderer;
			public MaterialPropertyBlock lineRendererMPB;
		}

		[Serializable]
		public struct SettingsRoots
		{
			public GameObject rootsTarget;
			public bool rootsAttached;
		}

		[Serializable]
		public struct SettingsStrands
		{
			public enum Renderer
			{
				PrimitiveLines,
				VFXGraph,
			}

			public enum Scale
			{
				Fixed,
				UniformFromHierarchy,
			}

			public Scale strandScale;
			public Renderer strandRenderer;
			[VisibleIf(nameof(strandRenderer), Renderer.VFXGraph)]
			public GameObject strandOutputGraph;
			public Material strandMaterial;
		}

		public GroomAsset groomAsset;
		public bool groomAssetQuickEdit;

		public GroomContainer[] groomContainers;
		public string groomContainersChecksum;

		public SettingsRoots settingsRoots;
		public SettingsStrands settingsStrands;

		public HairSim.SolverSettings solverSettings = HairSim.SolverSettings.defaults;
		public HairSim.VolumeSettings volumeSettings = HairSim.VolumeSettings.defaults;
		public HairSim.DebugSettings debugSettings = HairSim.DebugSettings.defaults;
		[NonReorderable]
		public List<HairSimBoundary> boundaries = new List<HairSimBoundary>(HairSim.MAX_BOUNDARIES);

		public HairSim.SolverData[] solverData;
		public HairSim.VolumeData volumeData;

		void OnEnable()
		{
			InitializeContainers();

			s_instances.Add(this);
		}

		void OnDisable()
		{
			ReleaseRuntimeData();

			s_instances.Remove(this);
		}

		void OnValidate()
		{
			volumeSettings.volumeGridResolution = (Mathf.Max(8, volumeSettings.volumeGridResolution) / 8) * 8;

			if (boundaries.Count > HairSim.MAX_BOUNDARIES)
			{
				boundaries.RemoveRange(HairSim.MAX_BOUNDARIES, boundaries.Count - HairSim.MAX_BOUNDARIES);
				boundaries.TrimExcess();
			}
		}

		void OnDrawGizmos()
		{
			Gizmos.color = Color.Lerp(Color.white, Color.clear, 0.5f);
			Gizmos.DrawWireCube(HairSim.GetVolumeCenter(volumeData.cbuffer), 2.0f * HairSim.GetVolumeExtent(volumeData.cbuffer));
		}

		void Update()
		{
			InitializeContainers();
		}

		public static Bounds GetRootBounds(MeshFilter rootFilter)
		{
			var rootBounds = rootFilter.sharedMesh.bounds;
			var rootTransform = rootFilter.transform;

			var localCenter = rootBounds.center;
			var localExtent = rootBounds.extents;

			var worldCenter = rootTransform.TransformPoint(localCenter);
			var worldExtent = rootTransform.TransformVector(localExtent);

			worldExtent.x = Mathf.Abs(worldExtent.x);
			worldExtent.y = Mathf.Abs(worldExtent.y);
			worldExtent.z = Mathf.Abs(worldExtent.z);

			return new Bounds(worldCenter, 2.0f * worldExtent);
		}

		public Bounds GetSimulationBounds()
		{
			Debug.Assert(groomAsset != null);
			Debug.Assert(groomAsset.strandGroups != null);

			var scaleFactor = GetSimulationStrandScale();
			var worldBounds = GetRootBounds(groomContainers[0].rootFilter);
			var worldMargin = groomAsset.strandGroups[0].strandLengthMax * scaleFactor;

			for (int i = 1; i != groomContainers.Length; i++)
			{
				worldBounds.Encapsulate(GetRootBounds(groomContainers[i].rootFilter));
				worldMargin = Mathf.Max(groomAsset.strandGroups[i].strandLengthMax * scaleFactor, worldMargin);
			}

			worldMargin *= 1.5f;
			worldBounds.Expand(2.0f * worldMargin);

			return new Bounds(worldBounds.center, worldBounds.size);
		}

		public Bounds GetSimulationBoundsSquare()
		{
			var worldBounds = GetSimulationBounds();
			var worldExtent = worldBounds.extents;

			return new Bounds(worldBounds.center, Vector3.one * (2.0f * Mathf.Max(worldExtent.x, worldExtent.y, worldExtent.z)));
		}

		public float GetSimulationStrandScale()
		{
			switch (settingsStrands.strandScale)
			{
				default:
				case SettingsStrands.Scale.Fixed:
					return 1.0f;

				case SettingsStrands.Scale.UniformFromHierarchy:
					return math.cmin(this.transform.lossyScale);
			}
		}

		public void DispatchStep(CommandBuffer cmd, float dt)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			// apply settings
			var strandScale = GetSimulationStrandScale();
			var volumeBounds = GetSimulationBoundsSquare();

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.UpdateSolverData(ref solverData[i], solverSettings, dt);
				HairSim.UpdateSolverRoots(cmd, groomContainers[i].rootFilter.sharedMesh, groomContainers[i].rootFilter.transform.localToWorldMatrix, solverData[i]);
			}

			volumeSettings.volumeWorldCenter = volumeBounds.center;
			volumeSettings.volumeWorldExtent = volumeBounds.extents;

			HairSim.UpdateVolumeData(ref volumeData, volumeSettings, boundaries);
			//TODO ^^ this needs to happen after stepping solver
			//TODO split boundary data update from volume data update

			// pre-step volume if resolution changed
			if (HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeGridResolution, halfPrecision: false))
			{
				HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
			}

			// perform time step
			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.StepSolverData(cmd, ref solverData[i], solverSettings, volumeData);

				groomContainers[i].lineRenderer.sharedMaterial.CopyPropertiesFromMaterial(groomAsset.settingsBasic.material);
				groomContainers[i].lineRenderer.sharedMaterial.EnableKeyword("HAIRSIMVERTEX_ENABLE_POSITION");

				HairSim.PushSolverData(cmd, groomContainers[i].lineRenderer.sharedMaterial, groomContainers[i].lineRendererMPB, solverData[i]);
			}

			HairSim.StepVolumeData(cmd, ref volumeData, volumeSettings, solverData);
		}

		public void DispatchDraw(CommandBuffer cmd, RTHandle color, RTHandle depth)
		{
			if (!InitializeRuntimeData(cmd))
				return;

			for (int i = 0; i != solverData.Length; i++)
			{
				HairSim.DrawSolverData(cmd, color, depth, solverData[i], debugSettings);
			}

			HairSim.DrawVolumeData(cmd, color, depth, volumeData, debugSettings);
		}

		// when to build runtime data?
		//   1. on object enabled
		//   2. on checksum changed

		// when to clear runtime data?
		//   1. on destroy

		void InitializeContainers()
		{
			if (groomAsset != null)
			{
				if (groomContainersChecksum != groomAsset.checksum)
				{
					GroomBuilder.BuildGroomInstance(this, groomAsset);
					groomContainersChecksum = groomAsset.checksum;

					ReleaseRuntimeData();

					var cmd = CommandBufferPool.Get();
					{
						InitializeRuntimeData(cmd);
						Graphics.ExecuteCommandBuffer(cmd);
						CommandBufferPool.Release(cmd);
					}
				}
			}
			else
			{
				GroomBuilder.ClearGroomInstance(this);
				groomContainersChecksum = string.Empty;

				ReleaseRuntimeData();
			}
		}

		bool InitializeRuntimeData(CommandBuffer cmd)
		{
			if (groomAsset == null)
				return false;

			if (groomAsset.checksum != groomContainersChecksum)
				return false;

			var strandGroups = groomAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return false;

			if (solverData != null && solverData.Length != 0)
				return true;

			solverData = new HairSim.SolverData[strandGroups.Length];

			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var strandGroup = ref strandGroups[i];

				var rootFilter = groomContainers[i].rootFilter;
				var rootTransform = rootFilter.transform.localToWorldMatrix;

				var strandScale = GetSimulationStrandScale();
				var strandTransform = Matrix4x4.TRS(rootFilter.transform.position, rootFilter.transform.rotation, Vector3.one * strandScale);

				HairSim.PrepareSolverData(ref solverData[i], strandGroup.strandCount, strandGroup.strandParticleCount);

				//TODO couple this with a scaling factor
				float strandParticleInterval = strandGroup.strandLengthAvg / (strandGroup.strandParticleCount - 1);

				solverData[i].cbuffer._StrandCount = (uint)strandGroup.strandCount;
				solverData[i].cbuffer._StrandParticleCount = (uint)strandGroup.strandParticleCount;
				solverData[i].cbuffer._StrandParticleInterval = strandParticleInterval * strandScale;

				int particleCount = strandGroup.strandCount * strandGroup.strandParticleCount;

				using (var tmpZero = new NativeArray<Vector4>(particleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpPosition = new NativeArray<Vector4>(particleCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpRootPosition = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				using (var tmpRootDirection = new NativeArray<Vector4>(strandGroup.strandCount, Allocator.Temp, NativeArrayOptions.ClearMemory))
				{
					unsafe
					{
						fixed (void* srcPosition = strandGroup.initialPosition)
						fixed (void* srcRootPosition = strandGroup.initialRootPosition)
						fixed (void* srcRootDirection = strandGroup.initialRootDirection)
						{
							UnsafeUtility.MemCpyStride(tmpPosition.GetUnsafePtr(), sizeof(Vector4), srcPosition, sizeof(Vector3), sizeof(Vector3), particleCount);
							UnsafeUtility.MemCpyStride(tmpRootPosition.GetUnsafePtr(), sizeof(Vector4), srcRootPosition, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
							UnsafeUtility.MemCpyStride(tmpRootDirection.GetUnsafePtr(), sizeof(Vector4), srcRootDirection, sizeof(Vector3), sizeof(Vector3), strandGroup.strandCount);
						}
					}

					solverData[i].length.SetData(strandGroup.initialLength);
					solverData[i].rootPosition.SetData(tmpRootPosition);
					solverData[i].rootDirection.SetData(tmpRootDirection);

					solverData[i].particlePosition.SetData(tmpPosition);
					solverData[i].particlePositionPrev.SetData(tmpPosition);
					solverData[i].particlePositionCorr.SetData(tmpZero);
					solverData[i].particleVelocity.SetData(tmpZero);
					solverData[i].particleVelocityPrev.SetData(tmpZero);
				}

				solverData[i].memoryLayout = strandGroup.memoryLayout;

				HairSim.UpdateSolverData(ref solverData[i], solverSettings, 1.0f);
				HairSim.UpdateSolverRoots(cmd, groomContainers[i].rootFilter.sharedMesh, rootTransform, solverData[i]);
				{
					HairSim.InitSolverParticles(cmd, solverData[i], strandTransform);
				}

				// initialize the renderer
				if (groomContainers[i].lineRendererMPB == null)
					groomContainers[i].lineRendererMPB = new MaterialPropertyBlock();

				groomContainers[i].lineRenderer.sharedMaterial.CopyPropertiesFromMaterial(groomAsset.settingsBasic.material);
				groomContainers[i].lineRenderer.sharedMaterial.EnableKeyword("HAIRSIMVERTEX_ENABLE_POSITION");

				HairSim.PushSolverData(cmd, groomContainers[i].lineRenderer.sharedMaterial, groomContainers[i].lineRendererMPB, solverData[i]);

				groomContainers[i].lineRenderer.SetPropertyBlock(groomContainers[i].lineRendererMPB);
			}

			HairSim.PrepareVolumeData(ref volumeData, volumeSettings.volumeGridResolution, halfPrecision: false);

			return true;
		}

		void ReleaseRuntimeData()
		{
			if (solverData != null)
			{
				for (int i = 0; i != solverData.Length; i++)
				{
					HairSim.ReleaseSolverData(ref solverData[i]);
				}

				solverData = null;
			}

			HairSim.ReleaseVolumeData(ref volumeData);
		}
	}

	//move to GroomEditorUtility ?
	public static class GroomBuilder
	{
		public static void ClearGroomInstance(Groom groom)
		{
			if (groom.groomContainers == null)
				return;

			foreach (var groomContainer in groom.groomContainers)
			{
				if (groomContainer.group != null)
				{
#if UNITY_EDITOR
					GameObject.DestroyImmediate(groomContainer.group);
#else
					GameObject.Destroy(groomContainer.group);
#endif
				}
			}

			groom.groomContainers = null;
		}

		public static void BuildGroomInstance(Groom groom, GroomAsset groomAsset)
		{
			ClearGroomInstance(groom);

			var strandGroups = groomAsset.strandGroups;
			if (strandGroups == null || strandGroups.Length == 0)
				return;

			// prep groom containers
			groom.groomContainers = new Groom.GroomContainer[strandGroups.Length];

			// build groom containers
			for (int i = 0; i != strandGroups.Length; i++)
			{
				ref var groomContainer = ref groom.groomContainers[i];

				var group = new GameObject();
				{
					group.name = "Group:" + i;
					group.transform.SetParent(groom.transform, worldPositionStays: false);
					group.hideFlags = HideFlags.NotEditable;

					var linesContainer = new GameObject();
					{
						linesContainer.name = "Lines:" + i;
						linesContainer.transform.SetParent(group.transform, worldPositionStays: false);
						linesContainer.hideFlags = HideFlags.NotEditable;

						groomContainer.lineFilter = linesContainer.AddComponent<MeshFilter>();
						groomContainer.lineFilter.sharedMesh = strandGroups[i].meshAssetLines;

						groomContainer.lineRenderer = linesContainer.AddComponent<MeshRenderer>();
						groomContainer.lineRenderer.sharedMaterial = new Material(groomAsset.settingsBasic.material);
						groomContainer.lineRenderer.sharedMaterial.hideFlags = HideFlags.NotEditable;
					}

					var rootsContainer = new GameObject();
					{
						rootsContainer.name = "Roots:" + i;
						rootsContainer.transform.SetParent(group.transform, worldPositionStays: false);
						rootsContainer.hideFlags = HideFlags.NotEditable;

						groomContainer.rootFilter = rootsContainer.AddComponent<MeshFilter>();
						groomContainer.rootFilter.sharedMesh = strandGroups[i].meshAssetRoots;

						//TODO
						//groomContainer.rootAttachment = rootObject.AddComponent<SkinAttachment>();
						//groomContainer.rootAttachment = rootAttachment;
					}
				}

				groom.groomContainers[i].group = group;
			}
		}
	}
}
