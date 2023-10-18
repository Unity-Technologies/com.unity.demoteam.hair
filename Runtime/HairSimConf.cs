using UnityEngine.Rendering;

namespace Unity.DemoTeam.Hair
{
	public static partial class HairSim
	{
		[GenerateHLSL]
		public struct Conf
		{
			public const int MAX_FRUSTUMS = 32;
			// N == max number of lod cameras

			public const int MAX_BOUNDARIES = 8;
			// N == max number of solid boundaries

			public const int MAX_EMITTERS = 8;
			// N == max number of wind emitters

			public const int SECOND_ORDER_UPDATE = 0;
			// 0 == off (smaller memory footprint)
			// 1 == on (experimental)

			public const int SPLAT_FRACTIONAL_BITS = 12;// => 1 / (1 << 14) = 0.00006103515625
			// N == controls smallest fraction stored on grid

			public const int SPLAT_TRILINEAR = 1;
			// 0 == splat only to nearest cell
			// 1 == trilinear weights

			public const int VOLUME_SQUARE_CELLS = 1;
			// 0 == support non-square cells
			// 1 == only square cells

			public const int VOLUME_STAGGERED_GRID = 0;
			// 0 == store everything at cell centers
			// 1 == store velocity and pressure gradient at cell faces
		}
	}
}
