# Time System

The in-game clock tracks time progression and drives day/night lighting cycles. Time affects quest progression, character stats, and scene appearance via the `light_use_time` feature.

## Time data structure

Time is tracked in `TimeData` as a single tick counter, from which all other values are derived:

| Property | Type | Description |
|---|---|---|
| `Tick` | `long` field | Raw clock tick (increments by 1 per in-game minute) |
| `Years` | `int` | Current year |
| `Months` | `int` | Current month (0–11) |
| `Days` | `int` | Current day (0–29) |
| `Hours` | `int` | Current hour (0–23) |
| `Minutes` | `int` | Current minute (0–59) |
| `Text` | `string` | Formatted display string (`"HH:MM"`) |

### Time-of-day properties

| Property | Description |
|---|---|
| `IsMorning` | True between 06:00 and 09:59 |
| `IsDay` | True between 10:00 and 17:59 |
| `IsEvening` | True between 18:00 and 21:59 |
| `IsNight` | True between 22:00 and 05:59 |
| `Phase` | `PhaseType` enum: `Morning`, `Day`, `Evening`, `Night` |
| `Normalized` | Float hour of day — `Hours + Minutes / 60f`, range 0.0–23.983 — used by lighting shaders |

## Time phases

| Phase | Hour Range | Description |
|---|---|---|
| `Morning` | 06:00–09:59 | Dawn lighting |
| `Day` | 10:00–17:59 | Full daylight |
| `Evening` | 18:00–21:59 | Dusk lighting |
| `Night` | 22:00–05:59 | Dark, lamp-dependent |

## Time advancement

- `TimeTickController` advances time on a real-time timer when enabled
- Scenario actions can manually set or advance time
- `TimeData.AddTick()` increments by one minute, recalculating all derived properties
- `TimeData.AddTime(years, months, days, hours, minutes)` advances by a specific amount

## Lighting integration

The `light_use_time` DSL command and `light_use_time` metadata field control time-of-day-driven lighting:

```scenario
light_use_time true
```

When enabled, the lighting shader reads `TimeData.Normalized` to adjust scene colour temperature and lamp intensity based on the time of day. The player's lamp (`LampData`) provides a radius and influence value that the shader uses as a light source during night phases.

In scenario metadata:

```yaml
scene: home_kitchen
priority: 10
light_use_time: true
---
# scenario body
```

## Saving

Time is persisted in save files as a `long` tick value. On load, all derived properties (hours, phases, etc.) are recalculated.
