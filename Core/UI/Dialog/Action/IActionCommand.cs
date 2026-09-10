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

        /// <summary>Unique action identifier, auto-generated GUID. Can be overridden for named GoTo targets.</summary>
        string ID { get; set; }
        
        /// <summary>Whether <see cref="Construct"/> has completed.</summary>
        bool IsConstructed { get; }
        /// <summary>Whether <see cref="Destruct"/> has been called and the action is finished.</summary>
        bool IsDestructed { get; }
        /// <summary>Controls when construction fires relative to the action's lifecycle.</summary>
        ConstructType ConstructType { get; }
        /// <summary>Controls when destruction fires relative to the action's lifecycle.</summary>
        DestructType DestructType { get; }
        /// <summary>How the runtime should handle advancement past this action.</summary>
        WaitType WaitType { get; }
        /// <summary>IDs of actions whose Destruct should be called when this action destructs.</summary>
        ISet<string> DelayedDestruct { get; }
        /// <summary>Duration in seconds when WaitType is WaitTime.</summary>
        float WaitTime { get; }
        /// <summary>Timestamp of the last <see cref="Run"/> call, in seconds since epoch.</summary>
        float StartTime { get; }
        
        #endregion

        #region Methods

        /// <summary>Called by the runtime to initialize the action before execution.</summary>
        void Construct();
        /// <summary>Called by the runtime to execute the action. Receives the runtime for context.</summary>
        void Run(DialogRuntime runtime);

        #endregion
        
    }
    
}
