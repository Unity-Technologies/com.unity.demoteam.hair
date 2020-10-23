using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.DemoTeam.Hair
{
	// Hair
	// HairAsset
	// HairCustomPass
	// HairCustomPassSpawner

	public class HairCustomPassSpawner : MonoBehaviour
	{
		static bool s_initialized = false;

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
#else
		[RuntimeInitializeOnLoadMethod]
#endif
		static void StaticInitialize()
		{
			if (s_initialized == false)
			{
				var instance = ComponentSingleton<HairCustomPassSpawner>.instance;
				{
					CreateCustomPass(instance.gameObject, HairCustomPass.Dispatch.Step, CustomPassInjectionPoint.BeforeRendering);
					CreateCustomPass(instance.gameObject, HairCustomPass.Dispatch.Draw, CustomPassInjectionPoint.BeforePreRefraction);

					instance.gameObject.SetActive(true);
				}

#if UNITY_EDITOR
				UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ComponentSingleton<HairCustomPassSpawner>.Release;
#endif

				s_initialized = true;
			}
		}

		static void CreateCustomPass(GameObject container, HairCustomPass.Dispatch dispatch, CustomPassInjectionPoint injectionPoint)
		{
			var hairPassVolume = container.AddComponent<CustomPassVolume>();
			var hairPass = hairPassVolume.AddPassOfType<HairCustomPass>() as HairCustomPass;

			hairPassVolume.injectionPoint = injectionPoint;
			hairPassVolume.isGlobal = true;
			hairPass.dispatch = dispatch;
		}
	}
}