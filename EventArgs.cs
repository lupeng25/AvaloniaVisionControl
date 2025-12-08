using System;
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
}

