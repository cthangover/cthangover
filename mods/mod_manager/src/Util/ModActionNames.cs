using Godot;

namespace Mods.ModManager
{
    /// <summary>Locale keys and styling hints for <see cref="ModActionKind"/>.</summary>
    public static class ModActionNames
    {
        /// <summary>Localized action button caption.</summary>
        public static string Translate(ModActionKind kind)
            => TranslationServer.Translate(Key(kind));

        /// <summary>Translation key of an action.</summary>
        public static string Key(ModActionKind kind) => kind switch
        {
            ModActionKind.Install => "mod_manager/action_install",
            ModActionKind.Update => "mod_manager/action_update",
            ModActionKind.Downgrade => "mod_manager/action_downgrade",
            ModActionKind.Reinstall => "mod_manager/action_reinstall",
            ModActionKind.Delete => "mod_manager/action_delete",
            ModActionKind.Enable => "mod_manager/action_enable",
            _ => "mod_manager/action_disable",
        };

        /// <summary>True for destructive actions that deserve the danger button style.</summary>
        public static bool IsDanger(ModActionKind kind) => kind == ModActionKind.Delete;
    }
}
