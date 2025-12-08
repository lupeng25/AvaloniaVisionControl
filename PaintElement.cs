using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图元类 - 存储要绘制的图形元素
    /// </summary>
    public class PaintElement
    {
        /// <summary>
        /// 图元类型
        /// </summary>
        public PaintElementType Type { get; set; }

        /// <summary>
        /// 坐标点列表（机械坐标，单位：mm）
        /// 格式：[x1, y1, x2, y2, ...]
        /// </summary>
        public List<double> Pts { get; set; }

        /// <summary>
        /// 线宽（像素）
        /// </summary>
        public double LineWidth { get; set; }

        /// <summary>
        /// 颜色
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// 是否填充（对于封闭图形）
        /// </summary>
        public bool IsFill { get; set; }

        /// <summary>
        /// 文本内容（当 Type 为 Text 时使用）
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 字体大小（当 Type 为 Text 时使用）
        /// </summary>
        public double FontSize { get; set; }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool Visible { get; set; }

        public PaintElement()
        {
            Pts = new List<double>();
            LineWidth = 1.0;
            Color = Colors.Red;
            IsFill = false;
            Visible = true;
            FontSize = 12;
            Text = string.Empty;
        }

        /// <summary>
        /// 绘制图元到 Avalonia DrawingContext
        /// </summary>
        /// <param name="context">Avalonia 绘图上下文</param>
        /// <param name="lineScale">线宽缩放比例</param>
        /// <param name="transformedPts">已转换到控件坐标系的点列表 [x1, y1, x2, y2, ...]</param>
        public void Paint(DrawingContext context, double lineScale, List<float> transformedPts)
        {
            if (!Visible || transformedPts == null || transformedPts.Count < 2)
                return;

            // 计算实际线宽
            double actualLineWidth = LineWidth * lineScale;
            if (actualLineWidth < 1) actualLineWidth = 1;

            var pen = new Pen(new SolidColorBrush(Color), actualLineWidth);
            var brush = new SolidColorBrush(Color);

            switch (Type)
            {
                case PaintElementType.Point:
                    PaintPoint(context, brush, transformedPts, actualLineWidth);
                    break;

                case PaintElementType.Line:
                    PaintLine(context, pen, transformedPts);
                    break;

                case PaintElementType.PolyLine:
                    PaintPolyLine(context, pen, transformedPts);
                    break;

                case PaintElementType.Circle:
                    PaintCircle(context, pen, brush, transformedPts);
                    break;

                case PaintElementType.Rectangle:
                    PaintRectangle(context, pen, brush, transformedPts);
                    break;

                case PaintElementType.Ellipse:
                    PaintEllipse(context, pen, brush, transformedPts);
                    break;

                case PaintElementType.Polygon:
                    PaintPolygon(context, pen, brush, transformedPts);
                    break;

                case PaintElementType.Cross:
                    PaintCross(context, pen, transformedPts, actualLineWidth);
                    break;

                case PaintElementType.Arrow:
                    PaintArrow(context, pen, transformedPts);
                    break;

                case PaintElementType.Ring:
                    PaintRing(context, pen, transformedPts);
                    break;

                case PaintElementType.Arc:
                    PaintArc(context, pen, transformedPts);
                    break;

                case PaintElementType.Text:
                    PaintText(context, brush, transformedPts);
                    break;
            }
        }

        private void PaintPoint(DrawingContext context, IBrush brush, List<float> pts, double size)
        {
            for (int i = 0; i < pts.Count; i += 2)
            {
                if (i + 1 < pts.Count)
                {
                    var center = new Point(pts[i], pts[i + 1]);
                    context.DrawEllipse(brush, null, center, size * 2, size * 2);
                }
            }
        }

        private void PaintLine(DrawingContext context, IPen pen, List<float> pts)
        {
            if (pts.Count >= 4)
            {
                var p1 = new Point(pts[0], pts[1]);
                var p2 = new Point(pts[2], pts[3]);
                context.DrawLine(pen, p1, p2);
            }
        }

        private void PaintPolyLine(DrawingContext context, IPen pen, List<float> pts)
        {
            for (int i = 0; i < pts.Count - 2; i += 2)
            {
                var p1 = new Point(pts[i], pts[i + 1]);
                var p2 = new Point(pts[i + 2], pts[i + 3]);
                context.DrawLine(pen, p1, p2);
            }
        }

        private void PaintCircle(DrawingContext context, IPen pen, IBrush brush, List<float> pts)
        {
            if (pts.Count >= 4)
            {
                var center = new Point(pts[0], pts[1]);
                var edgePoint = new Point(pts[2], pts[3]);
                double radius = Math.Sqrt(Math.Pow(edgePoint.X - center.X, 2) + Math.Pow(edgePoint.Y - center.Y, 2));
                
                if (IsFill)
                    context.DrawEllipse(brush, pen, center, radius, radius);
                else
                    context.DrawEllipse(null, pen, center, radius, radius);
            }
        }

        private void PaintRectangle(DrawingContext context, IPen pen, IBrush brush, List<float> pts)
        {
            if (pts.Count >= 4)
            {
                var p1 = new Point(pts[0], pts[1]);
                var p2 = new Point(pts[2], pts[3]);
                var rect = new Rect(
                    Math.Min(p1.X, p2.X),
                    Math.Min(p1.Y, p2.Y),
                    Math.Abs(p2.X - p1.X),
                    Math.Abs(p2.Y - p1.Y)
                );
                
                if (IsFill)
                    context.DrawRectangle(brush, pen, rect);
                else
                    context.DrawRectangle(null, pen, rect);
            }
        }

        private void PaintEllipse(DrawingContext context, IPen pen, IBrush brush, List<float> pts)
        {
            if (pts.Count >= 4)
            {
                var p1 = new Point(pts[0], pts[1]);
                var p2 = new Point(pts[2], pts[3]);
                var center = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                double radiusX = Math.Abs(p2.X - p1.X) / 2;
                double radiusY = Math.Abs(p2.Y - p1.Y) / 2;
                
                if (IsFill)
                    context.DrawEllipse(brush, pen, center, radiusX, radiusY);
                else
                    context.DrawEllipse(null, pen, center, radiusX, radiusY);
            }
        }

        private void PaintPolygon(DrawingContext context, IPen pen, IBrush brush, List<float> pts)
        {
            if (pts.Count >= 6)
            {
                var points = new List<Point>();
                for (int i = 0; i < pts.Count; i += 2)
                {
                    if (i + 1 < pts.Count)
                        points.Add(new Point(pts[i], pts[i + 1]));
                }

                if (points.Count >= 3)
                {
                    var geometry = new PolylineGeometry(points, true);
                    
                    if (IsFill)
                        context.DrawGeometry(brush, pen, geometry);
                    else
                        context.DrawGeometry(null, pen, geometry);
                }
            }
        }

        private void PaintCross(DrawingContext context, IPen pen, List<float> pts, double size)
        {
            if (pts.Count >= 2)
            {
                var center = new Point(pts[0], pts[1]);
                double halfSize = size * 5;
                
                // 横线
                context.DrawLine(pen, 
                    new Point(center.X - halfSize, center.Y), 
                    new Point(center.X + halfSize, center.Y));
                
                // 竖线
                context.DrawLine(pen, 
                    new Point(center.X, center.Y - halfSize), 
                    new Point(center.X, center.Y + halfSize));
            }
        }

        private void PaintArrow(DrawingContext context, IPen pen, List<float> pts)
        {
            if (pts.Count >= 4)
            {
                var p1 = new Point(pts[0], pts[1]);
                var p2 = new Point(pts[2], pts[3]);
                
                // 画主线
                context.DrawLine(pen, p1, p2);
                
                // 计算箭头
                double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                double arrowLength = 10;
                double arrowAngle = Math.PI / 6; // 30度
                
                var arrow1 = new Point(
                    p2.X - arrowLength * Math.Cos(angle - arrowAngle),
                    p2.Y - arrowLength * Math.Sin(angle - arrowAngle)
                );
                
                var arrow2 = new Point(
                    p2.X - arrowLength * Math.Cos(angle + arrowAngle),
                    p2.Y - arrowLength * Math.Sin(angle + arrowAngle)
                );
                
                context.DrawLine(pen, p2, arrow1);
                context.DrawLine(pen, p2, arrow2);
            }
        }

        private void PaintRing(DrawingContext context, IPen pen, List<float> pts)
        {
            if (pts.Count >= 6)
            {
                var center = new Point(pts[0], pts[1]);
                var innerPoint = new Point(pts[2], pts[3]);
                var outerPoint = new Point(pts[4], pts[5]);
                
                double innerRadius = Math.Sqrt(Math.Pow(innerPoint.X - center.X, 2) + Math.Pow(innerPoint.Y - center.Y, 2));
                double outerRadius = Math.Sqrt(Math.Pow(outerPoint.X - center.X, 2) + Math.Pow(outerPoint.Y - center.Y, 2));
                
                context.DrawEllipse(null, pen, center, outerRadius, outerRadius);
                context.DrawEllipse(null, pen, center, innerRadius, innerRadius);
            }
        }

        private void PaintArc(DrawingContext context, IPen pen, List<float> pts)
        {
            if (pts.Count >= 6)
            {
                var center = new Point(pts[0], pts[1]);
                var startPoint = new Point(pts[2], pts[3]);
                var endPoint = new Point(pts[4], pts[5]);
                
                double radius = Math.Sqrt(Math.Pow(startPoint.X - center.X, 2) + Math.Pow(startPoint.Y - center.Y, 2));
                
                // Avalonia 的圆弧绘制需要使用 PathGeometry
                var geometry = new PathGeometry();
                var figure = new PathFigure { StartPoint = startPoint };
                
                var arcSegment = new ArcSegment
                {
                    Point = endPoint,
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise
                };
                
                figure.Segments.Add(arcSegment);
                geometry.Figures.Add(figure);
                
                context.DrawGeometry(null, pen, geometry);
            }
        }

        private void PaintText(DrawingContext context, IBrush brush, List<float> pts)
        {
            if (pts.Count >= 2 && !string.IsNullOrEmpty(Text))
            {
                var position = new Point(pts[0], pts[1]);
                var typeface = new Typeface("Microsoft YaHei");
                var formattedText = new FormattedText(
                    Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FontSize,
                    brush
                );
                
                context.DrawText(formattedText, position);
            }
        }
    }
}

