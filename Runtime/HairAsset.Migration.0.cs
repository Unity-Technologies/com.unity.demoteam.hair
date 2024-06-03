using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
using UnityEngine.Formats.Alembic.Importer;
#endif

namespace Unity.DemoTeam.Hair
{
	// data migration impl. checklist:
	//
	//	- block renamed: capture and migrate all fields
	//	- field(s) renamed: capture and migrate renamed fields
	//	- field(s) changed: capture and migrate changed fields
	//	- field(s) removed: no action
	//
	// data migration is then a simple two-step process:
	//
	//	1. capture old data into trimmed copies of old structures (via FormerlySerializedAs)
	//	2. migrate old data from trimmed copies
	//
	// (old structures are unfortunately also re-serialized)

	using __1__SettingsProcedural = HairAsset.SettingsProcedural;
	using __1__SettingsAlembic = HairAsset.SettingsAlembic;
	using __1__SettingsCustom = HairAsset.SettingsCustom;
	using __1__SettingsResolve = HairAsset.SettingsResolve;

	using __1__StrandGroup = HairAsset.StrandGroup;

	public partial class HairAsset
	{
		[SerializeField, FormerlySerializedAs("settingsProcedural")]
		__0__SettingsProcedural data_0_settingsProcedural = __0__SettingsProcedural.defaults;

		[SerializeField, FormerlySerializedAs("settingsAlembic")]
		__0__SettingsAlembic data_0_settingsAlembic = __0__SettingsAlembic.defaults;

		[SerializeField, FormerlySerializedAs("settingsCustom")]
		__0__SettingsCustom data_0_settingsCustom = __0__SettingsCustom.defaults;

		[SerializeField, FormerlySerializedAs("strandGroups")]
		__0__StrandGroup[] data_0_strandGroups;

