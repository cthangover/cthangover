using System;

namespace Cthangover.Core.Scenes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SceneEventAttribute : Attribute
    {
        public string SceneName { get; }
        public int Priority { get; set; }
        public string After { get; set; }
        public string Condition { get; set; }

        public SceneEventAttribute(string sceneName)
        {
            SceneName = sceneName;
        }
    }
}
