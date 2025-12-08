using System;
using Avalonia;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 运动管理器（单例模式）
    /// 管理当前机械平台的位置信息
    /// </summary>
    public class MotionMgr
    {
        private static readonly MotionMgr ins = new MotionMgr();
        public static MotionMgr Ins => ins;

        private MotionMgr() { }

        /// <summary>
        /// 当前机械位置（X, Y 单位：mm）
        /// </summary>
        public Point CurrMachPos { get; set; } = new Point(0, 0);

        /// <summary>
        /// 当前机械位置改变事件
        /// </summary>
        public event EventHandler CurrMachPosChanged;

        /// <summary>
        /// 更新当前机械位置
        /// </summary>
        /// <param name="x">X 坐标（mm）</param>
        /// <param name="y">Y 坐标（mm）</param>
        public void UpdateMachPos(double x, double y)
        {
            var newPt = new Point(x, y);
            if (Math.Abs(newPt.X - CurrMachPos.X) < 0.002 || Math.Abs(newPt.Y - CurrMachPos.Y) < 0.002)
            {
                // 触发坐标改变的事件
                CurrMachPos = newPt;
                CurrMachPosChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

