# MediaPipe hand capture

This module uses MediaPipe Unity Plugin v0.16.3 for Windows x64 CPU inference.

## Setup

Attach `Senas.AI.MediaPipe.HandLandmarkerCapture` to a dedicated GameObject owned by the AI module. The component starts the first available webcam when enabled and loads `MediaPipe/hand_landmarker.bytes` from `StreamingAssets`.

Subscribe to `LandmarksUpdated` to receive snapshots on Unity's main thread. Each snapshot contains zero to two hands, normalized image landmarks, world landmarks, handedness, and a monotonic timestamp in milliseconds. This module only captures landmarks; it does not classify signs or write gameplay state.

The bundled Windows native library is x64 and CPU-only. Windows support for the plugin is experimental; the plugin's native code can terminate the Unity Editor on fatal errors.
