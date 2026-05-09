# VFX Point Cloud Morph Sequence

This setup keeps one Visual Effect in place and morphs its particle maps on the GPU.

## Assets

- `Assets/Point Cloud/Flower2_To_Flower3_Morph.vfx` is the render graph. It is a copy of `Flower2.vfx` with exposed `MorphPositionMap` and `MorphColorMap` texture inputs.
- `PointCloudMorphSequence` binds `.vfx` assets such as `Flower2.vfx` and `Flower 3.vfx`, resolves their embedded Pcx baked point clouds, and generates the morph maps every frame.
- `PointCloudMorphMap.compute` performs the per-particle dance, swirl, stagger, color blend, and final breathing motion.

## Quick Use

1. Create or select one GameObject at the desired center position.
2. Add `Point Cloud/VFX Point Cloud Morph Sequence`.
3. Set `Morph Visual Effect Asset` to `Flower2_To_Flower3_Morph.vfx`.
4. Put `Flower2.vfx` and `Flower 3.vfx` into `VFX Targets`.
5. Adjust `Target Alignments` if a target shape needs local position, rotation, or scale correction.
6. Play the scene.

The component auto-fills those defaults in the Unity editor when the assets are present.

## Controls

- `Segment Duration`: time for one morph step, for example Flower2 -> Flower3.
- `Loop`: after the last target, morph back to the first target.
- `Target Alignments`: one entry per `VFX Targets` element. Each entry applies a local position, rotation, and scale to that target's point cloud before the morph.
- `Dance Radius`, `Vertical Wave`, `Swirl Turns`, `Dance Speed`, `Particle Stagger`: artistic motion while particles reorganize.
- `Color Shimmer`: extra color variation during the transition.
- `Breath Amount`, `Float Amount`, `Breath Speed`: subtle motion after the final form is reached.

## Reuse

Add more `.vfx` assets to `VFX Targets` for Object 1 -> Object 2 -> Object 3. The morph render graph must have capacity at least as large as the biggest point cloud in the sequence.
