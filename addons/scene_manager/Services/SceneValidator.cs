#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneManagerAddon
{
    public static class SceneValidator
    {
        public static void Validate(List<ModSceneInfo> mods)
        {
            var allScenes = ResourceResolver.GetRegisteredSceneNames(mods);
            var allBg = ResourceResolver.GetAllBackgroundIds(mods);
            var allLocale = ResourceResolver.GetAllLocaleKeys(mods);
            var allQuests = ResourceResolver.GetAllQuestIds(mods);

            foreach (var mod in mods)
            {
                foreach (var scene in mod.Scenes)
                {
                    scene.Errors.Clear();
                    scene.HasErrors = false;

                    foreach (var bg in scene.DefaultBackgrounds)
                    {
                        if (!string.IsNullOrEmpty(bg) && bg != "black" && bg != "white" && !allBg.Contains(bg))
                        {
                            scene.Errors.Add(new ValidationMessage
                            {
                                Message = $"Default background '{bg}' not found",
                                Severity = SeverityLevel.Error,
                                FilePath = scene.FilePath
                            });
                        }
                    }

                    foreach (var sc in scene.Scenarios)
                    {
                        sc.Errors.Clear();

                        foreach (var bg in sc.BackgroundRefs)
                        {
                            if (!allBg.Contains(bg))
                                sc.Errors.Add(Err($"Background '{bg}' not found", sc.FilePath));
                        }

                        foreach (var target in sc.SwitchSceneTargets)
                        {
                            var t = target.ToLowerInvariant();
                            if (t == "mainmenu" || t == "menu" || t == "battle") continue;
                            if (!allScenes.Contains(target))
                                sc.Errors.Add(Err($"Scene '{target}' not registered", sc.FilePath));
                        }

                        foreach (var lk in sc.LocaleKeys)
                        {
                            if (!allLocale.Contains(lk))
                                sc.Errors.Add(Warn($"Locale key '{lk}' not found", sc.FilePath));
                        }

                        foreach (var qid in sc.QuestRefs)
                        {
                            if (!allQuests.Contains(qid))
                                sc.Errors.Add(Err($"Quest '{qid}' not found", sc.FilePath));
                        }
                    }

                    var total = scene.Scenarios.Sum(s => s.Errors.Count);
                    if (scene.Errors.Count > 0 || total > 0)
                    {
                        scene.HasErrors = true;
                        foreach (var sc in scene.Scenarios)
                            scene.Errors.AddRange(sc.Errors);
                    }
                }
            }
        }

        private static ValidationMessage Err(string msg, string path) =>
            new() { Message = msg, FilePath = path, Severity = SeverityLevel.Error };

        private static ValidationMessage Warn(string msg, string path) =>
            new() { Message = msg, FilePath = path, Severity = SeverityLevel.Warning };
    }
}
#endif
