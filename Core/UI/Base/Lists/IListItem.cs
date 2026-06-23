using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    public interface IListItem<TModel>
    {
	
        Control Rect { get; }

        TModel Model { get; }

        void Construct(TModel model);

        void Destruct();
		
    }
    
}
