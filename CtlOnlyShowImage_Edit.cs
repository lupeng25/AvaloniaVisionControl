using System;
using System.Collections.Generic;
using System.Globalization;
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
            CircleRadiusPoint,
            LineStart,
            LineEnd,
            PolygonVertex
        }

        private readonly struct HandleHitResult
        {
            public HandleHitResult(int elementIndex, ElementHandleType handleType, int vertexIndex = -1)
            {
                ElementIndex = elementIndex;
                HandleType = handleType;
                VertexIndex = vertexIndex;
            }

            public int ElementIndex { get; }

            public ElementHandleType HandleType { get; }

            public int VertexIndex { get; }
        }

        private readonly struct HandlePoint
        {
            public HandlePoint(ElementHandleType handleType, Point position, int vertexIndex = -1)
            {
                HandleType = handleType;
                Position = position;
                VertexIndex = vertexIndex;
            }

            public ElementHandleType HandleType { get; }

            public Point Position { get; }

            public int VertexIndex { get; }
        }

        private EditInteractionMode _interactionMode = EditInteractionMode.None;
        private int _activeElementIndex = -1;
        private ElementHandleType _activeHandleType = ElementHandleType.None;
        private int _activeHandleVertexIndex = -1;
        private Point _interactionPressPoint;
        private Point _interactionLastPoint;
        private bool _interactionMoved;
        private PaintElement? _interactionInitialElementSnapshot;
        private PaintElement? _interactionLastPreviewElementSnapshot;
        private bool _interactionElementChanged;

        private const double HandleDrawSize = 8.0;
        private const double HandleHitSize = 12.0;
        private const double ElementHitTolerance = 4.0;
        private const double MinRectSizePixels = 8.0;
        private const double MinCircleRadiusPixels = 4.0;
        private const double MinPointHitTolerance = 6.0;

        private static readonly IBrush HandleFillBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
        private static readonly IPen HandleBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)), 1);
        private static readonly IPen SelectedAdornmentPen = new Pen(
            new SolidColorBrush(Color.FromArgb(255, 0, 255, 255)),
            2);

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

            var pressImagePos = ClampPointToImage(ControlToImagePoint(mousePos));
            ImageMouseDown?.Invoke(this, new ImageClickEventArgs(mousePos, pressImagePos));

            Focus();
            _interactionPressPoint = mousePos;
            _interactionLastPoint = mousePos;
            _interactionMoved = false;
            _interactionElementChanged = false;
            _interactionInitialElementSnapshot = null;
            _interactionLastPreviewElementSnapshot = null;

            if (IsElementEditingEnabled && TryHitTestHandle(mousePos, out var handleHit))
            {
                TrySetSelectedElementIndexInternal(handleHit.ElementIndex, PaintElementChangeSource.Interaction);

                _interactionMode = EditInteractionMode.ResizingElement;
                _activeElementIndex = handleHit.ElementIndex;
                _activeHandleType = handleHit.HandleType;
                _activeHandleVertexIndex = handleHit.VertexIndex;
                CtlMouseStatus = ImageCtlMouseStatus.Dragging;
                Cursor = GetCursorForHandle(handleHit.HandleType);
                _interactionInitialElementSnapshot = CloneElementAt(_activeElementIndex);
                _interactionLastPreviewElementSnapshot = _interactionInitialElementSnapshot?.DeepCopy();
                e.Pointer.Capture(this);
                return;
            }

            if (IsElementEditingEnabled && TryHitTestElementBody(mousePos, out int bodyIndex))
            {
                TrySetSelectedElementIndexInternal(bodyIndex, PaintElementChangeSource.Interaction);

                _interactionMode = EditInteractionMode.MovingElement;
                _activeElementIndex = bodyIndex;
                _activeHandleType = ElementHandleType.None;
                _activeHandleVertexIndex = -1;
                CtlMouseStatus = ImageCtlMouseStatus.Dragging;
                Cursor = new Cursor(StandardCursorType.SizeAll);
                _interactionInitialElementSnapshot = CloneElementAt(_activeElementIndex);
                _interactionLastPreviewElementSnapshot = _interactionInitialElementSnapshot?.DeepCopy();
                e.Pointer.Capture(this);
                return;
            }

            _interactionMode = EditInteractionMode.PanningImage;
            _activeElementIndex = -1;
            _activeHandleType = ElementHandleType.None;
            _activeHandleVertexIndex = -1;
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
                RaiseInteractionPreviewChanged();
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
            if (_originImage != null)
            {
                var releaseImagePos = ClampPointToImage(ControlToImagePoint(mousePos));
                ImageMouseUp?.Invoke(this, new ImageClickEventArgs(mousePos, releaseImagePos));
            }

            bool shouldRaiseImageClick = !_interactionMoved;

            if (shouldRaiseImageClick && _originImage != null && GetImageRectangle().Contains(mousePos))
            {
                var imagePos = ClampPointToImage(ControlToImagePoint(mousePos));
                ImageClick?.Invoke(this, new ImageClickEventArgs(mousePos, imagePos));
            }

            RaiseInteractionCommittedChanged();

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

            if (!IsElementEditingEnabled)
            {
                return GetImageRectangle().Contains(mousePosition)
                    ? new Cursor(StandardCursorType.Hand)
                    : Cursor.Default;
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
                ElementHandleType.LineStart => new Cursor(StandardCursorType.Hand),
                ElementHandleType.LineEnd => new Cursor(StandardCursorType.Hand),
                ElementHandleType.PolygonVertex => new Cursor(StandardCursorType.Hand),
                _ => Cursor.Default
            };
        }

        private bool TryHitTestHandle(Point mousePosition, out HandleHitResult hitResult)
        {
            if (_selectedElementIndex < 0 || _selectedElementIndex >= m_CurrShowElement.Count)
            {
                hitResult = default;
                return false;
            }

            int i = _selectedElementIndex;
            if (!ShouldRenderElement(i))
            {
                hitResult = default;
                return false;
            }

            var element = m_CurrShowElement[i];
            if (!IsHandleEditableElement(element))
            {
                hitResult = default;
                return false;
            }

            var handles = GetHandlePoints(element);
            foreach (var handle in handles)
            {
                if (GetHandleRect(handle.Position, HandleHitSize).Contains(mousePosition))
                {
                    hitResult = new HandleHitResult(i, handle.HandleType, handle.VertexIndex);
                    return true;
                }
            }

            hitResult = default;
            return false;
        }

        private bool TryHitTestElementBody(Point mousePosition, out int elementIndex)
        {
            for (int i = m_CurrShowElement.Count - 1; i >= 0; i--)
            {
                if (!ShouldRenderElement(i))
                {
                    continue;
                }

                var element = m_CurrShowElement[i];
                if (!IsMovableElement(element))
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
                else if (element.Type == PaintElementType.Ellipse &&
                         TryGetRectangleControlRect(element, out var ellipseRect))
                {
                    if (IsPointInEllipse(mousePosition, ellipseRect, ElementHitTolerance))
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
                else if ((element.Type == PaintElementType.Line || element.Type == PaintElementType.Arrow) &&
                         TryGetLineControlGeometry(element, out var lineStart, out var lineEnd))
                {
                    if (DistanceToSegment(mousePosition, lineStart, lineEnd) <= ElementHitTolerance)
                    {
                        elementIndex = i;
                        return true;
                    }
                }
                else if (element.Type == PaintElementType.Polygon &&
                         TryGetPolygonControlPoints(element, out var polygonPoints))
                {
                    if (IsPointInPolygon(mousePosition, polygonPoints) ||
                        IsPointNearPolygonEdge(mousePosition, polygonPoints, ElementHitTolerance))
                    {
                        elementIndex = i;
                        return true;
                    }
                }
                else if (element.Type == PaintElementType.Text &&
                         TryGetTextControlRect(element, out var textRect))
                {
                    if (InflateRect(textRect, ElementHitTolerance).Contains(mousePosition))
                    {
                        elementIndex = i;
                        return true;
                    }
                }
                else if (element.Type == PaintElementType.Cross &&
                         TryGetSinglePointControlPosition(element, out var crossCenter))
                {
                    double hitTolerance = Math.Max(ElementHitTolerance, MinPointHitTolerance) + element.LineWidth * _currentZoomFactor * 3;
                    if (GetHandleRect(crossCenter, hitTolerance * 2).Contains(mousePosition))
                    {
                        elementIndex = i;
                        return true;
                    }
                }
                else if (element.Type == PaintElementType.Point &&
                         TryGetSinglePointControlPosition(element, out var pointCenter))
                {
                    double hitTolerance = Math.Max(ElementHitTolerance, MinPointHitTolerance) + element.LineWidth * _currentZoomFactor * 2;
                    if (GetHandleRect(pointCenter, hitTolerance * 2).Contains(mousePosition))
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
                PaintElementType.Ellipse => TryMoveRectangleElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Circle => TryMoveCircleElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Line => TryMoveLineElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Arrow => TryMoveLineElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Polygon => TryMovePolygonElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Text => TryMovePointBasedElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Cross => TryMovePointBasedElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                PaintElementType.Point => TryMovePointBasedElement(
                    element,
                    deltaImageX,
                    deltaImageY,
                    pixelToMachineMatrix),
                _ => false
            };
        }

        private bool TryMovePointBasedElement(
            PaintElement element,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (element.Pts.Count < 2)
            {
                return false;
            }

            var newPts = new List<double>(element.Pts.Count);
            for (int i = 0; i < element.Pts.Count; i += 2)
            {
                var imagePoint = MachineToImagePoint(new Point(element.Pts[i], element.Pts[i + 1]));
                var movedPoint = new Point(imagePoint.X + deltaImageX, imagePoint.Y + deltaImageY);
                var machinePoint = ImageToMachinePoint(movedPoint, pixelToMachineMatrix);
                newPts.Add(machinePoint.X);
                newPts.Add(machinePoint.Y);
            }

            element.Pts = newPts;
            return true;
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

            if ((element.Type == PaintElementType.Rectangle || element.Type == PaintElementType.Ellipse) &&
                IsRectangleHandle(_activeHandleType))
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

            if ((element.Type == PaintElementType.Line || element.Type == PaintElementType.Arrow) &&
                (_activeHandleType == ElementHandleType.LineStart || _activeHandleType == ElementHandleType.LineEnd))
            {
                return TryResizeLineElement(element, mousePosition, pixelToMachineMatrix);
            }

            if (element.Type == PaintElementType.Polygon &&
                _activeHandleType == ElementHandleType.PolygonVertex)
            {
                return TryResizePolygonVertex(element, mousePosition, _activeHandleVertexIndex, pixelToMachineMatrix);
            }

            return false;
        }

        private bool TryMoveRectangleElement(
            PaintElement element,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (_originImage == null)
            {
                return false;
            }

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
            if (_originImage == null)
            {
                return false;
            }

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

        private bool TryMoveLineElement(
            PaintElement element,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (_originImage == null)
            {
                return false;
            }

            if (!TryGetLineImageGeometry(element, out var startImage, out var endImage))
            {
                return false;
            }

            double imageWidth = _originImage.PixelSize.Width;
            double imageHeight = _originImage.PixelSize.Height;
            double minX = Math.Min(startImage.X, endImage.X);
            double maxX = Math.Max(startImage.X, endImage.X);
            double minY = Math.Min(startImage.Y, endImage.Y);
            double maxY = Math.Max(startImage.Y, endImage.Y);

            double moveX = Math.Clamp(deltaImageX, -minX, imageWidth - maxX);
            double moveY = Math.Clamp(deltaImageY, -minY, imageHeight - maxY);
            if (Math.Abs(moveX) < double.Epsilon && Math.Abs(moveY) < double.Epsilon)
            {
                return false;
            }

            var startNew = new Point(startImage.X + moveX, startImage.Y + moveY);
            var endNew = new Point(endImage.X + moveX, endImage.Y + moveY);
            return TrySetLineFromImagePoints(element, startNew, endNew, pixelToMachineMatrix);
        }

        private bool TryMovePolygonElement(
            PaintElement element,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (_originImage == null)
            {
                return false;
            }

            if (!TryGetPolygonImagePoints(element, out var imagePoints) || imagePoints.Count < 3)
            {
                return false;
            }

            double imageWidth = _originImage.PixelSize.Width;
            double imageHeight = _originImage.PixelSize.Height;

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            foreach (var point in imagePoints)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            double moveX = Math.Clamp(deltaImageX, -minX, imageWidth - maxX);
            double moveY = Math.Clamp(deltaImageY, -minY, imageHeight - maxY);
            if (Math.Abs(moveX) < double.Epsilon && Math.Abs(moveY) < double.Epsilon)
            {
                return false;
            }

            var moved = new List<Point>(imagePoints.Count);
            foreach (var point in imagePoints)
            {
                moved.Add(new Point(point.X + moveX, point.Y + moveY));
            }

            return TrySetPolygonFromImagePoints(element, moved, pixelToMachineMatrix);
        }

        private bool TryResizeRectangleElement(
            PaintElement element,
            ElementHandleType handleType,
            double deltaImageX,
            double deltaImageY,
            double[] pixelToMachineMatrix)
        {
            if (_originImage == null)
            {
                return false;
            }

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
            if (_originImage == null)
            {
                return false;
            }

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

        private bool TryResizeLineElement(
            PaintElement element,
            Point mousePosition,
            double[] pixelToMachineMatrix)
        {
            if (!TryGetLineImageGeometry(element, out var startImage, out var endImage))
            {
                return false;
            }

            var targetImage = ClampPointToImage(ControlToImagePoint(mousePosition));
            if (_activeHandleType == ElementHandleType.LineStart)
            {
                if (Distance(targetImage, startImage) < double.Epsilon)
                {
                    return false;
                }

                return TrySetLineFromImagePoints(element, targetImage, endImage, pixelToMachineMatrix);
            }

            if (_activeHandleType == ElementHandleType.LineEnd)
            {
                if (Distance(targetImage, endImage) < double.Epsilon)
                {
                    return false;
                }

                return TrySetLineFromImagePoints(element, startImage, targetImage, pixelToMachineMatrix);
            }

            return false;
        }

        private bool TryResizePolygonVertex(
            PaintElement element,
            Point mousePosition,
            int vertexIndex,
            double[] pixelToMachineMatrix)
        {
            if (!TryGetPolygonImagePoints(element, out var imagePoints))
            {
                return false;
            }

            if (vertexIndex < 0 || vertexIndex >= imagePoints.Count)
            {
                return false;
            }

            var targetImage = ClampPointToImage(ControlToImagePoint(mousePosition));
            if (Distance(targetImage, imagePoints[vertexIndex]) < double.Epsilon)
            {
                return false;
            }

            imagePoints[vertexIndex] = targetImage;
            return TrySetPolygonFromImagePoints(element, imagePoints, pixelToMachineMatrix);
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

        private bool TrySetLineFromImagePoints(
            PaintElement element,
            Point startImage,
            Point endImage,
            double[] pixelToMachineMatrix)
        {
            var startMachine = ImageToMachinePoint(startImage, pixelToMachineMatrix);
            var endMachine = ImageToMachinePoint(endImage, pixelToMachineMatrix);

            EnsureElementPointCount(element, 4);
            element.Pts[0] = startMachine.X;
            element.Pts[1] = startMachine.Y;
            element.Pts[2] = endMachine.X;
            element.Pts[3] = endMachine.Y;
            return true;
        }

        private bool TrySetPolygonFromImagePoints(
            PaintElement element,
            List<Point> imagePoints,
            double[] pixelToMachineMatrix)
        {
            if (imagePoints.Count < 3)
            {
                return false;
            }

            var newPts = new List<double>(imagePoints.Count * 2);
            foreach (var imagePoint in imagePoints)
            {
                var machinePoint = ImageToMachinePoint(imagePoint, pixelToMachineMatrix);
                newPts.Add(machinePoint.X);
                newPts.Add(machinePoint.Y);
            }

            element.Pts = newPts;
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
            if ((element.Type != PaintElementType.Rectangle && element.Type != PaintElementType.Ellipse) ||
                element.Pts.Count < 4)
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
            if ((element.Type != PaintElementType.Rectangle && element.Type != PaintElementType.Ellipse) ||
                element.Pts.Count < 4)
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

        private bool TryGetLineControlGeometry(PaintElement element, out Point start, out Point end)
        {
            start = default;
            end = default;
            if ((element.Type != PaintElementType.Line && element.Type != PaintElementType.Arrow) ||
                element.Pts.Count < 4)
            {
                return false;
            }

            start = MachineToControlPoint(new Point(element.Pts[0], element.Pts[1]));
            end = MachineToControlPoint(new Point(element.Pts[2], element.Pts[3]));
            return true;
        }

        private bool TryGetLineImageGeometry(PaintElement element, out Point startImage, out Point endImage)
        {
            startImage = default;
            endImage = default;
            if ((element.Type != PaintElementType.Line && element.Type != PaintElementType.Arrow) ||
                element.Pts.Count < 4)
            {
                return false;
            }

            startImage = MachineToImagePoint(new Point(element.Pts[0], element.Pts[1]));
            endImage = MachineToImagePoint(new Point(element.Pts[2], element.Pts[3]));
            return true;
        }

        private bool TryGetPolygonControlPoints(PaintElement element, out List<Point> points)
        {
            points = new List<Point>();
            if (element.Type != PaintElementType.Polygon || element.Pts.Count < 6 || element.Pts.Count % 2 != 0)
            {
                return false;
            }

            for (int i = 0; i < element.Pts.Count; i += 2)
            {
                var machinePoint = new Point(element.Pts[i], element.Pts[i + 1]);
                points.Add(MachineToControlPoint(machinePoint));
            }

            return points.Count >= 3;
        }

        private bool TryGetPolygonImagePoints(PaintElement element, out List<Point> points)
        {
            points = new List<Point>();
            if (element.Type != PaintElementType.Polygon || element.Pts.Count < 6 || element.Pts.Count % 2 != 0)
            {
                return false;
            }

            for (int i = 0; i < element.Pts.Count; i += 2)
            {
                var machinePoint = new Point(element.Pts[i], element.Pts[i + 1]);
                points.Add(MachineToImagePoint(machinePoint));
            }

            return points.Count >= 3;
        }

        private List<HandlePoint> GetHandlePoints(PaintElement element)
        {
            var handlePoints = new List<HandlePoint>();
            if (!IsHandleEditableElement(element))
            {
                return handlePoints;
            }

            if ((element.Type == PaintElementType.Rectangle || element.Type == PaintElementType.Ellipse) &&
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
            else if ((element.Type == PaintElementType.Line || element.Type == PaintElementType.Arrow) &&
                     TryGetLineControlGeometry(element, out var start, out var end))
            {
                handlePoints.Add(new HandlePoint(ElementHandleType.LineStart, start));
                handlePoints.Add(new HandlePoint(ElementHandleType.LineEnd, end));
            }
            else if (element.Type == PaintElementType.Polygon &&
                     TryGetPolygonControlPoints(element, out var polygonPoints))
            {
                for (int i = 0; i < polygonPoints.Count; i++)
                {
                    handlePoints.Add(new HandlePoint(ElementHandleType.PolygonVertex, polygonPoints[i], i));
                }
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

        private static bool IsMovableElement(PaintElement element)
        {
            return element.Visible &&
                element.Type switch
                {
                    PaintElementType.Rectangle => element.Pts.Count >= 4,
                    PaintElementType.Ellipse => element.Pts.Count >= 4,
                    PaintElementType.Circle => element.Pts.Count >= 4,
                    PaintElementType.Line => element.Pts.Count >= 4,
                    PaintElementType.Arrow => element.Pts.Count >= 4,
                    PaintElementType.Polygon => element.Pts.Count >= 6 && element.Pts.Count % 2 == 0,
                    PaintElementType.Text => element.Pts.Count >= 2,
                    PaintElementType.Cross => element.Pts.Count >= 2,
                    PaintElementType.Point => element.Pts.Count >= 2,
                    _ => false
                };
        }

        private static bool IsHandleEditableElement(PaintElement element)
        {
            return element.Visible &&
                element.Type switch
                {
                    PaintElementType.Rectangle => element.Pts.Count >= 4,
                    PaintElementType.Ellipse => element.Pts.Count >= 4,
                    PaintElementType.Circle => element.Pts.Count >= 4,
                    PaintElementType.Line => element.Pts.Count >= 4,
                    PaintElementType.Arrow => element.Pts.Count >= 4,
                    PaintElementType.Polygon => element.Pts.Count >= 6 && element.Pts.Count % 2 == 0,
                    _ => false
                };
        }

        private bool TryGetPixelToMachineMatrix(out double[] pixelToMachineMatrix)
        {
            // Pure image mode: element points are already in image pixels.
            pixelToMachineMatrix = new double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            return true;
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
            return machinePoint;
        }

        private Point MachineToControlPoint(Point machinePoint)
        {
            return ImageToControlPoint(MachineToImagePoint(machinePoint));
        }

        private Point ImageToMachinePoint(Point imagePoint, double[] pixelToMachineMatrix)
        {
            _ = pixelToMachineMatrix;
            return imagePoint;
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
            if (_originImage == null || CtlShowPaintStatus <= 0 || m_CurrShowElement.Count == 0 || !IsElementEditingEnabled)
            {
                return;
            }

            if (_selectedElementIndex < 0 || _selectedElementIndex >= m_CurrShowElement.Count)
            {
                return;
            }

            if (!ShouldRenderElement(_selectedElementIndex))
            {
                return;
            }

            var element = m_CurrShowElement[_selectedElementIndex];
            if (!IsHandleEditableElement(element))
            {
                return;
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

        private void ResetInteractionState()
        {
            _interactionMode = EditInteractionMode.None;
            _activeElementIndex = -1;
            _activeHandleType = ElementHandleType.None;
            _activeHandleVertexIndex = -1;
            _interactionMoved = false;
            _interactionInitialElementSnapshot = null;
            _interactionLastPreviewElementSnapshot = null;
            _interactionElementChanged = false;
            CtlMouseStatus = ImageCtlMouseStatus.Normal;
        }

        private bool TryCancelCurrentInteraction()
        {
            if (_interactionMode == EditInteractionMode.MovingElement ||
                _interactionMode == EditInteractionMode.ResizingElement)
            {
                if (IsIndexValid(_activeElementIndex) && _interactionInitialElementSnapshot != null)
                {
                    m_CurrShowElement[_activeElementIndex] = _interactionInitialElementSnapshot.DeepCopy();
                }

                ResetInteractionState();
                InvalidateVisual();
                UpdateCursorStyle(_interactionLastPoint);
                return true;
            }

            return false;
        }

        private bool TryGetSinglePointControlPosition(PaintElement element, out Point point)
        {
            point = default;
            if (element.Pts.Count < 2)
            {
                return false;
            }

            point = MachineToControlPoint(new Point(element.Pts[0], element.Pts[1]));
            return true;
        }

        private bool TryGetTextControlRect(PaintElement element, out Rect rect)
        {
            rect = default;
            if (element.Type != PaintElementType.Text ||
                element.Pts.Count < 2 ||
                string.IsNullOrEmpty(element.Text))
            {
                return false;
            }

            var origin = MachineToControlPoint(new Point(element.Pts[0], element.Pts[1]));
            var formattedText = new FormattedText(
                element.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"),
                Math.Max(1.0, element.FontSize),
                Brushes.White);
            rect = new Rect(origin, new Size(formattedText.Width, formattedText.Height));
            return true;
        }

        private void DrawSelectedElementAdornment(DrawingContext context, PaintElement element, List<float> transformedPts)
        {
            if (_selectedElementIndex < 0 || transformedPts.Count < 2)
            {
                return;
            }

            switch (element.Type)
            {
                case PaintElementType.Rectangle:
                    if (TryGetRectangleControlRect(element, out var rect))
                    {
                        context.DrawRectangle(null, SelectedAdornmentPen, InflateRect(rect, 2));
                    }
                    break;
                case PaintElementType.Ellipse:
                    if (TryGetRectangleControlRect(element, out var ellipseRect))
                    {
                        var center = new Point(ellipseRect.Center.X, ellipseRect.Center.Y);
                        context.DrawEllipse(
                            null,
                            SelectedAdornmentPen,
                            center,
                            ellipseRect.Width / 2.0 + 2,
                            ellipseRect.Height / 2.0 + 2);
                    }
                    break;
                case PaintElementType.Circle:
                    if (TryGetCircleControlGeometry(element, out var circleCenter, out var edge))
                    {
                        context.DrawEllipse(null, SelectedAdornmentPen, circleCenter, Distance(circleCenter, edge) + 2, Distance(circleCenter, edge) + 2);
                    }
                    break;
                case PaintElementType.Line:
                case PaintElementType.Arrow:
                    if (TryGetLineControlGeometry(element, out var lineStart, out var lineEnd))
                    {
                        context.DrawLine(SelectedAdornmentPen, lineStart, lineEnd);
                    }
                    break;
                case PaintElementType.Polygon:
                    if (TryGetPolygonControlPoints(element, out var polygonPoints) && polygonPoints.Count >= 3)
                    {
                        var geometry = new PolylineGeometry(polygonPoints, true);
                        context.DrawGeometry(null, SelectedAdornmentPen, geometry);
                    }
                    break;
                case PaintElementType.Text:
                    if (TryGetTextControlRect(element, out var textRect))
                    {
                        context.DrawRectangle(null, SelectedAdornmentPen, InflateRect(textRect, 3));
                    }
                    break;
                case PaintElementType.Cross:
                case PaintElementType.Point:
                    if (TryGetSinglePointControlPosition(element, out var pointCenter))
                    {
                        context.DrawEllipse(null, SelectedAdornmentPen, pointCenter, 10, 10);
                    }
                    break;
            }
        }

        private void RaiseInteractionPreviewChanged()
        {
            if (_interactionMode != EditInteractionMode.MovingElement &&
                _interactionMode != EditInteractionMode.ResizingElement)
            {
                return;
            }

            if (!IsIndexValid(_activeElementIndex))
            {
                return;
            }

            var current = CloneElementAt(_activeElementIndex);
            if (current == null)
            {
                return;
            }

            if (_interactionLastPreviewElementSnapshot != null &&
                AreElementsEquivalent(_interactionLastPreviewElementSnapshot, current))
            {
                return;
            }

            RaiseElementChanged(
                PaintElementChangeAction.Updated,
                _activeElementIndex,
                _interactionLastPreviewElementSnapshot,
                current,
                PaintElementChangeSource.Interaction,
                PaintElementChangePhase.Preview);

            _interactionLastPreviewElementSnapshot = current.DeepCopy();
            _interactionElementChanged = true;
        }

        private void RaiseInteractionCommittedChanged()
        {
            if (!_interactionElementChanged)
            {
                return;
            }

            if (!IsIndexValid(_activeElementIndex))
            {
                return;
            }

            var current = CloneElementAt(_activeElementIndex);
            if (current == null || _interactionInitialElementSnapshot == null)
            {
                return;
            }

            if (AreElementsEquivalent(_interactionInitialElementSnapshot, current))
            {
                return;
            }

            RaiseElementChanged(
                PaintElementChangeAction.Updated,
                _activeElementIndex,
                _interactionInitialElementSnapshot,
                current,
                PaintElementChangeSource.Interaction,
                PaintElementChangePhase.Committed);
        }

        private static bool AreElementsEquivalent(PaintElement left, PaintElement right)
        {
            if (left.Type != right.Type ||
                left.LineWidth != right.LineWidth ||
                left.Color != right.Color ||
                left.IsFill != right.IsFill ||
                left.Text != right.Text ||
                left.FontSize != right.FontSize ||
                left.Visible != right.Visible ||
                left.Pts.Count != right.Pts.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Pts.Count; i++)
            {
                if (Math.Abs(left.Pts[i] - right.Pts[i]) > double.Epsilon)
                {
                    return false;
                }
            }

            return true;
        }

        private static double DistanceToSegment(Point point, Point segmentStart, Point segmentEnd)
        {
            double dx = segmentEnd.X - segmentStart.X;
            double dy = segmentEnd.Y - segmentStart.Y;
            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                return Distance(point, segmentStart);
            }

            double t = ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) /
                       (dx * dx + dy * dy);
            t = Math.Clamp(t, 0, 1);
            var projection = new Point(segmentStart.X + t * dx, segmentStart.Y + t * dy);
            return Distance(point, projection);
        }

        private static bool IsPointInEllipse(Point point, Rect rect, double tolerance)
        {
            if (rect.Width < double.Epsilon || rect.Height < double.Epsilon)
            {
                return false;
            }

            double centerX = rect.X + rect.Width / 2.0;
            double centerY = rect.Y + rect.Height / 2.0;
            double radiusX = rect.Width / 2.0;
            double radiusY = rect.Height / 2.0;

            // 扩展半径用于命中容差，提升边缘可选中性。
            double expandedRadiusX = radiusX + tolerance;
            double expandedRadiusY = radiusY + tolerance;
            if (expandedRadiusX <= double.Epsilon || expandedRadiusY <= double.Epsilon)
            {
                return false;
            }

            double normalizedX = (point.X - centerX) / expandedRadiusX;
            double normalizedY = (point.Y - centerY) / expandedRadiusY;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1.0;
        }

        private static bool IsPointInPolygon(Point point, List<Point> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                bool intersects = (pi.Y > point.Y) != (pj.Y > point.Y) &&
                                  point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + double.Epsilon) + pi.X;
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static bool IsPointNearPolygonEdge(Point point, List<Point> polygon, double tolerance)
        {
            if (polygon.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < polygon.Count; i++)
            {
                var start = polygon[i];
                var end = polygon[(i + 1) % polygon.Count];
                if (DistanceToSegment(point, start, end) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
