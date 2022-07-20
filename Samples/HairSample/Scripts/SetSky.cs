using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class SetSky : MonoBehaviour
{
	public Material builtinSkybox;

	public void Update()
	{
		if (RenderSettings.skybox != builtinSkybox)
			RenderSettings.skybox = builtinSkybox;
	}
}
