# Water Lilies VR-VFX Experiment

This prototype runs a fixed Water Lilies point-cloud VFX stimulus for the first-stage Intensity x Frequency data collection experiment.

## Scene Entry

Open `Assets/Scenes/Meditation.unity`.

The scene contains a root object named `Water Lilies Experiment` with these runtime components:

- `WaterLiliesExperimentManager`
- `WaterLiliesExperimentLogger`
- `WaterLiliesTrackingSampler`

The manager uses `Assets/Resources/WaterLiliesExperimentConfig.asset`. When Play Mode starts, it previews the fixed `5_Water_Lilies` painting with baseline parameters. The experiment clock and log session begin only after Start or `S`.

`2d_paintings/5_Water_Lilies` is visible in the hierarchy so researchers can position, rotate, and scale it manually. `Place Painting In Front Of Viewer` is disabled by default; enable it only for quick debugging when Play Mode should move the painting in front of `MainCamera` or `CenterEyeAnchor`.

## Operator Controls

- `S`: start experiment
- `Space`: continue after questionnaire break
- `H`: manually record `headset_removed`
- `J`: manually record `headset_worn`
- `K`: pilot-mode skip current timed phase
- `Esc`: abort experiment

The runtime overlay is a researcher/operator status panel in the desktop Game view. It is not a world-space VR instruction panel for the participant. Keep `Auto Create Runtime Ui` enabled for editor/operator runs, and disable it for builds that should show no experiment-control UI to the participant.

The `Window > Water Lilies > Operator Panel` editor window shows the live VFX intensity and frequency values currently applied to the painting, so researchers can confirm baseline, adaptation, condition, and frozen/rest states without opening the logs.

## Optional Natural Vortex Texture

`WaterLiliesNaturalVortexModulator` is an optional pilot layer for making the existing Water Lilies motion feel more like water texture and less like fixed experimental targets. It does not replace the Intensity x Frequency condition table. Instead, each condition still supplies the target intensity and frequency, and the modulator applies a small, slow, multi-scale Perlin envelope around those target values.

The `2d_paintings/5_Water_Lilies` scene object is pre-wired with `WaterLiliesVfxController` and `WaterLiliesNaturalVortexModulator`, with `Natural Texture Modulation` enabled for pilot viewing. In Play Mode, when the experiment applies baseline, adaptation, or a condition, the controller gently animates the active intensity/frequency around the configured target values. If this layer is used in a formal run, it should be described as a preregistered deterministic stimulus-generation template rather than as a separate independent variable.

Formal condition viewing uses a condition-locked template: every `ConditionViewing` phase resets the natural texture template to elapsed time `0`, using the same seed, modulation depths, and scale timings for every condition. This keeps the temporal envelope identical across C1-C9; only the condition's base intensity and frequency values change.

The default modulation depths are intentionally small (`0.12` for intensity and `0.08` for frequency). During freeze/recenter/rest phases where frequency is `0`, the layer returns to the base values instead of continuing to breathe.

## Current Parameter Table

Both intensity and frequency use the same level values:

| Level | Value |
| --- | ---: |
| Low | `0.15` |
| Medium | `0.40` |
| High | `0.65` |

The default 3 x 3 condition grid is:

| Condition | Intensity | Frequency |
| --- | --- | --- |
| `C1` | Low | Low |
| `C2` | Low | Medium |
| `C3` | Low | High |
| `C4` | Medium | Low |
| `C5` | Medium | Medium |
| `C6` | Medium | High |
| `C7` | High | Low |
| `C8` | High | Medium |
| `C9` | High | High |

`events.csv`, `samples.csv`, and `eye_tracking.csv` write the resolved numeric values from the active config, so `Low / Medium / High` should appear as `0.15 / 0.40 / 0.65` for both intensity and frequency. These VFX parameters are engineering controls only; they are not interpreted as calmness, meditation state, engagement, or relaxation.

## Timing Profiles

`Mode` selects the active duration profile at runtime. In the config Inspector, edit `Active Durations (Formal)` for Formal mode and `Active Durations (Pilot)` for Pilot mode. The manager reads only the selected profile when the run starts.

## Formal Flow

