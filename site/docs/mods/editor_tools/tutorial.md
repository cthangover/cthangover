# Creating a Custom Tool

Depend on `tools_base>=1` and `ui_base>=1`, and include the new `.cs` files in `manifest.sources`. Implement a tool with three classes, each in its own file. Tool windows are launched from the in-game toolbar.

## 1. Provider — `IToolProvider`

```csharp
namespace MyMod
{
    using Cthangover.Core.UI.Tool;
    using Cthangover.Mods.Interface.Tool;
    using Godot;
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
namespace MyMod
{
    using Cthangover.Core.UI.Tool;
    using Cthangover.Mods.Interface.Tool;
    using Godot;
    public class MyToolWindow : ToolWindow
    {
        public MyToolWindow() : base("tools/my_tool/title")
        {
            var root = CreateFillContainer();
            AddChild(root);

            var toolbar = CreateToolbar();
            root.AddChild(toolbar);

            var closeBtn = new Button { Text = TranslationServer.Translate("tools/my_tool/close") };
            closeBtn.Pressed += () => QueueFree();
            toolbar.AddChild(closeBtn);
        }
    }
}
```

## 3. Toolbar entry — `IToolBoxButton`

```csharp
namespace MyMod
{
    using Cthangover.Core.UI.Tool;
    using Cthangover.Mods.Interface.Tool;
    using Godot;
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
tools/my_tool/close = Close
```

## Registration checklist

| Step | Action |
|---|---|
| 1 | Create provider class implementing `IToolProvider` with a unique `Id` |
| 2 | Create window class extending `ToolWindow` — use `CreateFillContainer`, `CreateToolbar`, etc. |
| 3 | Create an `IToolBoxButton` class for the toolbar tools list |
| 4 | Add locale keys in your mod's `locale/ui_en.properties` |
| 5 | Ensure your mod's `manifest.json` has `"sources"` covering the new `.cs` files |

The tool is auto-discovered — no manual registration needed. The provider and button classes are found via reflection when the mod assembly loads.

## Locale keys reference

Common UI locale keys used by the tool system:

| Key | Context |
|---|---|
| `tools/unsaved_title` | Unsaved changes dialog title |
| `tools/unsaved_text` | Unsaved changes dialog body |

Tool-specific keys (`tools/<tool_id>/...`) live in the tool mod's own locale files.

## Related

- [Source Code in Mods](../src/) — C# compilation and mod bootstrapping
- [Scenes](../scenes/) — scene lifecycle and how scenes reference tools
- [Interactive Objects](../interactives/) — `interactive_editor` creates these definitions
- [Locale](../locale/) — `.properties` files for tool UI strings
