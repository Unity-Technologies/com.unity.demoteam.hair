using UnityEngine;
using Unity.DemoTeam.Hair;

[CreateAssetMenu(menuName = "Hair/Custom Placement/SineWave1D")]
public class SineWave1D : HairAssetCustomPlacement
{
	public float amplitude = 0.5f;
	public float frequency = 1.0f;
	public float duration = 2.0f;
	public float phase = 0.0f;

	public override bool GenerateRoots(in HairAssetProvisional.ProceduralRoots roots)
	{
		var step = 1.0f / (roots.strandCount - 1);
		var stepA = step * frequency * 2.0f * Mathf.PI;
		var offsetA = (0.5f * frequency + phase) * 2.0f * Mathf.PI;

		unsafe
		{
			roots.GetUnsafePtrs(out var rootPos, out var rootDir, out var rootUV0, out var rootVar);

			for (int i = 0; i != roots.strandCount; i++)
			{
				rootPos[i] = new Vector3(duration * ((step * i) - 0.5f), amplitude * Mathf.Sin(stepA * i - offsetA), 0.0f);
				rootDir[i] = Vector3.down;
				rootUV0[i] = new Vector2((step * i), 0.0f);
				rootVar[i] = HairAssetProvisional.ProceduralRoots.RootParameters.defaults;
			}
		}

		return true;// success
	}
}
