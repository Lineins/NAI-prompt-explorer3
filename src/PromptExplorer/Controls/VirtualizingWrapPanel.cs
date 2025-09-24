using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PromptExplorer.Controls
{
    public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {
        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(128d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(128d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        private Size _extent = new Size(0, 0);
        private Size _viewport = new Size(0, 0);
        private Point _offset;
        private int _firstIndex;
        private int _lastIndex;

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public bool CanHorizontallyScroll { get; set; }

        public bool CanVerticallyScroll { get; set; } = true;

        public double ExtentHeight => _extent.Height;

        public double ExtentWidth => _extent.Width;

        public double HorizontalOffset => _offset.X;

        public double VerticalOffset => _offset.Y;

        public double ViewportHeight => _viewport.Height;

        public double ViewportWidth => _viewport.Width;

        public ScrollViewer? ScrollOwner { get; set; }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            base.OnItemsChanged(sender, args);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (double.IsInfinity(availableSize.Width))
            {
                availableSize.Width = _viewport.Width > 0 ? _viewport.Width : ItemWidth;
            }

            if (double.IsInfinity(availableSize.Height))
            {
                availableSize.Height = _viewport.Height > 0 ? _viewport.Height : ItemHeight;
            }

            int itemCount = GetItemCount();
            if (itemCount == 0)
            {
                CleanUpItems(-1, -1);
                _extent = new Size(availableSize.Width, 0);
                _viewport = availableSize;
                ScrollOwner?.InvalidateScrollInfo();
                return availableSize;
            }

            int itemsPerRow = Math.Max(1, (int)Math.Floor(availableSize.Width / ItemWidth));
            double usedWidth = itemsPerRow * ItemWidth;
            int rowCount = (int)Math.Ceiling((double)itemCount / itemsPerRow);

            _extent = new Size(usedWidth, rowCount * ItemHeight);
            _viewport = availableSize;
            ScrollOwner?.InvalidateScrollInfo();

            var (startIndex, endIndex) = GetVisibleRange(itemsPerRow, itemCount);
            _firstIndex = startIndex;
            _lastIndex = endIndex;

            CleanUpItems(startIndex, endIndex);
            GenerateItems(startIndex, endIndex);

            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count == 0)
            {
                return finalSize;
            }

            int itemsPerRow = Math.Max(1, (int)Math.Floor(finalSize.Width / ItemWidth));
            int itemIndex = _firstIndex;

            for (int i = 0; i < InternalChildren.Count; i++, itemIndex++)
            {
                var child = InternalChildren[i];
                int row = itemIndex / itemsPerRow;
                int column = itemIndex % itemsPerRow;
                double x = column * ItemWidth - _offset.X;
                double y = row * ItemHeight - _offset.Y;
                child.Arrange(new Rect(new Point(x, y), new Size(ItemWidth, ItemHeight)));
            }

            return finalSize;
        }

        protected override void BringIndexIntoView(int index)
        {
            if (index < 0 || index >= GetItemCount())
            {
                return;
            }

            int itemsPerRow = Math.Max(1, (int)Math.Floor(ViewportWidth / ItemWidth));
            int row = index / itemsPerRow;
            double targetOffset = row * ItemHeight;
            SetVerticalOffset(targetOffset);
        }

        public void LineDown() => SetVerticalOffset(VerticalOffset + ItemHeight / 2);

        public void LineUp() => SetVerticalOffset(VerticalOffset - ItemHeight / 2);

        public void LineLeft() => SetHorizontalOffset(HorizontalOffset - ItemWidth);

        public void LineRight() => SetHorizontalOffset(HorizontalOffset + ItemWidth);

        public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + ItemHeight);

        public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - ItemHeight);

        public void MouseWheelLeft() => LineLeft();

        public void MouseWheelRight() => LineRight();

        public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);

        public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);

        public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);

        public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            if (visual is UIElement element)
            {
                int childIndex = InternalChildren.IndexOf(element);
                if (childIndex >= 0)
                {
                    int itemsPerRow = Math.Max(1, (int)Math.Floor(ViewportWidth / ItemWidth));
                    int absoluteIndex = _firstIndex + childIndex;
                    int row = absoluteIndex / itemsPerRow;
                    SetVerticalOffset(row * ItemHeight);
                }
            }

            return rectangle;
        }

        public void SetHorizontalOffset(double offset)
        {
            if (offset < 0 || ViewportWidth >= ExtentWidth)
            {
                offset = 0;
            }
            else
            {
                offset = Math.Min(offset, ExtentWidth - ViewportWidth);
            }

            if (!offset.Equals(_offset.X))
            {
                _offset.X = offset;
                InvalidateMeasure();
                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        public void SetVerticalOffset(double offset)
        {
            if (offset < 0 || ViewportHeight >= ExtentHeight)
            {
                offset = 0;
            }
            else
            {
                offset = Math.Min(offset, ExtentHeight - ViewportHeight);
            }

            if (!offset.Equals(_offset.Y))
            {
                _offset.Y = offset;
                InvalidateMeasure();
                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        private void GenerateItems(int startIndex, int endIndex)
        {
            if (endIndex < startIndex)
            {
                return;
            }

            var generator = ItemContainerGenerator;
            GeneratorPosition startPos = generator.GeneratorPositionFromIndex(startIndex);
            int childIndex = startPos.Offset == 0 ? startPos.Index : startPos.Index + 1;
            if (childIndex < 0)
            {
                childIndex = 0;
            }

            using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
            {
                for (int itemIndex = startIndex; itemIndex <= endIndex; itemIndex++, childIndex++)
                {
                    bool newlyRealized = false;
                    if (!(generator.GenerateNext(out newlyRealized) is UIElement child))
                    {
                        continue;
                    }

                    if (newlyRealized)
                    {
                        if (childIndex >= InternalChildren.Count)
                        {
                            AddInternalChild(child);
                        }
                        else
                        {
                            InsertInternalChild(childIndex, child);
                        }

                        generator.PrepareItemContainer(child);
                    }

                    child.Measure(new Size(ItemWidth, ItemHeight));
                }
            }
        }

        private void CleanUpItems(int startIndex, int endIndex)
        {
            for (int i = InternalChildren.Count - 1; i >= 0; i--)
            {
                GeneratorPosition position = new GeneratorPosition(i, 0);
                int itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(position);
                if (itemIndex < startIndex || itemIndex > endIndex)
                {
                    ItemContainerGenerator.Remove(position, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        private (int start, int end) GetVisibleRange(int itemsPerRow, int itemCount)
        {
            if (itemCount == 0)
            {
                return (0, -1);
            }

            int firstVisibleRow = (int)Math.Floor(VerticalOffset / ItemHeight);
            int lastVisibleRow = (int)Math.Ceiling((VerticalOffset + ViewportHeight) / ItemHeight) - 1;
            firstVisibleRow = Math.Max(firstVisibleRow, 0);
            int maxRow = Math.Max(0, (int)Math.Ceiling((double)itemCount / itemsPerRow) - 1);
            lastVisibleRow = Math.Min(lastVisibleRow, maxRow);

            int startIndex = firstVisibleRow * itemsPerRow;
            int endIndex = Math.Min(itemCount - 1, ((lastVisibleRow + 1) * itemsPerRow) - 1);
            if (endIndex < startIndex)
            {
                endIndex = startIndex;
            }

            int cacheBuffer = itemsPerRow * 2;
            startIndex = Math.Max(0, startIndex - cacheBuffer);
            endIndex = Math.Min(itemCount - 1, endIndex + cacheBuffer);
            return (startIndex, endIndex);
        }

        private int GetItemCount()
        {
            if (ItemsControl.GetItemsOwner(this) is ItemsControl itemsControl)
            {
                return itemsControl.HasItems ? itemsControl.Items.Count : 0;
            }

            return 0;
        }
    }
}
