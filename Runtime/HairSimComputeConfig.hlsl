#ifndef __HAIRSIMCOMPUTE_CONFIG__
#define __HAIRSIMCOMPUTE_CONFIG__

#define LAYOUT_INTERLEAVED 1
// 0 == particles grouped by strand, i.e. root, root+1, root, root+1
// 1 == particles grouped by index, i.e. root, root, root+1, root+1

#define MAX_BOUNDARIES 8
// N == max number of colliders

#define SPLAT_PRECISION 1e-4// == include down to fourth decimal
// R == smallest decimal stored on grid

#define SPLAT_TRILINEAR 1
// 0 == splat only to cell centers
// 1 == trilinear weights

#define VOLUME_SQUARE_CELLS 1
// 0 == support non-square cells
// 1 == only square cells

#define STRAND_31_32_DEBUG 2
// 0 == off
// 1 == on (full strands)
// 2 == on (first segments)

#endif//__HAIRSIMCOMPUTE_CONFIG__
