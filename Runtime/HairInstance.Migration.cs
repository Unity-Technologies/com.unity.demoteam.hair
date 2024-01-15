using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public partial class HairInstance : IVersionedDataContext
	{
		const int VERSION_IMPL = 1;

		[field: SerializeField]
		public int version { get; private set; } = 0;
		public int VERSION => VERSION_IMPL;

		public void PerformMigrationStep()
		{
			switch (version)
			{
				case 0: PerformMigration_0();
					version = 1;
					break;

				case 1: // at latest version
					break;
			}
		}
	}
}