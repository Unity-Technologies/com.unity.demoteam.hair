using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	[CustomEditor(typeof(HairSim))]
	public class HairSimInspector : Editor
	{
		public override void OnInspectorGUI()
		{
			var hairSim = target as HairSim;
			if (hairSim != null)
			{
				var strandLength = hairSim.strands.strandLength * 1000.0f;
				var strandDiameter = hairSim.strands.strandDiameter;

				var cellInterval = hairSim.GetCellSize().x * 1000.0f;
				var cellCrossSection = cellInterval * cellInterval;
				var cellVolume = cellInterval * cellInterval * cellInterval;

				var strandParticleInterval = strandLength / (hairSim.strands.strandParticleCount - 1);
				var strandParticleCrossSection = 0.25f * Mathf.PI * strandDiameter * strandDiameter;
				var strandParticleVolume = strandParticleInterval * strandParticleCrossSection;

				var contribCrossSection = strandParticleCrossSection / cellCrossSection;
				var contribVolume = strandParticleVolume / cellVolume;

				EditorGUILayout.HelpBox(
					"\n" +
					"strandLength = " + strandLength + " mm\n" +
					"strandDiameter = " + strandDiameter + " mm\n" +
					"\n" +
					"strandParticleInterval = " + strandParticleInterval + " mm\n" +
					"strandParticleCrossSection = " + strandParticleCrossSection + " mm^2\n" +
					"strandParticleVolume = " + strandParticleVolume + " mm^3\n" +
					"\n" +
					"cellInterval = " + cellInterval + " mm\n" +
					"cellCrossSection = " + cellCrossSection + " mm^2\n" +
					"cellVolume = " + cellVolume + " mm^3\n" +
					"\n" +
					"contribCrossSection = " + contribCrossSection + " rho\n" +
					"contribVolume = " + contribVolume + " rho\n" +
					"\n" +
					"one cell fits " + cellVolume / strandParticleVolume + " particles\n" +
					"",
					MessageType.Info);
			}

			base.OnInspectorGUI();

			/*
			
			strandLength =
				USER_INPUT

			strandParticleCount =
				USER_INPUT

			strandParticleInterval =
				strandLength / (strandParticleCount - 1)

			strandDiameter =
				USER_INPUT

			strandCrossSectionArea =
				0.25 * PI * strandDiameter * strandDiameter
			

			cellCount =
				USER_INPUT
			
			cellSize =
				VolumeSize() / cellCount

			cellCenterInterval =
				cellSize.y

			cellCrossSectionArea =
				cellSize.x * cellSize.z
			
			cellParticleCount =
				COMPUTED

			cellCrossSectionDensity =
				cellParticleCount * (strandCrossSectionArea / cellCrossSectionArea)
			
			cellDensity =
				cellCrossSectionDensity * (cellCenterInterval / strandParticleInterval)



			if (cellCenterInterval > strandParticleInterval)
				Warning: Few particles for the granularity of the grid. Consider reducing the resolution of the grid, or increasing the number of particles per strand.

			*/
		}
	}
}