		void PerformMigration_0()
		{
			ref var data_1_settingsProcedural = ref this.settingsProcedural;
			ref var data_1_settingsAlembic = ref this.settingsAlembic;
			ref var data_1_settingsCustom = ref this.settingsCustom;

			ref var data_1_strandGroups = ref this.strandGroups;

			// migrate data_0_settingsProcedural
			{
				ref readonly var in_0 = ref data_0_settingsProcedural;

				// => data_1_settingsProcedural
				{
					static void TransferSettingsProcedural(in __0__SettingsProcedural in_0, ref __1__SettingsProcedural out_1)
					{
						static __1__SettingsProcedural.SubmeshMask TranslateSubmeshMask(__0__SettingsProcedural.SubmeshMask x) => (__1__SettingsProcedural.SubmeshMask)x;

						out_1.placementProvider = in_0.placementProvider;
						out_1.placementMeshGroups = TranslateSubmeshMask(in_0.placementMeshGroups);
						out_1.mappedDensity = in_0.mappedDensity;
						out_1.mappedDirection = in_0.mappedDirection;
						out_1.mappedParameters = in_0.mappedParameters;

						out_1.tipScale = HairAsset.SharedDefaults.defaultTipScale;
						out_1.tipScaleOffset = HairAsset.SharedDefaults.defaultTipScaleOffset;
					}

					TransferSettingsProcedural(in_0, ref data_1_settingsProcedural);
				}
			}

			// migrate data_0_settings*.settingsResolve
			{
				// => data_1_settings*.settingsResolve
				{
					static void TransferSettingsResolve(in __0__SettingsResolve in_0, ref __1__SettingsResolve out_1)
					{
						static __1__SettingsResolve.RootUV TranslateRootUV(__0__SettingsResolve.RootUV x)
						{
							switch (x)
							{
								case __0__SettingsResolve.RootUV.ResolveFromMesh: return __1__SettingsResolve.RootUV.ResolveFromMesh;
								case __0__SettingsResolve.RootUV.ResolveFromCurveUV: return __1__SettingsResolve.RootUV.ResolveFromCurves;
								default:
								case __0__SettingsResolve.RootUV.Uniform: return __1__SettingsResolve.RootUV.UseFallback;
							}
						}

						out_1.resampleResolution = in_0.resampleParticleCount;

						out_1.rootUV = TranslateRootUV(in_0.rootUV);
						out_1.rootUVFallback = in_0.rootUVConstant;

						out_1.strandDiameter = __1__SettingsResolve.StrandDiameter.UseFallback;
						out_1.strandDiameterScale = 0.01f;
						out_1.strandDiameterFallback = HairAsset.SharedDefaults.defaultStrandDiameter;
						out_1.tipScaleFallback = HairAsset.SharedDefaults.defaultTipScale;
						out_1.tipScaleFallbackOffset = HairAsset.SharedDefaults.defaultTipScaleOffset;

						out_1.exportAttributes = false;
					}

					TransferSettingsResolve(data_0_settingsAlembic.settingsResolve, ref data_1_settingsAlembic.settingsResolve);
					TransferSettingsResolve(data_0_settingsCustom.settingsResolve, ref data_1_settingsCustom.settingsResolve);
				}
			}

			// migrate data_0_settingsAlembic
			{
				ref readonly var in_0 = ref data_0_settingsAlembic;

				// => data_1_settingsAlembic.settingsResolve
				{
					static void TransferSettingsAlembic(in __0__SettingsAlembic in_0, ref __1__SettingsResolve out_1)
					{
						static __1__SettingsResolve.RootUV TranslateRootUV(__0__SettingsResolve.RootUV x)
						{
							switch (x)
							{
								case __0__SettingsResolve.RootUV.ResolveFromMesh: return __1__SettingsResolve.RootUV.ResolveFromMesh;
								case __0__SettingsResolve.RootUV.ResolveFromCurveUV: return __1__SettingsResolve.RootUV.ResolveFromCurves;
								default:
								case __0__SettingsResolve.RootUV.Uniform: return __1__SettingsResolve.RootUV.UseFallback;
							}
						}

						if (in_0.OLD__transferred == false)
						{
							out_1.rootUV = TranslateRootUV(in_0.OLD__rootUV);
							out_1.rootUVFallback = in_0.OLD__rootUVConstant;
							out_1.rootUVMesh = in_0.OLD__rootUVMesh;

							out_1.resampleCurves = in_0.OLD__resampleCurves;
							out_1.resampleResolution = in_0.OLD__resampleParticleCount;
							out_1.resampleQuality = in_0.OLD__resampleQuality;
						}
					}

					TransferSettingsAlembic(in_0, ref data_1_settingsAlembic.settingsResolve);
				}
			}

			// migrate data_0_strandGroups[]
			{
				// => data_1_strandGroups[]
				{
					static void TransferStrandGroup(in __0__StrandGroup in_0, ref __1__StrandGroup out_1, HairAsset hairAsset)
					{
						out_1.sumStrandLength = in_0.totalLength;
						out_1.avgStrandDiameter = HairAsset.SharedDefaults.defaultStrandDiameter * 0.001f;
						out_1.maxStrandDiameter = HairAsset.SharedDefaults.defaultStrandDiameter * 0.001f;

						out_1.rootScale = new Vector4[in_0.rootScale.Length];
						{
							for (int i = 0; i != out_1.rootScale.Length; i++)
							{
								out_1.rootScale[i].x = in_0.rootScale[i];
								out_1.rootScale[i].y = 1.0f;
								out_1.rootScale[i].z = HairAsset.SharedDefaults.defaultTipScaleOffset;
								out_1.rootScale[i].w = 1.0f;
							}
						}

						out_1.particleTexCoord = null;
						out_1.particleDiameter = null;
						out_1.particleFeatures = __1__StrandGroup.ParticleFeatures.Position;

						// ensure valid lod data (note: only meant as placeholder until asset is rebuilt)
						{
							if (out_1.lodCount == 0)
							{
								out_1.lodCount = 1;
								out_1.lodGuideCount = new int[1] { out_1.strandCount };
								out_1.lodGuideIndex = new int[out_1.strandCount];
								out_1.lodThreshold = new float[1] { 1.0f };
								for (int i = 0; i != out_1.lodGuideIndex.Length; i++)
								{
									out_1.lodGuideIndex[i] = i;
								}
							}

							if ((out_1.lodGuideCarry?.Length ?? 0) == 0)
							{
								out_1.lodGuideCarry = new float[out_1.lodGuideIndex.Length];
								for (int i = 0; i != out_1.lodGuideCarry.Length; i++)
								{
									out_1.lodGuideCarry[i] = 1.0f;
								}
							}

							if ((out_1.lodGuideReach?.Length ?? 0) == 0)
							{
								out_1.lodGuideReach = new float[out_1.lodGuideCarry.Length];
								for (int i = 0; i != out_1.lodGuideReach.Length; i++)
								{
									out_1.lodGuideReach[i] = 0.0f;
								}
							}
						}

						// ensure valid mesh assets
						/*REMOVED
						{
							static void ValidateRenderMesh(__1__StrandGroup out_1, HairAsset hairAsset, ref Mesh meshAsset, HairInstanceBuilder.FnCreateRenderMesh fnCreateRenderMesh)
							{
								var prevName = (meshAsset != null) ? meshAsset.name : null;
								var prevHideFlags = (meshAsset != null ? meshAsset.hideFlags : HideFlags.HideInHierarchy);

								var meshAssetObsolete = (meshAsset != null && (meshAsset.GetVertexAttributeFormat(VertexAttribute.TexCoord0) == VertexAttributeFormat.Float32));
								if (meshAssetObsolete)
								{
#if UNITY_EDITOR
									UnityEditor.AssetDatabase.RemoveObjectFromAsset(meshAsset);
									UnityEngine.Object.DestroyImmediate(meshAsset);
#endif
									meshAsset = null;
								}

								if (meshAsset == null)
								{
									meshAsset = fnCreateRenderMesh(prevHideFlags, out_1.particleMemoryLayout, out_1.strandCount, out_1.strandParticleCount, out_1.bounds);
									meshAsset.name = prevName ?? meshAsset.name;
#if UNITY_EDITOR
									UnityEditor.AssetDatabase.AddObjectToAsset(meshAsset, hairAsset);
#endif
								}
							}

							ValidateRenderMesh(out_1, hairAsset, ref out_1.meshAssetLines, HairInstanceBuilder.CreateRenderMeshLines);
							ValidateRenderMesh(out_1, hairAsset, ref out_1.meshAssetStrips, HairInstanceBuilder.CreateRenderMeshStrips);
							ValidateRenderMesh(out_1, hairAsset, ref out_1.meshAssetTubes, HairInstanceBuilder.CreateRenderMeshTubes);
						}
						*/
					}

					for (int i = 0; i != (data_1_strandGroups?.Length ?? 0); i++)
					{
						TransferStrandGroup(data_0_strandGroups[i], ref data_1_strandGroups[i], this);
					}
				}
			}
		}

