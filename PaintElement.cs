using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图元类 - 存储要绘制的图形元素
    /// </summary>
    public class PaintElement
    {
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

        public PaintElement(PaintElementType type, double lineWidth, Color color, params double[] ts) : this()
        {
            Type = type;
            LineWidth = lineWidth;
            Color = color;
            foreach (var v in ts)
            {
                Pts.Add(v);
            }
        }

        public PaintElement(PaintElement other)
        {
            Type = other.Type;
            LineWidth = other.LineWidth;
            Color = other.Color;
            if(other.Pts != null)
                Pts = other.Pts.ToList();
            IsShowIndex = other.IsShowIndex;
            Index = other.Index;
            IsSelected = other.IsSelected;
            ShowTextStr = other.ShowTextStr;
            if(other.m_Node != null)
                m_Node = other.m_Node.ToList();
            ExNoShowPrms = other.ExNoShowPrms;
            
            // Avalonia specific fields
            IsFill = other.IsFill;
            FontSize = other.FontSize;
            Visible = other.Visible;
            Text = other.Text;
            IndexShowCurrMachPos = other.IndexShowCurrMachPos;
        }

        /// <summary>
        /// 下标
        /// </summary>
        public int Index { get; set; } = -1;

        /// <summary>
        /// 指示是否在绘制时显示Index，注意 Index>=0 才会显示
        /// </summary>
        public bool IsShowIndex { get; set; } = true;

        /// <summary>
        /// 指示此点替换为机械坐标点
        /// </summary>
        public int IndexShowCurrMachPos { get; set; } = -1;

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
        /// 子节点
        /// </summary>
        public List<PaintElement> m_Node { get; set; }

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
        /// 用于显示的文字 (兼容 ShareMemRPC 命名)
        /// </summary>
        public string ShowTextStr
        {
            get => Text;
            set => Text = value;
        }

        /// <summary>
        /// 字体大小（当 Type 为 Text 时使用）
        /// </summary>
        public double FontSize { get; set; }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>
        /// 其他不用于显示的参数
        /// </summary>
        public Dictionary<string, double> ExNoShowPrms { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected { get; set; }

        // 运行动画效果
        public bool RunAnimation(bool waitRunEnd)
        {
            return true;
        }

        private double GetRealWidth(double widthScale)
        {
            var value = LineWidth * widthScale;
            if (value < 1)
                value = 1;
            return value;
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
            double actualLineWidth = GetRealWidth(lineScale);

            var pen = new Pen(new SolidColorBrush(Color), actualLineWidth);
            if (IsSelected)
            {
                pen.DashStyle = DashStyle.Dash;
                // 选中时使用黄色半透明
                pen.Brush = new SolidColorBrush(Color.FromRgb(255, 255, 0), 0.8); 
            }
            var brush = new SolidColorBrush(Color);

            switch (Type)
            {
                case PaintElementType.Dot:
                    PaintDot(context, brush, transformedPts, actualLineWidth);
                    break;

                case PaintElementType.Line:
                    PaintLine(context, pen, transformedPts);
                    break;

                case PaintElementType.PolyLine:
                    PaintPolyLine(context, pen, transformedPts);
                    break;

                case PaintElementType.Circle:
                    // ShareMemRPC 的 Circle 是三点圆
                    if (transformedPts.Count >= 6)
                        PaintCircle3Points(context, pen, brush, transformedPts);
                    else
                        PaintCircle2Points(context, pen, brush, transformedPts);
                    break;

                case PaintElementType.FilledCircle:
                    // ShareMemRPC 的实心圆，这里简单处理为 Fill=true 的圆
                    bool oldFill = IsFill;
                    IsFill = true;
                    if (transformedPts.Count >= 6)
                        PaintCircle3Points(context, pen, brush, transformedPts);
                    else
                        PaintCircle2Points(context, pen, brush, transformedPts);
                    IsFill = oldFill;
                    break;

                case PaintElementType.Rect:
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
                    // ShareMemRPC 的 Arc 是三点弧
                    PaintArc3Points(context, pen, transformedPts);
                    break;

                case PaintElementType.Text:
                    PaintText(context, brush, transformedPts);
                    break;

                case PaintElementType.TextCircle:
                case PaintElementType.TextRect:
                    PaintTextShape(context, pen, brush, transformedPts);
                    break;
            }

            // 绘制下标
            if (IsShowIndex && Index >= 0 && transformedPts.Count >= 2)
            {
                var pt = new Point(transformedPts[0] + actualLineWidth, transformedPts[1]);
                var typeface = new Typeface("Arial", FontStyle.Normal, FontWeight.Bold);
                var formattedText = new FormattedText(
                    Index.ToString(),
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12, // 默认字号
                    brush
                );
                context.DrawText(formattedText, pt);
            }
        }

        private void PaintDot(DrawingContext context, IBrush brush, List<float> pts, double size)
        {
            for (int i = 0; i < pts.Count; i += 2)
            {
                if (i + 1 < pts.Count)
                {
                    var center = new Point(pts[i], pts[i + 1]);
                    double radius = size / 2.0;
                    context.DrawEllipse(brush, null, center, radius, radius);
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

        private void PaintCircle2Points(DrawingContext context, IPen pen, IBrush brush, List<float> pts)
        {
            if (pts.Count >= 4)
            {
                var center = new Point(pts[0], pts[1]);
                var edgePoint = new Point(pts[2], pts[3]);
                double radius = Math.Sqrt(Math.Pow(edgePoint.X - center.X, 2) + Math.Pow(edgePoint.Y - center.Y, 2));
                
                context.DrawEllipse(IsFill ? brush : null, pen, center, radius, radius);
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
                
                context.DrawRectangle(IsFill ? brush : null, pen, rect);
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
                
                context.DrawEllipse(IsFill ? brush : null, pen, center, radiusX, radiusY);
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
                    context.DrawGeometry(IsFill ? brush : null, pen, geometry);
                }
            }
        }

        private void PaintCross(DrawingContext context, IPen pen, List<float> pts, double size)
        {
            if (pts.Count >= 2)
            {
                var center = new Point(pts[0], pts[1]);
                double halfSize = size * 5;
                
                context.DrawLine(pen, new Point(center.X - halfSize, center.Y), new Point(center.X + halfSize, center.Y));
                context.DrawLine(pen, new Point(center.X, center.Y - halfSize), new Point(center.X, center.Y + halfSize));
            }
        }

        private void PaintArrow(DrawingContext context, IPen pen, List<float> pts)
        {
            if (pts.Count >= 4)
            {
                var p1 = new Point(pts[0], pts[1]);
                var p2 = new Point(pts[2], pts[3]);
                
                context.DrawLine(pen, p1, p2);
                
                double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                double arrowLength = 10;
                double arrowAngle = Math.PI / 6; 
                
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

        private void PaintTextShape(DrawingContext context, IPen pen, IBrush brush, List<float> pts)
        {
            if (pts.Count < 2) return;

            string text = "";
            if (ShowTextStr == "测高") text = "H";
            else if (ShowTextStr == "Mark") text = "M";
            else text = ShowTextStr;

            var center = new Point(pts[0], pts[1]);
            double radius = 8.0;

            // 绘制圆框
            context.DrawEllipse(null, pen, center, radius, radius);

            // 绘制文字
            var typeface = new Typeface("Microsoft YaHei", FontStyle.Normal, FontWeight.Bold);
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                brush
            );
            
            // 居中
            var textPos = new Point(
                center.X - formattedText.Width / 2,
                center.Y - formattedText.Height / 2
            );
            context.DrawText(formattedText, textPos);
        }

        private void PaintCircle3Points(DrawingContext context, IPen pen, IBrush brush, List<float> pts)
        {
            try
            {
                var p1 = new Point(pts[0], pts[1]);
                var p2 = new Point(pts[2], pts[3]);
                var p3 = new Point(pts[4], pts[5]);

                var center = CalculateCircleCenter(p1, p2, p3);
                double radius = (CalculateDistance(center, p1) + CalculateDistance(center, p2) + CalculateDistance(center, p3)) / 3.0;

                context.DrawEllipse(IsFill ? brush : null, pen, center, radius, radius);
            }
            catch { }
        }

        private void PaintArc3Points(DrawingContext context, IPen pen, List<float> pts)
        {
            try
            {
                var p1 = new Point(pts[0], pts[1]);
                var p2 = new Point(pts[2], pts[3]);
                var p3 = new Point(pts[4], pts[5]);

                var center = CalculateCircleCenter(p1, p2, p3);
                double radius = CalculateDistance(center, p1);

                float startAngle, sweepAngle;
                DetermineArcParametersGeometric(center, p1, p2, p3, out startAngle, out sweepAngle);

                // Avalonia 绘制 Arc 需要 PathGeometry
                // ArcSegment 需要终点和 Size(radius)，以及 SweepDirection
                // startAngle 是起始角度，sweepAngle 是扫过的角度（正为顺时针，负为逆时针）
                
                // 计算起点坐标 (实际上 p1 应该是起点，但为了精确匹配角度，重新计算)
                // 这里直接用 p1 作为起点，p3 作为终点，但需验证方向

                // 注意：ShareMemRPC 的逻辑是 DrawArc(rect, startAngle, sweepAngle)
                // Avalonia 没有 DrawArc，需要构建 Path
                
                var path = new PathGeometry();
                var figure = new PathFigure { StartPoint = p1, IsClosed = false };
                
                // 如果 sweepAngle > 0，顺时针；Avalonia Clockwise
                var sweepDir = sweepAngle >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
                bool isLargeArc = Math.Abs(sweepAngle) > 180;

                var arcSegment = new ArcSegment
                {
                    Point = p3, // 终点
                    Size = new Size(radius, radius),
                    SweepDirection = sweepDir,
                    IsLargeArc = isLargeArc,
                    RotationAngle = 0 
                };
                
                figure.Segments.Add(arcSegment);
                path.Figures.Add(figure);
                
                context.DrawGeometry(null, pen, path);
            }
            catch { }
        }

        #region Math Helpers
        private Point CalculateCircleCenter(Point A, Point B, Point C)
        {
            double deltaA = B.X * B.X + B.Y * B.Y - A.X * A.X - A.Y * A.Y;
            double deltaB = C.X * C.X + C.Y * C.Y - A.X * A.X - A.Y * A.Y;
            double denominator = 2 * (B.X - A.X) * (C.Y - A.Y) - 2 * (B.Y - A.Y) * (C.X - A.X);

            if (Math.Abs(denominator) < 0.0001) throw new InvalidOperationException();

            double centerX = ((C.Y - A.Y) * deltaA - (B.Y - A.Y) * deltaB) / denominator;
            double centerY = (-(C.X - A.X) * deltaA + (B.X - A.X) * deltaB) / denominator;

            return new Point(centerX, centerY);
        }

        private double CalculateDistance(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private float CalculateAngle(Point center, Point point)
        {
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            return (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
        }

        private float NormalizeAngle(float angle)
        {
            angle = angle % 360;
            return angle < 0 ? angle + 360 : angle;
        }

        private void DetermineArcParametersGeometric(Point center, Point p1, Point p2, Point p3,
                                                     out float startAngle, out float sweepAngle)
        {
            float angle1 = CalculateAngle(center, p1);
            float angle2 = CalculateAngle(center, p2);
            float angle3 = CalculateAngle(center, p3);

            angle1 = NormalizeAngle(angle1);
            angle2 = NormalizeAngle(angle2);
            angle3 = NormalizeAngle(angle3);

            float delta12CW = (angle2 - angle1 + 360) % 360;
            float delta23CW = (angle3 - angle2 + 360) % 360;
            float delta13CW = (angle3 - angle1 + 360) % 360;

            float delta12CCW = (angle1 - angle2 + 360) % 360;
            float delta23CCW = (angle2 - angle3 + 360) % 360;
            float delta13CCW = (angle1 - angle3 + 360) % 360;

            float totalCW = delta12CW + delta23CW;
            float totalCCW = delta12CCW + delta23CCW;

            if (totalCW <= totalCCW)
            {
                startAngle = angle1;
                sweepAngle = delta13CW;
            }
            else
            {
                startAngle = angle1;
                sweepAngle = -delta13CCW;
            }
        }
        #endregion
    }
}
