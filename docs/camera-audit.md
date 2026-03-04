# Camera Behavior Audit

## Scope searched
`Assets/**/*.cs` was searched for camera and zoom related APIs/terms (`orthographicSize`, `PixelPerfectCamera`, `zoom`, `bounds`, `clamp`, `follow`, `MinimapCamera`, `RenderTexture`, `viewport`, `projection`).

## Runtime scripts touching camera behavior

| Script | Path | Controls | Runs in | Camera target |
|---|---|---|---|---|
| ArenaCameraPolicy | `Assets/Presentation/Camera/ArenaCameraPolicy.cs` | Main camera zoom policy, fit-to-bounds, clamp-to-bounds, arena bounds binding, optional pixel-grid snap | `Reset`, `OnValidate`, `Awake`, `LateUpdate`, public methods (`StepZoom`, `FitToBounds`, `BindArenaBounds`) | Main camera (`targetCamera`) |
| ArenaCameraControls | `Assets/Presentation/Camera/ArenaCameraControls.cs` | Pan input + scroll-wheel zoom input forwarding (`policy.StepZoom`) | `Reset`, `Awake`, `Update`, `OnDisable`, `OnApplicationFocus` | Main camera via `ArenaCameraPolicy.targetCamera` |
| CameraFollowController | `Assets/Presentation/Broadcast/CameraFollowController.cs` | Follow selected target, optional clamp to world bounds from `ArenaCameraPolicy` | `LateUpdate`, `SnapToTargetNow` | Main camera (`mainCamera`) |
| MinimapClickToPan | `Assets/Presentation/Minimap/MinimapClickToPan.cs` | Click/drag on minimap to reposition main camera in world bounds rect | `Awake`, `OnPointerClick`, `OnDrag` | Main camera (`mainCamera`) |
| MinimapMarkerOverlay | `Assets/Presentation/Broadcast/MinimapMarkerOverlay.cs` | Converts world positions to minimap viewport positions for markers | `LateUpdate` | Minimap camera (`minimapCamera`) |
| PresentationBoundsSync | `Assets/SimCore/Runtime/PresentationBoundsSync.cs` | Applies runtime world bounds to scripts, sets main camera framing once, sets minimap camera orthographic size | Static `ApplyFromConfig` / `Apply` call path | Main camera + `MinimapCamera` |
| Bootstrapper | `Assets/SimCore/Runtime/Bootstrapper.cs` | Binds `ArenaBounds` collider to `ArenaCameraPolicy`, triggers fit-to-bounds on sim switch/reset | `SyncCameraBoundsAndFit` (called from sim startup/switch flows) | Main camera policy |
| FantasySportRunner / MarbleRaceRunner | `Assets/Simulations/FantasySport/FantasySportRunner.cs`, `Assets/Simulations/MarbleRace/MarbleRaceRunner.cs` | Creates/updates `ArenaBounds` collider and calls `policy.BindArenaBounds(..., fitToBounds: true)` | Runner setup/reset paths | Main camera policy |
| AntColoniesRunner / RaceCarRunner / FantasySportRunner | `Assets/Simulations/AntColonies/AntColoniesRunner.cs`, `Assets/Simulations/RaceCar/RaceCarRunner.cs`, `Assets/Simulations/FantasySport/FantasySportRunner.cs` | Emergency main camera creation if none exists, sets initial `orthographicSize` | setup path | Main camera |

## Minimap camera + RenderTexture wiring

| Script | Path | Controls |
|---|---|---|
| RecreateBroadcastUiMenu (Editor) | `Assets/Editor/RecreateBroadcastUiMenu.cs` | Configures `MinimapCamera` and assigns `RT_Minimap` RenderTexture; editor-only scene wiring utility |

## Property write conflicts / overlaps

1. **Root-cause conflict:** `ArenaCameraPolicy.ApplyZoom()` previously returned early when `PixelPerfectCamera` was missing, so scroll input still fired but no zoom property was changed at all.
2. `ArenaCameraPolicy.FitToBounds()` and `PresentationBoundsSync.EnsureMainCameraFraming()` both can set `mainCamera.orthographicSize`, but both are startup/reconfigure events (not continuous per-frame fighting).
3. `ArenaCameraPolicy` and `CameraFollowController` both update camera position in `LateUpdate`, but on different transforms in common wiring (`policy` moves rig transform; follow writes `mainCamera.transform`). This is coordinated usage, not zoom conflict.
4. No other runtime script continuously overwrites `ArenaCameraPolicy` zoom every frame.

## Root cause identified

**Exact zoom application method:** `ArenaCameraPolicy.ApplyZoom()`.

**Failure mode:** when `pixelPerfectComponent == null`, the method logged warning and `return`ed without applying any fallback to `Camera.orthographicSize`. This made zoom appear "dead" whenever PixelPerfectCamera was disabled or absent.

## Fix applied

- Added a minimal fallback path in `ArenaCameraPolicy.ApplyZoom()`:
  - If PixelPerfectCamera is missing **or disabled**, zoom now applies to `targetCamera.orthographicSize` using the same `zoomLevel` + `zoomStep` factor.
  - If PixelPerfectCamera is active, existing pixel-perfect reference-resolution behavior is unchanged.
- Added active-state check for PixelPerfect (`Behaviour.isActiveAndEnabled`) so disabled PPC correctly uses fallback.
- Cached base orthographic size once at startup to keep fallback zoom relative to scene-configured camera framing.

