using Avalonia;
using System;
using System.Collections.Generic;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图像控件显示状态
    /// </summary>
    public enum ImageElementCtlStatus
    {
        /// <summary>
        /// /默认，显示图片，可放大缩小
        /// </summary>
        ShowDragImage,
        /// <summary>
        /// 显示图片和其上的图层，如虚拟点胶轨迹，可显示渐进图层
        /// </summary>
        ShowDragImageAndLayer,  
        /// <summary>
        /// 可以左键选中图层，不允许放大缩小
        /// </summary>
        SelectLayer,  
        /// <summary>
        /// 可以左键选中图层，以及放大缩小图片
        /// </summary>
        SelectLayerAndDragImage,  
        /// <summary>
        /// 可以左键选中、新增、删除 图层，不允许放大缩小
        /// </summary>
        EditLayer, 
        /// <summary>
        /// 可以左键选中、新增、删除 图层，以及放大缩小图片
        /// </summary>
        EditLayerAndDragImage,  
    }

    /// <summary>
    /// 图像控件鼠标状态
    /// </summary>
    public enum ImageCtlMouseStatus
    {
        /// <summary>
        /// 无操作
        /// </summary>
        None, 
        /// <summary>
        /// 放大缩小、拖拽图片
        /// </summary>
        ScaleImage, 
        /// <summary>
        /// 鼠标左键点击运动
        /// </summary>
        LeftClickMove, 
        /// <summary>
        /// 鼠标右键点击运动
        /// </summary>
        RightClickMove, 
    }

    /// <summary>
    /// 图元类型枚举
    /// </summary>
    public enum PaintElementType
    {
        /// <summary>
        /// 不显示
        /// </summary>
        Null, 
        /// <summary>
        /// 点
        /// </summary>
        Dot,
        /// <summary>
        /// 矩形
        /// </summary>
        Rect,
        /// <summary>
        /// 线
        /// </summary>
        Line,
        /// <summary>
        /// 圆弧
        /// </summary>
        Arc,
        /// <summary>
        /// 普通圆
        /// </summary>
        Circle, 
        /// <summary>
        /// 实心圆
        /// </summary>
        FilledCircle, 
        /// <summary>
        /// 多线段
        /// </summary>
        PolyLine,  
        /// <summary>
        /// 带文字的圆，常用于显示Mark点位置、贴装位置等信息
        /// </summary>
        TextCircle, 
        /// <summary>
        /// 带文字的矩形，常用于显示相机视野
        /// </summary>
        TextRect, 
        
        // 保留 Avalonia 原有的一些类型以防万一，但尽量使用 ShareMemRPC 的命名
        // ShareMemRPC 没有 Ellipse, Polygon, Cross, Arrow, Ring
        // 如果需要保留，可以放在后面
        /// <summary>
        /// 椭圆
        /// </summary>
        Ellipse,
        /// <summary>
        /// 多边形
        /// </summary>
        Polygon,
        /// <summary>
        /// 十字
        /// </summary>
        Cross,
        /// <summary>
        /// 箭头
        /// </summary>
        Arrow,
        /// <summary>
        /// 环
        /// </summary>
        Ring,
        /// <summary>
        /// 文字
        /// </summary>
        Text, // ShareMemRPC 没有 Text，但有 TextCircle/TextRect
    }

    /// <summary>
    /// 图元逻辑指令类型
    /// </summary>
    public enum ElementLogicOrderType
    {
        Null, //无操作
        PaintElement, //绘制普通图元
        MoveNoPaint, //只运动，不绘制任何
        SetSubParam, //设置后续一条或N条逻辑指令的参数
        Translation, //平移
        Delay,  //延时
        Mirror, //镜像
        LoopBegin, //指示需要阵列的指令开始
        LoopEnd, //指示需要阵列的指令结束
        CamMark, //相机Mark
        Cam2D, //相机检测类
    }

    public delegate void RunSingleElementDelegate(PaintElement before, PaintElement curr, PaintElement last,
        ref RunElementLogicCenter runCenter, out int outputInt, out string sErrMsg);

    /// <summary>
    /// 存放所有 ElementLogic 运算的中间数据，类似视觉库的CRunCenter
    /// </summary>
    public class RunElementLogicCenter
    {
        public Dictionary<string, string> m_StrDic = new Dictionary<string, string>();
        /// <summary>
        /// 上一次指令结束后所在的位置
        /// </summary>
        public Point LastElementPos = new Point();
        public void Clear()
        {
            m_StrDic.Clear();
            LastElementPos = new Point();
            ModelName = "";
        }
        public string ModelName;
    }

}
