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
        /// <summary>Live autoload instance; null outside the Godot runtime (the typed layer still works there, the GDScript bridge does not).</summary>
        public static ModEventBus Instance { get; private set; }

        /// <summary>Registers the autoload singleton on tree entry.</summary>
        public override void _EnterTree() => Instance = this;
        /// <summary>Clears the autoload singleton reference on tree exit.</summary>
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

                var subscription = new TypedSubscription
                {
                    Handler = e => handler((T)e),
                    Original = handler,
                    ModId = modId,
                    Priority = priority,
                };

                // Lifecycle subscribers go after the last one with a priority
                // not above theirs: ties keep subscription order (List.Sort is
                // unstable and reshuffled them once a list outgrew 16 entries).
                var index = list.Count;
                if (type.GetCustomAttribute<LifecycleEventAttribute>() != null)
                {
                    while (index > 0 && list[index - 1].Priority > priority)
                        index--;
                }
                list.Insert(index, subscription);
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : ModEvent
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (!_typedSubs.TryGetValue(type, out var list))
                    return;

                list.RemoveAll(s => s.Original is Action<T> typed && typed == handler);
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

        /// <summary>
        /// Unload pass by code ownership rather than by declared mod id: removes
        /// typed subscriptions whose handler lives in a dying mod assembly and
        /// subscriptions to event types declared there. Most mods subscribe
        /// without passing their id, so <see cref="ClearForMod"/> alone leaves
        /// their handlers behind — and every such handler pins the mod's load
        /// context. Returns the number of removed subscriptions.
        /// </summary>
        public static int RemoveOwned(Mods.Unload.AssemblyOwnership ownership)
        {
            if (ownership == null || ownership.IsEmpty)
                return 0;
            var removed = 0;
            lock (_lock)
            {
                var ownedTypes = new List<Type>();
                foreach (var type in _typedSubs.Keys)
                {
                    if (ownership.Owns(type))
                        ownedTypes.Add(type);
                }
                foreach (var type in ownedTypes)
                {
                    removed += _typedSubs[type].Count;
                    _typedSubs.Remove(type);
                }
                foreach (var list in _typedSubs.Values)
                {
                    for (var i = list.Count - 1; i >= 0; i--)
                    {
                        if (!ownership.Owns(list[i].Original))
                            continue;
                        list.RemoveAt(i);
                        removed++;
                    }
                }
            }
            return removed;
        }

        /// <summary>Wipes every subscription on both the typed and string layers. Test/setup helper — not part of the normal unload path.</summary>
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

            Godot.Collections.Dictionary data;
            try
            {
                data = ModEventPayload.ToDictionary(evt);
            }
            catch (Exception ex)
            {
                GameLogger.Log("EVENT_BUS",
                    $"GDScript bridge cannot convert {type.Name} for '{eventKey}': {ex.Message}",
                    LogLevel.Error);
                return;
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

        private sealed class TypedSubscription
        {
            /// <summary>Type-erased handler; the generic wrapper casts back to the event type.</summary>
            public Action<ModEvent> Handler;
            /// <summary>The subscriber's own delegate: identity for <see cref="Unsubscribe{T}"/>, ownership for <see cref="RemoveOwned"/>.</summary>
            public Delegate Original;
            /// <summary>Owning mod id for <see cref="ClearForMod"/>; null for engine subscriptions.</summary>
            public string ModId;
            /// <summary>Delivery order for [LifecycleEvent] event types (lower = earlier).</summary>
            public int Priority;
        }

        private sealed class CallableSubscription
        {
            /// <summary>GDScript callable invoked with the bridged Dictionary payload.</summary>
            public Callable Handler;
            /// <summary>Owning mod id for <see cref="ClearForMod"/>; null for engine subscriptions.</summary>
            public string ModId;
            /// <summary>Reserved for parity with the typed layer (the string layer delivers in subscription order).</summary>
            public int Priority;
        }
    }
}
