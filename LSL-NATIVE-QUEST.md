# Native LSL on Meta Quest 3S — Documentation

## Overview

This document explains how Lab Streaming Layer (LSL) was integrated natively into a Unity VR experiment running on the Meta Quest 3S. The Quest sends two LSL streams directly over WiFi — no Python bridge, no UDP relay — so LabRecorder on a laptop can record them with proper time synchronization.

### Why native LSL?

The previous approach was:

```
Quest (Unity) → UDP over WiFi → Python script (laptop) → pylsl → LabRecorder
```

This had two problems:
1. **Data loss**: actual rate was 36 Hz instead of 50 Hz (30% loss)
2. **No clock sync**: LSL's time synchronization only measured latency between the Python script and LabRecorder (both on the laptop), not between the Quest and the laptop. Any WiFi/UDP delay was invisible.

The new approach:

```
Quest (Unity + liblsl) → LSL over WiFi → LabRecorder (laptop)
```

LSL runs directly on the Quest. It handles network discovery (so LabRecorder auto-finds the streams) and continuously measures the clock offset between the Quest and the laptop.

---

## Architecture

### Two LSL streams

| Stream | Name | Type | Rate | Format | Purpose |
|--------|------|------|------|--------|---------|
| Head pose | `Quest.HeadPose` | `MoCap` | 50 Hz (FixedUpdate) | float32, 7 channels | Continuous position + orientation |
| Event markers | `Quest.Events` | `Markers` | Irregular (0 Hz) | string, 1 channel | Experiment events |

### Head pose channels

| Channel | Label | Unit | Description |
|---------|-------|------|-------------|
| 0 | PosX | m | World-space X position |
| 1 | PosY | m | World-space Y position (up) |
| 2 | PosZ | m | World-space Z position (forward) |
| 3 | RotX | quat | Quaternion X component |
| 4 | RotY | quat | Quaternion Y component |
| 5 | RotZ | quat | Quaternion Z component |
| 6 | RotW | quat | Quaternion W component |

Coordinate system: Unity left-handed, Y-up, Z-forward.

### Event markers

All experiment events are sent as string markers. Format is `event_type` or `event_type:detail`.

| Marker | Source script | When |
|--------|-------------|------|
| `app_start` | QuestEventOutlet | App launches |
| `app_quit` | QuestEventOutlet | App closes |
| `condition1_start` | ExperimentStateManager | Condition 1 begins |
| `middle_start` | ExperimentStateManager | Middle phase begins |
| `condition2_start` | ExperimentStateManager | Condition 2 begins |
| `experiment_end` | ExperimentStateManager | Experiment finished |
| `box_choose_black` | BoxChoiceManager | Participant picks black box |
| `box_choose_white` | BoxChoiceManager | Participant picks white box |
| `door_open:{name}` | DoorManager | A door opens |
| `door_close:{name}` | DoorManager | A door closes |
| `door_handle_activated:{name}` | DoorHandleInteractable | Hover-to-open handle triggered |
| `reveal_success` | RevealManager | Reveal room entered (success) |
| `reveal_fail` | RevealManager | Reveal room entered (fail) |
| `room_enter:{name}` | RoomManager | Player enters a timed room |
| `timer_start:{name}:{duration}` | RoomManager | Room countdown starts |
| `timer_end:{name}` | RoomManager | Room countdown ends |
| `glass_room_on:{name}` | RoomManager | Glass room isolation begins |
| `glass_room_off:{name}` | RoomManager | Glass room isolation ends |
| `return_plane_held` | StayOnPlaneToAdvance | Player held position to advance |

---

## How it was built

### Step 1: Cross-compile liblsl for Android ARM64

The Quest 3S runs Android on ARM64 (`arm64-v8a`). The LSL4Unity Unity package includes native libraries for Windows, Linux, and macOS — but not Android. So `liblsl.so` had to be compiled from source.

