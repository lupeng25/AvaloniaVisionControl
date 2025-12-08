using System;
using Avalonia;
using Avalonia.Media.Imaging;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 接收图像事件参数
    /// </summary>
    public class ReceiveBitmapEventArgs : EventArgs
    {
        /// <summary>
        /// 相机 ID
        /// </summary>
        public int CamID { get; }
        
        /// <summary>
        /// 图像数据
        /// </summary>
        public Bitmap Image { get; }

        public ReceiveBitmapEventArgs(int camId, Bitmap image)
        {
            CamID = camId;
            Image = image;
        }
    }

    /// <summary>
    /// 鼠标左键单击事件参数
    /// </summary>
    public class ImageClickEventArgs : EventArgs
    {
        /// <summary>
        /// 鼠标在控件中的位置（控件坐标）
        /// </summary>
        public Point ControlPosition { get; }
        
        /// <summary>
        /// 鼠标在图像中的位置（图像原始坐标，已考虑缩放和偏移）
        /// </summary>
        public Point ImagePosition { get; }

        public ImageClickEventArgs(Point controlPosition, Point imagePosition)
        {
            ControlPosition = controlPosition;
            ImagePosition = imagePosition;
        }
    }
}

