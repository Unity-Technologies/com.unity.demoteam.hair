using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public interface ISpatialComponentProxy<TComponent, TData> where TData : struct
	{
		bool TryGetData(TComponent component, ref TData data);
		bool TryGetComponentData(Component component, ref TData data);

		int ResolveDataHandle(in TData data);
		float ResolveDataDistance(in TData data, in Vector3 p);
	}

	public static class SpatialComponentFilter<TComponent, TData, TFn> where TComponent : MonoBehaviour where TData : struct where TFn : ISpatialComponentProxy<TComponent, TData>
	{
		const int MAX_OVERLAP_COUNT = 64;

		static Collider[] s_managedColliders = new Collider[MAX_OVERLAP_COUNT];
		static List<TComponent> s_managedComponents = new List<TComponent>();

		static HashSet<int> s_gatherMask = new HashSet<int>();
		static List<TData> s_gatherList = new List<TData>();
		static List<TData> s_gatherListVolume = new List<TData>();

		static TFn s_fn;

		static void FilterDirect(TComponent component, HashSet<int> mask, List<TData> list, ref TData data)
		{
			if (component == null || component.isActiveAndEnabled == false)
				return;

			if (s_fn.TryGetData(component, ref data))
			{
				var handle = s_fn.ResolveDataHandle(data);
				{
					if (mask.Contains(handle) == false)
					{
						mask.Add(handle);
						list.Add(data);
					}
				}
			}
		}

		static void FilterDerived(Component component, HashSet<int> mask, List<TData> list, ref TData data)
		{
			if (component == null)
				return;

			if (s_fn.TryGetComponentData(component, ref data))
			{
				var handle = s_fn.ResolveDataHandle(data);
				{
					if (mask.Contains(handle) == false)
					{
						mask.Add(handle);
						list.Add(data);
					}
				}
			}
		}

		public static List<TData> Gather(TComponent[] resident, bool volume, in Bounds volumeBounds, LayerMask volumeLayer, bool volumeSort, bool includeDerived)
		{
			var scratch = new TData();

			s_gatherMask.Clear();
			s_gatherList.Clear();
			s_gatherListVolume.Clear();

			// gather from resident
			if (resident != null)
			{
				foreach (var component in resident)
				{
					FilterDirect(component, s_gatherMask, s_gatherList, ref scratch);
				}
			}

			// gather from volume
			if (volume)
			{
				var colliderBuffer = s_managedColliders;
				var colliderCount = Physics.OverlapBoxNonAlloc(volumeBounds.center, volumeBounds.extents, colliderBuffer, Quaternion.identity, volumeLayer, QueryTriggerInteraction.Collide);

				// filter bound / standalone
				for (int i = 0; i != colliderCount; i++)
				{
					colliderBuffer[i].GetComponents(s_managedComponents);

					foreach (var component in s_managedComponents)
					{
						FilterDirect(component, s_gatherMask, s_gatherListVolume, ref scratch);
					}
				}

				// filter derived (e.g. untagged colliders)
				if (includeDerived)
				{
					for (int i = 0; i != colliderCount; i++)
					{
						FilterDerived(colliderBuffer[i], s_gatherMask, s_gatherListVolume, ref scratch);
					}
				}

				// sort and append
				unsafe
				{
					using (var sortedIndices = new NativeArray<ulong>(s_gatherListVolume.Count, Allocator.Temp))
					{
						var sortedIndicesPtr = (ulong*)sortedIndices.GetUnsafePtr();

						var captureOrigin = volumeBounds.center;
						var captureExtent = volumeBounds.extents.Abs().CMax();

						for (int i = 0; i != s_gatherListVolume.Count; i++)
						{
							var volumeSortValue = 0u;
							if (volumeSort)
							{
								var sdRaw = s_fn.ResolveDataDistance(s_gatherListVolume[i], captureOrigin);
								var sdClippedDoubleExtent = Mathf.Clamp(sdRaw / captureExtent, -1.0f, 1.0f);
								var udClippedDoubleExtent = Mathf.Clamp01(sdClippedDoubleExtent * 0.5f + 0.5f);
								{
									volumeSortValue = (uint)(udClippedDoubleExtent * UInt16.MaxValue);
								}
							}

							var sortDistance = ((ulong)volumeSortValue) << 48;
							var sortHandle = (((ulong)s_fn.ResolveDataHandle(s_gatherListVolume[i])) << 16) & 0xffffffff0000uL;
							var sortIndex = ((ulong)i) & 0xffffuL;
							{
								sortedIndicesPtr[i] = sortDistance | sortHandle | sortIndex;
							}
						}

						sortedIndices.Sort();

						for (int i = 0; i != s_gatherListVolume.Count; i++)
						{
							var index = (int)(sortedIndicesPtr[i] & 0xffffuL);
							{
								s_gatherList.Add(s_gatherListVolume[index]);
							}
						}
					}
				}
			}

			// done
			return s_gatherList;
		}
	}
}
