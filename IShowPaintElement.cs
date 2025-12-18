using System;
using System.Collections.Generic;
using Avalonia;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图元显示接口
    /// 定义图像控件如何显示和管理图元
    /// </summary>
    public interface IShowPaintElement
    {
        /// <summary>
        /// 控件显示图元的状态
        /// </summary>
        ImageElementCtlStatus CtlShowPaintStatus { get; set; }

        /// <summary>
        /// 控件鼠标状态
        /// </summary>
        ImageCtlMouseStatus CtlMouseStatus { get; set; }

        /// <summary>
        /// 设置相机标定参数（通过文件路径）
        /// </summary>
        /// <param name="calibFilePath">标定文件路径</param>
        /// <returns>0 表示成功，负数表示失败</returns>
        int SetCameraCalib(string calibFilePath);

        /// <summary>
        /// 设置相机标定参数（像素到机械坐标的变换矩阵）
        /// </summary>
        /// <param name="matrixPixToMM">像素→mm 的 3×3 仿射变换矩阵（9 元素数组）</param>
        /// <returns>0 表示成功，负数表示失败</returns>
        int SetCameraCalib(double[] matrixPixToMM);

        /// <summary>
        /// 简单标定：设置像素当量
        /// </summary>
        /// <param name="MMpix">像素当量（1像素代表多少mm）</param>
        /// <param name="imgWidth">图像宽度</param>
        /// <param name="imgHeight">图像高度</param>
        /// <param name="xRever">X轴反转（-1反转，1不反转）</param>
        /// <param name="yRever">Y轴反转（-1反转，1不反转）</param>
        /// <returns></returns>
        int SetCameraCalib(Point MMpix, int imgWidth, int imgHeight, int xRever = -1, int yRever = -1);

        /// <summary>
        /// 设置相机标定参数（机械坐标到像素的变换矩阵）
        /// </summary>
        /// <param name="matrixMMToPix">mm→像素 的 3×3 仿射变换矩阵（9 元素数组）</param>
        /// <returns>0 表示成功，负数表示失败</returns>
        int SetCameraCalibRef(double[] matrixMMToPix);

        /// <summary>
        /// 设置更新相机位置的回调函数
        /// </summary>
        /// <param name="getPosFunc">获取位置的函数</param>
        /// <returns>0 表示成功，负数表示失败</returns>
        int SetUpdateCameraPos(Func<Point> getPosFunc);

        /// <summary>
        /// 设置要显示的图元列表
        /// </summary>
        /// <param name="needShowElement">图元列表</param>
        /// <returns>0 表示成功，负数表示失败</returns>
        int SetPaintElements(List<PaintElement> needShowElement);

        /// <summary>
        /// 改变单个图元的参数
        /// </summary>
        /// <param name="index">图元索引</param>
        /// <param name="element">新的图元数据</param>
        /// <returns>0 表示成功，负数表示失败</returns>
        int ChangePaintElement(int index, PaintElement element);

        /// <summary>
        /// 刷新显示
        /// </summary>
        void ReFresh();
    }
}

