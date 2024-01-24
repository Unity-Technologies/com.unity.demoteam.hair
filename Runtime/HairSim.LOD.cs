using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairSim
	{
		private static Camera[] s_managedCameras = new Camera[128];
		private static Plane[] s_managedFrustum = new Plane[6];
			
		[GenerateHLSL(needAccessors = false)]
		public struct LODFrustum
		{
			public Vector3 cameraPosition;
			public Vector3 cameraForward;
			public float cameraNear;
			public float unitSpanSubpixelDepth;

			public Vector4 plane0;
			public Vector4 plane1;
			public Vector4 plane2;
			public Vector4 plane3;
			public Vector4 plane4;
			public Vector4 plane5;
		};

		[GenerateHLSL(needAccessors = false)]
		public struct LODBounds
		{
			public Vector3 center;
			public Vector3 extent;
			public float radius;
			public float reach;
		}

		[GenerateHLSL(needAccessors = false)]
		public struct LODGeometry
		{
			public float maxParticleDiameter;
			public float maxParticleInterval;
		}

		[GenerateHLSL(needAccessors = false)]
		public struct LODIndices
		{
			public uint lodIndexLo;
			public uint lodIndexHi;
			public float lodBlendFrac;
			public float lodValue;
		};

		public static NativeList<LODFrustum> AcquireLODFrustums(CameraType cameraType, Allocator allocator)
		{
			var lodFrustums = new NativeList<LODFrustum>(Conf.MAX_FRUSTUMS, allocator);
			{
				MakeLODFrustums(cameraType, ref lodFrustums);
			}

			if (lodFrustums.Length > Conf.MAX_FRUSTUMS)
			{
				lodFrustums.Resize(Conf.MAX_FRUSTUMS, NativeArrayOptions.UninitializedMemory);
			}

			return lodFrustums;
		}

		public static int MakeLODFrustums(CameraType cameraType, ref NativeList<LODFrustum> lodFrustums)
		{
			lodFrustums.Clear();

			// enumerate non-scene view cameras
			if ((cameraType & ~CameraType.SceneView) != 0)
			{
				var n = Camera.GetAllCameras(s_managedCameras);
				for (int i = 0; i != n; i++)
				{
					var camera = s_managedCameras[i];
					if (camera != null && camera.isActiveAndEnabled && cameraType.HasFlag(camera.cameraType))
					{
						lodFrustums.Add(MakeLODFrustum(camera));
					}

					s_managedCameras[i] = null;
				}
			}

			// enumerate scene view cameras
			if (cameraType.HasFlag(CameraType.SceneView))
			{
#if UNITY_EDITOR
				var sceneViews = UnityEditor.SceneView.sceneViews;
				for (int i = 0; i != sceneViews.Count; i++)
				{
					var sceneView = sceneViews[i] as UnityEditor.SceneView;
					if (sceneView != null)
					{
						var camera = sceneView.camera;
						if (camera != null)
						{
							lodFrustums.Add(MakeLODFrustum(camera));
						}
					}
				}
#endif
			}

			return lodFrustums.Length;
		}

		public static LODFrustum MakeLODFrustum(Camera camera)
		{
			var cameraPosition = camera.transform.position;
			var cameraForward = camera.transform.forward;
			var cameraOrtho = camera.orthographic;
			var cameraNear = camera.nearClipPlane;
			var cameraFar = camera.farClipPlane;

			GeometryUtility.CalculateFrustumPlanes(camera, s_managedFrustum);
			{
				var forwardDistance = Vector3.Dot(cameraForward, cameraPosition);

				// guard NaN near
				if (s_managedFrustum[4].IsNaN())
					s_managedFrustum[4] = cameraForward.ToPlane(-(forwardDistance + cameraNear));

				// guard NaN far
				if (s_managedFrustum[5].IsNaN())
					s_managedFrustum[5] = (-cameraForward).ToPlane(forwardDistance + cameraFar);
			}

			var unitDepthScreenSpan = 2.0f * (cameraOrtho ? camera.orthographicSize : Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.GetGateFittedFieldOfView()));
			var unitDepthPixelCount = camera.scaledPixelHeight;
			var unitSpanSubpixelDepth = unitDepthPixelCount / unitDepthScreenSpan;

			// NOTE: we use these values in LODFrustumCoverage to compute pixel coverage for a given sample as follows:
			//
			//		float sampleDepth = max(dot(cameraForward, samplePosition - cameraPosition), cameraNear);
			//		float sampleSpanSubpixelDepth = sampleSpan * unitSpanSubpixelDepth;
			//		float sampleCoverage = sampleSpanSubpixelDepth / sampleDepth;
			//
			// also: for orthographic cameras we need to take depth out of the equation, so in that case we force:
			//
			//		float cameraForward	= 0;
			//		float cameraNear = 1;
			//	=>	float sampleDepth = 1;

			return new LODFrustum
			{
				cameraPosition = cameraPosition,
				cameraForward = cameraOrtho ? Vector3.zero : cameraForward,
				cameraNear = cameraOrtho ? 1.0f : cameraNear,
				unitSpanSubpixelDepth = unitSpanSubpixelDepth,

				plane0 = s_managedFrustum[0].ToVector4(),
				plane1 = s_managedFrustum[1].ToVector4(),
				plane2 = s_managedFrustum[2].ToVector4(),
				plane3 = s_managedFrustum[3].ToVector4(),
				plane4 = s_managedFrustum[4].ToVector4(),
				plane5 = s_managedFrustum[5].ToVector4(),
			};
		}

		public static bool LODFrustumContains(in LODFrustum lodFrustum, in LODBounds lodBounds)
		{
			// see: https://fgiesen.wordpress.com/2010/10/17/view-frustum-culling/
			return (
				Vector3.Dot(lodBounds.center + lodBounds.extent.CMul(lodFrustum.plane0.Sign()), lodFrustum.plane0) > -lodFrustum.plane0.w &&
				Vector3.Dot(lodBounds.center + lodBounds.extent.CMul(lodFrustum.plane1.Sign()), lodFrustum.plane1) > -lodFrustum.plane1.w &&
				Vector3.Dot(lodBounds.center + lodBounds.extent.CMul(lodFrustum.plane2.Sign()), lodFrustum.plane2) > -lodFrustum.plane2.w &&
				Vector3.Dot(lodBounds.center + lodBounds.extent.CMul(lodFrustum.plane3.Sign()), lodFrustum.plane3) > -lodFrustum.plane3.w &&
				Vector3.Dot(lodBounds.center + lodBounds.extent.CMul(lodFrustum.plane4.Sign()), lodFrustum.plane4) > -lodFrustum.plane4.w &&
				Vector3.Dot(lodBounds.center + lodBounds.extent.CMul(lodFrustum.plane5.Sign()), lodFrustum.plane5) > -lodFrustum.plane5.w
			);
		}

		public static float LODFrustumCoverage(in LODFrustum lodFrustum, float sampleDepth, float sampleSpan)
		{
			var sampleSpanSubpixelDepth = sampleSpan * lodFrustum.unitSpanSubpixelDepth;
			var sampleCoverage = sampleSpanSubpixelDepth / Mathf.Max(sampleDepth, lodFrustum.cameraNear);
			{
				return sampleCoverage;
			}
		}

		public static float LODFrustumCoverage(in LODFrustum lodFrustum, in Vector3 samplePosition, float sampleSpan)
		{
			var sampleDepth = Vector3.Dot(lodFrustum.cameraForward, samplePosition - lodFrustum.cameraPosition);
			{
				return LODFrustumCoverage(lodFrustum, sampleDepth, sampleSpan);
			}
		}

		public static Vector2 LODFrustumCoverageCeiling(in LODFrustum lodFrustum, in LODBounds lodBounds, in LODGeometry lodGeometry)
		{
			if (LODFrustumContains(lodFrustum, lodBounds))
			{
				var cameraVector = lodFrustum.cameraPosition - lodBounds.center;
				var cameraDistance = cameraVector.magnitude;

				var nearestSamplePosition = lodBounds.center + cameraVector * (Mathf.Min(cameraDistance, lodBounds.radius) / cameraDistance);
				var nearestSampleCoverage = new Vector2(
					LODFrustumCoverage(lodFrustum, nearestSamplePosition, lodGeometry.maxParticleDiameter),
					LODFrustumCoverage(lodFrustum, nearestSamplePosition, lodGeometry.maxParticleInterval)
				);

				//Debug.Log("checking cam: " + camera.ToString() + " => vis, coverage " + nearestSampleCoverage);
				return nearestSampleCoverage;
			}
			else
			{
				//Debug.Log("checking cam: " + camera.ToString() + " => hidden");
				return Vector2.zero;
			}
		}

		public static Vector2 LODFrustumCoverageCeilingSequential(CameraType cameraType, in LODBounds lodBounds, in LODGeometry lodGeometry)
		{
			var maxCoverage = Vector2.zero;
			{
				using (var lodFrustums = AcquireLODFrustums(cameraType, Allocator.Temp))
				{
					for (int i = 0; i != lodFrustums.Length; i++)
					{
						maxCoverage = Vector2.Max(maxCoverage, LODFrustumCoverageCeiling(lodFrustums[i], lodBounds, lodGeometry));
					}
				}
			}

			return maxCoverage;
		}

		public static float ResolveLODQuantity(float sampleCoverage, float lodCeiling, float lodScale, float lodBias)
		{
			float lodValue = Mathf.Clamp01(Mathf.Clamp01(sampleCoverage * lodScale) + lodBias);
			{
				return Mathf.Min(lodValue, lodCeiling);
			}
		}

		public static LODIndices ResolveLODIndices(float lodValue, NativeArray<float> lodThresholds, bool lodBlending)
		{
			var lodCount = lodThresholds.Length;
			var lodSearch = lodThresholds.BinarySearch(lodValue);
			var lodDesc = new LODIndices
			{
				lodIndexLo = 0,
				lodIndexHi = 0,
				lodBlendFrac = 0.0f,
				lodValue = lodValue,
			};
				
			if (lodSearch < 0)
			{
				lodSearch = ~lodSearch;
				lodDesc.lodIndexLo = (uint)Mathf.Max(0, lodSearch - 1);
				lodDesc.lodIndexHi = (uint)Mathf.Min(lodSearch, lodCount - 1);

				var lodValueLo = lodThresholds[(int)lodDesc.lodIndexLo];
				var lodValueHi = lodThresholds[(int)lodDesc.lodIndexHi];
				{
					lodDesc.lodBlendFrac = (lodValueLo != lodValueHi) ? Mathf.Clamp01((lodValue - lodValueLo) / (lodValueHi - lodValueLo)) : 0.0f;
				}
			}

			if (lodBlending)
			{
				//Debug.Log("LOD: " + lodSelection.lodIndexLo + " -> " + lodSelection.lodIndexHi + " (" + lodSelection.lodBlendFrac + ")");
			}
			else
			{
				lodDesc.lodIndexLo = (lodDesc.lodBlendFrac > 0.5f) ? lodDesc.lodIndexHi : lodDesc.lodIndexLo;
				lodDesc.lodIndexHi = lodDesc.lodIndexLo;
				lodDesc.lodBlendFrac = 0.0f;
				//Debug.Log("LOD: " + lodSelection.lodIndexLo + " (no blend)");
			}

			return lodDesc;
		}
	}
}