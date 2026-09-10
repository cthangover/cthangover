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

        /// <summary>Padding between items in pixels, stored as Vector2(X, Y). The X component is horizontal gap, Y is vertical gap.</summary>
        Vector2 ItemsCellPadding { get; }

        /// <summary>The container Control that holds all child item nodes. Items are added as children of this node.</summary>
        Control Content { get; }

        /// <summary>The collection of instantiated item widgets. Populated by <see cref="ConstructUI"/> and cleared by <see cref="DestructUI"/>.</summary>
        List<TItem> Items { get; set; }

        /// <summary>Implementations return the collection of data models to display. Called once per <see cref="ConstructUI"/> cycle.</summary>
        ICollection<TModel> CreateModels();

        /// <summary>Instantiates item widgets from the model collection. Separates model sourcing from instantiation so subclasses control both steps.</summary>
        List<TItem> CreateContent(ICollection<TModel> models);

        /// <summary>Calculates the total content size in pixels for <paramref name="count"/> items, used for scroll container sizing.</summary>
        Vector2 GetContentSize(int count);

        /// <summary>Builds the full item tree: creates models, instantiates items, and positions them via layout.</summary>
        void ConstructUI();
		
        /// <summary>Tears down all child items by calling each item's <see cref="IListItem{TModel}.Destruct"/> and clearing the Items list.</summary>
        void DestructUI();
		
    }
    
}
