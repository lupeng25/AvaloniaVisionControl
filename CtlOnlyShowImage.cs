using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Interactivity;
namespace AvaloniaVisionControl
{
    /// <summary>
    /// Avalonia 版图像显示控件
    /// 支持图像显示、鼠标缩放拖拽、图元叠加显示
    /// </summary>
    public partial class CtlOnlyShowImage : Control, IShowPaintElement
    {
        private Bitmap _originImage;
        private double _currentZoomFactor = 1.0;
        private double _defZoomFactor = 1.0;
        private const double ZoomStep = 0.3;
        private bool _isDragging;
        private Point _dragStartPoint;
        private Point _pressStartPoint; // 按下时的初始位置，用于判断单击
        private Point _scrollImageLocation = new Point(0, 0);
        private int _lastImageHeight = 0;
        private int _lastImageWidth = 0;
        private const double ClickThreshold = 5.0; // 单击判断阈值（像素）

        /// <summary>
        /// 是否允许鼠标滚轮缩放
        /// </summary>
        public bool AllowMouseScroll { get; set; } = true;

        /// <summary>
        /// 需要显示的相机 ID 列表
        /// </summary>
        public int[] NeedShowCam { get; set; }

        /// <summary>
        /// 鼠标左键单击事件
        /// 当用户在图像上单击鼠标左键时触发，用于控制机械手移动
        /// </summary>
        public event EventHandler<ImageClickEventArgs> ImageClick;

        public CtlOnlyShowImage(params int[] camIndex)
        {
            NeedShowCam = camIndex;
            
            // 启用鼠标事件
            Focusable = true;
            
            // 订阅双击事件
            DoubleTapped += OnDoubleTapped;
            
            // 订阅运动位置改变事件
            MotionMgr.Ins.CurrMachPosChanged += CurrMachPosChanged;
        }

        /// <summary>
        /// 无参数构造函数（用于 XAML）
        /// </summary>
        public CtlOnlyShowImage() : this(0) { }

        private void CurrMachPosChanged(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (!AllowMouseScroll || _originImage == null)
                return;

            var imageRect = GetImageRectangle();
            var mousePos = e.GetPosition(this);
            
            if (!imageRect.Contains(mousePos))
                return;

            // 获取鼠标相对于图片的位置
            double mouseX = (mousePos.X - _scrollImageLocation.X) / _currentZoomFactor;
            double mouseY = (mousePos.Y - _scrollImageLocation.Y) / _currentZoomFactor;

            double oldZoomFactor = _currentZoomFactor;

            // 根据滚轮方向调整缩放比例
            if (e.Delta.Y > 0)
            {
                _currentZoomFactor *= (1 + ZoomStep);
            }
            else
            {
                _currentZoomFactor *= (1 - ZoomStep);
            }

            _currentZoomFactor = Math.Max(_defZoomFactor, Math.Min(_currentZoomFactor, 100.0));

            if (_currentZoomFactor == _defZoomFactor)
            {
                _scrollImageLocation = new Point(0, 0);
            }
            else
            {
                // 计算缩放后的图片位置，使鼠标位置保持相对不变
                _scrollImageLocation = new Point(
                    mousePos.X - mouseX * _currentZoomFactor,
                    mousePos.Y - mouseY * _currentZoomFactor
                );
            }

            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                var imageRect = GetImageRectangle();
                var mousePos = e.GetPosition(this);
                
                if (!imageRect.Contains(mousePos))
                    return;

                _isDragging = true;
                _dragStartPoint = mousePos;
                _pressStartPoint = mousePos; // 保存按下时的初始位置
                Cursor = new Cursor(StandardCursorType.Hand);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isDragging)
            {
                var mousePos = e.GetPosition(this);
                double deltaX = mousePos.X - _dragStartPoint.X;
                double deltaY = mousePos.Y - _dragStartPoint.Y;

                _scrollImageLocation = new Point(
                    _scrollImageLocation.X + deltaX,
                    _scrollImageLocation.Y + deltaY
                );

                LimitImageWithinBounds();
                _dragStartPoint = mousePos;
                InvalidateVisual();
            }
            else
            {
                UpdateCursorStyle(e.GetPosition(this));
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            var point = e.GetCurrentPoint(this);
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                var mousePos = e.GetPosition(this);
                
                // 检查是否是单击（不是拖拽）
                if (_isDragging)
                {
                    // 使用按下时的初始位置计算总移动距离
                    double dragDistance = Math.Sqrt(
                        Math.Pow(mousePos.X - _pressStartPoint.X, 2) + 
                        Math.Pow(mousePos.Y - _pressStartPoint.Y, 2)
                    );
                    
                    // 如果移动距离小于阈值，认为是单击
                    if (dragDistance < ClickThreshold)
                    {
                        var imageRect = GetImageRectangle();
                        if (imageRect.Contains(mousePos) && _originImage != null)
                        {
                            // 计算鼠标在图像中的原始坐标
                            double imageX = (mousePos.X - _scrollImageLocation.X) / _currentZoomFactor;
                            double imageY = (mousePos.Y - _scrollImageLocation.Y) / _currentZoomFactor;
                            
                            // 确保坐标在图像范围内
                            imageX = Math.Max(0, Math.Min(imageX, _originImage.PixelSize.Width));
                            imageY = Math.Max(0, Math.Min(imageY, _originImage.PixelSize.Height));
                            
                            // 触发单击事件
                            ImageClick?.Invoke(this, new ImageClickEventArgs(
                                mousePos, 
                                new Point(imageX, imageY)
                            ));
                        }
                    }
                }
                
                _isDragging = false;
                Cursor = Cursor.Default;
            }
        }

