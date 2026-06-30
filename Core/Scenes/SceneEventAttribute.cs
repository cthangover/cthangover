using System;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Custom attribute applied to <see cref="ExecutableEvent"/> subclasses to declare
    /// their scene association and execution ordering constraints. Discovered via
    /// reflection by <see cref="SceneEventRegistry.RegisterFromAttributes"/> during
    /// initialization. Cannot be inherited or applied multiple times to the same class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SceneEventAttribute : Attribute
    {
        /// <summary>
        /// The scene identifier that this event class belongs to.
        /// </summary>
        public string SceneName { get; }

        /// <summary>
        /// Execution priority; lower values execute earlier. Default is 0.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// The identifier of another event that must execute before this one, used for
        /// topological ordering within a scene's event queue.
        /// </summary>
        public string After { get; set; }

        /// <summary>
        /// A condition expression string parsed by <see cref="ConditionParser"/> that
        /// must evaluate to <c>true</c> for this event to be included in the queue.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Creates a new <see cref="SceneEventAttribute"/> bound to the specified scene.
        /// </summary>
        /// <param name="sceneName">The scene identifier this event class belongs to.</param>
        public SceneEventAttribute(string sceneName)
        {
            SceneName = sceneName;
        }
    }
}