**Tools used:**
- CMake (installed via Homebrew)
- Android NDK r27c (bundled with Unity 6 at `[Unity Editor]/PlaybackEngines/AndroidPlayer/NDK/`)
- liblsl source from https://github.com/sccn/liblsl

**Build commands (run on macOS):**

```bash
# Clone liblsl
cd /tmp && mkdir liblsl-build && cd liblsl-build
git clone --depth=1 https://github.com/sccn/liblsl.git

# Set NDK path (from Unity's bundled NDK)
export NDK="/Applications/Unity/Hub/Editor/6000.2.14f1/PlaybackEngines/AndroidPlayer/NDK"

# Configure for Android ARM64
mkdir build-android-arm64 && cd build-android-arm64
cmake ../liblsl \
  -DCMAKE_TOOLCHAIN_FILE="$NDK/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI=arm64-v8a \
  -DANDROID_PLATFORM=android-29 \
  -DCMAKE_BUILD_TYPE=Release \
  -DLSL_BUILD_STATIC=OFF \
  -DLSL_UNIXFOLDERS=ON

# Build
cmake --build . --config Release -j$(sysctl -n hw.ncpu)
```

This produces `liblsl.so` (~11 MB, ELF 64-bit ARM aarch64).

**Placed at:** `Assets/Plugins/Android/arm64-v8a/liblsl.so`

The `.meta` file configures it as an Android ARM64-only plugin so Unity includes it in Quest APK builds but not in Editor or other platforms.

### Step 2: Fix LSL4Unity package for Android builds

The LSL4Unity package (installed from GitHub) had plugin configuration issues that caused Android builds to fail:

- Windows `.lib` files used `DefaultImporter` instead of `PluginImporter`, causing name collisions
- Desktop `.dll`/`.so` files had `Any: enabled: 1`, making Unity try to include them in Android builds

**Fix:** The package was converted from a git dependency to a local package:

```json
// Packages/manifest.json — before:
"com.labstreaminglayer.lsl4unity": "https://github.com/labstreaminglayer/LSL4Unity.git"

// after:
"com.labstreaminglayer.lsl4unity": "file:com.labstreaminglayer.lsl4unity"
```

The package now lives at `Packages/com.labstreaminglayer.lsl4unity/` where the `.meta` files were fixed:

| File | Fix |
|------|-----|
| `Windows/x86/lsl.lib.meta` | Changed from `DefaultImporter` to `PluginImporter`, Windows x86 only |
| `Windows/x64/lsl.lib.meta` | Changed from `DefaultImporter` to `PluginImporter`, Windows x64 only |
| `Windows/x86/lsl.dll.meta` | Added `Exclude Android: 1`, set `Any: enabled: 0` |
| `Windows/x64/lsl.dll.meta` | Added `Exclude Android: 1`, set `Any: enabled: 0` |
| `linux/liblsl.so.meta` | Added `Exclude Android: 1`, set `Any: enabled: 0` |

### Step 3: Create the streaming scripts

Two C# scripts in `Assets/Scenes/RecentScene/`:

**HeadPoseOutlet.cs** — extends LSL4Unity's `AFloatOutlet` base class:
- The base class handles outlet creation, timestamping (via `TimeSync`), and per-frame pushing
- `BuildSample()` reads `transform.position` and `transform.rotation` from the XR Camera
- Uses `FixedUpdate` at Unity's default 50 Hz fixed timestep
- Attach to: **Main Camera** (under XR Origin)

**QuestEventOutlet.cs** — uses raw `LSL.StreamOutlet` API:
- Singleton pattern: call `QuestEventOutlet.Send("marker")` from any script
- Irregular rate (event-driven, not per-frame)
- Uses raw API because LSL4Unity's `AStringOutlet` base class is designed for per-frame sampling, not on-demand events. This matches the approach in LSL4Unity's own `SimpleOutletTriggerEvent` sample.
- Attach to: any persistent GameObject (e.g., an empty `LSL` object)

### Step 4: Hook events into experiment scripts

One-line `QuestEventOutlet.Send(...)` calls were added to all experiment scripts at each significant event point. See the event markers table above for the full list.

