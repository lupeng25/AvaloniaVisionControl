using System;
using Avalonia;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Avalonia.Platform;
namespace AvaloniaVisionControl
{
    /// <summary>
    /// Avalonia 版图像显示控件
    /// 支持图像显示、鼠标缩放拖拽、图元叠加显示
    /// </summary>
    public partial class CtlOnlyShowImage : Control, IShowPaintElement, IEditablePaintElement
    {
        private Bitmap? _originImage;
        private int[] _needShowCam = Array.Empty<int>();
        private double _currentZoomFactor = 1.0;
        private double _defZoomFactor = 1.0;
        private const double ZoomStep = 0.3;
        private const int CheckerboardGridSize = 10;
        private const int ShowImageCameraNotMatchedCode = -1;
        private const int ShowImageInvalidParameterCode = -2;

        private static readonly IBrush CheckerboardDarkBrush = new SolidColorBrush(Color.FromRgb(28, 28, 28));
        private static readonly IBrush CheckerboardLightBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

        private Point _scrollImageLocation = new Point(0, 0);
        private const double ClickThreshold = 5.0; // 单击判断阈值（像素）

        public static readonly StyledProperty<bool> AllowMouseScrollProperty =
            AvaloniaProperty.Register<CtlOnlyShowImage, bool>(
                nameof(AllowMouseScroll),
                true);

        public static readonly StyledProperty<bool> IsElementEditingEnabledProperty =
            AvaloniaProperty.Register<CtlOnlyShowImage, bool>(
                nameof(IsElementEditingEnabled),
                true);

        public static readonly DirectProperty<CtlOnlyShowImage, int[]> NeedShowCamProperty =
            AvaloniaProperty.RegisterDirect<CtlOnlyShowImage, int[]>(
                nameof(NeedShowCam),
                o => o.NeedShowCam,
                (o, v) => o.NeedShowCam = v,
                Array.Empty<int>());

        static CtlOnlyShowImage()
        {
            CtlShowPaintStatusProperty.Changed.AddClassHandler<CtlOnlyShowImage>((control, _) =>
            {
                control.InvalidateVisual();
            });
            IsElementEditingEnabledProperty.Changed.AddClassHandler<CtlOnlyShowImage>((control, _) =>
            {
                control.InvalidateVisual();
            });
        }

        /// <summary>
        /// 是否允许鼠标滚轮缩放
        /// </summary>
        public bool AllowMouseScroll
        {
            get => GetValue(AllowMouseScrollProperty);
            set => SetValue(AllowMouseScrollProperty, value);
        }

        /// <summary>
        /// 是否启用图元编辑交互。
        /// </summary>
        public bool IsElementEditingEnabled
        {
            get => GetValue(IsElementEditingEnabledProperty);
            set => SetValue(IsElementEditingEnabledProperty, value);
        }

        /// <summary>
        /// 需要显示的相机 ID 列表
        /// </summary>
        public int[] NeedShowCam
        {
            get => _needShowCam.Length == 0 ? Array.Empty<int>() : _needShowCam;
            set
            {
                var normalized = value == null || value.Length == 0
                    ? Array.Empty<int>()
                    : value;
                if (ReferenceEquals(_needShowCam, normalized))
                {
                    return;
                }

                SetAndRaise(NeedShowCamProperty, ref _needShowCam, normalized);
            }
        }

        /// <summary>
        /// 鼠标左键单击事件
        /// 当用户在图像上单击鼠标左键时触发，返回控件坐标与图像像素坐标
        /// </summary>
        public event EventHandler<ImageClickEventArgs>? ImageClick;

        /// <summary>
        /// 鼠标左键按下事件（图像区域内）。
        /// 用于外部 ROI 绘制等交互起点采集。
        /// </summary>
        public event EventHandler<ImageClickEventArgs>? ImageMouseDown;

        /// <summary>
        /// 鼠标左键释放事件。
        /// 用于外部 ROI 绘制等交互终点采集。
        /// </summary>
        public event EventHandler<ImageClickEventArgs>? ImageMouseUp;

        public event EventHandler<PaintElementChangedEventArgs>? ElementChanged;

        public CtlOnlyShowImage(params int[] camIndex)
        {
            NeedShowCam = camIndex ?? Array.Empty<int>();
            
            // 启用鼠标事件
            Focusable = true;
            
            // 订阅双击事件
            DoubleTapped += OnDoubleTapped;
        }

        /// <summary>
        /// 无参数构造函数（用于 XAML）
        /// </summary>
        public CtlOnlyShowImage() : this(0) { }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty)
            {
                Rect oldBounds = change.GetOldValue<Rect>();
                Rect newBounds = change.GetNewValue<Rect>();
                if (!oldBounds.Size.Equals(newBounds.Size))
                {
                    UpdateViewportState(resetToFit: false);
                    InvalidateVisual();
                }
            }
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

