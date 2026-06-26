using System.Collections.Generic;

namespace Cthangover.Core.UI.Dialog.Action
{
    /// <summary>
    /// Full dialog action contract: extends IActionDestruct with construction,
    /// execution (Run), and queryable state (WaitType, timing enums, delayed-destruct
    /// dependencies). The ID property enables cross-action references (GoTo, If).
    /// </summary>
    public interface IActionCommand : IActionDestruct
    {

        #region Properties

        string ID { get; set; }
        
        bool IsConstructed { get; }
        bool IsDestructed { get; }
        ConstructType ConstructType { get; }
        DestructType DestructType { get; }
        WaitType WaitType { get; }
        ISet<string> DelayedDestruct { get; }
        float WaitTime { get; }
        float StartTime { get; }
        
        #endregion

        #region Methods

        void Construct();
        void Run(DialogRuntime runtime);

        #endregion
        
    }
    
}
