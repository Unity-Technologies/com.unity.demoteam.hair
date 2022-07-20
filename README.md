
# Package: com.unity.demoteam.hair

<div align="justify">An integrated solution for authoring / importing / simulating / rendering strand-based hair in Unity. Built from the ground up with Unity users in mind, and evolved and hardened during the production of 'Enemies', the hair system is applicable not only to realistic digital humans, but also to more stylized content and games. It allows users to easily create 'Hair Assets', either from grooms from external DCC tools (through the alembic file format for curve data), or through simple built-in procedural generation tools (e.g. scatter x amount of hairs on a mesh or other primitive, or using a custom generator). Using a fast and flexible GPU-based solver that works on both strand- and volume-information, the system enables users to interactively set up ‘Hair Instances’ within scenes, and then see and interact with those instances as they are simulated and rendered in real time.</div>


## Requirements

- Unity 2020.2.0f1 +


## Features

* Authoring
	+ Import grooms from external DCC tools through the alembic file format (.abc)
	[ *depends on **com.unity.formats.alembic >= 2.2.2*** ]
	+ Make procedural grooms in just a few clicks
		- Scatter strands on built-in primitives, or on user specified meshes
		- Shape strands using simple parameters like length and curl
		- Plug in your own generators for custom placement
	+ Clustering / Level of detail
		- Build LOD chain from list of region maps
		- Build LOD chain procedurally

* Skinning
	+ Easily attach strand roots to skinned geometry in a scene
	[ *depends on **com.unity.demoteam.digital-human >= 0.1.1-preview*** ]

* Simulation
	+ Strand-based solver supporting tens of thousands of individual strands
	+ Solver adds volume-based quantities such as density and pressure
		- Uses physical strand diameter, physical strand margin
		- Applies pressure to preserve the volume of a groom
			- Allows targeting uniform density
			- Allows targeting initial pose density
		- Applies pressure to soften strand-strand collisions
		- Encodes directional strand count to support physical shading model in HDRP
	+ Fully configurable set of constraints
		- Boundary collision w/ friction
		- Particle-particle distance (soft, hard)
		- Particle-root distance
		- Local bend limiter (<, >, =)
		- Local shape
		- Global position
		- Global rotation
	+ Level of detail support
		- Allows simulating partial set of strands (e.g. at a distance)
		- Volume-based effects also work for partial set of strands

* Rendering
	+ Supports all existing rendering pipelines
		- Built-in RP
		- HDRP
		- URP
	+ Easily build your own hair materials
		- Add the ‘HairVertex’ node to any Shader Graph to read the simulation data
		- (**planned**) Optionally, use generic materials at the cost of copying the data
		[ *depends on **Unity >= 2021.2** for vertex buffer access* ]
	+ Multiple modes of rendering
		- Render strands as simple line primitives
		- Render strands as view facing triangle strips w/ tangent space normals
		- Render high quality strands using the compute-based HDRP hair renderer
		[ *depends on **Unity >= 2023.1*** ]


## Usage

Declare the package as a git dependency in `Packages/manifest.json`:

```
"dependencies": {
    "com.unity.demoteam.hair": "https://github.com/Unity-Technologies/com.unity.demoteam.hair.git",
    ...
}
```


## See also

https://github.com/Unity-Technologies/com.unity.demoteam.digital-human


## Related links

Video: [Enemies – real-time cinematic teaser](https://www.youtube.com/watch?v=eXYUNrgqWUU)


## References

[list of papers]
