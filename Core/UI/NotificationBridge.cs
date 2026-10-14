using System;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Core-side bridge for floating UI notifications. The implementation
    /// (MessagesHelper, interface engine mod) installs its hook through
    /// <see cref="Register"/> when the mod initializer runs; the kernel call
    /// sites (CharacterData.SendAddNotification) only know this delegate slot.
    /// Registration is first-wins; while no hooks are bound the message is
    /// logged and dropped instead of throwing, so a kernel-only runtime keeps
    /// working without the interface mod. Same pattern as
    /// <c>LightingBridge</c> and <see cref="UiThemeBridge"/>.
    /// </summary>
    public static class NotificationBridge
    {
        private static readonly object Gate = new();
        private static Action<string> _showMessage;

        /// <summary>Installs the notification hook; first registration wins, re-registrations are logged and ignored.</summary>
        public static void Register(Action<string> showMessage)
        {
            if (showMessage == null)
                return;

            lock (Gate)
            {
                if (_showMessage != null)
                {
                    GameLogger.Log("UI", "NotificationBridge: notification hook already registered; ignoring re-registration", LogLevel.Warning);
                    return;
                }

                _showMessage = showMessage;
            }
        }

        /// <summary>
        /// Shows a floating UI message through the bound implementation.
        /// Logged and dropped when no interface implementation is bound.
        /// </summary>
        public static void Show(string text)
        {
            Action<string> hook;
            lock (Gate)
                hook = _showMessage;

            if (hook == null)
            {
                GameLogger.Log("UI", "NotificationBridge: no notification hook bound (interface mod not loaded); message dropped: " + text, LogLevel.Warning);
                return;
            }

            hook(text);
        }
    }
}