1. `video_recording_started`
2. `experiment_start`
3. `baseline_start` / `baseline_end`
4. `adaptation_start` / `adaptation_end`
5. For each condition:
   - `condition_prepare`
   - `recenter_start` / `recenter_end`
   - `condition_start` / `condition_end`
   - `questionnaire_break_start`
   - `headset_removed`
   - participant completes the Google Form outside Unity
   - `headset_worn`
   - `questionnaire_break_end`
6. After every 3 completed conditions:
   - `rest_start` / `rest_end`
7. `experiment_end`
8. `video_recording_stopped`

If headset presence is available, `headset_removed` and `headset_worn` are recorded automatically. Otherwise, use the manual keys. By default, each questionnaire break must include both markers before Continue or `Space` can advance. Disable `Require Headset Cycle Before Questionnaire Continue` only for editor dry runs.

## Gaze Sampling

For Meta Quest Pro, gaze sampling first tries Unity XR `Eyes` data and then falls back to the Meta/OVR gaze transforms named `[BuildingBlock] Eye Gaze Left`, `[BuildingBlock] Eye Gaze Right`, or `[BuildingBlock] Eye Gaze Center` if they are present in the scene.

`WaterLiliesTrackingSampler` can fall back to head-forward gaze when eye data and gaze transforms are unavailable. When analyzing eye tracking, use `gaze_available`, `gaze_hit`, and `gaze_on_painting` to separate usable gaze samples from fallback or missing data.

## Logs

Each session writes to the configured log root:

```text
<log_root>/<session_id>/
```

The current config uses:

```text
C:\Users\linki\amaster\data_collection_v2\<session_id>\
```

Generated files:

| File | Purpose |
| --- | --- |
| `events.csv` / `events.jsonl` | Event markers plus the tracking snapshot captured at each event time. |
| `samples.csv` / `samples.jsonl` | Regular session samples: phase, condition, parameter values, headset presence, and head pose. Gaze columns are intentionally excluded. |
| `eye_tracking.csv` / `eye_tracking.jsonl` | Regular gaze samples: phase/condition context plus gaze origin, direction, hit point, and painting-hit flags. Head-pose columns are intentionally excluded. |
| `video_frames.csv` / `video_frames.jsonl` | One row per encoded video frame, including camera pose, output path, latency, and cumulative dropped frame count. |
| `video_manifest.json` | Recording settings, final frame counts, dropped-frame counters by cause, and an ffmpeg example. |
| `video_frames/` | Encoded image sequence. |

Use `formal_viewing=true` and `condition_start` / `condition_end` markers to extract valid VFX exposure windows. Questionnaire breaks, headset-off intervals, re-centering, and rest intervals are explicitly marked so they can be excluded from condition-level physiology analysis.

Natural texture template fields are logged in the regular event/sample/gaze rows: `applied_intensity_value`, `applied_frequency_value`, `natural_modulation_enabled`, `natural_modulation_seed`, `natural_modulation_template_elapsed_seconds`, and the modulation depth/scale columns. For scheme A, `condition_start` rows should show the template elapsed time close to `0` for every condition.

## Video Drop Diagnostics

`droppedFrames` in `video_manifest.json` is the total count. The manifest also records cause-specific counters:

- `captureSourceDroppedFrames`
- `captureTargetDroppedFrames`
- `readbackBackpressureDroppedFrames`
- `encodeBackpressureDroppedFrames`
- `readbackErrorDroppedFrames`
- `encodeFailedDroppedFrames`
- `workerDroppedFrames`

Recent sessions commonly showed `droppedFrames=1` after a temporary AsyncGPUReadback latency spike at 15 fps. The recorder defaults now use 10 fps, JPEG quality 70, `maxPendingReadbacks=4`, and `maxQueuedEncodeFrames=8` for auto-created recorders to reduce this startup/run-time backpressure.

## LSL Markers

The project depends on `com.labstreaminglayer.lsl4unity` for PCVR event synchronization.

`WaterLiliesExperimentManager` auto-creates `WaterLiliesLslMarkerOutlet` on the `Water Lilies Experiment` object unless `Auto Create Lsl Marker Outlet` is disabled. The outlet publishes one irregular string marker stream:

- stream name: `WaterLiliesExperimentMarkers`
- stream type: `Markers`
- payload: the same JSON row written to `events.jsonl`

Use LabRecorder or another LSL recorder before starting Play Mode if the marker stream should be captured in the same `.xdf` recording as external physiology streams.