---

## How to set up a recording session

### On the Quest (Unity scene setup)

1. Create an empty GameObject called `LSL` in the scene
2. Add the `QuestEventOutlet` component to it
3. On the **Main Camera** (under XR Origin), add the `HeadPoseOutlet` component
4. In the Inspector, verify:
   - HeadPoseOutlet: `StreamName` = "Quest.HeadPose", `StreamType` = "MoCap", `Moment` = FixedUpdate
   - QuestEventOutlet: `StreamName` = "Quest.Events", `StreamType` = "Markers"
5. Build and deploy the APK to the Quest

### On the laptop (recording)

1. Connect the laptop to the **same WiFi network** as the Quest
2. Open **LabRecorder**
3. The two streams should appear automatically:
   - `Quest.HeadPose` (7 channels, 50 Hz)
   - `Quest.Events` (1 channel, irregular)
4. Check both streams and press **Start**
5. Run the experiment on the Quest
6. Press **Stop** in LabRecorder when done
7. The `.xdf` file contains both streams with synchronized timestamps

### Verifying the recording

```bash
pip install pyxdf numpy
cd /path/to/recording/folder
python3 verify_xdf.py
```

The verification script checks:
- Both streams are present and non-empty
- Actual sampling rate matches nominal (50 Hz)
- No gaps in the data
- Quaternions are normalized (magnitude = 1.0)
- Position values are in a reasonable range
- Data is not frozen (unique samples)
- All event markers are listed with timestamps

---

## Debug logging

Both scripts log extensively with the prefix `LOG` for easy filtering in `adb logcat`:

```bash
adb logcat -s Unity | grep "LOG "
```

Key log messages:
- `LOG [*] NETWORK: Not reachable!` — WiFi is off
- `LOG [*] FATAL — liblsl native library not found!` — the `.so` is missing from the APK
- `LOG [*] liblsl loaded — version X.Y` — native lib loaded OK
- `LOG [*] Outlet created successfully` — stream is broadcasting
- `LOG [*] Streaming OK — N samples sent, consumer connected` — LabRecorder is receiving (every 10s)
- `LOG [*] NO consumer` — LabRecorder is not receiving (check network)
- `LOG [QuestEventOutlet] Marker #N: 'event'` — each event as it's sent

---

## File structure

```
VR-Experiment/
├── Assets/
│   ├── Plugins/
│   │   └── Android/
│   │       └── arm64-v8a/
│   │           └── liblsl.so              ← cross-compiled for Quest
│   └── Scenes/
│       └── RecentScene/
│           ├── HeadPoseOutlet.cs           ← continuous head tracking stream
│           ├── QuestEventOutlet.cs         ← event marker stream
│           ├── ExperimentStateManager.cs   ← (modified: added markers)
│           ├── BoxChoiceManager.cs         ← (modified: added markers)
│           ├── DoorManager.cs             ← (modified: added markers)
│           ├── DoorHandleInteractable.cs  ← (modified: added markers)
│           ├── RevealManager.cs           ← (modified: added markers)
│           ├── RoomManager.cs             ← (modified: added markers)
│           └── StayOnPlaneToAdvance.cs    ← (modified: added markers)
└── Packages/
    ├── manifest.json                      ← references local LSL4Unity
    └── com.labstreaminglayer.lsl4unity/   ← local copy with fixed .meta files
```

---

## Rebuilding liblsl (if needed)

If you need to rebuild liblsl (e.g., for a different NDK version or Quest model):

1. Find your Unity NDK path: `[Unity Editor]/PlaybackEngines/AndroidPlayer/NDK/`
2. Check NDK version: `cat [NDK path]/source.properties`
3. Follow the build commands in Step 1 above, updating the `NDK` path
4. Replace `Assets/Plugins/Android/arm64-v8a/liblsl.so` with the new build
5. Verify architecture: `file liblsl.so` should say `ELF 64-bit LSB shared object, ARM aarch64`
