using System;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.Mathematics;

namespace Unity.DemoTeam.Hair
{
	public partial class HairBoundary : MonoBehaviour
	{
		static readonly Vector3[] s_normal =
		{
			new Vector3(1.0f, 0.0f, 0.0f),
			new Vector3(0.0f, 1.0f, 0.0f),
			new Vector3(0.0f, 0.0f, 1.0f),
		};

		static readonly Vector3[] s_planar =
		{
			new Vector3(0.0f, 1.0f, 1.0f),
			new Vector3(1.0f, 0.0f, 1.0f),
			new Vector3(1.0f, 1.0f, 0.0f),
		};

		public Settings settings = Settings.defaults;
		public SettingsCapsule settingsCapsule = SettingsCapsule.defaults;
		public SettingsSphere settingsSphere = SettingsSphere.defaults;
		public SettingsTorus settingsTorus = SettingsTorus.defaults;
		public SettingsCube settingsCube = SettingsCube.defaults;
		public SettingsSDF settingsSDF = SettingsSDF.defaults;

		//--------------
		// runtime data

		public struct RuntimeData
		{
			public enum Type
			{
				Shape,
				SDF,
			};

			public Type type;
			public RuntimeTransform xform;
			public RuntimeShape shape;
			public RuntimeSDF sdf;
		}

		public struct RuntimeTransform
		{
			public int handle;
			public Matrix4x4 matrix;
		}

		public struct RuntimeShape
		{
			public enum Type
			{
				Discrete,
				Capsule,
				Sphere,
				Torus,
				Cube,
			};

			public struct Data
			{
				// shape    |  float3     float      float3     float
				// -------------------------------------------------------
				// discrete |  __pad      scale      __pad      __pad
				// capsule  |  centerA    radius     centerB    __pad
				// sphere   |  center     radius     __pad      __pad
				// torus    |  center     radiusA    axis       radiusB
				// cube     |  center     rotf16x    extent     rotf16y

				public Vector3 pA; public float tA;
				public Vector3 pB; public float tB;
			}

			public Type type;
			public Data data;
		}

		public struct RuntimeSDF
		{
			public Texture sdfTexture;
			public float sdfCellSize;
		}

		//--------------------
		// runtime conversion

		public static RuntimeData GetRuntimeCapsule(CapsuleCollider collider) => GetRuntimeCapsule(collider.GetInstanceID(), collider.transform, collider.center, collider.direction, collider.height, collider.radius);
		public static RuntimeData GetRuntimeCapsule(Transform transform, in SettingsCapsule settings) => GetRuntimeCapsule(transform.GetInstanceID(), transform, Vector3.zero, (int)settings.direction, settings.height, settings.radius);
		public static RuntimeData GetRuntimeCapsule(int handle, Transform transform, in Vector3 center, int axis, float height, float radius)
		{
			var lossyScaleAbs = transform.lossyScale.Abs();
			var lossyScaleAbsPlanar = Vector3.Scale(lossyScaleAbs, s_planar[axis]);

			var worldCenter = transform.TransformPoint(center);
			var worldRadius = radius * lossyScaleAbsPlanar.CMax();
			var worldHeight = Mathf.Max(height * lossyScaleAbs[axis], 2.0f * worldRadius);
			var worldExtent = transform.rotation * (s_normal[axis] * (0.5f * worldHeight - worldRadius));

			return new RuntimeData
			{
				type = RuntimeData.Type.Shape,
				xform = new RuntimeTransform
				{
					handle = handle,
					matrix = Matrix4x4.TRS(worldCenter, transform.rotation, s_normal[axis] * worldHeight + s_planar[axis] * (2.0f * worldRadius)),
				},
				shape = new RuntimeShape
				{
					type = RuntimeShape.Type.Capsule,
					data = new RuntimeShape.Data
					{
						pA = worldCenter - worldExtent,
						pB = worldCenter + worldExtent,
						tA = worldRadius,
					},
				},
			};
		}

		public static RuntimeData GetRuntimeSphere(SphereCollider collider) => GetRuntimeSphere(collider.GetInstanceID(), collider.transform, collider.center, collider.radius);
		public static RuntimeData GetRuntimeSphere(Transform transform, in SettingsSphere settings) => GetRuntimeSphere(transform.GetInstanceID(), transform, Vector3.zero, settings.radius);
		public static RuntimeData GetRuntimeSphere(int handle, Transform transform, in Vector3 center, float radius)
		{
			var lossyScaleAbs = transform.lossyScale.Abs();
			var lossyScaleAbsMax = lossyScaleAbs.CMax();

			var worldCenter = transform.TransformPoint(center);
			var worldRadius = radius * lossyScaleAbsMax;

			return new RuntimeData
			{
				type = RuntimeData.Type.Shape,
				xform = new RuntimeTransform
				{
					handle = handle,
					matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one * (2.0f * worldRadius)),
				},
				shape = new RuntimeShape
				{
					type = RuntimeShape.Type.Sphere,
					data = new RuntimeShape.Data
					{
						pA = worldCenter,
						tA = worldRadius,
					},
				},
			};
		}

