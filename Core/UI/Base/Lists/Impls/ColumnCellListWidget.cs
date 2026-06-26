using Godot;

namespace Cthangover.Core.UI.Base.Lists.Impls
{
    /// <summary>
    /// Grid-based list layout with configurable column count, aspect-ratio cells,
    /// and border/padding offsets. Cell dimensions are computed dynamically from
    /// the content area width, so they scale with the parent. GetContentSize
    /// calculates the total scrollable height from item count and cell geometry,
    /// enabling vertical scrolling through a grid. PutToLayout positions items
    /// by flat-index-to-grid conversion. The isScaledCanvas flag supports
    /// resolution-independent layouts.
    /// </summary>
    public abstract partial class ColumnCellListWidget<TItem, TModel> : ListWidget<TItem, TModel>, ICellListWidget<TItem, TModel>
        where TItem : Control, IListItem<TModel>
    {
        [Export] private int columnCount;
        [Export] private float aspectHeight = 1f;
        [Export] private float borderLeft;
        [Export] private float borderRight;
        [Export] private float borderTop;
        [Export] private float borderBottom;
        [Export] private bool isScaledCanvas = true;

        private Border contentBorder => new() { left = borderLeft, right = borderRight, top = borderTop, bottom = borderBottom };

        public Vector2 CellSize => GetCellSize(Content.Size);

        private Vector2 GetCellSize(Vector2 contentSize)
        {
            var size = contentSize;
            var effectiveWidth = size.X - contentBorder.left - contentBorder.right;
            var cellWidth = (effectiveWidth - (columnCount - 1) * ItemsCellPadding.X) / columnCount;
            var cellHeight = cellWidth * aspectHeight;
            return new Vector2(cellWidth, cellHeight);
        }

        public Vector2I ViewCellsCount
        {
            get
            {
                var size = Content.Size;
                return new Vector2I(
                    columnCount,
                    Mathf.FloorToInt((size.Y - contentBorder.top - contentBorder.bottom) / (CellSize.Y + ItemsCellPadding.Y))
                );
            }
        }

        public override Vector2 GetContentSize(int count)
        {
            if (count == 0) return Vector2.Zero;

            var rect = Content.Size;
            var effectiveWidth = rect.X - contentBorder.left - contentBorder.right;
            var cellWidth = (effectiveWidth - (columnCount - 1) * ItemsCellPadding.X) / columnCount;
            var cellHeight = cellWidth * aspectHeight;

            var rows = Mathf.CeilToInt((float)count / columnCount);
            var contentHeight = contentBorder.top + contentBorder.bottom + rows * cellHeight + (rows - 1) * ItemsCellPadding.Y;
            var contentWidth = rect.X;

            return new Vector2(contentWidth, contentHeight);
        }

        protected override void PutToLayout(TItem item, int index, Control container, Vector2 contentSize)
        {
            var rectTransform = item;
            var cellIndex = GetIndex(index);
            var cellSize = GetCellSize(contentSize);

            if (rectTransform.GetParent() != container)
                rectTransform.Reparent(container, false);
            rectTransform.Position = new Vector2(
                contentBorder.left + cellIndex.X * (cellSize.X + ItemsCellPadding.X),
                contentBorder.top + cellIndex.Y * (cellSize.Y + ItemsCellPadding.Y)
            );
            rectTransform.Size = cellSize;
        }

        private Vector2I GetIndex(int flatIndex)
        {
            var count = ViewCellsCount;
            return new Vector2I(
                flatIndex % count.X,
                flatIndex / count.X
            );
        }
    }
}
