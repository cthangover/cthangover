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

        protected T Prefab => itemScene?.Instantiate<T>();
        protected Control PrefabRect => Prefab;

        public Control Content => content;

        public abstract Vector2 GetContentSize(int count);

        public List<T> Items { get; set; }

        public override void Show()
        {
            base.Show();
            ConstructUI();
        }

        public override void Hide()
        {
            DestructUI();
            base.Hide();
        }

        public void Refresh()
        {
            if (!Visible)
                return;

            DestructUI();
            ConstructUI();
        }

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

        protected abstract void PutToLayout(T item, int index, Control container, Vector2 contentSize);

        public abstract ICollection<M> CreateModels();

        protected virtual void Init(T item, M model) { }

        protected virtual T Create(M model)
        {
            var instance = itemScene.Instantiate<T>();
            Content.AddChild(instance);
            Init(instance, model);
            return instance;
        }

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
