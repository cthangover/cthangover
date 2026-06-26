using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Generic list widget contract: binds a collection of models to Item widgets.
    /// The two-step CreateModels() → CreateContent() pattern separates data sourcing
    /// from instantiation, so subclasses can pull models from anywhere (runtime state,
    /// config, save data) while the framework handles layout. ConstructUI/DestructUI
    /// manage the full lifecycle of child items.
    /// </summary>
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
