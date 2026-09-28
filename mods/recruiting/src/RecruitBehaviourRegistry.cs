using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Characters;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Central registry and lifecycle dispatcher for recruit behaviours
    /// and conditions. Behaviours (<see cref="IRecruitBehaviour"/>) and
    /// conditions (<see cref="IRecruitCondition"/>) are auto-discovered
    /// from mod assemblies by <c>RecruitingInitializer</c>, which rescans
    /// assemblies as mods announce themselves and does a full AppDomain
    /// pass at resources-ready — the core's ModAssemblyLoader contains no
    /// recruiting knowledge.
    ///
    /// <b>Condition gating</b> — <see cref="CanRecruit"/> evaluates every
    /// registered condition conjunctively (logical AND), so a single
    /// <c>false</c> blocks recruitment. This allows mods to compose
    /// recruitment gates without coordination.
    ///
    /// <b>Id-based deduplication</b> — a rescan replaces any earlier
    /// implementation with the same <c>Id</c>, so mod reloads (which re-run
    /// registration) do not double-dispatch notifications.
    ///
    /// <b>Tick registration</b> — the first call to any lifecycle method
    /// triggers <see cref="RecruitTickController.EnsureRegistered"/>,
    /// which registers for the scene's timer event. This avoids paying
    /// the tick overhead unless at least one behaviour is active.
    ///
    /// <b>Singleton</b> — accessed via the static <c>Instance</c> field
    /// rather than a <c>Lazy&lt;T&gt;</c> wrapper because Godot
    /// reflection-based discovery must tolerate partial-initialisation
    /// scenarios where <c>Lazy</c>'s thread-safety check could deadlock.
    /// </summary>
    public class RecruitBehaviourRegistry
    {
        /// <summary>Eagerly-initialised singleton for reflection-safe access.</summary>
        public static readonly RecruitBehaviourRegistry Instance = new();

        private readonly List<IRecruitBehaviour> _behaviours = new();
        private readonly List<IRecruitCondition> _conditions = new();
        private readonly RecruitTickController _tickController = new();

        private void EnsureTickRegistered()
        {
            _tickController.EnsureRegistered();
        }

        /// <summary>
        /// Scans an assembly for implementations of
        /// <see cref="IRecruitBehaviour"/> and
        /// <see cref="IRecruitCondition"/>, instantiates them via
        /// the parameterless constructor, and registers them by Id.
        /// Called by <c>RecruitingInitializer</c> as mods load.
        /// </summary>
        public void RegisterAssembly(Assembly assembly)
        {
            foreach (var behaviour in Reflections.FindAndCreate<IRecruitBehaviour>(assembly))
                RegisterBehaviour(behaviour);

            foreach (var condition in Reflections.FindAndCreate<IRecruitCondition>(assembly))
                RegisterCondition(condition);
        }

        /// <summary>
        /// Scans every assembly loaded in the current <see cref="System.AppDomain"/>
        /// and registers all behaviours and conditions. Serves as the
        /// resources-ready safety net for assemblies whose name differs from the
        /// mod id (precompiled DLLs).
        /// </summary>
        public void RegisterAll()
        {
            foreach (var behaviour in Reflections.FindAndCreate<IRecruitBehaviour>())
                RegisterBehaviour(behaviour);

            foreach (var condition in Reflections.FindAndCreate<IRecruitCondition>())
                RegisterCondition(condition);

            GameLogger.Log("RECRUIT",
                $"RecruitBehaviourRegistry.RegisterAll: {_behaviours.Count} behaviour(s), {_conditions.Count} condition(s)");
        }

        private void RegisterBehaviour(IRecruitBehaviour behaviour)
        {
            if (behaviour == null)
                return;
            _behaviours.RemoveAll(existing => existing.Id == behaviour.Id);
            _behaviours.Add(behaviour);
        }

        private void RegisterCondition(IRecruitCondition condition)
        {
            if (condition == null)
                return;
            _conditions.RemoveAll(existing => existing.Id == condition.Id);
            _conditions.Add(condition);
        }

        /// <summary>
        /// Conjunctive gate: returns <c>true</c> only if every registered
        /// <see cref="IRecruitCondition"/> returns <c>true</c> for the
        /// given enemy and runtime state.
        /// </summary>
        public bool CanRecruit(Character enemy, RuntimeData runtime)
        {
            return _conditions.All(c => c.CanRecruit(enemy, runtime));
        }

        /// <summary>
        /// Looks up a behaviour by its <c>Id</c> (linear scan —
        /// behaviour lists are small enough that a dictionary is
        /// unnecessary). Returns <c>null</c> if not found.
        /// </summary>
        public IRecruitBehaviour Get(string id)
        {
            return _behaviours.Find(b => b.Id == id);
        }

        /// <summary>Returns all registered behaviours for external enumeration.</summary>
        public IEnumerable<IRecruitBehaviour> All()
        {
            return _behaviours;
        }

        /// <summary>
        /// Dispatches <see cref="IRecruitBehaviour.OnTick"/> to every
        /// behaviour for every active recruit. Ensures tick registration
        /// on first call via lazy init.
        /// </summary>
        public void OnTick()
        {
            EnsureTickRegistered();
            var runtime = GameData.Instance.Runtime;
            if (runtime == null)
                return;
            var recruits = RecruitingData.Instance.Data;
            foreach (var behaviour in _behaviours)
            {
                foreach (var recruit in recruits)
                {
                    behaviour.OnTick(recruit, runtime, runtime.Time.Tick);
                }
            }
        }

        /// <summary>
        /// Dispatches <see cref="IRecruitBehaviour.ConfigureRecruit"/> to
        /// every behaviour for the newly-added <paramref name="recruit"/>.
        /// </summary>
        public void OnConfigure(Recruit recruit)
        {
            EnsureTickRegistered();
            var runtime = GameData.Instance.Runtime;
            if (runtime == null)
                return;
            foreach (var behaviour in _behaviours)
                behaviour.ConfigureRecruit(recruit, runtime);
        }

        /// <summary>
        /// Dispatches <see cref="IRecruitBehaviour.OnRemove"/> to every
        /// behaviour for the departing <paramref name="recruit"/>.
        /// </summary>
        public void OnRemove(Recruit recruit)
        {
            EnsureTickRegistered();
            var runtime = GameData.Instance.Runtime;
            if (runtime == null)
                return;
            foreach (var behaviour in _behaviours)
                behaviour.OnRemove(recruit, runtime);
        }
    }

}
