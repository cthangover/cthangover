# Recruiting

The recruiting system manages character recruitment after battles. When a character is defeated with a non-zero `RecruitmentChance`, the game may offer to recruit them into the player's party.

## Data structures

### `Recruit`

| Property | Type | Description |
|---|---|---|
| `ID` | `string` | Unique instance ID (auto-generated Guid) |
| `CharacterID` | `string` | Template character ID (references a character definition) |
| `Properties` | `PropertyData` | Mutable state bag — stores per-recruit custom data |

`Properties` is a free-form key-value bag. The conventional health key is
formalized as the constant `Recruit.PROP_HEALTH` (`"Health"`): `RecruitingData.Add`
seeds it to 10, and behaviours read/write it via
`recruit.Properties.GetInt(Recruit.PROP_HEALTH)` / `SetInt(...)` — e.g. the core
cooking mod's `NutritionBehaviour` drains it tick by tick until the recruit is fed.

### `RecruitingData`

| Method/Property | Description |
|---|---|
| `Data` | `List<Recruit>` — current roster |
| `HasID(id)` | Check if a recruit exists |
| `GetByID(id)` | Look up a recruit by instance ID |
| `Add(id, characterID)` | Create a new recruit (uses the given `id`, falling back to a fresh Guid if taken; initialises Health=10 via `Recruit.PROP_HEALTH`; fires `OnConfigure`). **Returns the created `Recruit` instance** so callers can set additional state immediately |
| `Remove(id)` | Remove a recruit from the roster |

### `RecruitBehaviourRegistry`

Registry and dispatch hub for recruit lifecycle hooks **and** recruitment
conditions. Implementations are auto-discovered from mod assemblies via
`RecruitBehaviourRegistry.RegisterAssembly` (called by `ModAssemblyLoader`).
Dispatch is cross-recruit: every registered `IRecruitBehaviour` instance receives every active recruit on tick, configure, and removal events — not just recruits from its own mod.

| Member | Description |
|---|---|
| `Instance` | Static eager singleton (`public static readonly`) — no lazy wrapper, deliberately, to tolerate partial-initialisation during reflection discovery |
| `RegisterAssembly(Assembly)` | Scans one assembly for `IRecruitBehaviour` and `IRecruitCondition` implementations (parameterless constructor required) and registers them |
| `CanRecruit(Character enemy, RuntimeData runtime)` | Conjunctive gate over all registered conditions — returns true only if **every** `IRecruitCondition` passes |
| `Get(string id)` | Behaviour lookup by its `Id`; linear scan, `null` if absent |
| `All()` | All registered behaviours |
| `OnTick()` | Dispatches `OnTick(recruit, runtime, currentTick)` to every behaviour × every active recruit; lazily ensures the scene timer registration on first call |
| `OnConfigure(Recruit)` | Dispatches `ConfigureRecruit` to every behaviour for a newly added recruit |
| `OnRemove(Recruit)` | Dispatches `OnRemove` to every behaviour for a departing recruit |

### `IRecruitCondition`

A gating predicate consulted before any recruitment roll happens
(`Core/Relationship/IRecruitCondition.cs`). Conditions receive the defeated
`Character` and the full `RuntimeData`, so they can inspect stats, quest state,
inventory, or relationship flags:

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

- Conditions are auto-discovered from mod assemblies via
  `RecruitBehaviourRegistry.RegisterAssembly` — just ship a public class.
- The gate is **conjunctive**: all discovered conditions must return `true`.
  A single `false` blocks recruitment for that enemy — mods compose gates
  without coordinating with each other.
- If no conditions are registered at all, the gate passes vacuously.

### `RecruitTickController`

Periodic tick handler that processes recruit-related updates (e.g. hunger depletion, status regeneration). Runs on a timer.

## Recruitment flow

1. Character is defeated in battle (`WingroundBehaviour.ProcessRecruitment`)
2. Enemies with `RecruitmentChance > 0` are passed through the condition gate:
   `RecruitBehaviourRegistry.CanRecruit` must return `true` (all `IRecruitCondition`s pass)
3. The engine rolls 1–100 against the enemy's `RecruitmentChance`
4. On success the enemy is registered as a recruit via `RecruitingData.Add`
5. On acceptance in the UI: `character.add_to_party` action fires → `CharacterData.AddCharacterToParty()` → creates `CharacterInfoData` entry
6. Character joins the battle set and becomes controllable

## Scenario integration

Use `character.add_to_party` to recruit directly from a scenario:

```scenario
action character.add_to_party type=Murakami
```

Show a notification without roster changes:

```scenario
action character.send_notification type=Murakami
```

## Custom recruit behaviour

Implement `IRecruitBehaviour` to add custom logic on recruit lifecycle events. Implementations are auto-discovered from mod assemblies via `RecruitBehaviourRegistry.RegisterAssembly`. Because dispatch is cross-recruit — each behaviour receives every active recruit on each event — a behaviour can also introspect other recruits:

```csharp
public class MyRecruitBehaviour : IRecruitBehaviour
{
    // Unique identifier for RecruitBehaviourRegistry.Get lookup
    public string Id => "my_mod/loyalty";

    public void ConfigureRecruit(Recruit recruit, RuntimeData runtime)
    {
        // Called when a recruit is added — set initial properties;
        // RecruitingData.Add returns the instance, but this hook fires too
        recruit.Properties.SetInt("loyalty", 50);
    }

    public void OnTick(Recruit recruit, RuntimeData runtime, long currentTick)
    {
        // Called every game tick for each active recruit;
        // currentTick is RuntimeData.Time.Tick — use it for periodic logic
    }

    public void OnRemove(Recruit recruit, RuntimeData runtime)
    {
        // Called when a recruit is removed — clean up state
    }
}
```

## Relationship to skills

Recruited characters can acquire skills. `SkillData` is not a singleton — it is a plain instance owned by `RuntimeData`, accessible as `GameData.Instance.Runtime.SkillData`. It holds a single global set of player-owned skill IDs (lowercase strings such as `"slave_master"`) and is persisted with the save. Skills are not tied to individual recruits — they are a global unlock pool.
