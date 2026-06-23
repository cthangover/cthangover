using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Characters;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{

    public class RecruitBehaviourRegistry
    {
        public static readonly RecruitBehaviourRegistry Instance = new();

        private readonly List<IRecruitBehaviour> _behaviours = new();
        private readonly List<IRecruitCondition> _conditions = new();
        private readonly RecruitTickController _tickController = new();

        private void EnsureTickRegistered()
        {
            _tickController.EnsureRegistered();
        }

        public void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IRecruitBehaviour).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var instance = (IRecruitBehaviour)Activator.CreateInstance(type);
                    _behaviours.Add(instance);
                }

                if (typeof(IRecruitCondition).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    var instance = (IRecruitCondition)Activator.CreateInstance(type);
                    _conditions.Add(instance);
                }
            }
        }

        public bool CanRecruit(Character enemy, RuntimeData runtime)
        {
            return _conditions.All(c => c.CanRecruit(enemy, runtime));
        }

        public IRecruitBehaviour Get(string id)
        {
            return _behaviours.Find(b => b.Id == id);
        }

        public IEnumerable<IRecruitBehaviour> All()
        {
            return _behaviours;
        }

        public void OnTick()
        {
            EnsureTickRegistered();
            var runtime = GameData.Instance.Runtime;
            if (runtime == null)
                return;
            var recruits = runtime.RecruitingData.Data;
            foreach (var behaviour in _behaviours)
            {
                foreach (var recruit in recruits)
                {
                    behaviour.OnTick(recruit, runtime, runtime.Time.Tick);
                }
            }
        }
        
        public void OnConfigure(Recruit recruit)
        {
            EnsureTickRegistered();
            var runtime = GameData.Instance.Runtime;
            if (runtime == null)
                return;
            foreach (var behaviour in _behaviours)
                behaviour.ConfigureRecruit(recruit, runtime);
        }
        
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
