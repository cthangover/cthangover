using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;
using Godot;
using GdDictionary = Godot.Collections.Dictionary;
using GdArray = Godot.Collections.Array;

namespace Cthangover.Core.Events
{
    /// <summary>
    /// Global event bus. Autoload Node, survives scene changes.
    ///
    /// Two subscription layers:
    /// 1. Typed (C#): Subscribe&lt;T&gt;/Publish&lt;T&gt; — dispatch by type.
    /// 2. String (GDScript): subscribe/publish — key + Callable/Dictionary.
    ///
    /// Auto-bridge: Publish&lt;T&gt; additionally broadcasts on the [EventKey]
    /// string so GDScript subscribers receive the same events as C#.
    ///
    /// Cleanup: ClearForMod(modId) on mod unload removes its subscriptions.
    /// </summary>
    public partial class ModEventBus : Node
    {
        public static ModEventBus Instance { get; private set; }

        public override void _EnterTree() => Instance = this;
        public override void _ExitTree()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── typed layer (static, for C#) ──

        private static readonly Dictionary<Type, List<TypedSubscription>> _typedSubs = new();
        private static readonly object _lock = new();

        /// <summary>
        /// Subscribe to a typed event.
        /// <paramref name="priority"/> only meaningful for [LifecycleEvent] —
        /// lower values fire first. Otherwise ignored.
        /// <paramref name="modId"/> — mod ID for ClearForMod.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler, int priority = 0, string modId = null)
            where T : ModEvent
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (!_typedSubs.TryGetValue(type, out var list))
                {
                    list = new List<TypedSubscription>();
                    _typedSubs[type] = list;
                }

                list.Add(new TypedSubscription
                {
                    Handler = e => handler((T)e),
                    ModId = modId,
                    Priority = priority,
                });

                var isLifecycle = type.GetCustomAttribute<LifecycleEventAttribute>() != null;
                if (isLifecycle)
                    list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : ModEvent
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (!_typedSubs.TryGetValue(type, out var list))
                    return;

                list.RemoveAll(s => s.Handler is Action<T> typed && typed == handler);
            }
        }

        /// <summary>
        /// Publishes event to typed subscribers, then auto-bridges
        /// to the string layer for GDScript.
        ///
        /// Typed (C#) subscribers are always invoked — even without a live
        /// autoload <see cref="Instance"/> — so unit tests and headless code
        /// paths can observe events. The GDScript bridge is a no-op when
        /// <see cref="Instance"/> is null (bridge requires the Godot node).
        /// </summary>
        public static void Publish<T>(T evt) where T : ModEvent
        {
            List<TypedSubscription> snapshot;
            lock (_lock)
            {
                if (!_typedSubs.TryGetValue(typeof(T), out var list) || list.Count == 0)
                    snapshot = null;
                else
                    snapshot = new List<TypedSubscription>(list);
            }

            if (snapshot != null)
            {
                foreach (var sub in snapshot)
                {
                    try
                    {
                        sub.Handler(evt);
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Log("EVENT_BUS",
                            $"Typed handler for {typeof(T).Name} failed (mod='{sub.ModId}'): {ex.Message}",
                            LogLevel.Error);
                    }
                }
            }

            // The GDScript bridge creates Godot Dictionary/Callable objects, which
            // is only legal on the main thread. During the background mod-load phase
            // no GDScript subscribers are registered yet, so skipping the bridge off
            // the main thread is a no-op in practice and keeps the load thread safe.
            if (GameEnvironment.IsMainThread)
                Instance?.BridgeToGdScript(evt);
        }

        // ── string layer (instance, for GDScript) ──

        private readonly Dictionary<string, List<CallableSubscription>> _stringSubs = new();

        /// <summary>
        /// GDScript: ModEventBus.subscribe("scene_entered", _on_scene, 0, "my_mod")
        /// </summary>
        public void Subscribe(string eventKey, Callable handler, int priority = 0, string modId = null)
        {
            if (string.IsNullOrEmpty(eventKey) || handler.Target == null)
                return;

            lock (_stringSubs)
            {
                if (!_stringSubs.TryGetValue(eventKey, out var list))
                {
                    list = new List<CallableSubscription>();
                    _stringSubs[eventKey] = list;
                }
                list.Add(new CallableSubscription
                {
                    Handler = handler,
                    ModId = modId,
                    Priority = priority,
                });
            }
        }