        private void OnDoubleTapped(object sender, RoutedEventArgs e)
        {
            _currentZoomFactor = _defZoomFactor;
            _scrollImageLocation = new Point(0, 0);
            InvalidateVisual();
        }

        private void UpdateCursorStyle(Point mousePosition)
        {
            if (_originImage != null)
            {
                var imageRect = GetImageRectangle();
                if (imageRect.Contains(mousePosition) && _currentZoomFactor > _defZoomFactor)
                {
                    Cursor = new Cursor(StandardCursorType.Hand);
                }
                else
                {
                    Cursor = Cursor.Default;
                }
            }
        }

        private void LimitImageWithinBounds()
        {
            if (_originImage == null) return;

            double imageWidth = _originImage.PixelSize.Width * _currentZoomFactor;
            double imageHeight = _originImage.PixelSize.Height * _currentZoomFactor;

            double minX = Math.Min(0, Bounds.Width - imageWidth);
            double maxX = Math.Max(0, Bounds.Width - imageWidth);
            double minY = Math.Min(0, Bounds.Height - imageHeight);
            double maxY = Math.Max(0, Bounds.Height - imageHeight);

            _scrollImageLocation = new Point(
                Math.Max(minX, Math.Min(_scrollImageLocation.X, maxX)),
                Math.Max(minY, Math.Min(_scrollImageLocation.Y, maxY))
            );
        }

        private Rect GetImageRectangle()
        {
            if (_originImage == null)
                return new Rect();

            double imageWidth = _originImage.PixelSize.Width * _currentZoomFactor;
            double imageHeight = _originImage.PixelSize.Height * _currentZoomFactor;
            
            return new Rect(_scrollImageLocation, new Size(imageWidth, imageHeight));
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // 绘制棋盘背景
            DrawCheckerboardBackground(context, Bounds);

            if (_originImage == null)
                return;

            // 计算默认缩放比
            if (_lastImageHeight != _originImage.PixelSize.Height || 
                _lastImageWidth != _originImage.PixelSize.Width)
            {
                _defZoomFactor = Math.Min(
                    Bounds.Width / _originImage.PixelSize.Width,
                    Bounds.Height / _originImage.PixelSize.Height
                );
                _currentZoomFactor = _defZoomFactor;
                _lastImageHeight = _originImage.PixelSize.Height;
                _lastImageWidth = _originImage.PixelSize.Width;
            }

            // 计算缩放后的图片大小
            double newW = _originImage.PixelSize.Width * _currentZoomFactor;
            double newH = _originImage.PixelSize.Height * _currentZoomFactor;

            // 绘制图像
            var destRect = new Rect(_scrollImageLocation.X, _scrollImageLocation.Y, newW, newH);
            
            // 直接绘制图像（Avalonia 会自动处理插值）
            context.DrawImage(_originImage, destRect);

            // 绘制图元
            if (m_CurrShowElement.Count > 0 && CtlShowPaintStatus > 0)
            {
                var imageRect = GetImageRectangle();
                foreach (var element in m_CurrShowElement)
                {
                    var newPt = GetTransedPts(element.Pts, element.Type, imageRect);
                    if (newPt.Count > 0)
                        element.Paint(context, m_lineWidthScale * _currentZoomFactor, newPt);
                }
            }
        }
        //修改处：
        /// <summary>
        /// 
        /// </summary>
        /// <param name="availableSize"></param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            // 若有图片，返回图片尺寸；若无，返回默认尺寸（避免宽高为0）
            if (_originImage != null)
            {
                // 按图片原始尺寸返回（或根据缩放比例计算）
                return new Size(
                    _originImage.PixelSize.Width * _currentZoomFactor,
                    _originImage.PixelSize.Height * _currentZoomFactor
                );
            }
            // 无图片时返回默认尺寸（避免布局异常）
            return new Size(800, 450); // 可根据需求调整默认值
        }
        private void DrawCheckerboardBackground(DrawingContext context, Rect area)
        {
            const int gridSize = 10;
            var darkBrush = new SolidColorBrush(Color.FromRgb(28, 28, 28));
            var lightBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

            for (int y = 0; y < area.Height; y += gridSize)
            {
                for (int x = 0; x < area.Width; x += gridSize)
                {
                    var brush = ((x / gridSize + y / gridSize) % 2 == 0) ? darkBrush : lightBrush;
                    context.FillRectangle(brush, new Rect(x, y, gridSize, gridSize));
                }
            }
        }

        /// <summary>
        /// 显示图像
        /// </summary>
        public int ShowImage(ReceiveBitmapEventArgs e)
        {
            foreach (var index in NeedShowCam)
            {
                if (e.CamID == index)
                {
                    if (e.Image == null)
                        continue;

                    // 在 UI 线程更新图像
                    Dispatcher.UIThread.Post(() =>
                    {
                        _originImage?.Dispose();
                        _originImage = e.Image;
                        InvalidateVisual();
                    });

                    return 0;
                }
            }
            return -1;
        }

        /// <summary>
        /// 获取需要显示的相机 ID 列表
        /// </summary>
        public int[] GetShowCam()
        {
            return NeedShowCam;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            
            // 取消订阅事件
            DoubleTapped -= OnDoubleTapped;
            MotionMgr.Ins.CurrMachPosChanged -= CurrMachPosChanged;
            
            // 释放资源
            _originImage?.Dispose();
            //修改处：
            _originImage = null;
        }
    }
}

