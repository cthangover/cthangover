# Recruiting

The recruiting system manages the post-battle recruit roster. It is a **standalone mod** (`mods/recruiting/src/`): it owns its data, rules, save state and event subscription, and no other mod depends on it. Removing the mod removes recruiting entirely — battles, cooking, story content and the core keep working.

## How it is triggered

The recruiting mod never knows about battles. Whatever system defeats characters publishes a core-neutral event on the global bus:

```csharp
public class CharactersDefeatedEvent : ModEvent
{
    public string   Source       { get; init; }  // "battle", "trap", ...
    public string[] CharacterIds { get; init; }
}
```

- `turn_battle_base` publishes it from the victory screen (`Source = "battle"`) after EXP and loot are processed;
- `RecruitingInitializer` subscribes to it and routes it into `DefeatedCharactersRecruiter`, which resolves the templates through `CharacterFactory`, runs the condition gate, rolls `RecruitmentChance` and adds the winner to the roster.

Because the event type lives in the core and carries ids only, neither side depends on the other, and any mod (or GDScript subscriber via the `characters_defeated` key) can publish or observe group defeats.

## Removing the mod

With `recruiting` disabled:

- `turn_battle_base` still publishes `CharactersDefeatedEvent` — into an empty subscription list;
- `visual_novel_core` and `cooking` load normally (they no longer depend on recruiting);
- the save file simply has no `recruiting` slot;
- a core save from an older version opens with an empty roster (no backward-compatible `Recruits` blob is read).

## Data structures

### `Recruit`

| Property | Type | Description |
|---|---|---|
| `ID` | `string` | Unique instance ID (auto-generated Guid) |
| `CharacterID` | `string` | Template character ID (references a character definition) |
| `Properties` | `PropertyData` | Mutable state bag - stores per-recruit custom data |

`Properties` is a free-form key-value bag. The conventional health key is
formalized as the constant `Recruit.PROP_HEALTH` (`"Health"`): `RecruitingData.Add`
seeds it to 10, and behaviours read/write it via
`recruit.Properties.GetInt(Recruit.PROP_HEALTH)` / `SetInt(...)` — e.g. the
built-in `NutritionBehaviour` drains it tick by tick until the recruit is fed.

### `RecruitingData`

The roster is a session singleton — there is exactly one per playthrough:

| Method/Property | Description |
|---|---|
| `Instance` | Static roster owned by the mod (replaces the old `RuntimeData.RecruitingData`) |
| `Data` | `List<Recruit>` - current roster |
| `HasID(id)` | Check if a recruit exists |
| `GetByID(id)` | Look up a recruit by instance ID |
| `Add(id, characterID)` | Create a new recruit (uses the given `id`, falling back to a fresh Guid if taken; initialises Health=10 via `Recruit.PROP_HEALTH`; fires `OnConfigure`). **Returns the created `Recruit` instance** so callers can set additional state immediately |
| `Remove(id)` | Remove a recruit from the roster |

A new game clears `Data` through the `NewGameStartedEvent` subscription in
`RecruitingInitializer`; loading a save replaces it via `RecruitingSaveable`.

### `RecruitBehaviourRegistry`

Registry and dispatch hub for recruit lifecycle hooks **and** recruitment
conditions. Implementations are auto-discovered from mod assemblies by
`RecruitingInitializer` — the initializer scans each mod assembly as it is
announced and does a full AppDomain pass at resources-ready. Registration is
id-keyed: a rescan replaces the implementation with the same `Id`, so mod
reloads do not double-dispatch. Dispatch is cross-recruit: every registered
`IRecruitBehaviour` instance receives every active recruit on tick, configure,
and removal events — not just recruits from its own mod.

| Member | Description |
|---|---|
| `Instance` | Static eager singleton (`public static readonly`) - no lazy wrapper, deliberately, to tolerate partial-initialisation during reflection discovery |
| `RegisterAssembly(Assembly)` | Scans one assembly for `IRecruitBehaviour` and `IRecruitCondition` implementations (parameterless constructor required) and registers them by `Id` |
| `RegisterAll()` | Rescans every loaded assembly; used at resources-ready |
| `CanRecruit(Character enemy, RuntimeData runtime)` | Conjunctive gate over all registered conditions - returns true only if **every** `IRecruitCondition` passes |
| `Get(string id)` | Behaviour lookup by its `Id`; linear scan, `null` if absent |
| `All()` | All registered behaviours |
| `OnTick()` | Dispatches `OnTick(recruit, runtime, currentTick)` to every behaviour for every active recruit; lazily ensures the scene timer registration on first call |
| `OnConfigure(Recruit)` | Dispatches `ConfigureRecruit` to every behaviour for a newly added recruit |
| `OnRemove(Recruit)` | Dispatches `OnRemove` to every behaviour for a departing recruit |

### Built-in rules

The mod ships two rules that used to live in content mods:

