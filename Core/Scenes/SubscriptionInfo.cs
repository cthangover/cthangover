namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Holds the metadata for a single scene subscription entry from a mod manifest.
    /// Includes the wrapper template source code (with <c>{{USER_CODE}}</c> substitution
    /// placeholder), optional user-supplied C# code, and the trigger type that determines
    /// whether the subscription runs on scene enter or exit. Populated by
    /// <see cref="SceneSubscriptionRegistry.CollectFromMods"/>.
    /// </summary>
    public class SubscriptionInfo
    {
        /// <summary>The identifier of the mod that defines this subscription.</summary>
        public string ModId { get; set; }

        /// <summary>The scene identifier that triggers this subscription.</summary>
        public string Scene { get; set; }

        /// <summary>The name of the wrapper template (without extension) in the <c>wrappers/</c> directory.</summary>
        public string TemplateName { get; set; }

        /// <summary>The raw C# source code of the wrapper template, containing a <c>{{USER_CODE}}</c> marker.</summary>
        public string TemplateContent { get; set; }

        /// <summary>The name of the user code file (without extension) to inject into the template.</summary>
        public string CodeName { get; set; }

        /// <summary>The raw C# source code of the user file, substituted in place of <c>{{USER_CODE}}</c>.</summary>
        public string CodeContent { get; set; }

        /// <summary>Execution priority; lower values execute earlier.</summary>
        public int Priority { get; set; }

        /// <summary>The trigger type: <c>"on_enter"</c> or <c>"on_exit"</c>.</summary>
        public string Trigger { get; set; }
    }
}
