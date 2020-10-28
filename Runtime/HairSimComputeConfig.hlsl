#ifndef __HAIRSIMCOMPUTECONFIG_HLSL__
#define __HAIRSIMCOMPUTECONFIG_HLSL__

#define MAX_BOUNDARIES 8
// N == max number of colliders

#define SPLAT_FRACTIONAL_BITS 12// => 1 / (1 << 14) = 0.00006103515625
// N == controls smallest fraction stored on grid

#define SPLAT_TRILINEAR 1
// 0 == splat only to nearest cell
// 1 == trilinear weights

#define VOLUME_SQUARE_CELLS 1
// 0 == support non-square cells
// 1 == only square cells

#define VOLUME_STAGGERED_GRID 0
// 0 == store everything at cell centers
// 1 == store velocity and pressure gradient at cell faces

#define DEBUG_STRAND_31_32 0
// 0 == off
// 1 == on (full strands)
// 2 == on (first segments)

#endif//__HAIRSIMCOMPUTECONFIG_HLSL__
