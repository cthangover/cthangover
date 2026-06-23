using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Base.Lists
{

    public interface IListWidget<TItem, TModel> : IWidget
        where TItem : class, IListItem<TModel>
    {

        Vector2 ItemsCellPadding { get; }

        Control Content { get; }

        List<TItem> Items { get; set; }

        ICollection<TModel> CreateModels();

        List<TItem> CreateContent(ICollection<TModel> models);

        Vector2 GetContentSize(int count);

        void ConstructUI();
		
        void DestructUI();
		
    }
    
}
