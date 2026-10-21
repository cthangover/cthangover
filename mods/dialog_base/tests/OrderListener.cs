namespace Cthangover.Mods.Dialog.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.UI.Event;

    /// <summary>
    /// Update listener for <see cref="SceneEventController"/> tests: appends its
    /// <see cref="Name"/> to a shared <see cref="Log"/> on every update and may change
    /// the controller's listener list from inside the callback — remove
    /// <see cref="RemoveOnUpdate"/>, add <see cref="AddOnUpdate"/> — to check how a
    /// dispatch copes with a list changed under it.
    /// </summary>
    public class OrderListener : IOnUpdateEvent
    {
        /// <summary>Name written to <see cref="Log"/>.</summary>
        public string Name { get; set; }

        /// <summary>Dispatch priority.</summary>
        public int Priority { get; set; }

        /// <summary>Shared call log.</summary>
        public List<string> Log { get; set; }

        /// <summary>Controller whose list the callback changes.</summary>
        public SceneEventController Controller { get; set; }

        /// <summary>Listener to unregister during the update, if any.</summary>
        public IOnUpdateEvent RemoveOnUpdate { get; set; }

        /// <summary>Listener to register during the update, if any.</summary>
        public IOnUpdateEvent AddOnUpdate { get; set; }

        /// <summary>Logs the call and applies the requested list changes once.</summary>
        public void OnUpdate()
        {
            Log.Add(Name);
            if (RemoveOnUpdate != null)
            {
                Controller.RemoveUpdateEventListener(RemoveOnUpdate);
                RemoveOnUpdate = null;
            }
            if (AddOnUpdate != null)
            {
                Controller.AddUpdateEventListener(AddOnUpdate);
                AddOnUpdate = null;
            }
        }
    }
}
