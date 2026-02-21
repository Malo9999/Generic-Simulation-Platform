# Run Events Schema v1 (`events.jsonl`)

`events.jsonl` is a JSON Lines file. Each line is one independent JSON object representing one simulation event.

## Format

- File extension: `.jsonl`
- Encoding: UTF-8
- One event per line
- No outer array
- Line order is the event stream order (typically ascending `tick`)

## Top-level fields

- `schemaVersion` (int, required)
  - Schema version for compatibility. v1 is `1`.
- `tick` (int, required)
  - Simulation tick index at which the event occurred.
- `eventType` (string, required)
  - Stable event vocabulary identifier.
- `entityId` (string, optional)
  - Entity that emitted or owns the event.
- `teamId` (string, optional)
  - Team/group identifier for event context.
- `position` (object, optional)
  - 2D world position object with float members `{ "x": <float>, "y": <float> }`.
- `payload` (object, required)
  - Event-specific data map. Arbitrary extra keys are allowed.

## EventType vocabulary (v1)

1. `Spawn`
2. `Despawn`
3. `Score`
4. `Hit`
5. `Pickup`
6. `Dropoff`
7. `Lap`
8. `Overtake`
9. `Goal`
10. `HighlightTag`

## Example lines

```json
{"schemaVersion":1,"tick":0,"eventType":"Spawn","entityId":"runner-1","teamId":"blue","position":{"x":1.5,"y":2},"payload":{"prefab":"RunnerBot","kind":"racer","velocity":{"x":0.5,"y":0}}}
{"schemaVersion":1,"tick":0,"eventType":"Spawn","entityId":"runner-2","teamId":"red","position":{"x":2.5,"y":2},"payload":{"prefab":"RunnerBot","kind":"racer","velocity":{"x":0.4,"y":0.1}}}
{"schemaVersion":1,"tick":12,"eventType":"Pickup","entityId":"runner-1","teamId":"blue","position":{"x":3,"y":2.4},"payload":{"itemId":"flag-a","itemKind":"flag"}}
{"schemaVersion":1,"tick":20,"eventType":"Hit","entityId":"runner-2","teamId":"red","position":{"x":3.8,"y":2.4},"payload":{"targetEntityId":"runner-1","damage":15,"weapon":"pulse"}}
{"schemaVersion":1,"tick":30,"eventType":"Dropoff","entityId":"runner-1","teamId":"blue","position":{"x":5.5,"y":2.5},"payload":{"itemId":"flag-a","destination":"base-blue"}}
{"schemaVersion":1,"tick":31,"eventType":"Score","entityId":"runner-1","teamId":"blue","payload":{"delta":1,"total":1}}
{"schemaVersion":1,"tick":44,"eventType":"Overtake","entityId":"runner-2","teamId":"red","position":{"x":6.1,"y":2.6},"payload":{"passedEntityId":"runner-3","newPosition":2}}
{"schemaVersion":1,"tick":50,"eventType":"Lap","entityId":"runner-2","teamId":"red","payload":{"lapNumber":1,"lapTimeMs":60420}}
{"schemaVersion":1,"tick":62,"eventType":"Goal","entityId":"runner-1","teamId":"blue","payload":{"goalType":"checkpoint","value":0.75}}
{"schemaVersion":1,"tick":70,"eventType":"HighlightTag","entityId":"runner-2","payload":{"tag":"photo_finish","severity":"high"}}
{"schemaVersion":1,"tick":100,"eventType":"Despawn","entityId":"runner-3","teamId":"green","payload":{"reason":"eliminated"}}
```