		public static RuntimeData GetRuntimeTorus(Transform transform, in SettingsTorus settings) => GetRuntimeTorus(transform.GetInstanceID(), transform, Vector3.zero, (int)settings.axis, settings.radiusAxis, settings.radiusRing);
		public static RuntimeData GetRuntimeTorus(int handle, Transform transform, in Vector3 center, int axis, float radiusAxis, float radiusRing)
		{
			var lossyScaleAbs = transform.lossyScale.Abs();
			var lossyScaleAbsMax = lossyScaleAbs.CMax();

			var worldCenter = transform.TransformPoint(center);
			var worldAxis = transform.rotation * s_normal[axis];
			var worldRadiusAxis = radiusAxis * lossyScaleAbsMax;
			var worldRadiusRing = radiusRing * lossyScaleAbsMax;

			return new RuntimeData
			{
				type = RuntimeData.Type.Shape,
				xform = new RuntimeTransform
				{
					handle = handle,
					matrix = Matrix4x4.TRS(worldCenter, transform.rotation, s_planar[axis] * (2.0f * (worldRadiusAxis + worldRadiusRing)) + s_normal[axis] * (2.0f * worldRadiusRing)),
				},
				shape = new RuntimeShape
				{
					type = RuntimeShape.Type.Torus,
					data = new RuntimeShape.Data
					{
						pA = worldCenter,
						pB = worldAxis,
						tA = worldRadiusAxis,
						tB = worldRadiusRing,
					},
				},
			};
		}

		public static RuntimeData GetRuntimeCube(BoxCollider collider) => GetRuntimeCube(collider.GetInstanceID(), collider.transform, collider.center, collider.size);
		public static RuntimeData GetRuntimeCube(Transform transform, in SettingsCube settings) => GetRuntimeCube(transform.GetInstanceID(), transform, Vector3.zero, settings.size);
		public static RuntimeData GetRuntimeCube(int handle, Transform transform, in Vector3 center, in Vector3 size)
		{
			var lossyScaleAbs = transform.lossyScale.Abs();

			var worldCenter = transform.TransformPoint(center);
			var worldSize = Vector3.Scale(size, lossyScaleAbs);
			var worldRot = transform.rotation;

			var rotf16 = math.f32tof16(Quaternion.Inverse(worldRot).ToVector4());
			var rotf16x = rotf16.x | (rotf16.z << 16);
			var rotf16y = rotf16.y | (rotf16.w << 16);

			return new RuntimeData
			{
				type = RuntimeData.Type.Shape,
				xform = new RuntimeTransform
				{
					handle = handle,
					matrix = Matrix4x4.TRS(worldCenter, transform.rotation, worldSize),
				},
				shape = new RuntimeShape
				{
					type = RuntimeShape.Type.Cube,
					data = new RuntimeShape.Data
					{
						pA = worldCenter,
						pB = worldSize * 0.5f,
						tA = Unity.Mathematics.math.asfloat(rotf16x),
						tB = Unity.Mathematics.math.asfloat(rotf16y),
					},
				},
			};
		}

#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
		public static RuntimeData GetRuntimeSDF(SDFTexture sdfComponent) => GetRuntimeSDF(sdfComponent.transform.GetInstanceID(), sdfComponent.transform, sdfComponent.sdf, sdfComponent.voxelBounds.size);
#endif
		public static RuntimeData GetRuntimeSDF(Transform transform, in SettingsSDF settings) => GetRuntimeSDF(transform.GetInstanceID(), transform, settings.kSDFTexture, settings.kSDFWorldSize);
		public static RuntimeData GetRuntimeSDF(int handle, Transform transform, Texture sdfTexture, in Vector3 sdfWorldSize)
		{
			var sdfTextureResolution = Vector3.one;
			{	
				if (sdfTexture is Texture3D)
				{
					var sdfTexture3D = (Texture3D)sdfTexture;
					{
						sdfTextureResolution.x = sdfTexture3D.width;
						sdfTextureResolution.y = sdfTexture3D.height;
						sdfTextureResolution.z = sdfTexture3D.depth;
					}
				}
				else if (sdfTexture is RenderTexture)
				{
					var sdfTexture3D = (RenderTexture)sdfTexture;
					{
						sdfTextureResolution.x = sdfTexture3D.width;
						sdfTextureResolution.y = sdfTexture3D.height;
						sdfTextureResolution.z = Mathf.Max(1, sdfTexture3D.volumeDepth);
					}
				}
			}

			// compute scale factor for euclidean distances within non-uniformly scaled aabb,
			// given aabb initial dimensions (Lx, Ly, Lz) and aabb scale factors (Sx, Sy, Sz):
			// d' = d * cbrt((Lx*Lx*Sx*Sx + Ly*Ly*Sy*Sy + Lz*Lz*Sz*Sz) / (Lx*Lx + Ly*Ly + Lz*Lz))
			var sdfWorldScale = transform.lossyScale.Abs();
			var sdfValueScale = 1.0f;//sdfWorldScale.CMin();
			{
				static float cbrt(float value) => Mathf.Pow(value, 1.0f / 3.0f);

				var LL = sdfWorldSize.CMul(sdfWorldSize);
				var LLsum = LL.CSum();
				if (LLsum > 0.0f)
				{
					var SS = sdfWorldScale.CMul(sdfWorldScale);
					var SSLLsum = SS.CMul(LL).CSum();

					sdfValueScale = cbrt(SSLLsum / LLsum);
				}
			}

			return new RuntimeData
			{
				type = RuntimeData.Type.SDF,
				xform = new RuntimeTransform
				{
					handle = transform.GetInstanceID(),
					matrix = Matrix4x4.TRS(transform.position, transform.rotation, sdfWorldSize.CMul(sdfWorldScale)) * Matrix4x4.Translate(-0.5f * Vector3.one),
				},
				shape = new RuntimeShape
				{
					type = RuntimeShape.Type.Discrete,
					data = new RuntimeShape.Data
					{
						tA = sdfValueScale,
					},
				},
				sdf = new RuntimeSDF()
				{
					sdfTexture = sdfTexture,
					sdfCellSize = sdfWorldSize.CMul(sdfTextureResolution.Rcp()).CMax(),
				},
			};
		}

