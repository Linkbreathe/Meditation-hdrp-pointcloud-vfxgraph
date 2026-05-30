# Meditation HDRP Point-Cloud VFX Experiment

## Overview

This is a Unity HDRP / XR meditation gallery built around point-cloud painting VFX. The main experience lives in `Assets/Scenes/Meditation.unity`.

The runtime flow is:

1. Wait for an explicit start input.
2. Start a meditation experiment session and CSV logging.
3. Rotate through painting VFX objects.
4. For each painting, run staged VFX animation with randomized particle intensity/frequency ranges.
5. After each stage, present a gaze-based orb choice prompt.
6. Record experiment events, stage values, choices, summaries, and eye-tracking samples.

The project also includes the Water Lilies fixed-stimulus prototype, optional calmness feedback collection, ambient background music with runtime volume control, XR/OpenXR setup, editor tests, and helper tools for VFX setup.

This project references and builds on ideas from [yumayanagisawa/Unity-Point-Cloud-VFX-Graph](https://github.com/yumayanagisawa/Unity-Point-Cloud-VFX-Graph), a Unity HDRP/VFX Graph point-cloud rendering project based on Keijiro's Pcx workflow.

## Unity / Package Versions

- Unity: `2022.3.62f3`
- HDRP: `14.0.12`
- Meta XR SDK: `201.0.0`
- XR Interaction Toolkit: `2.6.5`
- OpenXR: `1.14.3`
- Input System: `1.14.0`
- TextMesh Pro: `3.0.7`
- Cinemachine: `2.10.3`

Package versions are declared in `Packages/manifest.json`. The Unity editor version is declared in `ProjectSettings/ProjectVersion.txt`.

## Main Scenes

- `Assets/Scenes/Meditation.unity` - main meditation experiment scene.
- `Assets/Scenes/SampleScene.unity` - larger sample/reference scene.
- `Assets/Scenes/PrefabEditingScene.unity` - helper scene for prefab work.

## Project Layout

- `Assets/Point Cloud Experiment/` - core point-cloud VFX assets, painting assets, water-lily VFX assets, and experiment scripts.
- `Assets/Point Cloud Experiment/script/` - runtime scripts for sequencing, VFX animation, gaze choice prompts, eye tracking, CSV logging, and calmness feedback.
- `Assets/VFX Ball/` - orb VFX assets used by the gaze-choice prompt.
- `Assets/Scenes/` - Unity scenes.
- `Assets/Scripts/` - general scene utilities, including background music volume control.
- `Assets/Editor/` - editor setup utilities.
- `Assets/Editor/Tests/` - Edit Mode tests for the custom runtime scripts.
- `Assets/data_collection/` - generated meditation experiment CSV sessions and the CSV data dictionary.
- `Assets/XR/` and `Assets/XRI/` - XR/OpenXR and XR Interaction Toolkit settings/assets.
- `Packages/` - Unity package manifest and package lock file.
- `ProjectSettings/` - Unity project settings.

## Key Runtime Components

- `PaintingRotationController` controls the main experiment sequence. It discovers painting children, waits for the start input, manages transitions, starts/stops painting runs, requests stage choice prompts, and forwards structured events to CSV logging.
- `StarryNightRhoneVfxAutoAnimator` animates particle intensity and frequency over staged random ranges. Each run shuffles the stage preset order and emits stage start/value/completion events.
- `VfxAutoAnimatorGroupController` applies shared animation settings to child `StarryNightRhoneVfxAutoAnimator` components.
- `MeditationChoiceEyeGazeFeedback` handles gaze-based orb selection, sustained hover feedback, selection bursts, and choice-prompt breathing visuals.
- `EyeTrackingDataLogger` requests/starts eye tracking when configured, samples eye gaze data, and can write samples into the active experiment session. Optional left/right gaze debug rays are disabled by default.
- `MeditationExperimentCsvLogger` creates the experiment session folder and writes the seven CSV tables.
- `WaterLiliesExperimentManager`, `WaterLiliesExperimentLogger`, and `WaterLiliesTrackingSampler` run the fixed Water Lilies Intensity x Frequency prototype and write separated event, head-pose sample, gaze, and video-frame logs.
- `CalmnessFeedbackCollector` and `CalmnessFeedbackLogger` support optional post-painting calmness feedback. Feedback is written as JSON Lines under `Application.persistentDataPath`.
- `BackgroundMusicVolumeControl` exposes runtime control over background music volume.

## Running in the Editor

1. Open the project with Unity `2022.3.62f3`.
2. Open `Assets/Scenes/Meditation.unity`.
3. Enter Play Mode.
4. In the Editor, the experiment can be started with `A`, `Space`, or `Return` when `PaintingRotationController` has `Allow Keyboard Start In Editor` enabled.

The Editor path is useful for scene flow, transitions, VFX timing, and CSV plumbing. Quest-specific eye tracking and controller behavior still need validation on a Quest runtime or Quest Link.

## Running on Quest / XR

The scene is configured for XR/OpenXR and Meta XR packages. For the intended experiment flow, use a Meta Quest headset/runtime:

- Right controller A button starts the meditation session.
- Eye-tracking-dependent gaze selection requires a device/runtime that supports and grants eye tracking.
- `EyeTrackingDataLogger` can request permission and retry startup when configured.

For participant-facing runs, keep `EyeTrackingDiagnostics > Draw Debug Rays` disabled. Enable it only when an operator needs to inspect the left/right gaze vectors in the Editor/Game view.

If eye tracking is required before starting, `PaintingRotationController` can gate the start flow until eye tracking becomes ready or a timeout is reached.

## Data Collection

### Meditation Rotation Logs

When a meditation rotation session starts, `MeditationExperimentCsvLogger` creates:

```text
Assets/data_collection/<sessionId>/
```

Each session folder contains seven CSV tables:

- `session.csv`
- `paintings.csv`
- `stage_values.csv`
- `choices.csv`
- `events.csv`
- `summary.csv`
- `eye_tracking.csv`

The full table design and field dictionary are documented in:

```text
Assets/data_collection/CSV_TABLES.md
```

CSV version `3` can write an initial `sep=,` line so Excel opens comma-separated files correctly on systems whose default list separator is semicolon. Programmatic readers should skip that first line if present.

Standalone eye-tracking CSV output is also available from `EyeTrackingDataLogger` when `Write Standalone Csv` is enabled. Calmness feedback uses JSONL and is written to `Application.persistentDataPath`.

### Water Lilies Logs

The fixed Water Lilies prototype is documented in:

```text
Assets/Point Cloud Experiment/script/WaterLiliesExperiment/README.md
```

Its current parameter table maps both intensity and frequency as `Low=0.15`, `Medium=0.40`, and `High=0.65`. Sessions write `events`, `samples`, `eye_tracking`, `video_frames`, and `video_manifest.json` under the configured Water Lilies log root.

## Tests

Edit Mode tests live in `Assets/Editor/Tests/`. They cover the custom animation controllers, music volume control, calmness feedback logging, and related runtime helpers.

Run them from Unity Test Runner:

```text
Window > General > Test Runner > Edit Mode
```

## Notes for Future Work

- Keep `README.md` aligned with `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` when Unity or package versions change.
- Update `Assets/data_collection/CSV_TABLES.md` whenever the CSV schema changes.
- Treat generated data under `Assets/data_collection/<sessionId>/` as experiment output. Commit only the sessions that are intentionally part of the repo history.
