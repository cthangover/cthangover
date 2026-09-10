# Creating a Custom Tool

Implement a tool in your mod with three classes:

## 1. Provider — `IToolProvider`

```csharp
using Cthangover.Core.UI.Tool;
using Godot;

namespace MyMod
{
    public class MyToolProvider : IToolProvider
    {
        public string Id => "my_tool";
        public string LocaleKey => "tools/my_tool/title";
        public Window CreateWindow() => new MyToolWindow();
    }
}
```

## 2. Window — extends `ToolWindow`

```csharp
using Cthangover.Core.UI.Tool;
using Godot;

namespace MyMod
{
    public class MyToolWindow : ToolWindow
    {
        public MyToolWindow() : base("tools/my_tool/title")
        {
            var root = CreateFillContainer();
            AddChild(root);

            var toolbar = CreateToolbar();
            root.AddChild(toolbar);

            var closeBtn = new Button { Text = TranslationServer.Translate("tools/close") };
            closeBtn.Pressed += () => QueueFree();
            toolbar.AddChild(closeBtn);
        }
    }
}
```

## 3. Toolbar button (optional) — `IToolBoxButton`

```csharp
using Cthangover.Core.UI.Tool;

namespace MyMod
{
    public class MyToolBoxButton : IToolBoxButton
    {
        public string ToolId => "my_tool";
        public string IconPath => "";
        public string LocaleKey => "tools/my_tool/title";
        public bool IsVisible() => true;
    }
}
```

## 4. Add locale keys

In your mod's `locale/ui_en.properties`:

```properties
tools/my_tool/title = My Custom Tool
```

## Registration checklist

| Step | Action |
|---|---|
| 1 | Create provider class implementing `IToolProvider` with a unique `Id` |
| 2 | Create window class extending `ToolWindow` — use `CreateFillContainer`, `CreateToolbar`, etc. |
| 3 | (Optional) Create `IToolBoxButton` class for a persistent HUD button |
| 4 | Add locale keys in your mod's `locale/ui_en.properties` |
| 5 | Ensure your mod's `manifest.json` has `"sources"` covering the new `.cs` files |

The tool is auto-discovered — no manual registration needed. The provider and button classes are found via reflection when the mod assembly loads.

## Locale keys reference

Common UI locale keys used by the tool system:

| Key | Context |
|---|---|
| `tools/title` | Main menu "Tools" label |
| `tools/select_tool` | Tool selection dialog |
| `tools/launch` | Launch button |
| `tools/close` | Close button |
| `tools/unsaved_title` | Unsaved changes dialog title |
| `tools/unsaved_text` | Unsaved changes dialog body |

## Related

- [Source Code in Mods](../src/) — C# compilation and mod bootstrapping
- [Scenes](../scenes/) — scene lifecycle and how scenes reference tools
- [Interactive Objects](../interactives/) — `interactive_editor` creates these definitions
- [Locale](../locale/) — `.properties` files for tool UI strings
