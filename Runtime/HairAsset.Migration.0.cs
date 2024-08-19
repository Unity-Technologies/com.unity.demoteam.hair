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

	using __IMPL__SettingsProcedural = HairAsset.SettingsProcedural;
	using __IMPL__SettingsAlembic = HairAsset.SettingsAlembic;
	using __IMPL__SettingsCustom = HairAsset.SettingsCustom;
	using __IMPL__SettingsResolve = HairAsset.SettingsResolve;

	using __IMPL__StrandGroup = HairAsset.StrandGroup;

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
			ref var data_IMPL_settingsProcedural = ref this.settingsProcedural;
			ref var data_IMPL_settingsAlembic = ref this.settingsAlembic;
			ref var data_IMPL_settingsCustom = ref this.settingsCustom;

			ref var data_IMPL_strandGroups = ref this.strandGroups;

			// prepare data_IMPL_settingsProcedural
			{
				static void PrepareSettingsProcedural(ref __IMPL__SettingsProcedural out_IMPL)
				{
					out_IMPL.strandDiameter = HairAsset.SharedDefaults.defaultStrandDiameter;
					out_IMPL.strandDiameterVariation = false;
					out_IMPL.strandDiameterVariationAmount = 0.2f;

					out_IMPL.tipScale = 1.0f;
					out_IMPL.tipScaleVariation = false;
					out_IMPL.tipScaleVariationAmount = 0.2f;
					out_IMPL.tipScaleOffset = HairAsset.SharedDefaults.defaultTipScaleOffset;
					out_IMPL.tipScaleOffsetVariation = false;
					out_IMPL.tipScaleOffsetVariationAmount = 0.2f;
				}

				PrepareSettingsProcedural(ref data_IMPL_settingsProcedural);
			}

			// prepare data_IMPL_settingsAlembic
			{
				static void PrepareSettingsAlembic(ref __IMPL__SettingsAlembic out_IMPL)
				{
					out_IMPL.alembicScalePositions = __IMPL__SettingsAlembic.SourceUnit.DataInMeters;
					out_IMPL.alembicScaleDiameters = __IMPL__SettingsAlembic.SourceUnit.DataInCentimeters;
				}

				PrepareSettingsAlembic(ref data_IMPL_settingsAlembic);
			}

			// prepare data_IMPL_settings*.settingsResolve
			{
				static void PrepareSettingsResolve(ref __IMPL__SettingsResolve out_IMPL)
				{
					out_IMPL.strandDiameter = __IMPL__SettingsResolve.StrandDiameter.UseFallback;
					out_IMPL.strandDiameterFallback = 1.0f;

					out_IMPL.tipScale = __IMPL__SettingsResolve.TipScale.UseFallback;
					out_IMPL.tipScaleFallback = 1.0f;
					out_IMPL.tipScaleFallbackOffset = HairAsset.SharedDefaults.defaultTipScaleOffset;

					out_IMPL.additionalData = false;
					out_IMPL.additionalDataMask = __IMPL__SettingsResolve.AdditionalData.All;
				}

				PrepareSettingsResolve(ref data_IMPL_settingsAlembic.settingsResolve);
				PrepareSettingsResolve(ref data_IMPL_settingsCustom.settingsResolve);
			}

			// migrate data_0_settingsProcedural
			{
				ref readonly var in_0 = ref data_0_settingsProcedural;

				// => data_IMPL_settingsProcedural
				{
					static void TransferSettingsProcedural(in __0__SettingsProcedural in_0, ref __IMPL__SettingsProcedural out_IMPL)
					{
						static __IMPL__SettingsProcedural.SubmeshMask TranslateSubmeshMask(__0__SettingsProcedural.SubmeshMask x) => (__IMPL__SettingsProcedural.SubmeshMask)x;

						out_IMPL.placementProvider = in_0.placementProvider;
						out_IMPL.placementMeshGroups = TranslateSubmeshMask(in_0.placementMeshGroups);
						out_IMPL.mappedDensity = in_0.mappedDensity;
						out_IMPL.mappedDirection = in_0.mappedDirection;
						out_IMPL.mappedParameters = in_0.mappedParameters;
					}

					TransferSettingsProcedural(in_0, ref data_IMPL_settingsProcedural);
				}
			}

			// migrate data_0_settings*.settingsResolve
			{
				// => data_IMPL_settings*.settingsResolve
				{
					static void TransferSettingsResolve(in __0__SettingsResolve in_0, ref __IMPL__SettingsResolve out_IMPL)
					{
						static __IMPL__SettingsResolve.RootUV TranslateRootUV(__0__SettingsResolve.RootUV x)
						{
							switch (x)
							{
								case __0__SettingsResolve.RootUV.ResolveFromMesh: return __IMPL__SettingsResolve.RootUV.ResolveFromMesh;
								case __0__SettingsResolve.RootUV.ResolveFromCurveUV: return __IMPL__SettingsResolve.RootUV.ResolveFromCurves;
								default:
								case __0__SettingsResolve.RootUV.Uniform: return __IMPL__SettingsResolve.RootUV.UseFallback;
							}
						}

						out_IMPL.resampleResolution = in_0.resampleParticleCount;

						out_IMPL.rootUV = TranslateRootUV(in_0.rootUV);
						out_IMPL.rootUVFallback = in_0.rootUVConstant;
					}

					TransferSettingsResolve(data_0_settingsAlembic.settingsResolve, ref data_IMPL_settingsAlembic.settingsResolve);
					TransferSettingsResolve(data_0_settingsCustom.settingsResolve, ref data_IMPL_settingsCustom.settingsResolve);
				}
			}

			// migrate data_0_settingsAlembic
			{
				ref readonly var in_0 = ref data_0_settingsAlembic;

				// => data_IMPL_settingsAlembic.settingsResolve
				{
					static void TransferSettingsAlembic(in __0__SettingsAlembic in_0, ref __IMPL__SettingsResolve out_IMPL)
					{
						static __IMPL__SettingsResolve.RootUV TranslateRootUV(__0__SettingsResolve.RootUV x)
						{
							switch (x)
							{
								case __0__SettingsResolve.RootUV.ResolveFromMesh: return __IMPL__SettingsResolve.RootUV.ResolveFromMesh;
								case __0__SettingsResolve.RootUV.ResolveFromCurveUV: return __IMPL__SettingsResolve.RootUV.ResolveFromCurves;
								default:
								case __0__SettingsResolve.RootUV.Uniform: return __IMPL__SettingsResolve.RootUV.UseFallback;
							}
						}

						if (in_0.OLD__transferred == false)
						{
							out_IMPL.rootUV = TranslateRootUV(in_0.OLD__rootUV);
							out_IMPL.rootUVFallback = in_0.OLD__rootUVConstant;
							out_IMPL.rootUVMesh = in_0.OLD__rootUVMesh;

							out_IMPL.resampleCurves = in_0.OLD__resampleCurves;
							out_IMPL.resampleResolution = in_0.OLD__resampleParticleCount;
							out_IMPL.resampleQuality = in_0.OLD__resampleQuality;
						}
					}

					TransferSettingsAlembic(in_0, ref data_IMPL_settingsAlembic.settingsResolve);
				}
			}

			// migrate data_0_strandGroups[]
			{
				// => data_IMPL_strandGroups[]
				{
					static void TransferStrandGroup(in __0__StrandGroup in_0, ref __IMPL__StrandGroup out_IMPL, HairAsset hairAsset)
					{
						out_IMPL.strandLengthTotal = in_0.totalLength;
						out_IMPL.strandParamsMax = new Vector4(in_0.maxStrandLength, 1.0f * 0.001f, 1.0f, 1.0f);
						out_IMPL.strandParamsAvg = new Vector4(in_0.maxStrandLength, 1.0f * 0.001f, 1.0f, 1.0f);

						out_IMPL.rootScale = new Vector4[in_0.rootScale.Length];
						{
							var sumRootScale = 0.0f;

							for (int i = 0; i != out_IMPL.rootScale.Length; i++)
							{
								out_IMPL.rootScale[i].x = in_0.rootScale[i];
								out_IMPL.rootScale[i].y = 1.0f;
								out_IMPL.rootScale[i].z = HairAsset.SharedDefaults.defaultTipScaleOffset;
								out_IMPL.rootScale[i].w = 1.0f;

								sumRootScale += in_0.rootScale[i];
							}

							out_IMPL.strandParamsAvg.x = out_IMPL.strandParamsMax.x * (sumRootScale / out_IMPL.rootScale.Length);
						}

						out_IMPL.particleTexCoord = null;
						out_IMPL.particleDiameter = null;
						out_IMPL.particleFeatures = __IMPL__StrandGroup.ParticleFeatures.Position;

						// ensure valid lod data (note: only meant as placeholder until asset is rebuilt)
						{
							if (out_IMPL.lodCount == 0)
							{
								out_IMPL.lodCount = 1;
								out_IMPL.lodGuideCount = new int[1] { out_IMPL.strandCount };
								out_IMPL.lodGuideIndex = new int[out_IMPL.strandCount];
								out_IMPL.lodThreshold = new float[1] { 1.0f };
								for (int i = 0; i != out_IMPL.lodGuideIndex.Length; i++)
								{
									out_IMPL.lodGuideIndex[i] = i;
								}
							}

							//TODO impl
							if ((out_IMPL.lodGuideCarry?.Length ?? 0) == 0)
							{
								out_IMPL.lodGuideCarry = new float[out_IMPL.lodGuideIndex.Length];
								for (int i = 0; i != out_IMPL.lodGuideCarry.Length; i++)
								{
									out_IMPL.lodGuideCarry[i] = 1.0f;
								}
							}

							//TODO impl
							if ((out_IMPL.lodGuideReach?.Length ?? 0) == 0)
							{
								out_IMPL.lodGuideReach = new float[out_IMPL.lodGuideCarry.Length];
								for (int i = 0; i != out_IMPL.lodGuideReach.Length; i++)
								{
									out_IMPL.lodGuideReach[i] = 0.0f;
								}
							}
						}

						// ensure valid mesh assets
						/*REMOVED
						{
							static void ValidateRenderMesh(__IMPL__StrandGroup out_IMPL, HairAsset hairAsset, ref Mesh meshAsset, HairInstanceBuilder.FnCreateRenderMesh fnCreateRenderMesh)
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
									meshAsset = fnCreateRenderMesh(prevHideFlags, out_IMPL.particleMemoryLayout, out_IMPL.strandCount, out_IMPL.strandParticleCount, out_IMPL.bounds);
									meshAsset.name = prevName ?? meshAsset.name;
#if UNITY_EDITOR
									UnityEditor.AssetDatabase.AddObjectToAsset(meshAsset, hairAsset);
#endif
								}
							}

							ValidateRenderMesh(out_IMPL, hairAsset, ref out_IMPL.meshAssetLines, HairInstanceBuilder.CreateRenderMeshLines);
							ValidateRenderMesh(out_IMPL, hairAsset, ref out_IMPL.meshAssetStrips, HairInstanceBuilder.CreateRenderMeshStrips);
							ValidateRenderMesh(out_IMPL, hairAsset, ref out_IMPL.meshAssetTubes, HairInstanceBuilder.CreateRenderMeshTubes);
						}
						*/
					}

					for (int i = 0; i != (data_IMPL_strandGroups?.Length ?? 0); i++)
					{
						TransferStrandGroup(data_0_strandGroups[i], ref data_IMPL_strandGroups[i], this);
					}
				}
			}
		}

		[Serializable]
		// captured @ 46a8b132
		struct __0__SettingsProcedural
		{
			//public enum PlacementType
			//{
			//	Primitive,
			//	Custom,
			//	Mesh,
			//}

			//public enum PrimitiveType
			//{
			//	Curtain,
			//	Brush,
			//	Cap,
			//	StratifiedCurtain,
			//}

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

			//public enum CurlSamplingStrategy
			//{
			//	RelaxStrandLength,
			//	RelaxCurlSlope,
			//}

			//public PlacementType placement;
			//public PrimitiveType placementPrimitive;
			[FormerlySerializedAs("placementGenerator")]
			public HairAssetCustomPlacement placementProvider;
			//public Mesh placementMesh;
			[FormerlySerializedAs("placementMeshInclude")]
			public SubmeshMask placementMeshGroups;
			[FormerlySerializedAs("placementDensity")]
			public Texture2D mappedDensity;
			[FormerlySerializedAs("paintedDirection")]
			public Texture2D mappedDirection;
			[FormerlySerializedAs("paintedParameters")]
			public Texture2D mappedParameters;

			//public int strandCount;
			//public int strandParticleCount;
			//public float strandLength;
			//public bool strandLengthVariation;
			//public float strandLengthVariationAmount;

			//public bool curl;
			//public float curlRadius;
			//public float curlSlope;
			//public bool curlVariation;
			//public float curlVariationRadius;
			//public float curlVariationSlope;
			//public CurlSamplingStrategy curlSamplingStrategy;

			public static readonly __0__SettingsProcedural defaults = new __0__SettingsProcedural()
			{
				//placement = PlacementType.Primitive,
				//placementPrimitive = PrimitiveType.Curtain,
				placementProvider = null,
				//placementMesh = null,
				placementMeshGroups = (SubmeshMask)(-1),
				mappedDensity = null,
				mappedDirection = null,
				mappedParameters = null,

				//strandCount = 64,
				//strandParticleCount = 32,

				//strandLength = 0.25f,
				//strandLengthVariation = false,
				//strandLengthVariationAmount = 0.2f,

				//curl = false,
				//curlRadius = 1.0f,
				//curlSlope = 0.3f,
				//curlVariation = false,
				//curlVariationRadius = 0.1f,
				//curlVariationSlope = 0.3f,
				//curlSamplingStrategy = CurlSamplingStrategy.RelaxStrandLength,
			};
		}

		[Serializable]
		// captured @ 46a8b132
		struct __0__SettingsAlembic
		{
			//public enum Groups
			//{
			//	Combine,
			//	Preserve,
			//}

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
			//public AlembicStreamPlayer alembicAsset;
#endif
			//public Groups alembicAssetGroups;

			public __0__SettingsResolve settingsResolve;

			public static readonly __0__SettingsAlembic defaults = new __0__SettingsAlembic()
			{
#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
				//alembicAsset = null,
#endif
				//alembicAssetGroups = Groups.Combine,

				settingsResolve = __0__SettingsResolve.defaults,
			};

			[FormerlySerializedAs("rootUV")] public __0__SettingsResolve.RootUV OLD__rootUV;
			[FormerlySerializedAs("rootUVConstant")] public Vector2 OLD__rootUVConstant;
			[FormerlySerializedAs("rootUVMesh")] public Mesh OLD__rootUVMesh;
			[FormerlySerializedAs("resampleCurves")] public bool OLD__resampleCurves;
			[FormerlySerializedAs("resampleParticleCount")] public int OLD__resampleParticleCount;
			[FormerlySerializedAs("resampleQuality")] public int OLD__resampleQuality;
			public bool OLD__transferred;
		}

		[Serializable]
		// captured @ 46a8b132
		struct __0__SettingsCustom
		{
			//public HairAssetCustomData dataProvider;

			public __0__SettingsResolve settingsResolve;

			public static readonly __0__SettingsCustom defaults = new __0__SettingsCustom()
			{
				//dataProvider = null,

				settingsResolve = __0__SettingsResolve.defaults,
			};
		}

		[Serializable]
		// captured @ 46a8b132
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
			//public Mesh rootUVMesh;

			//public bool resampleCurves;
			public int resampleParticleCount;
			//public int resampleQuality;

			public static readonly __0__SettingsResolve defaults = new __0__SettingsResolve()
			{
				rootUV = RootUV.Uniform,
				rootUVConstant = Vector2.zero,
				//rootUVMesh = null,

				//resampleCurves = true,
				resampleParticleCount = 16,
				//resampleQuality = 1,
			};
		}

		[Serializable]
		// captured @ 46a8b132
		struct __0__StrandGroup
		{
			//public int strandCount;
			//public int strandParticleCount;

			public float maxStrandLength;
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
