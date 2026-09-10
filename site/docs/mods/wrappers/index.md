# Wrappers — UI Templates

Wrapper templates (`.wrappertmpl`) define how mod UI is built inside a scene. A wrapper template is **not JSON** — it is a C# source file that is compiled at runtime, with your subscription's code fragment substituted into it.

## Directory structure

```filestree
wrappers/
├── default.wrappertmpl
└── custom_layout.wrappertmpl
```

## What wrappers do

When a mod subscribes to a scene (`manifest.json` → `subscriptions`), the `template` field selects which wrapper template to compile and run:

```jsonc
{
  "subscriptions": [
    {
      "scene": "home_kitchen",       // Target scene ID
      "template": "default",         // Wrapper template name (from wrappers/ directory) — MANDATORY
      "code": "cooking_panel_init",  // File name of the C# fragment loaded from subscriptions/{code}.cs
      "trigger": "on_enter",         // Lifecycle event: on_enter | on_exit
      "priority": 10                 // Execution order: lower runs earlier among multiple subscribers
    }
  ]
}
```

Subscription entry fields:
- `scene` — target scene ID
- `template` — wrapper template path/name (**mandatory**: if missing, the whole subscription entry is skipped as invalid)
- `code` — file name of the C# fragment loaded from `subscriptions/{code}.cs` (names a **file**, not a class)
- `trigger` — `"on_enter"` / `"on_exit"`, defaults to `"on_enter"`
- `priority` — defaults to `10`; subscriptions with a lower priority value run first

The `"template": "default"` means the cooking UI code runs inside the default wrapper template.

## Real example — `core/wrappers/default.wrappertmpl`

A wrapper template is plain C# source defining a static class named `SceneBuilderScript` with a few helper methods and a `Run(Node root)` method containing the `{{USER_CODE}}` substitution marker:

```csharp
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public static class SceneBuilderScript
{
    private static Node _root;

    public static T Get<T>(string name) where T : Node
    {
        return _root.GetNode<T>(name);
    }

    public static Node Get(string name)
    {
        return _root.GetNode(name);
    }

    public static T FindNode<T>(string nodeName) where T : Node
    {
        return Cthangover.Core.Scenes.SceneContextNode.FindNode<T>(nodeName);
    }

    public static void Run(Node root)
    {
        _root = root;
        {{USER_CODE}}
    }
}
```

When the trigger fires, the template text is taken, the `{{USER_CODE}}` marker is replaced with your subscription's C# fragment (the contents of `subscriptions/{code}.cs`), the result is compiled via Roslyn, and `SceneBuilderScript.Run(root)` is invoked via reflection with the scene root node. Compilation results are cached per template+code pair for reuse across scene visits.

In your subscription code fragment, you can use the template's helpers to find UI containers by name and build your UI:

```csharp
var modPanel = FindNode<Control>("Lastground");
if (modPanel == null)
    return;

var myPanel = new MyUIPanel();
myPanel.Name = "MyCustomPanel";
modPanel.AddChild(myPanel);
```

## Creating a custom wrapper

`wrappers/side_panel.wrappertmpl` — any C# class named `SceneBuilderScript` with a `Run(Node root)` method works; include whichever helpers your fragments need:

```csharp
using Godot;

public static class SceneBuilderScript
{
    public static void Run(Node root)
    {
        // Your subscription's code fragment replaces this marker:
        {{USER_CODE}}
    }
}
```

Then reference it in your manifest:

```jsonc
{ "scene": "my_scene", "template": "side_panel", "code": "my_handler" }  // Uses side_panel template
```

Remember: `template` is mandatory — an entry without it will not be registered at all.

## Execution order

On scene enter, event bus subscribers run in priority order:

1. **Priority 15** — `[SceneEvent]`-composed chain events run (`SceneManager.ComposeEvents`, combining `.scenario` definitions and `[SceneEvent]` classes)
2. **Priority 20** — manifest subscriptions execute (`SceneSubscriptionRegistry`)

Among subscriptions themselves, those with a lower `priority` value run first.

## Using ModWidget

When creating UI components from mod C# code, inherit from `ModWidget` (`Core/UI/Base/Lists/ModWidget.cs`). It provides an idempotent `Construct()`/`Destruct()` lifecycle for UI built from code inside mods: `EnsureConstructed()` calls the abstract `Construct()` exactly once, and the virtual `Destruct()` is called automatically on `_ExitTree`. Usage = override `Construct()` and build your children there:

```csharp
public class MyUIPanel : ModWidget
{
    protected override void Construct()
    {
        var label = new Label { Text = "Hello from mod!" };
        AddChild(label);
    }
}
```

Call `EnsureConstructed()` before adding the widget to the tree (as `CardBattleCore` does), so its children are constructed exactly once regardless of how many times construction is requested.