- `EnslaveCondition` (`IRecruitCondition`, `Id = "enslave"`) — the recruitment gate requires the player to own the `slave_master` skill (checked through `RuntimeData.SkillData`).
- `NutritionBehaviour` (`IRecruitBehaviour`, `Id = "nutrition"`) — seeds `FullnessTime` when a recruit joins and drains `PROP_HEALTH` once the 24 in-game-hour fullness window is exceeded.

Because they live inside `recruiting`, disabling the mod disables the gate and the hunger loop together, without touching the content mods.

## Custom recruit behaviour

Implement `IRecruitBehaviour` to add custom logic on recruit lifecycle events. External mods that implement the interfaces declare `"depends": ["recruiting>=1"]` so their assembly compiles after the mod. Discovery is automatic. Because dispatch is cross-recruit — each behaviour receives every active recruit on each event — a behaviour can also introspect other recruits:

```csharp
public class MyRecruitBehaviour : IRecruitBehaviour
{
    // Unique identifier for RecruitBehaviourRegistry.Get lookup
    public string Id => "my_mod/loyalty";

    public void ConfigureRecruit(Recruit recruit, RuntimeData runtime)
    {
        // Called when a recruit is added - set initial properties;
        // RecruitingData.Add returns the instance, but this hook fires too
        recruit.Properties.SetInt("loyalty", 50);
    }

    public void OnTick(Recruit recruit, RuntimeData runtime, long currentTick)
    {
        // Called every game tick for each active recruit;
        // currentTick is RuntimeData.Time.Tick - use it for periodic logic
    }

    public void OnRemove(Recruit recruit, RuntimeData runtime)
    {
        // Called when a recruit is removed - clean up state
    }
}
```

## `IRecruitCondition`

A gating predicate consulted before any recruitment roll happens
(`mods/recruiting/src/IRecruitCondition.cs`). Conditions receive the defeated
`Character` template and the full `RuntimeData`, so they can inspect stats,
quest state, inventory, or relationship flags:

```csharp
public class MyRecruitCondition : IRecruitCondition
{
    // Unique identifier for debugging and override lookups
    public string Id => "my_mod/level_cap";

    public bool CanRecruit(Character enemy, RuntimeData runtime)
    {
        return enemy.Level <= 5;
    }
}
```

Rules:

- Conditions are auto-discovered by `RecruitingInitializer` - just ship a public class.
- The gate is **conjunctive**: all discovered conditions must return `true`.
  A single `false` blocks recruitment for that enemy - mods compose gates
  without coordinating with each other.
- If no conditions are registered at all, the gate passes vacuously.

## `RecruitTickController`

Periodic tick handler that processes recruit-related updates (e.g. hunger depletion, status regeneration). It registers itself with the scene's `SceneEventController` via `AddTimerTickEventListener` on the first lifecycle call, so no timer cost is paid when no behaviours are loaded.

## Recruitment flow

1. Characters are defeated and a system publishes `CharactersDefeatedEvent` (e.g. `turn_battle_base`'s victory screen);
2. `DefeatedCharactersRecruiter` resolves each id through `CharacterFactory` and skips templates with `RecruitmentChance <= 0`;
3. The condition gate must pass: `RecruitBehaviourRegistry.CanRecruit` (all `IRecruitCondition`s, including the built-in `EnslaveCondition`);
4. The engine rolls 1-100 against the enemy's `RecruitmentChance`;
5. On success the enemy is registered via `RecruitingData.Instance.Add`;
6. On acceptance in the UI: `character.add_to_party` action fires → `CharacterData.AddCharacterToParty()` → creates `CharacterInfoData` entry;
7. Character joins the battle set and becomes controllable.

## Save state

`RecruitingSaveable` implements `IModSaveable` and stores the roster in the mod's own slot (mod id `recruiting`) as a `Recruits` `DataBlob`. `ModSaveableRegistry` discovers it through `ModAssemblyLoader` automatically. The core save (`__core__` slot) no longer contains a `Recruits` blob, and backward compatibility with old saves is not provided — old saves start with an empty roster.

On load the roster is replaced wholesale without re-firing `ConfigureRecruit`: persisted per-recruit properties (for example the nutrition timer) are restored as-is, so behaviours continue their tick logic from the saved state.

## Scenario integration

Use `character.add_to_party` to recruit directly from a scenario:

```scenario
action character.add_to_party type=Murakami
```

Show a notification without roster changes:

```scenario
action character.send_notification type=Murakami
```

## Relationship to skills

Recruited characters can acquire skills. `SkillData` is not a singleton - it is a plain instance owned by `RuntimeData`, accessible as `GameData.Instance.Runtime.SkillData`. It holds a single global set of player-owned skill IDs (lowercase strings such as `"slave_master"`) and is persisted with the save. Skills are not tied to individual recruits - they are a global unlock pool.
