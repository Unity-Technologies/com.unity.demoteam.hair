# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [Unreleased]

### Added

- Added support for automatic LOD selection based on size of bounds in viewport.

### Changed

- Renamed various fields.

### Fixed

- Fixed an issue with HairAsset inspector capturing mouse when clicking outside inspector after long progress bar (was triggering rebuild with the 'auto' option enabled).


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
[unreleased]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.11.0-exp.1...HEAD
[0.11.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.10.0-exp.1...0.11.0-exp.1
[0.10.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.9.1-exp.1...0.10.0-exp.1
[0.9.1-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/compare/0.9.0-exp.1...0.9.1-exp.1
[0.9.0-exp.1]: https://github.com/Unity-Technologies/com.unity.demoteam.hair/releases/tag/0.9.0-exp.1
