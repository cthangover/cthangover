using System.Collections.Generic;

namespace Cthangover.Core.UI.Dialog.Action
{

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
