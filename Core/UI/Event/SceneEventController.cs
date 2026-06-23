using System;
using System.Collections.Generic;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Event
{
    
    public partial class SceneEventController : Node
    {
        
        private string guid;
        
        private List<IOnUpdateEvent>      OnUpdateEventList      = new();
        private List<IOnDialogStartEvent> OnDialogStartEventList = new();
        private List<IOnDialogEndEvent>   OnDialogEndEventList   = new();
        private List<IOnTimeEvent>        OnTimerTickEventList   = new();
        public  bool                      IsDestroyed { get; private set; }
        public bool IsRunning { get; set; }
        public bool IsActive { get; set; } = true;

        private double timerAccum;
        private const double TickInterval = 1.0;
        
        public override void _Ready()
        {
            guid = Guid.NewGuid().ToString();
            ProcessPriority = int.MaxValue;
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _ExitTree()
        {
            ClearData();
            IsDestroyed = true;
        }

        public override void _Process(double delta)
        {
            timerAccum += delta;
            while (timerAccum >= TickInterval)
            {
                timerAccum -= TickInterval;
                TickTimer();
            }

            if (!IsActive)
                return;

            for (int i = OnUpdateEventList.Count - 1; i > -1; i--)
            {
                var eventSubscriber = OnUpdateEventList[i];
                if (eventSubscriber == null)
                {
                    OnUpdateEventList.RemoveAt(i);
                    continue;
                }
                eventSubscriber.OnUpdate();
            }
        }

		private void TickTimer()
		{
			for (int i = OnTimerTickEventList.Count - 1; i > -1; i--)
			{
				var eventSubscriber = OnTimerTickEventList[i];
				if (eventSubscriber == null)
				{
					OnTimerTickEventList.RemoveAt(i);
					continue;
				}
				try
				{
					eventSubscriber.OnTimerTick();
				}
				catch (Exception ex)
				{
					GameLogger.Log("EVENT", $"TickTimer subscriber {eventSubscriber.GetType().Name} threw: {ex.Message}", LogLevel.Error);
				}
			}
		}

        public void StartDialog(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent)
        {
            for (int i = OnDialogStartEventList.Count - 1; i > -1; i--)
            {
                var eventSubscriber = OnDialogStartEventList[i];
                if (eventSubscriber == null)
                {
                    OnDialogStartEventList.RemoveAt(i);
                    continue;
                }
                eventSubscriber.OnDialogStart(dialog, runtime, executableEvent);
            }
        }

        public void EndDialog(DialogQueue dialog, DialogRuntime runtime, ExecutableEvent executableEvent)
        {
            for (int i = OnDialogEndEventList.Count - 1; i > -1; i--)
            {
                var eventSubscriber = OnDialogEndEventList[i];
                if (eventSubscriber == null)
                {
                    OnDialogEndEventList.RemoveAt(i);
                    continue;
                }
                eventSubscriber.OnDialogEnd(dialog, runtime, executableEvent);
            }
        }

        #region Add
        public void AddUpdateEventListener(IOnUpdateEvent listener) => AddListener(listener, OnUpdateEventList);
        public void AddDialogStartEventListener(IOnDialogStartEvent listener) => AddListener(listener, OnDialogStartEventList);
        public void AddDialogEndEventListener(IOnDialogEndEvent listener) => AddListener(listener, OnDialogEndEventList);
        public void AddTimerTickEventListener(IOnTimeEvent listener) => AddListener(listener, OnTimerTickEventList);
        #endregion

        #region Remove
        public void RemoveUpdateEventListener(IOnUpdateEvent listener) => RemoveListener(listener, OnUpdateEventList);
        public void RemoveDialogStartEventListener(IOnDialogStartEvent listener) => RemoveListener(listener, OnDialogStartEventList);
        public void RemoveDialogEndEventListener(IOnDialogEndEvent listener) => RemoveListener(listener, OnDialogEndEventList);
        public void RemoveTimerTickEventListener(IOnTimeEvent listener) => RemoveListener(listener, OnTimerTickEventList);
        #endregion
        
        private void AddListener<T>(T listener, List<T> eventList) where T : class, IEventPriority
        {
            if (listener == null || eventList.Contains(listener))
                return;
            eventList.Add(listener);
            eventList.Sort((o1, o2) => o1.Priority - o2.Priority);
        }

        private void RemoveListener<T>(T listener, List<T> eventList) where T : class, IEventPriority
        {
            if (listener == null)
                return;
            if (eventList.Remove(listener))
                eventList.Sort((o1, o2) => o1.Priority - o2.Priority);
        }

        public void ClearData()
        {
            OnUpdateEventList.Clear();
            OnDialogStartEventList.Clear();
            OnDialogEndEventList.Clear();
            OnTimerTickEventList.Clear();
        }
    }
}
