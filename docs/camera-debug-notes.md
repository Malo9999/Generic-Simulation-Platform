# Camera debug notes

## Scope scanned
Before behavior changes, runtime camera writes were scanned for:
- `Camera.orthographicSize`
- camera `transform.position`
- Pixel Perfect members (`refResolutionX/refResolutionY/refResolution`, `assetsPPU`, `cropFrameX/cropFrameY`, `upscaleRT`)
- bounds-driven clamp code

Primary runtime writers found:
- `Assets/Presentation/Camera/ArenaCameraPolicy.cs` (`ApplyZoom`, `FitToBounds`, `LateUpdate` clamp)
- `Assets/SimCore/Runtime/PresentationBoundsSync.cs` (`EnsureMainCameraFraming`)
- startup fallback camera creation in sim runners (`AntColoniesRunner`, `RaceCarRunner`, `FantasySportRunner`, `PredatorPreyDocuRunner`)
- camera position helpers (`CameraFollowController`, `MinimapClickToPan`, `MinimapSelectToFollow`)

## Root conflict identified
1. **Fit vs zoom state mismatch**: `FitToBounds` computed a required framing, but zoom state could later re-apply a different size via `ApplyZoom` in subsequent updates.
2. **Bounds interaction during zoom-out**: bounds logic should constrain camera center/position, but not silently cap orthographic size during broadcast-style zoom-out.
3. **PixelPerfect runtime pressure**: changing PixelPerfect reference resolution aggressively at runtime can create warning-prone or counterintuitive framing behavior if ref resolution exceeds practical render target expectations.

## Fixes applied
- Added a central `SetOrtho(float value, string writer)` path in `ArenaCameraPolicy` and routed policy-owned orthographic writes through it.
- Added `SetOrthoFromExternal(float value, string writer, bool syncZoomLevel)` for external callers so writer attribution is preserved.
- `FitToBounds` now computes and stores zoom state using inverse zoom math (`SyncZoomLevelToOrtho`) before applying zoom, so later `ApplyZoom` preserves fitted framing.
- Kept zoom-out permissive: no orthographic-size cap from bounds; clamp behavior remains position/center-based.
- For FantasySport, PixelPerfect ref resolution is held at base `480x270` during zoom flow, while crop remains off and upscaleRT is disabled under existing FantasySport override.
- `PresentationBoundsSync` now routes framing ortho updates through `ArenaCameraPolicy` when available (writer tracked and zoomLevel synchronized).

## Debug instrumentation added
`ArenaCameraPolicy.debugHud` now renders an on-screen HUD in Play Mode with live values:
- `Screen.width/height`
- PixelPerfect active state and ref resolution
- zoom state (`zoomLevel`, min/max, `zoomStep`)
- desired and applied ortho
- active bounds rect
- FitToBounds activity (frame/time)
- last ortho writer + frame

This HUD is toggleable from the inspector and is intended to verify startup framing and zoom ownership in real time.
