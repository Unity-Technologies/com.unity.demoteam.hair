using UnityEngine;
using Unity.DemoTeam.Attributes;

namespace Unity.DemoTeam.Hair
{
	public class HairBoundarySDF : MonoBehaviour
	{
		public enum BindingType
		{
			Manual,
			SDFTextureComponent,
		}

		public BindingType binding;
		[VisibleIf(nameof(binding), BindingType.Manual)]
		public Texture SDFTexture;
		[VisibleIf(nameof(binding), BindingType.Manual)]
		public Bounds SDFWorldBounds = new Bounds(Vector3.zero, Vector3.one * 2.0f);

		public Transform SDFTransform;

		public Transform GetSDFTransform()
		{
			return SDFTransform;
		}

		public Texture GetSDFTexture()
		{
			switch (binding)
			{
				case BindingType.Manual:
					return SDFTexture;
				case BindingType.SDFTextureComponent:
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
					return this.GetComponent<SDFTexture>()?.sdf;
#else
					break;
#endif
			}
			return null;
		}

		public Matrix4x4 GetSDFLocalToWorld()
		{
			switch (binding)
			{
				case BindingType.Manual:
					{
						return Matrix4x4.TRS(SDFWorldBounds.center, Quaternion.identity, SDFWorldBounds.size);
					}

				case BindingType.SDFTextureComponent:
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
					var sdfComponent = this.GetComponent<SDFTexture>();
					if (sdfComponent != null && sdfComponent.sdf != null)
					{
						return sdfComponent.sdflocalToWorld;
					}
#endif
					break;
			}
			return Matrix4x4.identity;
		}

		public Matrix4x4 GetSDFWorldToUVW()
		{
			switch (binding)
			{
				case BindingType.Manual:
					if (SDFTexture != null)
					{
						var worldSize = SDFWorldBounds.size;
						var worldSizeToUVW = new Vector3(1.0f / worldSize.x, 1.0f / worldSize.y, 1.0f / worldSize.z);
						return Matrix4x4.Scale(worldSizeToUVW) * Matrix4x4.Translate(-SDFWorldBounds.min);
					}
					break;

				case BindingType.SDFTextureComponent:
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
					var sdfComponent = this.GetComponent<SDFTexture>();
					if (sdfComponent != null && sdfComponent.sdf != null)
					{
						return sdfComponent.worldToSDFTexCoords;
					}
#endif
					break;
			}
			return Matrix4x4.identity;
		}

		public float GetSDFWorldCellSize()
		{
			switch (binding)
			{
				case BindingType.Manual:
					if (SDFTexture != null)
					{
						var worldSize = SDFWorldBounds.size;
						var worldCellSize = worldSize / SDFTexture.width;// assume square
						return Mathf.Max(worldCellSize.x, worldCellSize.y, worldCellSize.z);
					}
					break;

				case BindingType.SDFTextureComponent:
#if HAS_PACKAGE_DEMOTEAM_MESHTOSDF
					var sdfComponent = this.GetComponent<SDFTexture>();
					if (sdfComponent != null && sdfComponent.sdf != null)
					{
						return sdfComponent.voxelSize;
					}
#endif
					break;
			}
			return 1e-4f;
		}

		public void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.red;
			Gizmos.matrix = GetSDFLocalToWorld();
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
		}
	}
}
