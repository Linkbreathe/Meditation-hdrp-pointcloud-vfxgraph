# Water Lilies VR-VFX Experiment Prototype

This prototype runs a fixed Water Lilies point-cloud VFX stimulus for the first-stage Intensity x Frequency data collection experiment.

## Scene Entry

Open `Assets/Scenes/Meditation.unity`.

The scene contains a root object named `Water Lilies Experiment` with:

- `WaterLiliesExperimentManager`
- `WaterLiliesExperimentLogger`
- `WaterLiliesTrackingSampler`

The manager loads `Assets/Resources/WaterLiliesExperimentConfig.asset` by default.

When Play Mode starts, the manager previews the fixed `5_Water_Lilies` painting with baseline parameters. The experiment clock and event log still begin only after `S` / Start.

`2d_paintings/5_Water_Lilies` is visible in the scene hierarchy so researchers can position, rotate, and scale it manually. The manager's `Target Painting` reference points to this object. `Place Painting In Front Of Viewer` is disabled by default; enable it only for quick debugging when you want Play Mode to move the painting in front of the current `MainCamera` / `CenterEyeAnchor`.

## Controls

- `S`: start experiment
- `Space`: continue after questionnaire break
- `H`: manually record `headset_removed`
- `J`: manually record `headset_worn`
- `K`: pilot-mode skip current timed phase
- `Esc`: abort experiment

The runtime overlay is intended as a researcher/operator status panel. It includes Start, Continue, Headset Removed, Headset Worn, and Skip buttons. Skip follows the same rule as `K`: it only works in Pilot mode when pilot skipping is enabled. The overlay uses a screen-space Unity Canvas for the desktop Game view, not a world-space VR instruction panel for the participant. Keep `Auto Create Runtime Ui` enabled for editor/operator runs, and disable it if a Quest build should show no experiment-control UI to the participant.

## Mode-Specific Durations

`Mode` selects the active duration profile at runtime. In the config Inspector, edit `Active Durations (Formal)` or `Active Durations (Pilot)` for the mode currently selected, and edit `Other Mode Durations (...)` when preparing the alternate mode. The manager reads only the selected mode's duration profile when the run starts.

## Condition Design

The default config defines 9 fixed conditions:

- `C1`: Low intensity + Low frequency
- `C2`: Low intensity + Medium frequency
- `C3`: Low intensity + High frequency
- `C4`: Medium intensity + Low frequency
- `C5`: Medium intensity + Medium frequency
- `C6`: Medium intensity + High frequency
- `C7`: High intensity + Low frequency
- `C8`: High intensity + Medium frequency
- `C9`: High intensity + High frequency

Default level values:

- Low = `0.20`
- Medium = `0.50`
- High = `0.80`

Pilot fallback values can be entered directly in the config:

- Low = `0.15`
- Medium = `0.40`
- High = `0.65`

The VFX parameters are engineering controls only. They are not interpreted as calmness, meditation state, engagement, or relaxation.

## Flow

The formal sequence is:

1. `experiment_start`
2. `baseline_start` / `baseline_end`
3. `adaptation_start` / `adaptation_end`
4. For each condition:
   - `condition_prepare`
   - `recenter_start` / `recenter_end`
   - `condition_start` / `condition_end`
   - `questionnaire_break_start`
   - `headset_removed`
   - participant completes Google Form outside Unity
   - `headset_worn`
   - `questionnaire_break_end`
5. After every 3 completed conditions:
   - `rest_start` / `rest_end`
6. `experiment_end`

If headset presence is available, `headset_removed` and `headset_worn` are recorded automatically. Otherwise, use the manual keys. By default, each questionnaire break must include both markers before `Space` / Continue can advance. Disable `Require Headset Cycle Before Questionnaire Continue` only for editor-only dry runs.

For Meta Quest Pro, gaze sampling first tries Unity XR `Eyes` data and then falls back to the Meta/OVR gaze transforms named `[BuildingBlock] Eye Gaze Left` and `[BuildingBlock] Eye Gaze Right` if they are present in the scene.

## LSL Markers

The project depends on `com.labstreaminglayer.lsl4unity` for PCVR event synchronization.

`WaterLiliesExperimentManager` auto-creates `WaterLiliesLslMarkerOutlet` on the `Water Lilies Experiment` object unless `Auto Create Lsl Marker Outlet` is disabled. The outlet publishes one irregular string marker stream:

- stream name: `WaterLiliesExperimentMarkers`
- stream type: `Markers`
- payload: the same JSON row written to `events.jsonl`

Use LabRecorder or another LSL recorder before starting Play Mode if you want the marker stream captured in the same `.xdf` recording as external physiology streams.

## Logs

Each session writes to:

`Application.persistentDataPath/water_lilies_vfx_experiment/<session_id>/`

Files:

- `events.csv`
- `events.jsonl`
- `samples.csv`
- `samples.jsonl`

Important columns:

- `event_type`
- `participant_id`
- `experiment_mode`
- `phase`
- `condition_id`
- `condition_order_index`
- `intensity_level`
- `frequency_level`
- `intensity_value`
- `frequency_value`
- `formal_viewing`
- `utc_timestamp_iso`
- `unix_time_ms`
- `realtime_since_startup_seconds`
- `session_elapsed_seconds`
- `headset_presence_available`
- `headset_user_present`
- `headset_off_interval_seconds`
- `head_position_*`
- `head_rotation_*`
- `head_velocity_*`
- `head_angular_velocity_deg_s`
- `gaze_available`
- `gaze_on_painting`

Use `formal_viewing=true` and `condition_start` / `condition_end` markers to extract valid VFX exposure windows. Questionnaire breaks, headset-off intervals, re-centering, and rest intervals are explicitly marked so they can be excluded from condition-level physiology analysis.
