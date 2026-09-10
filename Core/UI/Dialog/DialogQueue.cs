using System;
using System.Collections.Generic;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Godot;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// Fluent builder for dialog sequences. Each method creates an action of the
    /// corresponding type, appends it to the Queue, and returns the action for
    /// optional further configuration. The API vocabulary mirrors the scenario DSL
    /// commands (Text, Title, Background, Music, Sound, Delay, GoTo, If, Select,
    /// SwitchScene, etc.), making the C# API feel like a script. RuntimeObjectList
    /// is a separate list of IActionDestruct objects that must be cleaned up when
    /// the dialog ends but are not part of the sequential queue (e.g. spawned
    /// resources, scheduled callbacks).
    /// </summary>
    public class DialogQueue
    {

        /// <summary>Objects that need <see cref="IActionDestruct.Destruct"/> cleanup when the dialog ends but are not part of the sequential queue (e.g. spawned resources).</summary>
        public List<IActionDestruct> RuntimeObjectList { get; } = new();
        /// <summary>The ordered sequence of dialog actions. Appended via fluent builder methods; consumed by <see cref="DialogRuntime"/>.</summary>
        public List<IActionCommand> Queue { get; } = new();

        /// <summary>Spawns a visual effect identified by <paramref name="effectID"/> through <see cref="EffectFactory"/>. Returns the action for further configuration.</summary>
        public ActionEffect Effect(string effectID)
        {
            var action = new ActionEffect { EffectID = effectID };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Creates a frame-animation foreground from a base sprite path, generating frame IDs from <paramref name="start"/> to <paramref name="start"/> + <paramref name="count"/>.</summary>
        public ActionAnimatedForeground Animation(string sprite, int start, int count, float speed = 1, float nextFrameSpeed = 1, bool isLoop = true)
        {
            var list = new List<string>();
            for (int i = start; i <= start + count; i++)
                list.Add(sprite + $"{i:00}");
            return Animation(list, speed, nextFrameSpeed, isLoop);
        }
        
        /// <summary>Creates a frame-animation foreground from a pre-built list of sprite resource paths.</summary>
        public ActionAnimatedForeground Animation(List<string> sprites, float speed = 1, float nextFrameSpeed = 1, bool isLoop = true)
        {
            var action = new ActionAnimatedForeground { SpriteIDs = sprites, Speed = speed, NextFrameSpeed = nextFrameSpeed, IsLoop = isLoop };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Sets the scene background from a sprite ID resolved through <see cref="BackgroundFactory"/>. Also loads depth and albedo maps for lighting.</summary>
        public ActionBackground Background(string spriteId)
        {
            var action = new ActionBackground { SpriteID = spriteId };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Sets the scene background directly from a pre-loaded <see cref="Texture2D"/>. Bypasses factory resolution.</summary>
        public ActionBackground BackgroundTexture(Texture2D texture)
        {
            var action = new ActionBackground { Texture = texture };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Sets the scene foreground from a sprite ID resolved through <see cref="BackgroundFactory"/>.</summary>
        public ActionForeground Foreground(string spriteId)
        {
            var action = new ActionForeground { SpriteID = spriteId };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Sets the scene foreground directly from a pre-loaded <see cref="Texture2D"/>.</summary>
        public ActionForeground ForegroundTexture(Texture2D texture)
        {
            var action = new ActionForeground { Texture = texture };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Displays dialog text with optional avatars. Pauses for click before advancing.</summary>
        public ActionText Text(string text, string first = null, string second = null, bool hideColor = false)
        {
            var action = new ActionText { Text = text, FirstAvatar = first, SecondAvatar = second, HideColor = hideColor };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Like <see cref="Text"/> but enables runtime variable substitution (${var}) on the text content.</summary>
        public ActionText PText(string text, string first = null, string second = null, bool hideColor = false)
        {
            var action = new ActionText { Text = text, FirstAvatar = first, SecondAvatar = second, HideColor = hideColor, UseProcessText = true };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Sets the dialog title bar. Non-null shows it; null hides.</summary>
        public ActionTitle Title(string title)
        {
            var action = new ActionTitle { TitleText = title };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Explicitly hides the title bar by setting TitleText to null.</summary>
        public ActionTitle HideTitle()
        {
            var action = new ActionTitle { TitleText = null };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Sets a runtime variable by name and literal value. Available to subsequent actions via ${name} syntax.</summary>
        public ActionSet Set(string name, string value)
        {
            var action = new ActionSet { Name = name, Value = value };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Sets a runtime variable by name, using a lazy-evaluated callback for dynamic values not known at script authoring time.</summary>
        public ActionSet Set(string name, Func<string> callback)
        {
            var action = new ActionSet { Name = name, Callback = callback };
            Queue.Add(action);
            return action;
        }

        /// <summary>Presents player choices. Each <see cref="SelectVariant"/> has display text and a GoTo target. Dialog pauses until a choice is picked.</summary>
        public ActionSelect Select(List<SelectVariant> variants, string text = null, string first = null, string second = null)
        {
            var action = new ActionSelect { Variants = variants, Text = text, FirstAvatar = first, SecondAvatar = second };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Terminates the dialog queue, calling <see cref="DialogRuntime.End"/>.</summary>
        public ActionEnd End()
        {
            var action = new ActionEnd();
            Queue.Add(action);
            return action;
        }
        
        /// <summary>A labeled no-op. Used as a named jump target for GoTo/If commands.</summary>
        public ActionEmpty Empty()
        {
            var action = new ActionEmpty();
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Creates a named empty action (a "point") identified by <paramref name="id"/> for GoTo targeting.</summary>
        public ActionEmpty Point(string id)
        {
            return (ActionEmpty)Empty().SetID(id);
        }

        /// <summary>Starts background music playback via <see cref="AudioService"/> using <see cref="MusicType.Ambient"/> channel.</summary>
        public ActionMusic Music(string music)
        {
            var action = new ActionMusic { Music = music };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Pauses background music without stopping the track. Resume with <see cref="MusicPlay"/>.</summary>
        public ActionMusicPause MusicPause()
        {
            var action = new ActionMusicPause();
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Resumes previously paused background music.</summary>
        public ActionMusicPlay MusicPlay()
        {
            var action = new ActionMusicPlay();
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Waits for <paramref name="waitTime"/> seconds while optionally displaying a text message via <paramref name="showText"/>.</summary>
        public ActionDelay Delay(float waitTime, string showText)
        {
            var action = new ActionDelay { WaitTime = waitTime, HiddenMode = false, ShowText = showText };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Waits for <paramref name="waitTime"/> seconds. When <paramref name="hiddenMode"/> is true, hides the dialog body for the duration (dramatic pauses, cutscene beats).</summary>
        public ActionDelay Delay(float waitTime, bool hiddenMode = false)
        {
            var action = new ActionDelay { WaitTime = waitTime, HiddenMode = hiddenMode, ShowText = null };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Plays a UI sound effect through <see cref="AudioService"/>. Non-blocking — the dialog continues immediately.</summary>
        public ActionSound Sound(string sound)
        {
            var action = new ActionSound { Sound = sound };
            Queue.Add(action);
            return action;
        }

        /// <summary>Invokes an external scenario action by name, resolved through <see cref="ScenarioActionFactory"/>.</summary>
        public ActionScenario Action(string actionName)
        {
            var action = new ActionScenario { ActionName = actionName };
            Queue.Add(action);
            return action;
        }

        /// <summary>Runs arbitrary C# code as a dialog action. Useful for quest state changes or UI toggles without a dedicated action class.</summary>
        public ActionRun Run(System.Action executable)
        {
            var action = new ActionRun { Executable = executable };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Evaluates a condition and jumps to <paramref name="trueGoTo"/> or <paramref name="falseGoTo"/> based on the result. Falls through if the chosen target is empty.</summary>
        public ActionIf IfGoTo(Func<bool> condition, string trueGoTo, string falseGoTo = null)
        {
            var action = new ActionIf { Condition = condition, TrueGoTo = trueGoTo, FalseGoTo = falseGoTo };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Unconditionally jumps to the action identified by <paramref name="goTo"/> in the queue.</summary>
        public ActionGoTo GoTo(string goTo)
        {
            var action = new ActionGoTo { GoTo = goTo };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Deferred scene switch via <see cref="SceneManager"/>. Sets the locker as OneRun to prevent re-triggering after scene reload.</summary>
        public ActionSwitchScene SwitchScene(string targetScene, float speed = 4f, bool hiddenMode = true)
        {
            var action = new ActionSwitchScene { SceneName = targetScene, HiddenMode = hiddenMode, Speed = speed };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Declares a background tint color to be consumed by background-rendering nodes.</summary>
        public ActionBackgroundColor BackgroundColor(Color color)
        {
            var action = new ActionBackgroundColor { Color = color };
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Makes the dialog box visible. Paired with <see cref="HideDialog"/> for cutscenes that temporarily hide dialog chrome.</summary>
        public ActionShowDialog ShowDialog()
        {
            var action = new ActionShowDialog();
            Queue.Add(action);
            return action;
        }
        
        /// <summary>Hides the dialog box. Paired with <see cref="ShowDialog"/> for cutscene transitions.</summary>
        public ActionHideDialog HideDialog()
        {
            var action = new ActionHideDialog();
            Queue.Add(action);
            return action;
        }

        /// <summary>Toggles whether lighting responds to in-game time of day (true) or uses static lights (false).</summary>
        public ActionLightUseTime LightUseTime(bool useTime)
        {
            var action = new ActionLightUseTime { UseTime = useTime };
            Queue.Add(action);
            return action;
        }

        /// <summary>Applies static light definitions from a JSON string to the light controller. Empty string clears all static lights.</summary>
        public ActionLightSet LightSet(string json)
        {
            var action = new ActionLightSet { LightsJson = json };
            Queue.Add(action);
            return action;
        }

        /// <summary>Fade the dialog background in (<see cref="BackgroundActionType.Show"/>) or out (<see cref="BackgroundActionType.Hide"/>). Optionally set <paramref name="wait"/> to pause the dialog during the transition.</summary>
        public ActionBackgroundShowHide BackgroundShowHide(BackgroundActionType type, float duration, bool wait = false)
        {
            var action = new ActionBackgroundShowHide { ActionType = type, Duration = duration };
            if (wait)
            {
                action.WaitType = WaitType.WaitTime;
                action.WaitTime = duration;
            }
            Queue.Add(action);
            return action;
        }

        /// <summary>Adds an interactive element to the scene by definition ID.</summary>
        public ActionInteractiveAdd InteractiveAdd(string definitionId)
        {
            var action = new ActionInteractiveAdd { DefinitionId = definitionId };
            Queue.Add(action);
            return action;
        }

        /// <summary>Removes an interactive element from the scene by definition ID.</summary>
        public ActionInteractiveRemove InteractiveRemove(string definitionId)
        {
            var action = new ActionInteractiveRemove { DefinitionId = definitionId };
            Queue.Add(action);
            return action;
        }

        /// <summary>Clears all interactive elements from the scene.</summary>
        public ActionInteractiveClear InteractiveClear()
        {
            var action = new ActionInteractiveClear();
            Queue.Add(action);
            return action;
        }

        /// <summary>Sets a property on an interactive element identified by definition ID.</summary>
        public ActionInteractiveSet InteractiveSet(string definitionId, string property, string value)
        {
            var action = new ActionInteractiveSet { DefinitionId = definitionId, Property = property, Value = value };
            Queue.Add(action);
            return action;
        }
        
    }
    
}