		//-----------
		// accessors

		public static bool TryGetData(HairBoundary boundary, ref RuntimeData data)
		{
			if (boundary.settings.mode == Settings.Mode.BindToComponent)
			{
				if (TryGetMatchingComponent(boundary, out var component))
				{
					return TryGetComponentData(component, ref data);
				}
			}
			else
			{
				return TryGetStandaloneData(boundary, ref data);
			}
			return false;
		}

		public static bool TryGetMatchingComponent(HairBoundary boundary, Settings.Type type, out Component component)
		{
			static Component FilterSolid(Collider collider) => (collider != null && collider.isTrigger == false) ? collider : null;

			switch (type)
			{
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
				case Settings.Type.DiscreteSDF:
					component = boundary.GetComponent<SDFTexture>(); break;
#endif
				case Settings.Type.Capsule:
					component = FilterSolid(boundary.GetComponent<CapsuleCollider>()); break;
				case Settings.Type.Sphere:
					component = FilterSolid(boundary.GetComponent<SphereCollider>()); break;
				case Settings.Type.Cube:
					component = FilterSolid(boundary.GetComponent<BoxCollider>()); break;
				default:
					component = null; break;
			}
			return (component != null);
		}

		public static bool TryGetMatchingComponent(HairBoundary boundary, out Component component)
		{
			if (boundary.settings.type == Settings.Type.Any)
			{
				return
					TryGetMatchingComponent(boundary, Settings.Type.DiscreteSDF, out component) ||
					TryGetMatchingComponent(boundary, Settings.Type.Capsule, out component) ||
					TryGetMatchingComponent(boundary, Settings.Type.Sphere, out component) ||
					TryGetMatchingComponent(boundary, Settings.Type.Torus, out component) ||
					TryGetMatchingComponent(boundary, Settings.Type.Cube, out component);
			}
			else
			{
				return TryGetMatchingComponent(boundary, boundary.settings.type, out component);
			}
		}

		public static bool TryGetComponentData(Component component, ref RuntimeData data)
		{
			var collider = component as Collider;
			if (collider != null)
			{
				if (collider.isTrigger == false)
				{
					if (component is CapsuleCollider)
					{
						data = GetRuntimeCapsule(component as CapsuleCollider); return true;
					}
					else if (component is SphereCollider)
					{
						data = GetRuntimeSphere(component as SphereCollider); return true;
					}
					else if (component is BoxCollider)
					{
						data = GetRuntimeCube(component as BoxCollider); return true;
					}
				}
			}
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
			else if (component is SDFTexture)
			{
				data = GetRuntimeSDF(component as SDFTexture); return (data.sdf.sdfTexture != null);
			}
#endif
			return false;
		}

		public static bool TryGetStandaloneData(HairBoundary boundary, ref RuntimeData data)
		{
			switch (boundary.settings.type)
			{
				case Settings.Type.DiscreteSDF:
					data = GetRuntimeSDF(boundary.transform, boundary.settingsSDF); return (data.sdf.sdfTexture != null);
				case Settings.Type.Capsule:
					data = GetRuntimeCapsule(boundary.transform, boundary.settingsCapsule); return true;
				case Settings.Type.Sphere:
					data = GetRuntimeSphere(boundary.transform, boundary.settingsSphere); return true;
				case Settings.Type.Torus:
					data = GetRuntimeTorus(boundary.transform, boundary.settingsTorus); return true;
				case Settings.Type.Cube:
					data = GetRuntimeCube(boundary.transform, boundary.settingsCube); return true;
				default:
					return false;
			}
		}
	}
}
