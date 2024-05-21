using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.DemoTeam.Hair
{
	public partial class HairBoundary
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
				DiscreteSDF,// can bind to component -> SDFTexture (requires Unity.DemoTeam.MeshToSDF)
				Capsule,    // can bind to component -> CapsuleCollider
				Sphere,     // can bind to component -> SphereCollider
				Torus,
				Cube,       // can bind to component -> BoxCollider
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
			[FormerlySerializedAs("kSDF")]
			public Texture kSDFTexture;
			public Vector3 kSDFWorldSize;

			public static readonly SettingsSDF defaults = new SettingsSDF
			{
				kSDFTexture = null,
				kSDFWorldSize = Vector3.one,
			};
		}
	}
}
