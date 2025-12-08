using System;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图元类型枚举
    /// </summary>
    public enum PaintElementType
    {
        /// <summary>
        /// 点
        /// </summary>
        Point,
        /// <summary>
        /// 线段
        /// </summary>
        Line,
        /// <summary>
        /// 折线
        /// </summary>
        PolyLine,
        /// <summary>
        /// 圆
        /// </summary>
        Circle,
        /// <summary>
        /// 矩形
        /// </summary>
        Rectangle,
        /// <summary>
        /// 椭圆
        /// </summary>
        Ellipse,
        /// <summary>
        /// 多边形
        /// </summary>
        Polygon,
        /// <summary>
        /// 文本
        /// </summary>
        Text,
        /// <summary>
        /// 十字
        /// </summary>
        Cross,
        /// <summary>
        /// 箭头
        /// </summary>
        Arrow,
        /// <summary>
        /// 圆环
        /// </summary>
        Ring,
        /// <summary>
        /// 圆弧
        /// </summary>
        Arc,
    }

    /// <summary>
    /// 图像控件显示状态
    /// </summary>
    public enum ImageElementCtlStatus
    {
        /// <summary>
        /// 不显示任何图元
        /// </summary>
        None = 0,
        /// <summary>
        /// 显示所有图元
        /// </summary>
        ShowAll = 1,
        /// <summary>
        /// 仅显示选中的图元
        /// </summary>
        ShowSelected = 2,
    }

    /// <summary>
    /// 图像控件鼠标状态
    /// </summary>
    public enum ImageCtlMouseStatus
    {
        /// <summary>
        /// 正常状态
        /// </summary>
        Normal,
        /// <summary>
        /// 绘制状态
        /// </summary>
        Drawing,
        /// <summary>
        /// 选择状态
        /// </summary>
        Selecting,
        /// <summary>
        /// 拖拽状态
        /// </summary>
        Dragging,
    }
}

