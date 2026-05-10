# vfx

## Project Description

This is a Unity HDRP meditation/gallery project built around point-cloud painting VFX. The main experience is in `Assets/Scenes/Meditation.unity`, where painting VFX objects rotate sequentially, fade out by reducing Spawn Rate to zero, and fade in by restoring Spawn Rate before their staged animation begins.

The project also includes ambient background music with runtime volume control.

This project references and builds on ideas from [yumayanagisawa/Unity-Point-Cloud-VFX-Graph](https://github.com/yumayanagisawa/Unity-Point-Cloud-VFX-Graph), a Unity HDRP/VFX Graph point-cloud rendering project based on Keijiro's Pcx workflow.

## Unity / HDRP Version

- Unity: 2022.3.57f1
- HDRP: 14.0.11

## Project Layout

- `Assets/Scenes/` - Unity scenes, including the main `Meditation` scene.
- `Assets/Point Cloud/` - painting VFX graphs, point-cloud assets, and painting animation scripts.
- `Assets/Scripts/` - general scene scripts, including background music volume control.
- `Assets/Audio/` - audio assets.
- `Assets/Editor/Tests/` - editor tests for custom scripts.
- `Assets/XR/` and `Assets/XRI/` - XR/OpenXR and XR Interaction Toolkit project settings.
- `Assets/Samples/XR Interaction Toolkit/` - imported XRI Starter Assets and XR Device Simulator assets used for editor-side XR simulation.
- `Packages/` - Unity package manifest and lock file.
- `ProjectSettings/` - Unity project settings.

## XR Development Without a Quest

The project includes Unity XR Interaction Toolkit Starter Assets and the XR Device Simulator sample, so basic headset/controller movement can be tested in the Editor with keyboard and mouse. Meta Quest-specific features still require a Quest or Quest Link runtime for final validation.
