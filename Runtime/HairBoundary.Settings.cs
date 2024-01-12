using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.DemoTeam.Hair
{
	public partial class HairBoundary : MonoBehaviour
	{
		public enum Axis
		{
			XAxis = 0,
			YAxis = 1,
			ZAxis = 2,
		};

		[Serializable]
		public struct Settings
		{
			public enum Mode
			{
				BindToComponent,
				Standalone,
			}

			public enum Type
			{
				DiscreteSDF,// binds to component -> SDFTexture (requires Unity.DemoTeam.MeshToSDF)
				Capsule,    // binds to component -> CapsuleCollider
				Sphere,     // binds to component -> SphereCollider
				Torus,
				Cube,       // binds to component -> BoxCollider
				Any,
			}

			public Mode mode;
			public Type type;

			public static readonly Settings defaults = new Settings
			{
				mode = Mode.BindToComponent,
				type = Type.Any,
			};
		}

		[Serializable]
		public struct SettingsCapsule
		{
			public Axis direction;
			public float radius;
			public float height;

			public static readonly SettingsCapsule defaults = new SettingsCapsule
			{
				direction = Axis.YAxis,
				radius = 0.5f,
				height = 2.0f,
			};
		}

		[Serializable]
		public struct SettingsSphere
		{
			public float radius;

			public static readonly SettingsSphere defaults = new SettingsSphere
			{
				radius = 0.5f,
			};
		}

		[Serializable]
		public struct SettingsTorus
		{
			public Axis axis;
			public float radiusAxis;
			public float radiusRing;

			public static readonly SettingsTorus defaults = new SettingsTorus
			{
				axis = Axis.YAxis,
				radiusAxis = 0.5f,
				radiusRing = 0.125f,
			};
		}

		[Serializable]
		public struct SettingsCube
		{
			public Vector3 size;

			public static readonly SettingsCube defaults = new SettingsCube
			{
				size = Vector3.one,
			};
		}

		[Serializable]
		public struct SettingsSDF
		{
			public enum SDFSource
			{
				Texture,
				SDFComponent,
			}

			public SDFSource source;
			[VisibleIf(nameof(source), SDFSource.Texture), FormerlySerializedAs("kSDF")]
			public Texture kSDFTexture;
			[VisibleIf(nameof(source), SDFSource.Texture)]
			public Bounds kSDFWorldBounds;
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
			[VisibleIf(nameof(source), SDFSource.SDFComponent)]
			public SDFTexture kSDFComponent;
#endif

			[ToggleGroup, Tooltip("Enable this if the SDF will undergo rigid transformation (e.g. if the entire field will be translated, rotated, or scaled)")]
			public bool kSDFRigidTransform;
			[ToggleGroupItem(withLabel = true), Tooltip("Rigid transform origin -- this is used specifically to improve friction calculations when strands collide with a moving field")]
			public Transform kSDFRigidTransformOrigin;

			public static readonly SettingsSDF defaults = new SettingsSDF
			{
				source = SDFSource.Texture,
				kSDFTexture = null,
				kSDFWorldBounds = new Bounds(Vector3.zero, Vector3.one),
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
				kSDFComponent = null,
#endif
				kSDFRigidTransform = false,
				kSDFRigidTransformOrigin = null,
			};
		}
	}
}