        /// <summary>
        /// GDScript: ModEventBus.unsubscribe("scene_entered", _on_scene)
        /// </summary>
        public void Unsubscribe(string eventKey, Callable handler)
        {
            lock (_stringSubs)
            {
                if (!_stringSubs.TryGetValue(eventKey, out var list))
                    return;

                list.RemoveAll(s => s.Handler.Equals(handler));
            }
        }

        /// <summary>
        /// GDScript: ModEventBus.publish("cooking_meal_ready", {"dish": "wolf_stew"})
        /// </summary>
        public void Publish(string eventKey, Godot.Collections.Dictionary data)
        {
            if (string.IsNullOrEmpty(eventKey))
                return;

            List<CallableSubscription> snapshot;
            lock (_stringSubs)
            {
                if (!_stringSubs.TryGetValue(eventKey, out var list) || list.Count == 0)
                    return;
                snapshot = new List<CallableSubscription>(list);
            }

            foreach (var sub in snapshot)
            {
                try
                {
                    if (sub.Handler.Target == null)
                        continue;
                    sub.Handler.Call(data);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("EVENT_BUS",
                        $"String handler for '{eventKey}' failed (mod='{sub.ModId}'): {ex.Message}",
                        LogLevel.Error);
                }
            }
        }

        // ── cleanup ──

        /// <summary>
        /// Removes all subscriptions for a mod — both typed and string.
        /// Called on mod unload/reload.
        /// </summary>
        public void ClearForMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return;

            lock (_lock)
            {
                foreach (var kvp in _typedSubs)
                    kvp.Value.RemoveAll(s => s.ModId == modId);
            }

            lock (_stringSubs)
            {
                foreach (var kvp in _stringSubs)
                    kvp.Value.RemoveAll(s => s.ModId == modId);
            }

            GameLogger.Log("EVENT_BUS", $"Cleared all subscriptions for mod '{modId}'");
        }

        public void ClearAll()
        {
            lock (_lock)
                _typedSubs.Clear();

            lock (_stringSubs)
                _stringSubs.Clear();
        }

        // ── internals ──

        private void BridgeToGdScript(ModEvent evt)
        {
            var type = evt.GetType();
            var keyAttr = type.GetCustomAttribute<EventKeyAttribute>();
            if (keyAttr == null)
                return;

            var eventKey = keyAttr.Key;

            List<CallableSubscription> snapshot;
            lock (_stringSubs)
            {
                if (!_stringSubs.TryGetValue(eventKey, out var list) || list.Count == 0)
                    return;
                snapshot = new List<CallableSubscription>(list);
            }

            var data = new Godot.Collections.Dictionary();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var key = SnakeCase(prop.Name);
                var value = prop.GetValue(evt);

                if (value is Node node)
                    data[key] = node;
                else if (value is string[] arr)
                    data[key] = new Godot.Collections.Array<string>(arr);
                else if (value != null)
                    data[key] = Variant.From(value);
            }

            foreach (var sub in snapshot)
            {
                try
                {
                    if (sub.Handler.Target == null)
                        continue;
                    sub.Handler.Call(data);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("EVENT_BUS",
                        $"GDScript bridge handler for '{eventKey}' failed (mod='{sub.ModId}'): {ex.Message}",
                        LogLevel.Error);
                }
            }
        }

        private static string SnakeCase(string pascal)
        {
            if (string.IsNullOrEmpty(pascal))
                return pascal;

            var chars = new List<char> { char.ToLowerInvariant(pascal[0]) };
            for (int i = 1; i < pascal.Length; i++)
            {
                if (char.IsUpper(pascal[i]))
                {
                    chars.Add('_');
                    chars.Add(char.ToLowerInvariant(pascal[i]));
                }
                else
                {
                    chars.Add(pascal[i]);
                }
            }
            return new string(chars.ToArray());
        }

        private sealed class TypedSubscription
        {
            public Action<ModEvent> Handler;
            public string ModId;
            public int Priority;
        }

        private sealed class CallableSubscription
        {
            public Callable Handler;
            public string ModId;
            public int Priority;
        }
    }
}
