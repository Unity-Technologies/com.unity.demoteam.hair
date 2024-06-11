# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [unreleased]

### Added

- Added support for non-uniform scaling of discrete SDF boundaries specified via texture. (Also handles scaling of sampled distances.)

### Fixed

- Fixed a regression that prevented passing discrete SDF via RenderTexture.


## [0.14.2-exp.1] - 2024-06-06

### Fixed

- Added missing guard to prevent crash in HairVertex for curious cases where constant buffer was not bound (the guard existed earlier, and its disappearance was a regression in 0.14.0-exp.1.)

### Changed

- Delayed unloading of meshes in topology cache a bit.


## [0.14.1-exp.1] - 2024-06-03

### Fixed

- Fixed regression in fragment stage after limiting variants to vertex stage.


## [0.14.0-exp.1] - 2024-06-03

### Added

- Added runtime topology cache to enable automatic creation and sharing of topology data between multiple hair instances (reduces runtime memory footprint.)
- Added initial support for subframe interpolation of per-frame collision boundaries.
- Added initial support for subframe interpolation of per-frame wind emitters.
- Added icons for assets and components.

### Fixed

- Fixed a determinism issue caused by wind emitter clock not matching simulation time.
- Fixed a few compatibility issues with Metal.

### Changed

- Changed default diameter scale when ingesting alembic curve widths from 1.0 to 0.01 (since alembic curve widths are often specified in cm.)
- Changed root mesh construction to output 16 bit indices when possible.

### Removed

- Removed asset level topology data entirely in favour of shared runtime data, reducing per-particle storage requirements considerably (per-particle reduction of 6.5x - 12.1x, from 79-146 (min-max) to 12 bytes per particle), effectively resulting in smaller hair assets.


## [0.13.0-exp.1] - 2024-04-16

### Added

- Added support for seamless, automatic render LOD with coverage-preserving decimation based on hierarchical clustering.
- Added support for GPU based LOD selection, including GPU root bounds, GPU frustum culling.
- Added support for data/settings migration from older versions of the package.
- Added asset-level support for per-strand diameter and tapering.
- Added asset-level support for per-vertex attributes (uv, width) for shader graph.
- Added more optional fields to HairAssetProvisional.CurveSet for custom curve data: curveDataTexCoord, curveDataDiameter, curveDataTapering.
- Added LOD slider to HairAsset group preview.
- Added LOD indicator to HairInstance gizmos.
- Added simulation state to HairInstance inspector.
- Added per-material lod scale and bias to HairVertex node.

### Changed

- Moved all LOD dependent GPU workloads (most of them) to indirect dispatch.
- Substantially improved performance of constraint solver for primary strands (1.5x-2.5x on Turing.)
- Substantially improved performance of interpolation pass for non-primary strands.
- Refactored settings blocks in HairAsset.
- Refactored settings blocks in HairInstance.
- Refactored HairVertex graph outputs. (NOTE: Not yet final!)
- Updated the SRP default material to include default connections for motion vector and width.
- Updated the draw strand roots debug option to also include tangents and bitangents.
- Improved prefab handling.

### Fixed

- Fixed an issue with bad cast causing Texture3D to be ignored when supplied as static SDF.
- Fixed an issue with first frame initialization on 2021.2+.
- Fixed various regressions after restructuring.
- Fixed a memory leak in TriMeshBuffers.

### Removed

- Removed C# double definitions of most named GPU resources.
- Removed shader keyword for defining PSIZE (now always defined).
- Removed some old code.


## [0.12.0-exp.1] - 2024-04-15

### Added

- Added support for tube rendering.
- Added support for noise-based timing jitter in wind emitters (HairWind) to reduce uniformity of pulses.
- Added support for automatic LOD selection based on size of bounds in viewport.

### Changed

- Reduced size of topology meshes used for rendering. This is the result of a data format change, and affects meshes built at runtime (e.g. when changing subdivision setting), as well as meshes built within hair assets stored on disk. Existing hair assets in existing projects are still usable as-is, but they will need to be rebuilt if one wants to benefit from the reduction in size.
- Improved shader compilation time for all hair materials (removed use of multi_compile in HairVertex.hlsl).
- Improved tooltips in the HairWind component.
- Renamed various fields.

### Fixed

- Fixed a bug in triangle BVH lookup that was causing incorrect and slow root UV resolve on more elaborate meshes. 
- Fixed a precision issue that potentially could cause degenerate segments and skipped vertices when rendering very large groups of strands.
- Fixed an issue with the HairAsset inspector capturing mouse when clicking outside inspector after long progress bar (was triggering rebuild with the 'auto' option enabled).


## [0.11.0-exp.1] - 2023-05-26

### Added

- Added new option to account for solid occluders in wind propagation.
- Added new option to account for solid occluders in strand count probe construction.
- Added new component 'HairWind' for authoring wind emitters that can affect hair. The HairWind component represents a single emitter, and can be configured either as a standalone emitter (preferred), or as a proxy for an existing WindZone (more limited).
- Added support for volumetric wind propagation. Wind propagation is enabled per HairInstance (under volume settings). When enabled, wind will propagate through the volume of hair, and act on strands accordingly, taking into account the physical density of hair in the volume.

### Fixed

- Fixed an issue with root updates not taking the correct path on some platforms.
- Fixed missing 'noinstancing' option in the default builtin lit material.
- Fixed missing clamps on parameters used to control resampling.


## [0.10.0-exp.1] - 2023-03-22

### Added