            LimitImageWithinBounds();
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            HandlePointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            HandlePointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            HandlePointerReleased(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!IsElementEditingEnabled)
            {
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (_selectedElementIndex >= 0)
                {
                    RemovePaintElementAt(_selectedElementIndex);
                    e.Handled = true;
                }

                return;
            }

            if (e.Key == Key.Escape)
            {
                if (TryCancelCurrentInteraction())
                {
                    e.Handled = true;
                    return;
                }

                if (_selectedElementIndex >= 0)
                {
                    SetSelectedElementIndex(-1);
                    e.Handled = true;
                }
            }
        }

        private void OnDoubleTapped(object? sender, RoutedEventArgs e)
        {
            // New behavior: double-click clears current ROI selection first.
            if (IsElementEditingEnabled && _selectedElementIndex >= 0)
            {
                SetSelectedElementIndex(-1);
                return;
            }

            UpdateViewportState(resetToFit: true);
            InvalidateVisual();
        }

        private void UpdateCursorStyle(Point mousePosition)
        {
            Cursor = GetCursorForPosition(mousePosition);
        }

        private void LimitImageWithinBounds()
        {
            if (_originImage == null) return;

            double imageWidth = _originImage.PixelSize.Width * _currentZoomFactor;
            double imageHeight = _originImage.PixelSize.Height * _currentZoomFactor;

            double minX = imageWidth <= Bounds.Width
                ? 0
                : Bounds.Width - imageWidth;
            double maxX = 0;
            double minY = imageHeight <= Bounds.Height
                ? 0
                : Bounds.Height - imageHeight;
            double maxY = 0;

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
                for (int i = 0; i < m_CurrShowElement.Count; i++)
                {
                    if (!ShouldRenderElement(i))
                    {
                        continue;
                    }

                    var element = m_CurrShowElement[i];
                    var newPt = GetTransedPts(element.Pts);
                    if (newPt.Count > 0)
                    {
                        element.Paint(context, m_lineWidthScale * _currentZoomFactor, newPt);
                        if (i == _selectedElementIndex)
                        {
                            DrawSelectedElementAdornment(context, element, newPt);
                        }
                    }
                }

                DrawElementEditHandles(context);
            }
        }

        /// <summary>
        /// 为无内容场景提供默认尺寸，避免布局阶段宽高为 0。
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            // 若有图片，返回图片尺寸；若无，返回默认尺寸（避免宽高为0）
            if (_originImage != null)
            {
                // 测量阶段不绑定当前缩放，避免缩放行为干扰布局。
                return new Size(
                    _originImage.PixelSize.Width,
                    _originImage.PixelSize.Height
                );
            }
            // 无图片时返回默认尺寸（避免布局异常）
            return new Size(800, 450); // 可根据需求调整默认值
        }
        private void DrawCheckerboardBackground(DrawingContext context, Rect area)
        {
            int maxY = (int)Math.Ceiling(area.Height);
            int maxX = (int)Math.Ceiling(area.Width);
            for (int y = 0; y < maxY; y += CheckerboardGridSize)
            {
                for (int x = 0; x < maxX; x += CheckerboardGridSize)
                {
                    var brush = ((x / CheckerboardGridSize + y / CheckerboardGridSize) % 2 == 0)
                        ? CheckerboardDarkBrush
                        : CheckerboardLightBrush;
                    context.FillRectangle(brush, new Rect(x, y, CheckerboardGridSize, CheckerboardGridSize));
                }
            }
        }

        /// <summary>
        /// 显示图像并将 <paramref name="e"/> 中的位图所有权转移给控件。
        /// 调用返回 0 后，调用方不应再访问或释放该位图。
        /// </summary>
        /// <returns>
        /// 0：成功；-1：相机 ID 不匹配；-2：参数无效。
        /// </returns>
        public int ShowImage(ReceiveBitmapEventArgs e)
        {
            if (e == null || e.Image == null)
            {
                return ShowImageInvalidParameterCode;
            }

            return ShowImageCore(e.CamID, e.Image, validateCamera: true);
        }

        /// <summary>
        /// 安全路径：从图像流创建位图并交由控件接管生命周期。
        /// </summary>
        /// <returns>
        /// 0：成功；-1：相机 ID 不匹配；-2：参数或图像数据无效。
        /// </returns>
        public int ShowImageFromStream(int camId, Stream imageStream)
        {
            if (imageStream == null || !imageStream.CanRead)
            {
                return ShowImageInvalidParameterCode;
            }

            if (!TryAcceptCamera(camId))
            {
                return ShowImageCameraNotMatchedCode;
            }

            Bitmap? bitmap = null;
            try
            {
                bitmap = new Bitmap(imageStream);
                int result = ShowImageCore(camId, bitmap, validateCamera: false);
                if (result != SuccessCode)
                {
                    bitmap.Dispose();
                }

                return result;
            }
            catch
            {
                bitmap?.Dispose();
                return ShowImageInvalidParameterCode;
            }
        }

        /// <summary>
        /// 安全路径：复制源位图后显示，调用方继续拥有并负责源位图生命周期。
        /// </summary>
        /// <returns>
        /// 0：成功；-1：相机 ID 不匹配；-2：参数或图像数据无效。
        /// </returns>
        public int ShowImageCopy(int camId, Bitmap sourceImage)
        {
            if (sourceImage == null)
            {
                return ShowImageInvalidParameterCode;
            }

            if (!TryAcceptCamera(camId))
            {
                return ShowImageCameraNotMatchedCode;
            }

            WriteableBitmap? copy = null;
            try
            {
                copy = new WriteableBitmap(sourceImage.PixelSize, sourceImage.Dpi);
                using (var framebuffer = copy.Lock())
                {
                    sourceImage.CopyPixels(framebuffer, AlphaFormat.Unpremul);
                }

                int result = ShowImageCore(camId, copy, validateCamera: false);
                if (result != SuccessCode)
                {
                    copy.Dispose();
                }

                return result;
            }
            catch
            {
                copy?.Dispose();
                return ShowImageInvalidParameterCode;
            }
        }

        /// <summary>
        /// 获取需要显示的相机 ID 列表
        /// </summary>
        public int[] GetShowCam()
        {
            return (int[])NeedShowCam.Clone();
        }

        private bool TryAcceptCamera(int camId)
        {
            if (NeedShowCam.Length == 0)
            {
                return false;
            }

            foreach (int index in NeedShowCam)
            {
                if (camId == index)
                {
                    return true;
                }
            }

            return false;
        }

        private int ShowImageCore(int camId, Bitmap image, bool validateCamera)
        {
            if (image == null)
            {
                return ShowImageInvalidParameterCode;
            }

            if (validateCamera && !TryAcceptCamera(camId))
            {
                return ShowImageCameraNotMatchedCode;
            }

            SetImageOnUiThread(image);
            return SuccessCode;
        }

        private void SetImageOnUiThread(Bitmap image)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                ReplaceOriginImage(image);
                return;
            }

            Dispatcher.UIThread.InvokeAsync(() => ReplaceOriginImage(image)).GetAwaiter().GetResult();
        }

        private void ReplaceOriginImage(Bitmap newImage)
        {
            PixelSize? previousImageSize = _originImage?.PixelSize;

            if (!ReferenceEquals(_originImage, newImage))
            {
                _originImage?.Dispose();
                _originImage = newImage;
            }

            bool isImageSizeChanged = !previousImageSize.HasValue ||
                previousImageSize.Value != _originImage.PixelSize;

            UpdateViewportState(resetToFit: isImageSizeChanged);
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void UpdateViewportState(bool resetToFit)
        {
            if (_originImage == null)
            {
                _defZoomFactor = 1.0;
                _currentZoomFactor = 1.0;
                _scrollImageLocation = new Point(0, 0);
                return;
            }

            double nextDefaultZoom = CalculateDefaultZoomFactor();
            bool wasAtDefaultZoom = IsAtDefaultZoom();

            _defZoomFactor = nextDefaultZoom;

            if (resetToFit || wasAtDefaultZoom)
            {
                _currentZoomFactor = _defZoomFactor;
                _scrollImageLocation = new Point(0, 0);
            }
            else if (_currentZoomFactor < _defZoomFactor)
            {
                _currentZoomFactor = _defZoomFactor;
            }

            LimitImageWithinBounds();
        }

        private double CalculateDefaultZoomFactor()
        {
            if (_originImage == null)
            {
                return 1.0;
            }

            if (Bounds.Width <= double.Epsilon ||
                Bounds.Height <= double.Epsilon ||
                _originImage.PixelSize.Width <= 0 ||
                _originImage.PixelSize.Height <= 0)
            {
                return _defZoomFactor > 0 ? _defZoomFactor : 1.0;
            }

            double fitZoom = Math.Min(
                Bounds.Width / _originImage.PixelSize.Width,
                Bounds.Height / _originImage.PixelSize.Height);

            if (double.IsNaN(fitZoom) || double.IsInfinity(fitZoom) || fitZoom <= double.Epsilon)
            {
                return 1.0;
            }

            return fitZoom;
        }

        private bool IsAtDefaultZoom()
        {
            return Math.Abs(_currentZoomFactor - _defZoomFactor) <= 1e-6;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            
            // 取消订阅事件
            DoubleTapped -= OnDoubleTapped;
            
            // 释放资源
            _originImage?.Dispose();
            ResetInteractionState();
            _originImage = null;
        }
    }
}

