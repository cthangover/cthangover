using System;
using System.Collections.Generic;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Godot;

namespace Cthangover.Core.UI.Dialog
{
    
    public class DialogQueue
    {

        public List<IActionDestruct> RuntimeObjectList { get; } = new();
        public List<IActionCommand> Queue { get; } = new();

        public ActionEffect Effect(string effectID)
        {
            var action = new ActionEffect { EffectID = effectID };
            Queue.Add(action);
            return action;
        }
        
        public ActionAnimatedForeground Animation(string sprite, int start, int count, float speed = 1, float nextFrameSpeed = 1, bool isLoop = true)
        {
            var list = new List<string>();
            for (int i = start; i <= start + count; i++)
                list.Add(sprite + $"{i:00}");
            return Animation(list, speed, nextFrameSpeed, isLoop);
        }
        
        public ActionAnimatedForeground Animation(List<string> sprites, float speed = 1, float nextFrameSpeed = 1, bool isLoop = true)
        {
            var action = new ActionAnimatedForeground { SpriteIDs = sprites, Speed = speed, NextFrameSpeed = nextFrameSpeed, IsLoop = isLoop };
            Queue.Add(action);
            return action;
        }
        
        public ActionBackground Background(string spriteId)
        {
            var action = new ActionBackground { SpriteID = spriteId };
            Queue.Add(action);
            return action;
        }
        
        public ActionBackground BackgroundTexture(Texture2D texture)
        {
            var action = new ActionBackground { Texture = texture };
            Queue.Add(action);
            return action;
        }
        
        public ActionForeground Foreground(string spriteId)
        {
            var action = new ActionForeground { SpriteID = spriteId };
            Queue.Add(action);
            return action;
        }
        
        public ActionForeground ForegroundTexture(Texture2D texture)
        {
            var action = new ActionForeground { Texture = texture };
            Queue.Add(action);
            return action;
        }
        
        public ActionText Text(string text, string first = null, string second = null, bool hideColor = false)
        {
            var action = new ActionText { Text = text, FirstAvatar = first, SecondAvatar = second, HideColor = hideColor };
            Queue.Add(action);
            return action;
        }
        
        public ActionText PText(string text, string first = null, string second = null, bool hideColor = false)
        {
            var action = new ActionText { Text = text, FirstAvatar = first, SecondAvatar = second, HideColor = hideColor, UseProcessText = true };
            Queue.Add(action);
            return action;
        }
        
        public ActionTitle Title(string title)
        {
            var action = new ActionTitle { TitleText = title };
            Queue.Add(action);
            return action;
        }
        
        public ActionTitle HideTitle()
        {
            var action = new ActionTitle { TitleText = null };
            Queue.Add(action);
            return action;
        }
        
        public ActionSet Set(string name, string value)
        {
            var action = new ActionSet { Name = name, Value = value };
            Queue.Add(action);
            return action;
        }
        
        public ActionSet Set(string name, Func<string> callback)
        {
            var action = new ActionSet { Name = name, Callback = callback };
            Queue.Add(action);
            return action;
        }

        public ActionSelect Select(List<SelectVariant> variants, string text = null, string first = null, string second = null)
        {
            var action = new ActionSelect { Variants = variants, Text = text, FirstAvatar = first, SecondAvatar = second };
            Queue.Add(action);
            return action;
        }
        
        public ActionEnd End()
        {
            var action = new ActionEnd();
            Queue.Add(action);
            return action;
        }
        
        public ActionEmpty Empty()
        {
            var action = new ActionEmpty();
            Queue.Add(action);
            return action;
        }
        
        public ActionEmpty Point(string id)
        {
            return (ActionEmpty)Empty().SetID(id);
        }

        public ActionMusic Music(string music)
        {
            var action = new ActionMusic { Music = music };
            Queue.Add(action);
            return action;
        }
        
        public ActionMusicPause MusicPause()
        {
            var action = new ActionMusicPause();
            Queue.Add(action);
            return action;
        }
        
        public ActionMusicPlay MusicPlay()
        {
            var action = new ActionMusicPlay();
            Queue.Add(action);
            return action;
        }
        
        public ActionDelay Delay(float waitTime, string showText)
        {
            var action = new ActionDelay { WaitTime = waitTime, HiddenMode = false, ShowText = showText };
            Queue.Add(action);
            return action;
        }
        
        public ActionDelay Delay(float waitTime, bool hiddenMode = false)
        {
            var action = new ActionDelay { WaitTime = waitTime, HiddenMode = hiddenMode, ShowText = null };
            Queue.Add(action);
            return action;
        }
        
        public ActionSound Sound(string sound)
        {
            var action = new ActionSound { Sound = sound };
            Queue.Add(action);
            return action;
        }

        public ActionScenario Action(string actionName)
        {
            var action = new ActionScenario { ActionName = actionName };
            Queue.Add(action);
            return action;
        }

        public ActionRun Run(System.Action executable)
        {
            var action = new ActionRun { Executable = executable };
            Queue.Add(action);
            return action;
        }
        
        public ActionIf IfGoTo(Func<bool> condition, string trueGoTo, string falseGoTo = null)
        {
            var action = new ActionIf { Condition = condition, TrueGoTo = trueGoTo, FalseGoTo = falseGoTo };
            Queue.Add(action);
            return action;
        }
        
        public ActionGoTo GoTo(string goTo)
        {
            var action = new ActionGoTo { GoTo = goTo };
            Queue.Add(action);
            return action;
        }
        
        public ActionSwitchScene SwitchScene(string targetScene, float speed = 4f, bool hiddenMode = true)
        {
            var action = new ActionSwitchScene { SceneName = targetScene, HiddenMode = hiddenMode, Speed = speed };
            Queue.Add(action);
            return action;
        }
        
        public ActionBackgroundColor BackgroundColor(Color color)
        {
            var action = new ActionBackgroundColor { Color = color };
            Queue.Add(action);
            return action;
        }
        
        public ActionShowDialog ShowDialog()
        {
            var action = new ActionShowDialog();
            Queue.Add(action);
            return action;
        }
        
        public ActionHideDialog HideDialog()
        {
            var action = new ActionHideDialog();
            Queue.Add(action);
            return action;
        }

        public ActionLightUseTime LightUseTime(bool useTime)
        {
            var action = new ActionLightUseTime { UseTime = useTime };
            Queue.Add(action);
            return action;
        }

        public ActionLightSet LightSet(string json)
        {
            var action = new ActionLightSet { LightsJson = json };
            Queue.Add(action);
            return action;
        }

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
        
    }
    
}
