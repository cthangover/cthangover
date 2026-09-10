# Battle Action Executors

A GDScript that has both `get_action_id()` and `execute(action, user, target)` is registered as a **battle action executor**. The `action_id` must match an `ActionCharacter.ID` in the character's JSON data.

## Executor contract

```gdscript
extends RefCounted

func get_action_id():
    return "my.custom_attack"

func execute(action, user, target):
    var result = GDBattleResult.new()

    var damage = user.AttackValue * 2 - target.DefenceValue
    if damage < 1:
        damage = 1

    result.TargetDamage = damage
    result.TargetDefence = target.DefenceValue
    result.Result = true

    return result
```

## Parameters

| Parameter | Type | Description |
|---|---|---|
| `action` | `GDActionCharacter` | The ActionCharacter definition (Id, Name, Description, Type) |
| `user` | `GDCharacter` | The character performing the action |
| `target` | `GDCharacter` | The character receiving the action |

## GDActionCharacter fields

| Field | Type |
|---|---|
| `Id` | `string` |
| `Name` | `string` |
| `Description` | `string` |
| `Type` | `int` |

## GDCharacter fields

| Field | Type | Description |
|---|---|---|
| `Id` | `string` | Character identifier |
| `Name` | `string` | Display name |
| `Level` | `int` | Current level |
| `Exp` | `int` | Experience points |
| `RecruitmentChance` | `int` | Recruitment chance (0-100) |
| `HealthValue` / `HealthMax` | `int` | Current / max health |
| `DefenceValue` / `DefenceMax` | `int` | Current / max defence |
| `AttackValue` / `AttackMax` | `int` | Current / max attack |
| `StrengthValue` / `StrengthMax` | `int` | Current / max strength |
| `MagicValue` / `MagicMax` | `int` | Current / max magic |
| `PointValue` / `PointMax` | `int` | Current / max action points |

## GDBattleResult fields

| Field | Type | Description |
|---|---|---|
| `SourceDamage` | `int` | Damage dealt by source |
| `SourceDefence` | `int` | Source defence value |
| `TargetDamage` | `int` | Damage dealt to target |
| `TargetDefence` | `int` | Target defence value |
| `Result` | `bool` | Whether the action succeeded (default `true`) |
