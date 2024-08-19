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
		[SerializeField, FormerlySerializedAs("settingsAlembic")]
		__1__SettingsAlembic data_1_settingsAlembic = __1__SettingsAlembic.defaults;

		[SerializeField, FormerlySerializedAs("settingsCustom")]
		__1__SettingsCustom data_1_settingsCustom = __1__SettingsCustom.defaults;

		[SerializeField, FormerlySerializedAs("strandGroups")]
		__1__StrandGroup[] data_1_strandGroups;

		void PerformMigration_1()
		{
			ref var data_IMPL_settingsProcedural = ref this.settingsProcedural;
			ref var data_IMPL_settingsAlembic = ref this.settingsAlembic;
			ref var data_IMPL_settingsCustom = ref this.settingsCustom;

			ref var data_IMPL_strandGroups = ref this.strandGroups;

			// prepare data_IMPL_settingsProcedural
			{
				static void PrepareSettingsProcedural(ref __IMPL__SettingsProcedural out_IMPL)
				{
					out_IMPL.tipScaleVariation = false;
					out_IMPL.tipScaleVariationAmount = 0.2f;
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
					out_IMPL.tipScale = __IMPL__SettingsResolve.TipScale.ResolveFromCurves;
				}

				PrepareSettingsResolve(ref data_IMPL_settingsAlembic.settingsResolve);
				PrepareSettingsResolve(ref data_IMPL_settingsCustom.settingsResolve);
			}

			// migrate data_1_settings*.settingsResolve
			{
				// => data_IMPL_settings*.settingsResolve
				{
					static void TransferSettingsResolve(in __1__SettingsResolve in_1, ref __IMPL__SettingsResolve out_IMPL)
					{
						static __IMPL__SettingsResolve.AdditionalData TranslateTransferAttributes(__1__SettingsResolve.TransferAttributes x) => (__IMPL__SettingsResolve.AdditionalData)x;

						out_IMPL.additionalData = in_1.exportAttributes;
						out_IMPL.additionalDataMask = TranslateTransferAttributes(in_1.exportAttributesMask);
					}

					TransferSettingsResolve(data_1_settingsAlembic.settingsResolve, ref data_IMPL_settingsAlembic.settingsResolve);
					TransferSettingsResolve(data_1_settingsCustom.settingsResolve, ref data_IMPL_settingsCustom.settingsResolve);
				}
			}

			// migrate data_1_strandGroups[]
			{
				// => data_IMPL_strandGroups[]
				{
					static void TransferStrandGroup(in __1__StrandGroup in_1, ref __IMPL__StrandGroup out_IMPL, HairAsset hairAsset)
					{
						out_IMPL.strandLengthTotal = in_1.sumStrandLength;
						out_IMPL.strandParamsMax = new Vector4(in_1.maxStrandLength, in_1.maxStrandDiameter, 0.0f, 0.0f);
						out_IMPL.strandParamsAvg = Vector4.zero;
						{
							var maxTipScale = 0.0f;
							var maxTipScaleOffset = 0.0f;

							var sumRootScale = Vector4.zero;
							var sumRootScaleW = 0.0f;

							for (int i = 0; i != out_IMPL.rootScale.Length; i++)
							{
								var tipScale = out_IMPL.rootScale[i].w;
								var tipScaleOffset = out_IMPL.rootScale[i].z;

								maxTipScale = Mathf.Max(maxTipScale, tipScale);
								maxTipScaleOffset = Mathf.Max(maxTipScaleOffset, tipScaleOffset);

								out_IMPL.rootScale[i].z = tipScaleOffset;
								out_IMPL.rootScale[i].w = tipScale;

								sumRootScale += out_IMPL.rootScale[i].x * out_IMPL.rootScale[i];
								sumRootScaleW += out_IMPL.rootScale[i].x;
							}

							out_IMPL.strandParamsMax.z = maxTipScaleOffset;
							out_IMPL.strandParamsMax.w = maxTipScale;

							out_IMPL.strandParamsAvg = out_IMPL.strandParamsMax;
							out_IMPL.strandParamsAvg.Scale(sumRootScale / sumRootScaleW);
							out_IMPL.strandParamsAvg.y = in_1.avgStrandDiameter;
						}
					}

					for (int i = 0; i != (data_IMPL_strandGroups?.Length ?? 0); i++)
					{
						TransferStrandGroup(data_1_strandGroups[i], ref data_IMPL_strandGroups[i], this);
					}
				}
			}
		}

		[Serializable]
		// captured @ a4dedfe5
		public struct __1__SettingsProcedural
		{
			//public enum PlacementMode
			//{
			//	Primitive = 0,
			//	Custom = 1,
			//	Mesh = 2,
			//}

			//public enum PrimitiveType
			//{
			//	Curtain = 0,
			//	Brush = 1,
			//	Cap = 2,
			//	StratifiedCurtain = 3,
			//}

			//[Flags]
			//public enum SubmeshMask
			//{
			//	Submesh0 = 1 << 0,
			//	Submesh1 = 1 << 1,
			//	Submesh2 = 1 << 2,
			//	Submesh3 = 1 << 3,
			//	Submesh4 = 1 << 4,
			//	Submesh5 = 1 << 5,
			//	Submesh6 = 1 << 6,
			//	Submesh7 = 1 << 7,
			//}

			//public enum CurlSamplingStrategy
			//{
			//	RelaxStrandLength = 0,
			//	RelaxCurlSlope = 1,
			//}

			//public PlacementMode placement;
			//public PrimitiveType placementPrimitive;
			//public HairAssetCustomPlacement placementProvider;
			//public Mesh placementMesh;
			//public SubmeshMask placementMeshGroups;
			//public Texture2D mappedDensity;
			//public Texture2D mappedDirection;
			//public Texture2D mappedParameters;

			//public int strandCount;
			//public int strandParticleCount;

			//public float strandLength;
			//public bool strandLengthVariation;
			//public float strandLengthVariationAmount;
			//public float strandDiameter;
			//public bool strandDiameterVariation;
			//public float strandDiameterVariationAmount;
			//public float tipScale;
			//public float tipScaleOffset;

			//public bool curl;
			//public float curlRadius;
			//public float curlSlope;
			//public bool curlVariation;
			//public float curlVariationRadius;
			//public float curlVariationSlope;
			//public CurlSamplingStrategy curlSamplingStrategy;

			public static readonly __1__SettingsProcedural defaults = new __1__SettingsProcedural()
			{
				//placement = PlacementMode.Primitive,
				//placementPrimitive = PrimitiveType.Curtain,
				//placementProvider = null,
				//placementMesh = null,
				//placementMeshGroups = (SubmeshMask)(-1),
				//mappedDensity = null,
				//mappedDirection = null,
				//mappedParameters = null,

				//strandCount = 64,
				//strandParticleCount = 32,

				//strandLength = 0.25f,
				//strandLengthVariation = false,
				//strandLengthVariationAmount = 0.2f,
				//strandDiameter = SharedDefaults.defaultStrandDiameter,
				//strandDiameterVariation = false,
				//strandDiameterVariationAmount = 0.2f,
				//tipScale = SharedDefaults.defaultTipScale,
				//tipScaleOffset = SharedDefaults.defaultTipScaleOffset,

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
		// captured @ a4dedfe5
		struct __1__SettingsAlembic
		{
			//public enum Groups
			//{
			//	Combine = 0,
			//	Preserve = 1,
			//}

#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
			//public AlembicStreamPlayer alembicAsset;
#endif
			//public Groups alembicAssetGroups;

			public __1__SettingsResolve settingsResolve;

			public static readonly __1__SettingsAlembic defaults = new __1__SettingsAlembic()
			{
#if HAS_PACKAGE_UNITY_ALEMBIC && UNITY_EDITOR
				//alembicAsset = null,
#endif
				//alembicAssetGroups = Groups.Combine,

				settingsResolve = __1__SettingsResolve.defaults,
			};
		}

		[Serializable]
		// captured @ a4dedfe5
		struct __1__SettingsCustom
		{
			//public HairAssetCustomData dataProvider;

			public __1__SettingsResolve settingsResolve;

			public static readonly __1__SettingsCustom defaults = new __1__SettingsCustom()
			{
				//dataProvider = null,

				settingsResolve = __1__SettingsResolve.defaults,
			};
		}

		[Serializable]
		// captured @ a4dedfe5
		struct __1__SettingsResolve
		{
			//public enum StrandDiameter
			//{
			//	ResolveFromCurves = 0,
			//	UseFallback = 1,
			//}

			//public enum RootUV
			//{
			//	ResolveFromMesh = 0,
			//	ResolveFromCurves = 1,
			//	UseFallback = 2,
			//}

			[Flags]
			public enum TransferAttributes
			{
				None = 0,
				PerVertexUV = 1 << 1,
				PerVertexWidth = 1 << 0,
				All = PerVertexUV | PerVertexWidth,
			}

			//public bool resampleCurves;
			//public int resampleResolution;
			//public int resampleQuality;

			//public RootUV rootUV;
			//public Mesh rootUVMesh;
			//public Vector2 rootUVFallback;

			//public StrandDiameter strandDiameter;
			//public float strandDiameterScale;
			//public float strandDiameterFallback;
			//public float tipScaleFallback;
			//public float tipScaleFallbackOffset;

			public bool exportAttributes;
			public TransferAttributes exportAttributesMask;

			public static readonly __1__SettingsResolve defaults = new __1__SettingsResolve()
			{
				//resampleCurves = true,
				//resampleResolution = 16,
				//resampleQuality = 3,

				//rootUV = RootUV.UseFallback,
				//rootUVMesh = null,
				//rootUVFallback = Vector2.zero,

				//strandDiameter = StrandDiameter.ResolveFromCurves,
				//strandDiameterScale = 0.01f,
				//strandDiameterFallback = SharedDefaults.defaultStrandDiameter,
				//tipScaleFallback = SharedDefaults.defaultTipScale,
				//tipScaleFallbackOffset = SharedDefaults.defaultTipScaleOffset,

				exportAttributes = false,
				exportAttributesMask = TransferAttributes.All,
			};
		}

		[Serializable]
		// captured @ a4dedfe5
		struct __1__StrandGroup
		{
			//[Flags]
			//public enum ParticleFeatures
			//{
			//	Position = 1 << 0,
			//	TexCoord = 1 << 1,
			//	Diameter = 1 << 2,
			//}

			//public int strandCount;
			//public int strandParticleCount;

			public float sumStrandLength;
			public float maxStrandLength;
			public float maxStrandDiameter;
			public float avgStrandDiameter;

			//public Bounds bounds;

			//public Vector2[] rootUV;
			//public Vector4[] rootScale;

			//public ParticleFeatures particleFeatures;
			//public MemoryLayout particleMemoryLayout;

			//public Vector3[] particlePosition;
			//public Vector2[] particleTexCoord;
			//public float[] particleDiameter;

			//public int lodCount;

			//public int[] lodGuideCount;
			//public int[] lodGuideIndex;
			//public float[] lodGuideCarry;
			//public float[] lodGuideReach;
			//public float[] lodThreshold;

			//public Mesh meshAssetRoots;
		}
	}
}