- Added improved root frame resolve which also includes proper twist under deformation (instead of approximation via root bone of skinning target) when used in combination with com.unity.demoteam.digital-human >= 0.2.1-preview.
- Added support for custom scheduling (enabled per HairInstance via the update mode setting under system settings -- once enabled, the application is responsible for invoking 'DispatchUpdate' on the HairInstance).
- Reduced memory footprint of renderers by sharing topology meshes between similar renderers when possible (requires Unity 2021.2+ for direct renderer bounds + staging subdivision disabled).
- Reduced memory footprint of simulation by shrinking unused buffers according to memory requirements of dynamic and static configuration.
- Added system aware replacement shaders that are automatically swapped in during async compilation and as error fallback.

### Changed

- Renderer bounds are now updated directly instead of via instanced mesh when possible (requires Unity 2021.2+).
- Moved static shader configuration to C# struct with [GenerateHLSL] to be able to inspect static configuration from C# side.
- Separated common unlit/builtin shader setup into separate includes (see HairMaterialCommonUnlit.hlsl and HairMaterialCommonBuiltin.hlsl) to facilitate easier reuse in custom unlit/builtin shaders and materials.
- Increased verbosity of warning displayed if/when a custom data provider supplies incomplete vertex data for one or more of its declared vertex features.

### Fixed

- Fixed an issue with the boundary collision constraint not handling non-uniformly scaled box colliders (caused by incorrect distance computation due to not separating scale from transform).
- Fixed a synchronization issue with buffer uploads (from initialization) not always finishing before first read when using custom scheduling and async dispatch.
- Fixed a warning that was appearing for topology meshes (due to their non-standard vertex layout) with raytracing enabled.
- Fixed an issue that prevented use of material variants.
- Fixed an issue with the content upgrade handling incorrectly causing some of the default settings to be erased on a newly created HairAsset.
- Fixed an issue with runtime generated topology meshes being incomplete on some platforms due to the submesh declaration missing an explicit vertex count.
- Fixed an issue with root UV not being resolved properly from alembic curve data when using the 'Resolve From Curve UV' option.
- Fixed an issue with the graphics-based root update implicitly binding an unspecified render target (might be undefined, so it now explicitly binds a temporary 1x1 target despite not drawing anything).
- Fixed an issue with prerequisite state (for when depending on attachments resolving on GPU) not being reset after disabling/enabling a HairInstance.
- Fixed two related issues within rest pose initialization and root frame resolve that caused strands to sometimes have incorrect rest pose, and to sometimes respond incorrectly to external deformation of the root mesh (initialization now guarantees correct root frame relative rest poses that do not rely on the initial state of the possibly already deformed root mesh, and the root frame resolve now derives directly from the current state of the root mesh, and no longer depends on perturbing an initial root frame).
- Fixed an issue caused by the property drawer for RenderingLayerMask not gracefully handling missing layer mask names (supplied by the active render pipeline).
- Fixed an issue with manually assigned boundaries not taking priority over boundaries automatically collected via overlap (as promised in UI).
- Fixed local vertex descriptor/attribute inconsistencies that caused pipeline validation errors with builtin and Metal.
- Fixed incorrect guard that prevented builtin shaders from actually sampling the hair data on some platforms.
- Fixed out-of-bounds error in HairAssetBuilder that was caused by curve resampling ignoring provisional curve data feature flags.
- Fixed an issue (conditionally) with alembic curve assets not showing up in the object picker (requires Unity 2021.2+).
- Fixed compatibility with Unity.Collections >= 2.0.0.
- Fixed regression in HairAssetBuilder (since refactor) that caused false rejection of alembic curve sets without explicit curve width and UV.


## [0.9.1-exp.1] - 2022-12-15

### Added

- Added option to resolve root UV from input curves, if input curves provide that data.
- Added support for building HairAsset silently and/or without internal linking via scripting and HairAssetBuilder.BuildFlags.
- Added support for building HairAsset from custom curve data (adds type 'Custom' that requires assigning an implementation of HairAssetCustomData).
- Added base class HairAssetCustomPlacement to support user defined providers of custom root placement (replaces HairAssetProvider).
- Added base class HairAssetCustomData to support user defined providers of fully custom curve data.
- Added utility scope LongOperationOutput for controlling the output of the LongOperation utility.
- Enabled support for HDRP High Quality Line Renderering when used in combination HDRP >= 15.0.2.

### Changed

- Removed base type HairAssetProvider and added script upgrader for automatic conversion to HairAssetCustomPlacement.
- Refactored parts of HairAssetBuilder to support custom curve data in existing processing pipeline.

### Fixed

- Fixed a UI issue that sometimes caused a block to also list properties from the next block.
- Suppress compilation warning caused by intentional sqrt(-1) when compiling the debug draw shader.
- Fixed sporadic NaN output from vertex tangent resolve that sometimes caused black screen.
- Fixed duplicate definitions in shader setup that broke compatibility with URP >= 12.0.0.
- Fixed an issue with assemblies not being visible in the default namespace.
- Fixed compatibility with Unity.Collections >= 1.3.0.


## [0.9.0-exp.1] - 2022-08-10

### Added

- Initial public release.


<!--- LINKS --->
[unreleased]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.14.2-exp.1...HEAD
[0.14.2-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.14.1-exp.1...0.14.2-exp.1
[0.14.1-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.14.0-exp.1...0.14.1-exp.1
[0.14.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.13.0-exp.1...0.14.0-exp.1
[0.13.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.12.0-exp.1...0.13.0-exp.1
[0.12.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.11.0-exp.1...0.12.0-exp.1
[0.11.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.10.0-exp.1...0.11.0-exp.1
[0.10.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.9.1-exp.1...0.10.0-exp.1
[0.9.1-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.9.0-exp.1...0.9.1-exp.1
[0.9.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/releases/tag/0.9.0-exp.1
