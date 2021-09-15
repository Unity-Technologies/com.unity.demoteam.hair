using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public static class DerivedCache<TObject, TDerived> where TObject : UnityEngine.Object where TDerived : struct, IDisposable
	{
		static bool s_initialized = false;

		static Dictionary<TObject, Item> s_items;
		struct Item
		{
			public TDerived data;
			public Hash128 hash;
		}

		static void StaticInitialize()
		{
			if (s_initialized == false)
			{
				s_items = new Dictionary<TObject, Item>();

#if UNITY_EDITOR
				UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Clear;
#endif

				s_initialized = true;
			}
		}

		public static void Clear()
		{
			StaticInitialize();

			foreach (var item in s_items.Values)
			{
				item.data.Dispose();
			}

			s_items.Clear();
		}

		public static bool TryGet(TObject objectKey, out TDerived derivedData)
		{
			StaticInitialize();

			if (s_items.TryGetValue(objectKey, out var item))
			{
				var hash = DerivedHash.Compute(objectKey);
				if (hash == item.hash)
				{
					derivedData = item.data;
					return true;
				}
			}

			derivedData = new TDerived();
			return false;
		}

		public static void SetOrReplace(TObject objectKey, in TDerived derivedData)
		{
			StaticInitialize();

			if (s_items.TryGetValue(objectKey, out var item))
			{
				item.data.Dispose();
			}

			s_items[objectKey] = new Item
			{
				data = derivedData,
				hash = DerivedHash.Compute(objectKey),
			};
		}

		public static TDerived GetOrCreate(TObject objectKey, Func<TObject, TDerived> derivedDataConstructor)
		{
			if (TryGet(objectKey, out var derivedData) == false)
			{
				derivedData = derivedDataConstructor(objectKey);
				SetOrReplace(objectKey, derivedData);
			}

			return derivedData;
		}
	}

	public static class DerivedCache
	{
		public static bool TryGet<TObject, TDerived>(TObject objectKey, out TDerived derivedData) where TObject : UnityEngine.Object where TDerived : struct, IDisposable
		{
			return DerivedCache<TObject, TDerived>.TryGet(objectKey, out derivedData);
		}

		public static void SetOrReplace<TObject, TDerived>(TObject objectKey, in TDerived derivedData) where TObject : UnityEngine.Object where TDerived : struct, IDisposable
		{
			DerivedCache<TObject, TDerived>.SetOrReplace(objectKey, derivedData);
		}

		public static TDerived GetOrCreate<TObject, TDerived>(TObject objectKey, Func<TObject, TDerived> derivedDataConstructor) where TObject : UnityEngine.Object where TDerived : struct, IDisposable
		{
			return DerivedCache<TObject, TDerived>.GetOrCreate(objectKey, derivedDataConstructor);
		}
	}

	public partial class DerivedHash
	{
		public static Hash128 Compute<TObject>(TObject key) where TObject : UnityEngine.Object
		{
			return new Hash128((uint)key.GetInstanceID(), 0, 0, 0);
		}

		public static Hash128 Compute(Mesh mesh)
		{
			return new Hash128((uint)mesh.GetInstanceID(), 0, 0, 0);//TODO content hash?
		}

		public static Hash128 Compute(Texture2D texture)
		{
#if UNITY_EDITOR
			return texture.imageContentsHash;
#else
			return new Hash128((uint)texture.GetInstanceID(), 0, 0, 0);
#endif
		}
	}
}
