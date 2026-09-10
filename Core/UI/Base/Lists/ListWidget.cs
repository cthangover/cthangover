using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Abstract model-driven list: instantiates items from a PackedScene prefab
    /// for each model in CreateModels(), then delegates positioning to the abstract
    /// PutToLayout (defined by layout subclasses like ColumnCellListWidget or
    /// VerticalListWidget). Refresh() tears down and rebuilds the list in place
    /// for reactive updates. The virtual Create/Init hooks let subclasses inject
    /// extra setup between instantiation and model binding without overriding
    /// CreateContent.
    /// </summary>
    public abstract partial class ListWidget<T, M> : Widget, IListWidget<T, M>
        where T : Control, IListItem<M>
    {

        [Export]
        private Control content;

        [Export]
        private PackedScene itemScene;

        [Export] private Vector2 itemsCellPadding = new(0f, 1f);

        public Vector2 ItemsCellPadding => itemsCellPadding;

        /// <summary>Prefab instance for layout measurement. Instantiated on each access — not cached. Returns null if itemScene is not set.</summary>
        protected T Prefab => itemScene?.Instantiate<T>();
        /// <summary>The Control rect of the item prefab, used for layout size calculations.</summary>
        protected Control PrefabRect => Prefab;

        /// <summary>The container Control that holds child items. Set via [Export] in the Godot editor.</summary>
        public Control Content => content;

        /// <summary>Calculates the total content size for a given item count. Subclass-specific — different layouts have different formulas.</summary>
        public abstract Vector2 GetContentSize(int count);

        /// <summary>The instantiated item collection. Set by <see cref="ConstructUI"/> and cleared by <see cref="DestructUI"/>.</summary>
        public List<T> Items { get; set; }

        /// <summary>Shows the widget and rebuilds the UI tree via <see cref="ConstructUI"/>.</summary>
        public override void Show()
        {
            base.Show();
            ConstructUI();
        }

        /// <summary>Tears down child items via <see cref="DestructUI"/> then hides via base <see cref="Widget.Hide"/>.</summary>
        public override void Hide()
        {
            DestructUI();
            base.Hide();
        }

        /// <summary>Destructs and reconstructs the item tree in place. Use for reactive updates when the model collection changes.</summary>
        public void Refresh()
        {
            if (!Visible)
                return;

            DestructUI();
            ConstructUI();
        }

        /// <summary>Iterates <paramref name="models"/> and calls <see cref="Create"/> for each, building the item widget collection.</summary>
        public List<T> CreateContent(ICollection<M> models)
        {
            var items = new List<T>();

            foreach (var model in models)
            {
                var item = Create(model);
                item.Construct(model);
                items.Add(item);
            }

            return items;
        }

        /// <summary>
        /// Full build pipeline: gets models via <see cref="CreateModels"/>, instantiates items via <see cref="CreateContent"/>,
        /// sets the content container size via <see cref="CallDeferred"/> to size, then positions each item via <see cref="PutToLayout"/>.
        /// </summary>
        public void ConstructUI()
        {
            var rect = Content;
            var models = CreateModels();

            var items = CreateContent(models);
            var size = GetContentSize(items.Count);
            rect.CallDeferred("set_size", size);

            for (int i = 0; i < items.Count; i++)
                PutToLayout(items[i], i, rect, size);

            Items = items;
        }

        /// <summary>Subclasses implement positioning logic. Called for each item with its flat index, the content container, and total content size.</summary>
        protected abstract void PutToLayout(T item, int index, Control container, Vector2 contentSize);

        /// <summary>Subclasses implement model sourcing. Called by <see cref="ConstructUI"/> before item instantiation.</summary>
        public abstract ICollection<M> CreateModels();

        /// <summary>Hook for subclasses to inject setup between instantiation and model binding. Called by <see cref="Create"/>.</summary>
        protected virtual void Init(T item, M model) { }

        /// <summary>Instantiates the PackedScene prefab, adds it to <see cref="Content"/>, and calls <see cref="Init"/> for subclass setup.</summary>
        protected virtual T Create(M model)
        {
            var instance = itemScene.Instantiate<T>();
            Content.AddChild(instance);
            Init(instance, model);
            return instance;
        }

        /// <summary>Calls <see cref="IListItem{TModel}.Destruct"/> on each child item, clears the list, and sets Items to null.</summary>
        public void DestructUI()
        {
            if (Items == null || Items.Count == 0)
                return;

            foreach (var item in Items)
                item.Destruct();

            Items.Clear();
            Items = null;
        }

    }
}
