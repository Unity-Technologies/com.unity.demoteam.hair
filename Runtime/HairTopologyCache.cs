using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	public enum HairTopologyType
	{
		Lines	= 0,
		Strips	= 1,
		Tubes	= 2,
	}

	[Serializable]
	public struct HairTopologyDesc
	{
		public HairTopologyType type;
		public int strandCount;
		public int strandParticleCount;
		public HairAsset.MemoryLayout memoryLayout;
	}

	public static class HairTopologyCache
	{
		const int DELAY_UNUSED = 10;

		static bool s_initialized = false;

		static Dictionary<ulong, Mesh> s_keyToMesh = null;
		static Dictionary<ulong, int> s_keyToUsage = null;

		static HairTopologyCache()
		{
			if (s_initialized == false)
			{
				s_keyToMesh = new Dictionary<ulong, Mesh>();
				s_keyToUsage = new Dictionary<ulong, int>();

#if UNITY_EDITOR
				UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => DestroyAll();
#endif

				s_initialized = true;
			}
		}

		public static ulong GetSortKey(in HairTopologyDesc desc)
		{
			var a = (ulong)desc.strandCount;
			var b = (ulong)desc.strandParticleCount;
			var c = (ulong)desc.memoryLayout;
			var d = (ulong)desc.type;
			{
				// [ dddd cccc | bbbb bbbb | aaaa aaaa | aaaa aaaa ]
				return (d << 56) | (c << 48) | (b << 32) | a;
			}
		}

		public static Mesh GetSharedMesh(in HairTopologyDesc desc)
		{
			var key = GetSortKey(desc);

			if (s_keyToMesh.TryGetValue(key, out var mesh))
			{
				s_keyToUsage[key] = 1;
			}
			else
			{
				//Debug.Log("f " + Time.frameCount + " create mesh (strandCount " + desc.strandCount + ")");
				switch (desc.type)
				{
					case HairTopologyType.Lines:
						mesh = HairInstanceBuilder.CreateRenderMeshLines(HideFlags.HideAndDontSave, desc.memoryLayout, desc.strandCount, desc.strandParticleCount, new Bounds());
						break;

					case HairTopologyType.Strips:
						mesh = HairInstanceBuilder.CreateRenderMeshStrips(HideFlags.HideAndDontSave, desc.memoryLayout, desc.strandCount, desc.strandParticleCount, new Bounds());
						break;

					case HairTopologyType.Tubes:
						mesh = HairInstanceBuilder.CreateRenderMeshTubes(HideFlags.HideAndDontSave, desc.memoryLayout, desc.strandCount, desc.strandParticleCount, new Bounds());
						break;
				}

				mesh.UploadMeshData(markNoLongerReadable: true);

				s_keyToMesh[key] = mesh;
				s_keyToUsage[key] = 1;
			}

			return mesh;
		}

		static void DestroyAll()
		{
			foreach (var pair in s_keyToMesh)
			{
				//Debug.Log("f " + Time.frameCount + " delete mesh (strandCount " + (pair.Key & 0xffffffff) + ") (ALL)");
				CoreUtils.Destroy(pair.Value);
			}

			s_keyToMesh.Clear();
			s_keyToUsage.Clear();
		}

		static void DestroyUnused()
		{
			// decay references
			using (var decayKeys = new UnsafeList<ulong>(s_keyToUsage.Count, Allocator.Temp))
			{
				foreach (var key in s_keyToUsage.Keys)
				{
					decayKeys.Add(key);
				}

				foreach (var key in decayKeys)
				{
					s_keyToUsage[key]--;
				}
			}

			// find and destroy unused
#if HAS_PACKAGE_UNITY_COLLECTIONS_1_3_0
			using (var removeKeys = new UnsafeParallelHashSet<ulong>(s_keyToMesh.Count, Allocator.Temp))
#else
			using (var removeKeys = new UnsafeHashSet<ulong>(s_keyToMesh.Count, Allocator.Temp))
#endif
			{
				foreach (var pair in s_keyToMesh)
				{
					if (pair.Value == null)
					{
						removeKeys.Add(pair.Key);
					}
				}

				foreach (var pair in s_keyToUsage)
				{
					if (pair.Value + DELAY_UNUSED < 0)
					{
						removeKeys.Add(pair.Key);
					}
				}

				foreach (var key in removeKeys)
				{
					if (s_keyToMesh.TryGetValue(key, out var mesh))
					{
						if (mesh != null)
						{
							//Debug.Log("f " + Time.frameCount + " delete mesh (strandCount " + (key & 0xffffffff) + ") (expire)");
							CoreUtils.Destroy(mesh);
						}
					}

					s_keyToMesh.Remove(key);
					s_keyToUsage.Remove(key);
				}
			}
		}

		[ExecuteAlways]
		class DestroyUnusedPerFrame : MonoBehaviour
		{
			static bool s_initialized = false;

			static GameObject s_container = null;

#if UNITY_EDITOR
			[UnityEditor.InitializeOnLoadMethod]
#else
			[RuntimeInitializeOnLoadMethod]
#endif
			static void StaticInitialize()
			{
				if (s_initialized == false)
				{
					s_container = new GameObject(nameof(HairTopologyCache));
					s_container.hideFlags = HideFlags.HideAndDontSave;
					s_container.AddComponent<DestroyUnusedPerFrame>();

#if UNITY_EDITOR
					UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => CoreUtils.Destroy(s_container);
#endif

					s_initialized = true;
				}
			}

			void Update()
			{
				HairTopologyCache.DestroyUnused();
			}
		}
	}
}
