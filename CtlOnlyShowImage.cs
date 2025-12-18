using System;
using System.Collections.Generic;
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
        private Bitmap originImage;
        private double currentZoomFactor = 0.2;
        private double defZoomFactor = 0.2;
        private const double zoomStep = 0.3;
        private bool isDragging;
        private Point dragStartPoint;
        private Point pressStartPoint; // 按下时的初始位置，用于判断单击
        private Point scrollImageLocation = new Point(0, 0);
        /// <summary>
        /// 缩放图片的宽高
        /// </summary>
        private Point scrollImageWH = new Point(0, 0);
        private int lastImageHeight = 0;
        private int lastImageWidth = 0;
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
            ReFresh();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (!AllowMouseScroll || originImage == null)
                return;

            var imageRect = GetImageRectangle();
            var mousePos = e.GetPosition(this);
            
            if (!imageRect.Contains(mousePos))
                return;

            // 获取鼠标相对于图片的位置
            double mouseX = (mousePos.X - scrollImageLocation.X) / currentZoomFactor;
            double mouseY = (mousePos.Y - scrollImageLocation.Y) / currentZoomFactor;

            double oldZoomFactor = currentZoomFactor;

            // 根据滚轮方向调整缩放比例
            if (e.Delta.Y > 0)
            {
                currentZoomFactor *= (1 + zoomStep);
            }
            else
            {
                currentZoomFactor *= (1 - zoomStep);
            }

            currentZoomFactor = Math.Max(defZoomFactor, Math.Min(currentZoomFactor, 100.0));

            if (currentZoomFactor == defZoomFactor)
            {
                scrollImageLocation = new Point(0, 0);
            }
            else
            {
                // 计算缩放后的图片位置，使鼠标位置保持相对不变
                scrollImageLocation = new Point(
                    mousePos.X - mouseX * currentZoomFactor,
                    mousePos.Y - mouseY * currentZoomFactor
                );
            }

            UpdateCursorStyle(mousePos);
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var point = e.GetCurrentPoint(this);
            var mousePos = e.GetPosition(this);

            if (point.Properties.IsLeftButtonPressed)
            {
                var imageRect = GetImageRectangle();
                
                if (!imageRect.Contains(mousePos))
                    return;

                isDragging = true;
                dragStartPoint = mousePos;
                pressStartPoint = mousePos; // 保存按下时的初始位置
                Cursor = new Cursor(StandardCursorType.Hand);
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                // 右键移动逻辑
                if (CtlMouseStatus == ImageCtlMouseStatus.RightClickMove && originImage != null)
                {
                    try
                    {
                        var machPt = CtlPtToMachPt(mousePos);
                        if (MotionMgr.Ins.AxisFunc != null)
                        {
                            int outInt;
                            string sErr;
                            var list = new List<double> { machPt.X, machPt.Y };
                            MotionMgr.Ins.AxisFunc(AxisOperationType.MoveAndWait, AxisType.XY,
                                list, out outInt, out sErr);
                        }
                    }
                    catch { }
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (isDragging)
            {
                var mousePos = e.GetPosition(this);
                double deltaX = mousePos.X - dragStartPoint.X;
                double deltaY = mousePos.Y - dragStartPoint.Y;

                scrollImageLocation = new Point(
                    scrollImageLocation.X + deltaX,
                    scrollImageLocation.Y + deltaY
                );

                LimitImageWithinBounds();
                dragStartPoint = mousePos;
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
                if (isDragging)
                {
                    // 使用按下时的初始位置计算总移动距离
                    double dragDistance = Math.Sqrt(
                        Math.Pow(mousePos.X - pressStartPoint.X, 2) + 
                        Math.Pow(mousePos.Y - pressStartPoint.Y, 2)
                    );
                    
                    // 如果移动距离小于阈值，认为是单击
                    if (dragDistance < ClickThreshold)
                    {
                        var imageRect = GetImageRectangle();
                        if (imageRect.Contains(mousePos) && originImage != null)
                        {
                            // 计算鼠标在图像中的原始坐标
                            double imageX = (mousePos.X - scrollImageLocation.X) / currentZoomFactor;
                            double imageY = (mousePos.Y - scrollImageLocation.Y) / currentZoomFactor;
                            
                            // 确保坐标在图像范围内
                            imageX = Math.Max(0, Math.Min(imageX, originImage.PixelSize.Width));
                            imageY = Math.Max(0, Math.Min(imageY, originImage.PixelSize.Height));
                            
                            // 触发单击事件
                            ImageClick?.Invoke(this, new ImageClickEventArgs(
                                mousePos, 
                                new Point(imageX, imageY)
                            ));
                        }
                    }
                }
                
                isDragging = false;
                Cursor = Cursor.Default;
            }
        }

        private void OnDoubleTapped(object sender, RoutedEventArgs e)
        {
            currentZoomFactor = defZoomFactor;
            scrollImageLocation = new Point(0, 0);
            InvalidateVisual();
        }

        private void UpdateCursorStyle(Point mousePosition)
        {
            if (originImage != null)
            {
                var imageRect = GetImageRectangle();
                if (imageRect.Contains(mousePosition) && currentZoomFactor > defZoomFactor)
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
            if (originImage == null) return;

            double imageWidth = originImage.PixelSize.Width * currentZoomFactor;
            double imageHeight = originImage.PixelSize.Height * currentZoomFactor;

            double minX = Math.Min(0, Bounds.Width - imageWidth);
            double maxX = Math.Max(0, Bounds.Width - imageWidth);
            double minY = Math.Min(0, Bounds.Height - imageHeight);
            double maxY = Math.Max(0, Bounds.Height - imageHeight);

            scrollImageLocation = new Point(
                Math.Max(minX, Math.Min(scrollImageLocation.X, maxX)),
                Math.Max(minY, Math.Min(scrollImageLocation.Y, maxY))
            );
        }

        private Rect GetImageRectangle()
        {
            if (originImage == null)
                return new Rect();

            double imageWidth = originImage.PixelSize.Width * currentZoomFactor;
            double imageHeight = originImage.PixelSize.Height * currentZoomFactor;
            
            return new Rect(scrollImageLocation, new Size(imageWidth, imageHeight));
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // 绘制棋盘背景
            DrawCheckerboardBackground(context, Bounds);

            if (originImage == null)
                return;

            // 计算默认缩放比
            if (lastImageHeight != originImage.PixelSize.Height || 
                lastImageWidth != originImage.PixelSize.Width)
            {
                // 避免除以0
                if (originImage.PixelSize.Width > 0 && originImage.PixelSize.Height > 0 && Bounds.Width > 0 && Bounds.Height > 0)
                {
                    defZoomFactor = Math.Min(
                        Bounds.Width / originImage.PixelSize.Width,
                        Bounds.Height / originImage.PixelSize.Height
                    );
                    currentZoomFactor = defZoomFactor;
                    lastImageHeight = originImage.PixelSize.Height;
                    lastImageWidth = originImage.PixelSize.Width;
                }
            }

            // 计算缩放后的图片大小
            double newW = originImage.PixelSize.Width * currentZoomFactor;
            double newH = originImage.PixelSize.Height * currentZoomFactor;

            // 绘制图像
            var destRect = new Rect(scrollImageLocation.X, scrollImageLocation.Y, newW, newH);
            
            // 直接绘制图像（Avalonia 会自动处理插值）
            context.DrawImage(originImage, destRect);

            // 更新 scrollImageWH
            scrollImageWH = new Point(newW, newH);

            // 绘制图元
            if (m_CurrShowElement.Count > 0 && (int)CtlShowPaintStatus > 0)
            {
                var imageRect = GetImageRectangle();
                foreach (var element in m_CurrShowElement)
                {
                    // 处理动态更新机械位置
                    if (element.IndexShowCurrMachPos >= 0)
                    {
                        var machPos = MotionMgr.Ins.CurrMachPos;
                        if (element.Pts.Count > (element.IndexShowCurrMachPos + 1) * 2 + 1)
                        {
                            element.Pts[(element.IndexShowCurrMachPos + 1) * 2] = machPos.X;
                            element.Pts[(element.IndexShowCurrMachPos + 1) * 2 + 1] = machPos.Y;
                        }
                    }

                    var newPt = GetTransedPts(element.Pts, element.Type, imageRect);
                    if (newPt.Count > 0)
                        element.Paint(context, m_lineWidthScale * currentZoomFactor, newPt);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="availableSize"></param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            // 若有图片，返回图片尺寸；若无，返回默认尺寸（避免宽高为0）
            if (originImage != null)
            {
                // 按图片原始尺寸返回（或根据缩放比例计算）
                return new Size(
                    originImage.PixelSize.Width * currentZoomFactor,
                    originImage.PixelSize.Height * currentZoomFactor
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
                if (e.CamID == index || index == -1)
                {
                    if (e.Image == null)
                        continue;

                    // 在 UI 线程更新图像
                    Dispatcher.UIThread.Post(() =>
                    {
                        originImage?.Dispose();
                        originImage = e.Image;
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
            originImage?.Dispose();
            originImage = null;
        }
    }
}