		[Serializable]
		struct __0__SettingsProcedural
		{
			[Flags]
			public enum SubmeshMask
			{
				Submesh0 = 1 << 0,
				Submesh1 = 1 << 1,
				Submesh2 = 1 << 2,
				Submesh3 = 1 << 3,
				Submesh4 = 1 << 4,
				Submesh5 = 1 << 5,
				Submesh6 = 1 << 6,
				Submesh7 = 1 << 7,
			}

			[FormerlySerializedAs("placementGenerator")]
			public HairAssetCustomPlacement placementProvider;
			[FormerlySerializedAs("placementMeshInclude")]
			public SubmeshMask placementMeshGroups;
			[FormerlySerializedAs("placementDensity")]
			public Texture2D mappedDensity;
			[FormerlySerializedAs("paintedDirection")]
			public Texture2D mappedDirection;
			[FormerlySerializedAs("paintedParameters")]
			public Texture2D mappedParameters;

			public static readonly __0__SettingsProcedural defaults = new __0__SettingsProcedural()
			{
				placementProvider = null,
				placementMeshGroups = (SubmeshMask)(-1),
				mappedDensity = null,
				mappedDirection = null,
				mappedParameters = null,
			};
		}

		[Serializable]
		struct __0__SettingsAlembic
		{
			public __0__SettingsResolve settingsResolve;

			public bool OLD__transferred;
			[FormerlySerializedAs("rootUV")]
			public __0__SettingsResolve.RootUV OLD__rootUV;
			[FormerlySerializedAs("rootUVConstant")]
			public Vector2 OLD__rootUVConstant;
			[FormerlySerializedAs("rootUVMesh")]
			public Mesh OLD__rootUVMesh;
			[FormerlySerializedAs("resampleCurves")]
			public bool OLD__resampleCurves;
			[FormerlySerializedAs("resampleParticleCount")]
			public int OLD__resampleParticleCount;
			[FormerlySerializedAs("resampleQuality")]
			public int OLD__resampleQuality;

			public static readonly __0__SettingsAlembic defaults = new __0__SettingsAlembic()
			{
				settingsResolve = __0__SettingsResolve.defaults,
			};
		}

		[Serializable]
		struct __0__SettingsCustom
		{
			public __0__SettingsResolve settingsResolve;

			public static readonly __0__SettingsCustom defaults = new __0__SettingsCustom()
			{
				settingsResolve = __0__SettingsResolve.defaults,
			};
		}

		[Serializable]
		struct __0__SettingsResolve
		{
			public enum RootUV
			{
				Uniform,
				ResolveFromMesh,
				ResolveFromCurveUV,
			}

			public RootUV rootUV;
			public Vector2 rootUVConstant;

			public int resampleParticleCount;

			public static readonly __0__SettingsResolve defaults = new __0__SettingsResolve()
			{
				rootUV = RootUV.Uniform,
				rootUVConstant = Vector2.zero,

				resampleParticleCount = 16,
			};
		}

		[Serializable]
		struct __0__StrandGroup
		{
			//public int strandCount;
			//public int strandParticleCount;

			//public float maxStrandLength;
			//public float maxParticleInterval;

			public float totalLength;

			//public Bounds bounds;

			//public Vector2[] rootUV;
			public float[] rootScale;
			//public Vector3[] rootPosition;
			//public Vector3[] rootDirection;

			//public Vector3[] particlePosition;
			//public MemoryLayout particleMemoryLayout;

			//public int lodCount;
			//public int[] lodGuideCount;
			//public int[] lodGuideIndex;
			//public float[] lodGuideCarry;
			//public float[] lodThreshold;

			//public Mesh meshAssetRoots;
			//public Mesh meshAssetLines;
			//public Mesh meshAssetStrips;
			//public Mesh meshAssetTubes;

			//public int version;
		}
	}
}
