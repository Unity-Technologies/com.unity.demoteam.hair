using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.DemoTeam.Hair
{
	// Hair
	// HairAsset
	// HairCustomPass
	// HairCustomPassSpawner

	public class HairDriver : MonoBehaviour
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
				var instance = ComponentSingleton<HairDriver>.instance;
				{
					CreateCustomPass(instance.gameObject, HairDriverCustomPass.Dispatch.Step, CustomPassInjectionPoint.BeforeRendering);
					CreateCustomPass(instance.gameObject, HairDriverCustomPass.Dispatch.Draw, CustomPassInjectionPoint.BeforePreRefraction);

					instance.gameObject.SetActive(true);
				}

#if UNITY_EDITOR
				UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ComponentSingleton<HairDriver>.Release;
#endif

				s_initialized = true;
			}
		}

		static void CreateCustomPass(GameObject container, HairDriverCustomPass.Dispatch dispatch, CustomPassInjectionPoint injectionPoint)
		{
			var hairPassVolume = container.AddComponent<CustomPassVolume>();
			var hairPass = hairPassVolume.AddPassOfType<HairDriverCustomPass>() as HairDriverCustomPass;

			hairPassVolume.injectionPoint = injectionPoint;
			hairPassVolume.isGlobal = true;
			hairPass.dispatch = dispatch;
		}
	}
}