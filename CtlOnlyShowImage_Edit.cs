using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AvaloniaVisionControl
{
    public partial class CtlOnlyShowImage
    {
        private enum EditInteractionMode
        {
            None,
            MovingElement,
            ResizingElement,
            PanningImage
        }

        private enum ElementHandleType
        {
            None,
            RectTopLeft,
            RectTop,
            RectTopRight,
            RectRight,
            RectBottomRight,
            RectBottom,
            RectBottomLeft,
            RectLeft,
            CircleCenter,
            CircleRadiusPoint
        }

        private readonly struct HandleHitResult
        {
            public HandleHitResult(int elementIndex, ElementHandleType handleType)
            {
                ElementIndex = elementIndex;
                HandleType = handleType;
            }

            public int ElementIndex { get; }

            public ElementHandleType HandleType { get; }
        }

        private readonly struct HandlePoint
        {
            public HandlePoint(ElementHandleType handleType, Point position)
            {
                HandleType = handleType;
                Position = position;
            }

            public ElementHandleType HandleType { get; }

            public Point Position { get; }
        }

        private EditInteractionMode _interactionMode = EditInteractionMode.None;
        private int _activeElementIndex = -1;
        private ElementHandleType _activeHandleType = ElementHandleType.None;
        private Point _interactionPressPoint;
        private Point _interactionLastPoint;
        private bool _interactionMoved;
        private bool _suppressImageClickForCurrentPress;

        private const double HandleDrawSize = 8.0;
        private const double HandleHitSize = 12.0;
        private const double ElementHitTolerance = 4.0;
        private const double MinRectSizePixels = 8.0;
        private const double MinCircleRadiusPixels = 4.0;

        private static readonly IBrush HandleFillBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
        private static readonly IPen HandleBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)), 1);

        private void HandlePointerPressed(PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed || _originImage == null)
            {
                return;
            }

            var mousePos = e.GetPosition(this);
            var imageRect = GetImageRectangle();
            if (!imageRect.Contains(mousePos))
            {
                return;
            }

            Focus();
            _interactionPressPoint = mousePos;
            _interactionLastPoint = mousePos;
            _interactionMoved = false;
            _suppressImageClickForCurrentPress = false;

            if (TryHitTestHandle(mousePos, out var handleHit))
            {
                _interactionMode = EditInteractionMode.ResizingElement;
                _activeElementIndex = handleHit.ElementIndex;
                _activeHandleType = handleHit.HandleType;
                _suppressImageClickForCurrentPress = true;
                CtlMouseStatus = ImageCtlMouseStatus.Dragging;
                Cursor = GetCursorForHandle(handleHit.HandleType);
                e.Pointer.Capture(this);
                return;
            }

            if (TryHitTestElementBody(mousePos, out int bodyIndex))
            {
                _interactionMode = EditInteractionMode.MovingElement;
                _activeElementIndex = bodyIndex;
                _activeHandleType = ElementHandleType.None;
                _suppressImageClickForCurrentPress = true;
                CtlMouseStatus = ImageCtlMouseStatus.Dragging;
                Cursor = new Cursor(StandardCursorType.SizeAll);
                e.Pointer.Capture(this);
                return;
            }

            _interactionMode = EditInteractionMode.PanningImage;
            _activeElementIndex = -1;
            _activeHandleType = ElementHandleType.None;
            CtlMouseStatus = ImageCtlMouseStatus.Dragging;
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(this);
        }

        private void HandlePointerMoved(PointerEventArgs e)
        {
            var mousePos = e.GetPosition(this);

            if (_interactionMode == EditInteractionMode.None)
            {
                UpdateCursorStyle(mousePos);
                return;
            }

            if (!_interactionMoved)
            {
                double totalDx = mousePos.X - _interactionPressPoint.X;
                double totalDy = mousePos.Y - _interactionPressPoint.Y;
                double totalDistance = Math.Sqrt(totalDx * totalDx + totalDy * totalDy);
                _interactionMoved = totalDistance >= ClickThreshold;
            }

            double deltaX = mousePos.X - _interactionLastPoint.X;
            double deltaY = mousePos.Y - _interactionLastPoint.Y;
            bool changed = false;

            switch (_interactionMode)
            {
                case EditInteractionMode.PanningImage:
                    if (Math.Abs(deltaX) > double.Epsilon || Math.Abs(deltaY) > double.Epsilon)
                    {
                        _scrollImageLocation = new Point(
                            _scrollImageLocation.X + deltaX,
                            _scrollImageLocation.Y + deltaY);
                        LimitImageWithinBounds();
                        changed = true;
                    }
                    Cursor = new Cursor(StandardCursorType.Hand);
                    break;

                case EditInteractionMode.MovingElement:
                    changed = TryMoveActiveElement(deltaX, deltaY);
                    Cursor = new Cursor(StandardCursorType.SizeAll);
                    break;

                case EditInteractionMode.ResizingElement:
                    changed = TryResizeActiveElement(mousePos, deltaX, deltaY);
                    Cursor = GetCursorForHandle(_activeHandleType);
                    break;
            }

            _interactionLastPoint = mousePos;
            if (changed)
            {
                InvalidateVisual();
            }
        }

        private void HandlePointerReleased(PointerReleasedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
            {
                return;
            }

            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }

            var mousePos = e.GetPosition(this);
            bool shouldRaiseImageClick =
                _interactionMode == EditInteractionMode.PanningImage &&
                !_suppressImageClickForCurrentPress &&
                !_interactionMoved;

            if (shouldRaiseImageClick && _originImage != null && GetImageRectangle().Contains(mousePos))
            {
                var imagePos = ClampPointToImage(ControlToImagePoint(mousePos));
                ImageClick?.Invoke(this, new ImageClickEventArgs(mousePos, imagePos));
            }

            ResetInteractionState();
            UpdateCursorStyle(mousePos);
        }

        private Cursor GetCursorForPosition(Point mousePosition)
        {
            if (_originImage == null)
            {
                return Cursor.Default;
            }

            if (_interactionMode == EditInteractionMode.ResizingElement)
            {
                return GetCursorForHandle(_activeHandleType);
            }

            if (_interactionMode == EditInteractionMode.MovingElement)
            {
                return new Cursor(StandardCursorType.SizeAll);
            }

            if (_interactionMode == EditInteractionMode.PanningImage)
            {
                return new Cursor(StandardCursorType.Hand);
            }

            if (TryHitTestHandle(mousePosition, out var handleHit))
            {
                return GetCursorForHandle(handleHit.HandleType);
            }

            if (TryHitTestElementBody(mousePosition, out _))
            {
                return new Cursor(StandardCursorType.SizeAll);
            }

            return GetImageRectangle().Contains(mousePosition)
                ? new Cursor(StandardCursorType.Hand)
                : Cursor.Default;
        }

        private Cursor GetCursorForHandle(ElementHandleType handleType)
        {
            return handleType switch
            {
                ElementHandleType.RectTopLeft => new Cursor(StandardCursorType.TopLeftCorner),
                ElementHandleType.RectBottomRight => new Cursor(StandardCursorType.TopLeftCorner),
                ElementHandleType.RectTopRight => new Cursor(StandardCursorType.TopRightCorner),
                ElementHandleType.RectBottomLeft => new Cursor(StandardCursorType.TopRightCorner),
                ElementHandleType.RectTop => new Cursor(StandardCursorType.TopSide),
                ElementHandleType.RectBottom => new Cursor(StandardCursorType.BottomSide),
                ElementHandleType.RectLeft => new Cursor(StandardCursorType.LeftSide),
                ElementHandleType.RectRight => new Cursor(StandardCursorType.RightSide),
                ElementHandleType.CircleCenter => new Cursor(StandardCursorType.SizeAll),
                ElementHandleType.CircleRadiusPoint => new Cursor(StandardCursorType.Hand),
                _ => Cursor.Default
            };
        }

        private bool TryHitTestHandle(Point mousePosition, out HandleHitResult hitResult)
        {
            for (int i = m_CurrShowElement.Count - 1; i >= 0; i--)
            {
                var element = m_CurrShowElement[i];
                if (!IsEditableElement(element))
                {
                    continue;
                }

                var handles = GetHandlePoints(element);
                foreach (var handle in handles)
                {
                    if (GetHandleRect(handle.Position, HandleHitSize).Contains(mousePosition))
                    {
                        hitResult = new HandleHitResult(i, handle.HandleType);
                        return true;
                    }
                }
            }

            hitResult = default;
            return false;
        }

        private bool TryHitTestElementBody(Point mousePosition, out int elementIndex)
        {
            for (int i = m_CurrShowElement.Count - 1; i >= 0; i--)
            {
                var element = m_CurrShowElement[i];
                if (!IsEditableElement(element))
                {
                    continue;
                }

                if (element.Type == PaintElementType.Rectangle &&
                    TryGetRectangleControlRect(element, out var rect))
                {
                    var hitRect = InflateRect(rect, ElementHitTolerance);
                    if (hitRect.Contains(mousePosition))
                    {
                        elementIndex = i;
                        return true;
                    }
                }
                else if (element.Type == PaintElementType.Circle &&
                         TryGetCircleControlGeometry(element, out var center, out var edge))
                {
                    double radius = Distance(center, edge);
                    double dx = mousePosition.X - center.X;
                    double dy = mousePosition.Y - center.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance <= radius + ElementHitTolerance)
                    {
                        elementIndex = i;
                        return true;
                    }
                }
            }

            elementIndex = -1;
            return false;
        }

        private bool TryMoveActiveElement(double deltaControlX, double deltaControlY)
        {
            if (_activeElementIndex < 0 || _activeElementIndex >= m_CurrShowElement.Count)
            {
                return false;
            }

            if (_originImage == null || _currentZoomFactor <= 0)
            {
                return false;
            }

            double deltaImageX = deltaControlX / _currentZoomFactor;
            double deltaImageY = deltaControlY / _currentZoomFactor;
            if (Math.Abs(deltaImageX) < double.Epsilon && Math.Abs(deltaImageY) < double.Epsilon)
            {
                return false;
            }

            if (!TryGetPixelToMachineMatrix(out var pixelToMachineMatrix))
            {
                return false;
            }

            var element = m_CurrShowElement[_activeElementIndex];
            return element.Type switch
            {
                PaintElementType.Rectangle => TryMoveRectangleElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Circle => TryMoveCircleElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                _ => false
            };
        }

        private bool TryResizeActiveElement(Point mousePosition, double deltaControlX, double deltaControlY)
        {
            if (_activeElementIndex < 0 || _activeElementIndex >= m_CurrShowElement.Count)
            {
                return false;
            }

            var element = m_CurrShowElement[_activeElementIndex];
            if (!TryGetPixelToMachineMatrix(out var pixelToMachineMatrix))
            {
                return false;
            }

            if (_activeHandleType == ElementHandleType.CircleCenter)
            {
                return TryMoveActiveElement(deltaControlX, deltaControlY);
            }

            if (_activeHandleType == ElementHandleType.CircleRadiusPoint &&
                element.Type == PaintElementType.Circle)
            {
                return TryResizeCircleRadius(element, mousePosition, pixelToMachineMatrix);
            }

            if (element.Type == PaintElementType.Rectangle && IsRectangleHandle(_activeHandleType))
            {
                if (_currentZoomFactor <= 0)
                {
                    return false;
                }

                double deltaImageX = deltaControlX / _currentZoomFactor;
                double deltaImageY = deltaControlY / _currentZoomFactor;
                return TryResizeRectangleElement(
                    element,
                    _activeHandleType,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix);
            }

            return false;
        }

        private bool TryMoveRectangleElement(
            PaintElement element,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (!TryGetRectangleImageGeometry(element, out var p1Image, out var p2Image, out var normalizedRect))
            {
                return false;
            }

            double imageWidth = _originImage.PixelSize.Width;
            double imageHeight = _originImage.PixelSize.Height;

            double moveX = Math.Clamp(deltaImageX, -normalizedRect.Left, imageWidth - normalizedRect.Right);
            double moveY = Math.Clamp(deltaImageY, -normalizedRect.Top, imageHeight - normalizedRect.Bottom);
            if (Math.Abs(moveX) < double.Epsilon && Math.Abs(moveY) < double.Epsilon)
            {
                return false;
            }

            var p1New = new Point(p1Image.X + moveX, p1Image.Y + moveY);
            var p2New = new Point(p2Image.X + moveX, p2Image.Y + moveY);
            return TrySetRectangleFromImagePoints(element, p1New, p2New, pixelToMachineMatrix);
        }

        private bool TryMoveCircleElement(
            PaintElement element,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (!TryGetCircleImageGeometry(element, out var centerImage, out var edgeImage, out var radius))
            {
                return false;
            }

            double imageWidth = _originImage.PixelSize.Width;
            double imageHeight = _originImage.PixelSize.Height;

            double left = centerImage.X - radius;
            double right = centerImage.X + radius;
            double top = centerImage.Y - radius;
            double bottom = centerImage.Y + radius;

            double moveX = Math.Clamp(deltaImageX, -left, imageWidth - right);
            double moveY = Math.Clamp(deltaImageY, -top, imageHeight - bottom);
            if (Math.Abs(moveX) < double.Epsilon && Math.Abs(moveY) < double.Epsilon)
            {
                return false;
            }

            var centerNew = new Point(centerImage.X + moveX, centerImage.Y + moveY);
            var edgeNew = new Point(edgeImage.X + moveX, edgeImage.Y + moveY);
            return TrySetCircleFromImagePoints(element, centerNew, edgeNew, pixelToMachineMatrix);
        }

        private bool TryResizeRectangleElement(
            PaintElement element,
            ElementHandleType handleType,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (!TryGetRectangleImageGeometry(element, out _, out _, out var normalizedRect))
            {
                return false;
            }

            double left = normalizedRect.Left;
            double right = normalizedRect.Right;
            double top = normalizedRect.Top;
            double bottom = normalizedRect.Bottom;

            bool affectLeft = handleType is ElementHandleType.RectTopLeft or ElementHandleType.RectLeft or ElementHandleType.RectBottomLeft;
            bool affectRight = handleType is ElementHandleType.RectTopRight or ElementHandleType.RectRight or ElementHandleType.RectBottomRight;
            bool affectTop = handleType is ElementHandleType.RectTopLeft or ElementHandleType.RectTop or ElementHandleType.RectTopRight;
            bool affectBottom = handleType is ElementHandleType.RectBottomLeft or ElementHandleType.RectBottom or ElementHandleType.RectBottomRight;

            if (affectLeft)
            {
                left += deltaImageX;
            }

            if (affectRight)
            {
                right += deltaImageX;
            }

            if (affectTop)
            {
                top += deltaImageY;
            }

            if (affectBottom)
            {
                bottom += deltaImageY;
            }

            double imageWidth = _originImage.PixelSize.Width;
            double imageHeight = _originImage.PixelSize.Height;

            if (affectLeft)
            {
                left = Math.Max(0, Math.Min(left, right - MinRectSizePixels));
            }

            if (affectRight)
            {
                right = Math.Min(imageWidth, Math.Max(right, left + MinRectSizePixels));
            }

            if (affectTop)
            {
                top = Math.Max(0, Math.Min(top, bottom - MinRectSizePixels));
            }

            if (affectBottom)
            {
                bottom = Math.Min(imageHeight, Math.Max(bottom, top + MinRectSizePixels));
            }

            left = Math.Max(0, left);
            right = Math.Min(imageWidth, right);
            top = Math.Max(0, top);
            bottom = Math.Min(imageHeight, bottom);

            if (right - left < MinRectSizePixels)
            {
                if (affectLeft)
                {
                    left = right - MinRectSizePixels;
                }
                else
                {
                    right = left + MinRectSizePixels;
                }
            }

            if (bottom - top < MinRectSizePixels)
            {
                if (affectTop)
                {
                    top = bottom - MinRectSizePixels;
                }
                else
                {
                    bottom = top + MinRectSizePixels;
                }
            }

            if (left < 0)
            {
                left = 0;
            }

            if (top < 0)
            {
                top = 0;
            }

            if (right > imageWidth)
            {
                right = imageWidth;
            }

            if (bottom > imageHeight)
            {
                bottom = imageHeight;
            }

            if (Math.Abs(left - normalizedRect.Left) < double.Epsilon &&
                Math.Abs(top - normalizedRect.Top) < double.Epsilon &&
                Math.Abs(right - normalizedRect.Right) < double.Epsilon &&
                Math.Abs(bottom - normalizedRect.Bottom) < double.Epsilon)
            {
                return false;
            }

            var p1New = new Point(left, top);
            var p2New = new Point(right, bottom);
            return TrySetRectangleFromImagePoints(element, p1New, p2New, pixelToMachineMatrix);
        }

        private bool TryResizeCircleRadius(
            PaintElement element,
            Point mousePosition,
            double[] pixelToMachineMatrix)
        {
            if (!TryGetCircleImageGeometry(element, out var centerImage, out var edgeImage, out var oldRadius))
            {
                return false;
            }

            var targetImage = ClampPointToImage(ControlToImagePoint(mousePosition));
            double dx = targetImage.X - centerImage.X;
            double dy = targetImage.Y - centerImage.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            double dirX;
            double dirY;
            if (distance > 1e-6)
            {
                dirX = dx / distance;
                dirY = dy / distance;
            }
            else
            {
                double oldDx = edgeImage.X - centerImage.X;
                double oldDy = edgeImage.Y - centerImage.Y;
                double oldDistance = Math.Sqrt(oldDx * oldDx + oldDy * oldDy);
                if (oldDistance > 1e-6)
                {
                    dirX = oldDx / oldDistance;
                    dirY = oldDy / oldDistance;
                }
                else
                {
                    dirX = 1.0;
                    dirY = 0.0;
                }
            }

            double imageWidth = _originImage.PixelSize.Width;
            double imageHeight = _originImage.PixelSize.Height;
            double maxRadius = Math.Min(
                Math.Min(centerImage.X, imageWidth - centerImage.X),
                Math.Min(centerImage.Y, imageHeight - centerImage.Y));
            if (maxRadius <= 0)
            {
                return false;
            }

            double minRadius = Math.Min(MinCircleRadiusPixels, maxRadius);
            double newRadius = Math.Clamp(distance, minRadius, maxRadius);
            var newEdgeImage = new Point(
                centerImage.X + dirX * newRadius,
                centerImage.Y + dirY * newRadius);

            if (Math.Abs(newRadius - oldRadius) < double.Epsilon &&
                Distance(newEdgeImage, edgeImage) < double.Epsilon)
            {
                return false;
            }

            return TrySetCircleFromImagePoints(element, centerImage, newEdgeImage, pixelToMachineMatrix);
        }

        private bool TrySetRectangleFromImagePoints(
            PaintElement element,
            Point p1Image,
            Point p2Image,
            double[] pixelToMachineMatrix)
        {
            var p1Machine = ImageToMachinePoint(p1Image, pixelToMachineMatrix);
            var p2Machine = ImageToMachinePoint(p2Image, pixelToMachineMatrix);

            EnsureElementPointCount(element, 4);
            element.Pts[0] = p1Machine.X;
            element.Pts[1] = p1Machine.Y;
            element.Pts[2] = p2Machine.X;
            element.Pts[3] = p2Machine.Y;
            return true;
        }

        private bool TrySetCircleFromImagePoints(
            PaintElement element,
            Point centerImage,
            Point edgeImage,
            double[] pixelToMachineMatrix)
        {
            var centerMachine = ImageToMachinePoint(centerImage, pixelToMachineMatrix);
            var edgeMachine = ImageToMachinePoint(edgeImage, pixelToMachineMatrix);

            EnsureElementPointCount(element, 4);
            element.Pts[0] = centerMachine.X;
            element.Pts[1] = centerMachine.Y;
            element.Pts[2] = edgeMachine.X;
            element.Pts[3] = edgeMachine.Y;
            return true;
        }

        private static void EnsureElementPointCount(PaintElement element, int minCount)
        {
            while (element.Pts.Count < minCount)
            {
                element.Pts.Add(0);
            }
        }

        private bool TryGetRectangleControlRect(PaintElement element, out Rect rect)
        {
            rect = default;
            if (element.Type != PaintElementType.Rectangle || element.Pts.Count < 4)
            {
                return false;
            }

            var p1 = MachineToControlPoint(new Point(element.Pts[0], element.Pts[1]));
            var p2 = MachineToControlPoint(new Point(element.Pts[2], element.Pts[3]));
            rect = NormalizeRect(p1, p2);
            return true;
        }

        private bool TryGetRectangleImageGeometry(
            PaintElement element,
            out Point p1Image,
            out Point p2Image,
            out Rect normalizedRect)
        {
            p1Image = default;
            p2Image = default;
            normalizedRect = default;
            if (element.Type != PaintElementType.Rectangle || element.Pts.Count < 4)
            {
                return false;
            }

            p1Image = MachineToImagePoint(new Point(element.Pts[0], element.Pts[1]));
            p2Image = MachineToImagePoint(new Point(element.Pts[2], element.Pts[3]));
            normalizedRect = NormalizeRect(p1Image, p2Image);
            return true;
        }

        private bool TryGetCircleControlGeometry(PaintElement element, out Point center, out Point edge)
        {
            center = default;
            edge = default;
            if (element.Type != PaintElementType.Circle || element.Pts.Count < 4)
            {
                return false;
            }

            center = MachineToControlPoint(new Point(element.Pts[0], element.Pts[1]));
            edge = MachineToControlPoint(new Point(element.Pts[2], element.Pts[3]));
            return true;
        }

        private bool TryGetCircleImageGeometry(
            PaintElement element,
            out Point centerImage,
            out Point edgeImage,
            out double radius)
        {
            centerImage = default;
            edgeImage = default;
            radius = 0;
            if (element.Type != PaintElementType.Circle || element.Pts.Count < 4)
            {
                return false;
            }

            centerImage = MachineToImagePoint(new Point(element.Pts[0], element.Pts[1]));
            edgeImage = MachineToImagePoint(new Point(element.Pts[2], element.Pts[3]));
            radius = Distance(centerImage, edgeImage);
            return true;
        }

        private List<HandlePoint> GetHandlePoints(PaintElement element)
        {
            var handlePoints = new List<HandlePoint>();
            if (element.Type == PaintElementType.Rectangle &&
                TryGetRectangleControlRect(element, out var rect))
            {
                double left = rect.Left;
                double right = rect.Right;
                double top = rect.Top;
                double bottom = rect.Bottom;
                double midX = (left + right) / 2.0;
                double midY = (top + bottom) / 2.0;

                handlePoints.Add(new HandlePoint(ElementHandleType.RectTopLeft, new Point(left, top)));
                handlePoints.Add(new HandlePoint(ElementHandleType.RectTop, new Point(midX, top)));
                handlePoints.Add(new HandlePoint(ElementHandleType.RectTopRight, new Point(right, top)));
                handlePoints.Add(new HandlePoint(ElementHandleType.RectRight, new Point(right, midY)));
                handlePoints.Add(new HandlePoint(ElementHandleType.RectBottomRight, new Point(right, bottom)));
                handlePoints.Add(new HandlePoint(ElementHandleType.RectBottom, new Point(midX, bottom)));
                handlePoints.Add(new HandlePoint(ElementHandleType.RectBottomLeft, new Point(left, bottom)));
                handlePoints.Add(new HandlePoint(ElementHandleType.RectLeft, new Point(left, midY)));
            }
            else if (element.Type == PaintElementType.Circle &&
                     TryGetCircleControlGeometry(element, out var center, out var edge))
            {
                handlePoints.Add(new HandlePoint(ElementHandleType.CircleCenter, center));
                handlePoints.Add(new HandlePoint(ElementHandleType.CircleRadiusPoint, edge));
            }

            return handlePoints;
        }

        private static bool IsRectangleHandle(ElementHandleType handleType)
        {
            return handleType is
                ElementHandleType.RectTopLeft or
                ElementHandleType.RectTop or
                ElementHandleType.RectTopRight or
                ElementHandleType.RectRight or
                ElementHandleType.RectBottomRight or
                ElementHandleType.RectBottom or
                ElementHandleType.RectBottomLeft or
                ElementHandleType.RectLeft;
        }

        private static bool IsEditableElement(PaintElement element)
        {
            return element.Visible &&
                   element.Pts.Count >= 4 &&
                   (element.Type == PaintElementType.Rectangle || element.Type == PaintElementType.Circle);
        }

        private bool TryGetPixelToMachineMatrix(out double[] pixelToMachineMatrix)
        {
            try
            {
                pixelToMachineMatrix = CalculateInverseTransform(m_9MMToPixMatrix);
                return true;
            }
            catch
            {
                pixelToMachineMatrix = Array.Empty<double>();
                return false;
            }
        }

        private Point ControlToImagePoint(Point controlPoint)
        {
            return new Point(
                (controlPoint.X - _scrollImageLocation.X) / _currentZoomFactor,
                (controlPoint.Y - _scrollImageLocation.Y) / _currentZoomFactor);
        }

        private Point ImageToControlPoint(Point imagePoint)
        {
            return new Point(
                imagePoint.X * _currentZoomFactor + _scrollImageLocation.X,
                imagePoint.Y * _currentZoomFactor + _scrollImageLocation.Y);
        }

        private Point MachineToImagePoint(Point machinePoint)
        {
            var machPos = MotionMgr.Ins.CurrMachPos;
            var relativePoint = new Point(machinePoint.X - machPos.X, machinePoint.Y - machPos.Y);
            return TransformPoint(relativePoint, m_9MMToPixMatrix);
        }

        private Point MachineToControlPoint(Point machinePoint)
        {
            return ImageToControlPoint(MachineToImagePoint(machinePoint));
        }

        private Point ImageToMachinePoint(Point imagePoint, double[] pixelToMachineMatrix)
        {
            var relativeMachinePoint = TransformPoint(imagePoint, pixelToMachineMatrix);
            var machPos = MotionMgr.Ins.CurrMachPos;
            return new Point(relativeMachinePoint.X + machPos.X, relativeMachinePoint.Y + machPos.Y);
        }

        private Point ClampPointToImage(Point imagePoint)
        {
            if (_originImage == null)
            {
                return imagePoint;
            }

            double maxX = _originImage.PixelSize.Width;
            double maxY = _originImage.PixelSize.Height;
            return new Point(
                Math.Clamp(imagePoint.X, 0, maxX),
                Math.Clamp(imagePoint.Y, 0, maxY));
        }

        private static Rect NormalizeRect(Point p1, Point p2)
        {
            return new Rect(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p2.X - p1.X),
                Math.Abs(p2.Y - p1.Y));
        }

        private static Rect InflateRect(Rect rect, double delta)
        {
            return new Rect(
                rect.X - delta,
                rect.Y - delta,
                rect.Width + delta * 2,
                rect.Height + delta * 2);
        }

        private static Rect GetHandleRect(Point center, double size)
        {
            double half = size / 2.0;
            return new Rect(center.X - half, center.Y - half, size, size);
        }

        private static double Distance(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private void DrawElementEditHandles(DrawingContext context)
        {
            if (_originImage == null || CtlShowPaintStatus <= 0 || m_CurrShowElement.Count == 0)
            {
                return;
            }

            foreach (var element in m_CurrShowElement)
            {
                if (!IsEditableElement(element))
                {
                    continue;
                }

                var handles = GetHandlePoints(element);
                foreach (var handle in handles)
                {
                    context.DrawRectangle(
                        HandleFillBrush,
                        HandleBorderPen,
                        GetHandleRect(handle.Position, HandleDrawSize));
                }
            }
        }

        private void ResetInteractionState()
        {
            _interactionMode = EditInteractionMode.None;
            _activeElementIndex = -1;
            _activeHandleType = ElementHandleType.None;
            _interactionMoved = false;
            _suppressImageClickForCurrentPress = false;
            CtlMouseStatus = ImageCtlMouseStatus.Normal;
        }
    }
}
