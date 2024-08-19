using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;

#if HAS_PACKAGE_DEMOTEAM_DIGITALHUMAN
using Unity.DemoTeam.DigitalHuman;
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

	using __IMPL__SettingsGeometry = HairSim.SettingsGeometry;

	public partial class HairInstance
	{
		void PerformMigration_1()
		{
			ref var data_IMPL_strandGroupDefaults = ref this.strandGroupDefaults;
			ref var data_IMPL_strandGroupSettings = ref this.strandGroupSettings;

			// prepare data_IMPL_strandGroup*.settingsGeometry
			{
				static void PrepareSettingsGeometry(ref __IMPL__SettingsGeometry out_IMPL)
				{
					out_IMPL.tipScale = false;
					out_IMPL.tipScaleValue = HairAsset.SharedDefaults.defaultTipScale;

					out_IMPL.tipScaleOffset = false;
					out_IMPL.tipScaleOffsetValue = HairAsset.SharedDefaults.defaultTipScaleOffset;
				}

				PrepareSettingsGeometry(ref data_IMPL_strandGroupDefaults.settingsGeometry);

				for (int i = 0; i != (data_IMPL_strandGroupSettings?.Length ?? 0); i++)
				{
					PrepareSettingsGeometry(ref data_IMPL_strandGroupSettings[i].settingsGeometry);
				}
			}
		}
	}

	[Serializable]
	// captured @ a4dedfe5
	struct __1__GroupSettings
	{
		//public List<GroupAssetReference> groupAssetReferences;

		//public SettingsSkinning settingsSkinning;
		//public bool settingsSkinningToggle;

		//public __1__SettingsGeometry settingsGeometry;
		//public bool settingsGeometryToggle;

		//public HairSim.SettingsRendering settingsRendering;
		//public bool settingsRenderingToggle;

		//public HairSim.SettingsPhysics settingsPhysics;
		//public bool settingsPhysicsToggle;

		public static __1__GroupSettings defaults => new __1__GroupSettings()
		{
			//groupAssetReferences = new List<GroupAssetReference>(1),

			//settingsSkinning = SettingsSkinning.defaults,
			//settingsSkinningToggle = false,

			//settingsGeometry = __1__SettingsGeometry.defaults,
			//settingsGeometryToggle = false,

			//settingsRendering = HairSim.SettingsRendering.defaults,
			//settingsRenderingToggle = false,

			//settingsPhysics = HairSim.SettingsPhysics.defaults,
			//settingsPhysicsToggle = false,
		};
	}

	[Serializable]
	// captured @ a4dedfe5
	struct __1__SettingsGeometry
	{
		//public enum StrandScale
		//{
		//	Fixed = 0,
		//	UniformWorldMin = 1,
		//	UniformWorldMax = 2,
		//}

		//public enum BoundsMode
		//{
		//	Automatic = 0,
		//	Manual = 1,
		//}

		//public enum StagingPrecision
		//{
		//	Full = 0,
		//	Half = 1,
		//}

		//public StrandScale strandScale;
		//public bool strandLength;
		//public float strandLengthValue;
		//public bool strandDiameter;
		//public float strandDiameterValue;
		//public float strandSeparation;

		//public BoundsMode boundsMode;
		//public Vector3 boundsCenter;
		//public Vector3 boundsExtent;
		//public bool boundsScale;
		//public float boundsScaleValue;

		//public StagingPrecision stagingPrecision;
		//public bool stagingSubdivision;
		//public uint stagingSubdivisionCount;

		public static readonly __1__SettingsGeometry defaults = new __1__SettingsGeometry()
		{
			//strandScale = StrandScale.Fixed,
			//strandDiameter = false,
			//strandDiameterValue = 1.0f,
			//strandLength = false,
			//strandLengthValue = 1.0f,
			//strandSeparation = 0.0f,

			//boundsMode = BoundsMode.Automatic,
			//boundsCenter = Vector3.zero,
			//boundsExtent = Vector3.one,
			//boundsScale = false,
			//boundsScaleValue = 1.25f,

			//stagingPrecision = StagingPrecision.Half,
			//stagingSubdivision = false,
			//stagingSubdivisionCount = 1,
		};
	}
}
