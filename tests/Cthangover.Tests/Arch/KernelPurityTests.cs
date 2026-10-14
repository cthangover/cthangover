namespace Engine.Tests.Arch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Xunit;

    /// <summary>
    /// Architecture guard for the "core = mod player" migration. The core
    /// assembly must not own gameplay subsystems — those live in engine mods
    /// (mods/&lt;name&gt;) compiled at runtime. This test scans the compiled core
    /// assembly by reflection and fails when a type sits in one of the banned
    /// subsystem namespaces.
    /// <para/>
    /// Both the ban list and the allowlist are data-driven: edit
    /// <see cref="Rules"/> and <see cref="AllowedTypes"/> at the top of the file.
    /// <para/>
    /// Transitional procedure: while a subsystem still has types in the core
    /// assembly its rule stays <c>Enforced: false</c> (types are reported, run
    /// stays green). Once the subsystem has fully moved to its mod, flip the
    /// rule to <c>Enforced: true</c> — from then on any type reappearing in the
    /// namespace fails the build, so dependencies cannot grow back.
    /// </summary>
    public class KernelPurityTests
    {
        /// <summary>One rule per subsystem namespace that must leave the core assembly.</summary>
        private sealed record SubsystemRule(string Namespace, bool Enforced, string Note);

        /// <summary>
        /// Ban list of subsystem namespaces. Matching is by namespace prefix:
        /// <c>Cthangover.Core.UI.Dialog</c> also covers
        /// <c>Cthangover.Core.UI.Dialog.Action</c> and so on.
        /// Flip <c>Enforced</c> to <c>true</c> when the subsystem has finished
        /// moving into its engine mod.
        /// </summary>
        private static readonly SubsystemRule[] Rules =
        {
            new("Cthangover.Core.Items", Enforced: true, "предметы/инвентарь перенесены в engine-мод items_base; в ядре остаются контракты (IItem/IItemAction/IItemContainer/IIngredient/IRecipe/IInventory), enum'ы ItemType/WorkbenchType, GD-обёртки GDItem/GDItemAction/GDInventoryService и мост ItemActionBridge — точный список в AllowedTypes"),
            new("Cthangover.Core.Quests", Enforced: true, "квесты перенесены в engine-мод quests_base; в ядре остаётся контракт доступа (IQuest, QuestStatus, QuestServiceRegistry) — точный список в AllowedTypes"),
            new("Cthangover.Core.Characters", Enforced: true, "персонажи перенесены в engine-мод characters_base; в ядре остаются модель данных (Character/ActionCharacter/CharacterData и др.) и контракты провайдеров — точный список в AllowedTypes"),
            new("Cthangover.Core.Interactive", Enforced: true, "интерактивы перенесены в engine-мод interactive_base; в ядре остаётся контракт переходного периода IInteractiveDialogRunner (резолв /root/DialogRunner) — точный список в AllowedTypes"),
            new("Cthangover.Core.Skills", Enforced: true, "скиллы перенесены в engine-мод skills_base; в ядре остаётся переходная DTO SkillData, которую читают RuntimeData и RuntimeSerializer, — точный список в AllowedTypes"),
            new("Cthangover.Core.UI.Lights", Enforced: true, "свет перенесён в engine-мод time_base (UiLightController/LampBehaviour/LightDef); SceneManager работает через LightingBridge"),
            new("Cthangover.Core.UI.Dialog", Enforced: true, "диалоги перенесены в engine-мод dialog_base; в ядре остаётся контракт переходного периода IDialogBox — точный список в AllowedTypes"),
            new("Cthangover.Core.UI.Executable", Enforced: true, "executable-события перенесены в engine-мод dialog_base; в ядре остаются контракты IExecutableEvent/IExecutableEventChain/IConditionedEvent/IScenarioEventFactory/IEventObject — точный список в AllowedTypes"),
            new("Cthangover.Core.Scenarios", Enforced: true, "сценарный DSL перенесён в engine-мод dialog_base; в ядре остаются ScenarioMetadata (метаданные .scenario) и мост ScenarioCommandBridge — точный список в AllowedTypes"),
            new("Cthangover.Core.Audio", Enforced: true, "аудио перенесено в engine-мод audio_base; контракт переходного периода IAudioService + enum'ы MixerType/MusicType/SoundType остаются в ядре"),
            new("Cthangover.Core.UI.View", Enforced: true, "видовой экран фона перенесён в engine-мод interface (ViewBox/ViewBoxNavigator/ZoomViewBoxController/AlignType); SceneManager и SaveScreenshotService работают через контракт IBackgroundView"),
            new("Cthangover.Core.UI.Theming", Enforced: true, "дизайн-система перенесена в engine-мод interface (UiKit); в ядре остаются CthangoverTheme/UiTokens — их использует Launcher в boot-окне до компиляции модов — точный список в AllowedTypes"),
            new("Cthangover.Core.UI.Messages", Enforced: true, "уведомления перенесены в engine-мод interface (MessagesHelper/NotificationPanel/UiMessage); в ядре остаётся чистый C# буфер NotificationBuffer (unit-тестируемый) — точный список в AllowedTypes"),
            new("Cthangover.Core.UI.Event", Enforced: true, "ввод перенесён в engine-мод interface (DeviceInput/TouchPhase); в ядре остаётся SceneEventController — Godot-autoload, тикающий с первых кадров до компиляции модов, и контракты IOnUpdateEvent/IOnTimeEvent/IEventPriority — точный список в AllowedTypes"),
            new("Cthangover.Core.UI.Tool", Enforced: true, "тулбар перенесён в engine-мод interface (ToolBox/ToolWindow/ToolMenuButton/icon-loaders); в ядре остаются контракты IToolProvider/IToolBoxButton, фабрики ToolFactory/ToolBoxButtonFactory и DTO MapZoneStateData (слот __core__ RuntimeSerializer) — точный список в AllowedTypes"),
            new("Cthangover.Core.UI.Menu", Enforced: true, "меню перенесено в engine-мод interface (GameMenu/MenuBuilder/SaveIcon/EmptyBlackBehaviour); в ядре остаются Launcher/BootLogPanel (главная сцена и boot-оверлей до компиляции модов) и контракты реестра MenuRegistry/MenuButtonEntry/MenuPanelEntry/IMenuPanel — точный список в AllowedTypes"),
            new("Cthangover.Core.UI.Base", Enforced: true, "виджет-тулкит перенесён в engine-мод interface (Widget/ModWidget/списки/ModControl/ModNode/InputHandlerNode); в ядре остаётся ModSpriteLoader — скрипт boot-сцен launcher.tscn/main_menu.tscn, инстанцируемых до компиляции модов — точный список в AllowedTypes"),
        };

        /// <summary>
        /// Types explicitly exempted from the ban (exact full name). Three
        /// families live here. The transitional audio contract that stays in
        /// the core assembly: the <c>IAudioService</c> interface and the
        /// MusicType/SoundType/MixerType enums referenced by its signatures.
        /// The characters contract set: the data model
        /// (<c>Character</c>, <c>ActionCharacter</c>, <c>CharacterData</c>,
        /// attributes, loot) still consumed by the core itself
        /// (GDCharacterService, inventory, battle wiring) and by foreign
        /// mods, plus the provider contracts
        /// (<c>ICharacterTemplateProvider</c> / <c>IActionTemplateProvider</c>
        /// / <c>IAvatarProvider</c> side registries) through which the
        /// characters_base mod serves templates to the core player.
        /// And the quests contract set: the <c>IQuest</c> interface and
        /// <c>QuestStatus</c> enum referenced by the core-side
        /// <c>IQuestService</c> signatures, plus the
        /// <c>QuestServiceRegistry</c> through which the quests_base mod
        /// serves quest operations to the core player. Remove
        /// entries together with their owning rule when the contract itself
        /// leaves the core (enforced by
        /// <see cref="KernelPurity_AllowlistIsCurrent"/>).
        /// </summary>
        private static readonly string[] AllowedTypes =
        {
            "Cthangover.Core.Audio.IAudioService",
            "Cthangover.Core.Audio.MixerType",
            "Cthangover.Core.Audio.MusicType",
            "Cthangover.Core.Audio.SoundType",

            // items_base: kernel contract that stays in the core. Item/recipe
            // data (Item/ItemInfo/ItemContainer/Ingredient/IngredientInfo/
            // Recipe/RecipeInfo/Inventory), the factories and the inventory UI
            // live in the items_base engine mod; the core keeps the interface
            // contracts its own services (RuntimeData, ScenarioActionContext,
            // GDInventoryService) are written against, plus the GDScript
            // wrappers and the ItemActionBridge delivery point.
            "Cthangover.Core.Items.IItem",
            "Cthangover.Core.Items.IItemAction",
            "Cthangover.Core.Items.IItemContainer",
            "Cthangover.Core.Items.IIngredient",
            "Cthangover.Core.Items.IRecipe",
            "Cthangover.Core.Items.IInventory",
            "Cthangover.Core.Items.ItemType",
            "Cthangover.Core.Items.WorkbenchType",
            "Cthangover.Core.Items.GDItem",
            "Cthangover.Core.Items.GDItemAction",
            "Cthangover.Core.Items.GDInventoryService",
            "Cthangover.Core.Items.ItemActionBridge",

            // interactive_base: kernel contract that stays in the core. The
            // interactive definitions (InteractiveDefinition + hit-area/highlight/
            // action DTOs), the manager (InteractiveManager), the object/builder
            // runtime, the resolver and the factory live in the interactive_base
            // engine mod; the core keeps only the dialog-runner contract the
            // interactive click flow resolves through /root/DialogRunner.
            "Cthangover.Core.Interactive.IInteractiveDialogRunner",

            // Godot source generators emit these nested classes for the
            // core-side [GlobalClass] partial wrappers above.
            "Cthangover.Core.Items.GDItem+MethodName",
            "Cthangover.Core.Items.GDItem+PropertyName",
            "Cthangover.Core.Items.GDItem+SignalName",
            "Cthangover.Core.Items.GDInventoryService+MethodName",
            "Cthangover.Core.Items.GDInventoryService+PropertyName",
            "Cthangover.Core.Items.GDInventoryService+SignalName",

            // quests_base: kernel contract that stays in the core. Quest state
            // (QuestFactory/QuestBase/QuestData), the IQuestService
            // implementation, quest actions/conditions/parsers and the journal
            // live in the quests_base engine mod; the core resolves them
            // through QuestServiceRegistry.Provider.
            "Cthangover.Core.Quests.IQuest",
            "Cthangover.Core.Quests.QuestStatus",
            "Cthangover.Core.Quests.QuestServiceRegistry",

            // dialog_base: transition contracts that stay in the core. The dialog
            // implementation (DialogBox, the DSL parser, the command strategies),
            // the executable event engine and the scenario actions live in the
            // dialog_base engine mod; the core resolves them through IDialogBox,
            // the executable contracts, IScenarioEventFactory and the
            // ScenarioCommandBridge delivery point.
            "Cthangover.Core.UI.Dialog.IDialogBox",
            "Cthangover.Core.UI.Executable.IExecutableEvent",
            "Cthangover.Core.UI.Executable.IExecutableEventChain",
            "Cthangover.Core.UI.Executable.IConditionedEvent",
            "Cthangover.Core.UI.Executable.IScenarioEventFactory",
            "Cthangover.Core.UI.Executable.IEventObject",
            "Cthangover.Core.Scenarios.ScenarioMetadata",
            "Cthangover.Core.Scenarios.ScenarioCommandBridge",

            // skills_base: transition DTO that stays in the core. The grant
            // service, the factory and the skills UI live in the skills_base
            // engine mod; RuntimeData and the serializers still read SkillData
            // through the core data model.
            "Cthangover.Core.Skills.SkillData",

            // characters_base: data model + provider contracts that stay in the core.
            "Cthangover.Core.Characters.Attribute",
            "Cthangover.Core.Characters.ChangeAttribute",
            "Cthangover.Core.Characters.Character",
            "Cthangover.Core.Characters.CharacterAttributes",
            "Cthangover.Core.Characters.CharacterData",
            "Cthangover.Core.Characters.ActionCharacter",
            "Cthangover.Core.Characters.ActionCharacterType",
            "Cthangover.Core.Characters.ActionCharacterTypeExtension",
            "Cthangover.Core.Characters.ActiveCharacterRegistry",
            "Cthangover.Core.Characters.IActiveCharacterProvider",
            "Cthangover.Core.Characters.IPropertyInterceptor",
            "Cthangover.Core.Characters.PropertyInterceptor`1",
            "Cthangover.Core.Characters.LootEntry",
            "Cthangover.Core.Characters.ICharacterTemplateProvider",
            "Cthangover.Core.Characters.CharacterTemplateRegistry",
            "Cthangover.Core.Characters.IActionTemplateProvider",
            "Cthangover.Core.Characters.ActionTemplateRegistry",

            // interface: kernel contracts that stay in the core. The widget
            // toolkit (Widget/ModWidget/Border/ListWidget/ListItem and the rest
            // of Base.Lists), ModControl/ModNode, ViewBox, ToolBox, GameMenu,
            // MenuBuilder, SaveIcon, MessagesHelper/NotificationPanel/UiMessage,
            // DeviceInput/TouchPhase and UiKit live in the interface engine
            // mod; the core keeps the root-namespace contracts (IWidget,
            // IDialogBox, IBackgroundView, IToolbarHost, ISaveIconHost,
            // IUiLazyConstruct), the menu/tool registries its own AppServices
            // slots are written against, the boot-window scripts (Launcher,
            // BootLogPanel, ModSpriteLoader — project.godot main_scene and
            // launcher.tscn instantiate them before the mod pipeline
            // compiles), the SceneEventController autoload that ticks from
            // the first frames, the pure-C# NotificationBuffer and the
            // CthangoverTheme/UiTokens boot styling.
            "Cthangover.Core.UI.Base.ModSpriteLoader",
            "Cthangover.Core.UI.Menu.Launcher",
            "Cthangover.Core.UI.Menu.BootLogPanel",
            "Cthangover.Core.UI.Menu.BootProgressBar",
            "Cthangover.Core.UI.Menu.MenuRegistry",
            "Cthangover.Core.UI.Menu.MenuButtonEntry",
            "Cthangover.Core.UI.Menu.MenuPanelEntry",
            "Cthangover.Core.UI.Menu.IMenuPanel",
            "Cthangover.Core.UI.Menu.ISettingsPanel",
            "Cthangover.Core.UI.Menu.ISaveLoadPanel",
            "Cthangover.Core.UI.Tool.IToolProvider",
            "Cthangover.Core.UI.Tool.IToolBoxButton",
            "Cthangover.Core.UI.Tool.ToolFactory",
            "Cthangover.Core.UI.Tool.ToolBoxButtonFactory",
            "Cthangover.Core.UI.Tool.MapZoneStateData",
            "Cthangover.Core.UI.Event.SceneEventController",
            "Cthangover.Core.UI.Event.IOnUpdateEvent",
            "Cthangover.Core.UI.Event.IOnTimeEvent",
            "Cthangover.Core.UI.Event.IEventPriority",
            "Cthangover.Core.UI.Messages.NotificationBuffer",
            "Cthangover.Core.UI.Theming.CthangoverTheme",
            "Cthangover.Core.UI.Theming.UiTokens",

// Godot source generators emit these nested classes for the
            // kernel-side [Signal] partial Node scripts above (and LogLine is
            // the user-defined nested record of BootLogPanel).
            "Cthangover.Core.UI.Base.ModSpriteLoader+MethodName",
            "Cthangover.Core.UI.Base.ModSpriteLoader+PropertyName",
            "Cthangover.Core.UI.Base.ModSpriteLoader+SignalName",
            "Cthangover.Core.UI.Menu.Launcher+MethodName",
            "Cthangover.Core.UI.Menu.Launcher+PropertyName",
            "Cthangover.Core.UI.Menu.Launcher+SignalName",
            "Cthangover.Core.UI.Menu.BootLogPanel+LogLine",
            "Cthangover.Core.UI.Menu.BootProgressBar+MethodName",
            "Cthangover.Core.UI.Menu.BootLogPanel+MethodName",
            "Cthangover.Core.UI.Menu.BootLogPanel+PropertyName",
            "Cthangover.Core.UI.Menu.BootProgressBar+PropertyName",
            "Cthangover.Core.UI.Menu.BootLogPanel+SignalName",
            "Cthangover.Core.UI.Menu.BootProgressBar+SignalName",
            "Cthangover.Core.UI.Event.SceneEventController+MethodName",
            "Cthangover.Core.UI.Event.SceneEventController+PropertyName",
            "Cthangover.Core.UI.Event.SceneEventController+SignalName",
        };

        [Fact]
        public void KernelPurity_CoreAssemblyContainsNoSubsystemTypes()
        {
            var kernelTypes = KernelTypes();
            var violations = new List<string>();
            var transition = new List<string>();

            foreach (var rule in Rules)
            {
                var offenders = kernelTypes
                    .Where(t => InNamespace(t, rule.Namespace) && !AllowedTypes.Contains(t.FullName) && !CompilerGenerated(t))
                    .OrderBy(t => t.FullName, StringComparer.Ordinal)
                    .ToList();

                if (offenders.Count == 0)
                    continue;

                if (rule.Enforced)
                {
                    violations.AddRange(offenders.Select(t =>
                        "- " + t.FullName + ": тип подсистемы остался в сборке ядра (namespace '" + t.Namespace + "'). " +
                        rule.Note + " — перенесите тип в engine-мод; namespace '" + rule.Namespace + "' объявлен enforced."));
                }
                else
                {
                    transition.Add("- " + rule.Namespace + ": " + offenders.Count + " типов в ядре (переходное состояние, не enforced) — " + rule.Note);
                }
            }

            foreach (var line in transition)
                Console.WriteLine("[KERNEL_PURITY] transition: " + line);

            Assert.True(
                violations.Count == 0,
                "Ядро содержит типы подсистем (сборка ядра должна быть только проигрывателем модов):\n" +
                string.Join("\n", violations) +
                "\nСписок запретов и allowlist — в KernelPurityTests.Rules / AllowedTypes.");
        }

        [Fact]
        public void KernelPurity_AllowlistIsCurrent()
        {
            var kernelTypeNames = KernelTypes()
                .Select(t => t.FullName)
                .ToHashSet(StringComparer.Ordinal);
            var enforcedRules = Rules.Where(r => r.Enforced).ToList();

            var problems = new List<string>();
            var notes = new List<string>();

            foreach (var allowed in AllowedTypes)
            {
                if (kernelTypeNames.Contains(allowed))
                    continue;

                var separator = allowed.LastIndexOf('.');
                var typeNamespace = separator > 0 ? allowed[..separator] : allowed;
                var owningRule = enforcedRules.FirstOrDefault(r => InNamespace(typeNamespace, r.Namespace));

                if (owningRule != null)
                {
                    problems.Add("- " + allowed + ": записи нет в сборке ядра, а namespace '" + owningRule.Namespace +
                                 "' уже enforced — удалите устаревшую запись из AllowedTypes.");
                }
                else
                {
                    notes.Add("- " + allowed + ": записи нет в сборке ядра (namespace ещё не enforced) — переходное состояние, " +
                              "запись можно удалить вместе с переводом подсистемы в enforced.");
                }
            }

            foreach (var note in notes)
                Console.WriteLine("[KERNEL_PURITY] allowlist: " + note);

            Assert.True(
                problems.Count == 0,
                "Allowlist чистоты ядра устарел:\n" + string.Join("\n", problems));
        }

        /// <summary>
        /// All types of the core assembly (the one that owns
        /// <c>Cthangover.Core.Settings.GameData</c>). Load errors are tolerated:
        /// only the types that resolved are scanned.
        /// </summary>
        private static Type[] KernelTypes()
        {
            var assembly = typeof(Cthangover.Core.Settings.GameData).Assembly;
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).Select(t => t!).ToArray();
            }
        }

        /// <summary>
        /// True for compiler-generated closure and lambda classes (<c>&lt;&gt;c</c>,
        /// <c>&lt;Get...&gt;b__0</c>): they appear in any namespace that uses
        /// lambdas and are not types a developer placed there, so they never
        /// count as purity violations.
        /// </summary>
        private static bool CompilerGenerated(Type type) => type.FullName.Contains("<", StringComparison.Ordinal);

        /// <summary>True when the type's namespace equals <paramref name="ruleNamespace"/> or sits under it.</summary>
        private static bool InNamespace(Type type, string ruleNamespace) => InNamespace(type.Namespace, ruleNamespace);

        private static bool InNamespace(string typeNamespace, string ruleNamespace)
        {
            if (string.IsNullOrEmpty(typeNamespace))
                return false;
            return typeNamespace == ruleNamespace
                   || typeNamespace.StartsWith(ruleNamespace + ".", StringComparison.Ordinal);
        }
    }
}
