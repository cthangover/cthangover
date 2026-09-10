# Status Effects

Status effects are temporary buffs, debuffs, or crowd-control effects applied to characters during battle. Defined as JSON data and managed through `StatusEffectQueue` on each character.

## JSON structure

```jsonc
{
  // Unique status effect identifier — used to look up and apply effects
  "Id": "stun",
  // Locale key for the effect's display name in tooltips and the status panel
  "Name": "status/stun/name",
  // Locale key explaining what the effect does
  "Description": "status/stun/desc",
  // Effect category: "Buff" (positive), "Debuff" (negative), or "Stun" (skip turn)
  "Type": "Stun",
  // Number of turns the effect lasts before expiring
  "Duration": 2,
  // Behaviour set ID — resolved via StatusEffectActionFactory into IStatusActions
  "Actions": "stun",
  // Icon filename loaded from the characters mod group
  "Icon": "stun"
}
```

## Field reference

| Field | Type | Description |
|---|---|---|
| `Id` | string | Unique effect identifier |
| `Name` | string | Locale key for display name (shown in tooltips and status panel) |
| `Description` | string | Locale key explaining what the effect does |
| `Type` | string | `"Buff"`, `"Debuff"`, or `"Stun"` |
| `Duration` | int | Number of turns the effect lasts before expiring |
| `Actions` | string | Behaviour set ID, resolved via `StatusEffectActionFactory` into an `IStatusActions` instance |
| `Icon` | string | Icon filename loaded from the `characters` mod group |

## Status effect types

| Type | Description |
|---|---|
| `Buff` | Positive effect that benefits the bearer (e.g. stat boost, regeneration). Rendered with a friendly tint in the UI |
| `Debuff` | Negative effect that harms the bearer (e.g. damage over time, stat reduction). Rendered with a hostile tint |
| `Stun` | Crowd-control — causes the bearer to skip their turn. Checked by the battle engine via `StatusEffectQueue.HasStun()` |

## Status effect index file

Status effects are registered via an index file:

```jsonc
{
  // $ref entries — ${stun} expands to status_effects/stun.json with its contents merged in
  "Items": [
    "${stun}"
  ]
}
```

## Runtime behaviour

- Each character has a `StatusEffectQueue` — an ordered list of active status effects
- `RemainingTurns` decrements in both turn phases: in `OnTurnStart` (only for timed effects with `Duration > 0`) and unconditionally in `OnTurnEnd`
- When `RemainingTurns` reaches 0, natural expiry fires `OnFinalAction`; manual removal via `Remove()` fires `OnRemove` instead
- The battle engine checks `HasStun()` to skip a stunned character's turn
- Effect behaviour callbacks are provided by `IStatusActions`, resolved from the `Actions` field via the factory

## StatusEffectQueue API reference

The queue lives in `Core/Battle/StatusEffect/StatusEffectQueue.cs` (namespace `Cthangover.Core.Cards.StatusEffect`). It is attached per character as `Character.StatusEffectQueue`, created automatically in the `Character` constructor.

| Member | Description |
|---|---|
| `ActiveStatusEffects` | Defensive copy of the internal effect list (a new `List` per access) — safe to enumerate, external code cannot mutate the backing store |
| `Add(string statusEffectId, int duration = -1)` | Resolves the ID via `StatusEffectInfoFactory`, constructs a `StatusEffectItem`, fires `OnApply`, and appends it. Returns `false` when the factory lookup fails. A positive `duration` overrides both `Duration` and `RemainingTurns` from the definition |
| `Remove(string statusEffectId)` | Removes the first effect matching the ID, firing `OnRemove` first. Returns `false` if nothing matched |
| `HasStun()` | `true` if any active effect has type `Stun` — checked by the battle engine to skip turns |
| `OnTurnStart()` | Fires `OnTurnStart` on each effect, decrements `RemainingTurns` for timed effects (`Duration > 0`), and expires at zero via `OnFinalAction` |
| `OnTurnEnd()` | Fires `OnTurnEnd` on each effect, decrements `RemainingTurns` unconditionally, removes expired effects via `Remove` (which also fires `OnRemove`) |
| `OnDealDamage(Character target, ref int damage)` | Pipes outgoing damage through every effect's `OnDealDamage` hook, in insertion order |
| `OnTakeDamage(Character source, ref int damage)` | Pipes incoming damage through every effect's `OnTakeDamage` hook |
| `ClearAll()` | Fires `OnRemove` on each active effect, then clears the list |
| `Copy(Character newOwner)` | Builds a new queue bound to `newOwner`; each effect is cloned via `Copy()` and `OnApply` is re-fired in the new owner's context |

Timing semantics worth knowing:

- `IStatusEffect.Duration <= 0` means the effect never expires naturally — only `Remove()` or `ClearAll()` ends it
- `StatusEffectItem.RemainingTurns` is tracked independently of the definition's `Duration`, so the original value survives for display or re-apply logic
- `StatusEffectItem.Copy()` is a shallow clone: primitives are copied while the stateless `Actions` singleton is shared by reference

Built-in behaviour sets live under `Core/Battle/StatusEffect/Impls/` — e.g. `StunStatusEffect` (`ID = "basic/stun"`) implements every `IStatusActions` hook as a no-op, because stun is detected via `HasStun()` rather than through action callbacks.

## Implementing custom status effect behaviour

Create an `IStatusActions` implementation. Registration is fully automatic: the `StatusEffectActionFactory` constructor calls `Reflections.FindAndCreate<IStatusActions>()`, and mod assemblies are scanned by `ModAssemblyLoader` — no manual registration is needed:

```csharp
public class PoisonStatusActions : IStatusActions
{
    // Mandatory factory key matching the JSON "Actions" field (e.g. "basic/stun")
    public string ID => "basic/poison";

    public void OnApply(Character target) { /* called when effect is applied */ }
    public void OnTurnStart(Character target) { /* start-of-turn tick: DoT, regen */ }
    public void OnTurnEnd(Character target) { /* end-of-turn hook */ }
    public void OnDealDamage(Character source, Character target, ref int damage) { /* amplify/reduce outgoing damage */ }
    public void OnTakeDamage(Character target, Character source, ref int damage) { /* amplify/reduce incoming damage */ }
    public void OnRemove(Character target) { /* manual removal (cleanse) */ }
    public void OnFinalAction(Character target) { /* natural expiry */ }
    public void ModifyAttributes(CharacterAttributes attributes) { /* passive stat modifier */ }
}
```

The mandatory `string ID` property is the factory key matched against the effect's `Actions` field.

## Locale keys

```properties
status/stun/name = Stunned
status/stun/desc = Character skips their next turn
```
