using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 图像控件辅助类 - 简化常用操作
    /// </summary>
    public static class ImageControlHelper
    {
        /// <summary>
        /// 快速创建并配置图像控件
        /// </summary>
        /// <param name="cameraId">相机ID</param>
        /// <param name="mmPerPixel">像素当量（1像素对应的毫米数）</param>
        /// <param name="imageWidth">图像宽度（像素）</param>
        /// <param name="imageHeight">图像高度（像素）</param>
        /// <param name="allowMouseScroll">是否允许鼠标滚轮缩放</param>
        /// <returns>配置好的图像控件</returns>
        public static CtlOnlyShowImage CreateImageControl(
            int cameraId = 0,
            Point? mmPerPixel = null,
            int imageWidth = 1024,
            int imageHeight = 768,
            bool allowMouseScroll = true)
        {
            var control = new CtlOnlyShowImage(cameraId);
            control.AllowMouseScroll = allowMouseScroll;

            if (mmPerPixel.HasValue)
            {
                control.SetCameraCalib(mmPerPixel.Value, imageWidth, imageHeight);
            }

            return control;
        }

        /// <summary>
        /// 快速创建圆形图元
        /// </summary>
        public static PaintElement CreateCircle(
            double centerX, double centerY, double radius,
            Color color, double lineWidth = 2.0, bool isFill = false)
        {
            return new PaintElement
            {
                Type = PaintElementType.Circle,
                Pts = new List<double> { centerX, centerY, centerX + radius, centerY },
                Color = color,
                LineWidth = lineWidth,
                IsFill = isFill,
                Visible = true
            };
        }

        /// <summary>
        /// 快速创建线段图元
        /// </summary>
        public static PaintElement CreateLine(
            double x1, double y1, double x2, double y2,
            Color color, double lineWidth = 2.0)
        {
            return new PaintElement
            {
                Type = PaintElementType.Line,
                Pts = new List<double> { x1, y1, x2, y2 },
                Color = color,
                LineWidth = lineWidth,
                Visible = true
            };
        }

        /// <summary>
        /// 快速创建矩形图元
        /// </summary>
        public static PaintElement CreateRectangle(
            double x1, double y1, double x2, double y2,
            Color color, double lineWidth = 2.0, bool isFill = false)
        {
            return new PaintElement
            {
                Type = PaintElementType.Rect,
                Pts = new List<double> { x1, y1, x2, y2 },
                Color = color,
                LineWidth = lineWidth,
                IsFill = isFill,
                Visible = true
            };
        }

        /// <summary>
        /// 快速创建十字图元
        /// </summary>
        public static PaintElement CreateCross(
            double x, double y, Color color, double lineWidth = 2.0)
        {
            return new PaintElement
            {
                Type = PaintElementType.Cross,
                Pts = new List<double> { x, y },
                Color = color,
                LineWidth = lineWidth,
                Visible = true
            };
        }

        /// <summary>
        /// 快速创建点图元
        /// </summary>
        public static PaintElement CreatePoint(
            double x, double y, Color color, double size = 3.0)
        {
            return new PaintElement
            {
                Type = PaintElementType.Dot,
                Pts = new List<double> { x, y },
                Color = color,
                LineWidth = size,
                Visible = true
            };
        }

        /// <summary>
        /// 快速创建文本图元
        /// </summary>
        public static PaintElement CreateText(
            double x, double y, string text, Color color, double fontSize = 12.0)
        {
            return new PaintElement
            {
                Type = PaintElementType.Text,
                Pts = new List<double> { x, y },
                Text = text,
                FontSize = fontSize,
                Color = color,
                Visible = true
            };
        }

        /// <summary>
        /// 快速创建箭头图元
        /// </summary>
        public static PaintElement CreateArrow(
            double x1, double y1, double x2, double y2,
            Color color, double lineWidth = 2.0)
        {
            return new PaintElement
            {
                Type = PaintElementType.Arrow,
                Pts = new List<double> { x1, y1, x2, y2 },
                Color = color,
                LineWidth = lineWidth,
                Visible = true
            };
        }

        /// <summary>
        /// 显示图像（从文件路径）
        /// </summary>
        public static int ShowImageFromFile(CtlOnlyShowImage control, int cameraId, string filePath)
        {
            try
            {
                using var stream = System.IO.File.OpenRead(filePath);
                var bitmap = new Bitmap(stream);
                var eventArgs = new ReceiveBitmapEventArgs(cameraId, bitmap);
                return control.ShowImage(eventArgs);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 显示图像（从 Bitmap）
        /// </summary>
        public static int ShowImageFromBitmap(CtlOnlyShowImage control, int cameraId, Bitmap bitmap)
        {
            if (bitmap == null)
                return -1;

            var eventArgs = new ReceiveBitmapEventArgs(cameraId, bitmap);
            return control.ShowImage(eventArgs);
        }

        /// <summary>
        /// 批量添加图元并刷新显示
        /// </summary>
        public static void AddPaintElements(CtlOnlyShowImage control, params PaintElement[] elements)
        {
            var currentElements = new List<PaintElement>();
            
            // 获取现有图元（如果有）
            // 注意：这里需要访问私有成员，实际使用时可能需要通过其他方式获取
            
            // 添加新图元
            foreach (var element in elements)
            {
                if (element != null)
                    currentElements.Add(element);
            }

            control.SetPaintElements(currentElements);
            control.CtlShowPaintStatus = ImageElementCtlStatus.ShowDragImageAndLayer;
            control.ReFresh();
        }

        /// <summary>
        /// 清除所有图元
        /// </summary>
        public static void ClearPaintElements(CtlOnlyShowImage control)
        {
            control.SetPaintElements(new List<PaintElement>());
            control.ReFresh();
        }

        /// <summary>
        /// 设置简化的标定参数（像素当量）
        /// </summary>
        public static void SetSimpleCalibration(
            CtlOnlyShowImage control,
            double mmPerPixelX,
            double mmPerPixelY,
            int imageWidth,
            int imageHeight)
        {
            var mmPerPixel = new Point(mmPerPixelX, mmPerPixelY);
            control.SetCameraCalib(mmPerPixel, imageWidth, imageHeight);
        }

        /// <summary>
        /// 设置等比例标定参数（X和Y方向像素当量相同）
        /// </summary>
        public static void SetUniformCalibration(
            CtlOnlyShowImage control,
            double mmPerPixel,
            int imageWidth,
            int imageHeight)
        {
            SetSimpleCalibration(control, mmPerPixel, mmPerPixel, imageWidth, imageHeight);
        }
    }
}

