using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 运动方向的类型
    /// </summary>
    public enum MoveVecEnum
    {
        ToLeft,
        ToTop,
        ToRight,
        ToDown,
        ZUp,
        ZDown,
        //R轴增加
        RPlus,
        //R轴减少
        RMinus
    }

    public delegate void FuncAxisOperation(AxisOperationType order, AxisType axis, List<double> inParams,
        out int outputInt, out string sErrMsg);

    public delegate void FuncGetAxisPos(AxisType axis, int axisIndex,
        out double pos1, out double pos2, out int outErr);

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
        private Point m_currMachPos = new Point(0, 0);

        public Point CurrMachPos
        {
            get => m_currMachPos;
            private set
            {
                var newPt = value;
                if (Math.Abs(newPt.X - m_currMachPos.X) > 0.002 || Math.Abs(newPt.Y - m_currMachPos.Y) > 0.002)
                {
                    m_currMachPos = newPt;
                    CurrMachPosChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

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
            CurrMachPos = new Point(x, y);
        }

        /// <summary>
        /// 手动调试用
        /// </summary>
        /// <param name="machPos"></param>
        public void UpdateCurrMachPos(Point machPos)
        {
            CurrMachPos = machPos;
        }

        /// <summary>
        /// RPC内部调用运控使用此接口
        /// </summary>
        public FuncAxisOperation AxisFunc { get; set; } = null;

        public double CurrJogDis { get; set; } = 5;
        public double CurrJogVec { get; set; } = 10;

        /// <summary>
        /// 获得可以寸动和连续运动的按钮
        /// </summary>
        /// <param name="btn"></param>
        /// <returns></returns>
        public int GetMotionJogButton(ref Button btn, MoveVecEnum moveVec,
            double jogDis, AxisType assertAxis = AxisType.Null)
        {
            // 使用 PointerPressed 来模拟 MouseDown，确保“按下即动”
            btn.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (sender, e) =>
            {
                // 只有左键才触发
                var point = e.GetCurrentPoint(btn);
                if (point.Properties.IsLeftButtonPressed)
                {
                    CurrJogDis = jogDis;
                    var button = (sender as Button);
                    ButtonPerformClick(button, moveVec, assertAxis);
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);

            return 0;
        }

        public void ButtonPerformClick(Button button, MoveVecEnum moveVec, AxisType assertAxis = AxisType.Null)
        {
            if (button == null)
                return;
            button.IsEnabled = false;
            Task.Run(() =>
            {
                AxisType axis = assertAxis;
                // 单次移动
                if (axis == AxisType.Null)
                {
                    switch (moveVec)
                    {
                        case MoveVecEnum.ToLeft:
                            axis = AxisType.X;
                            CurrJogDis = Math.Abs(CurrJogDis);
                            break;
                        case MoveVecEnum.ToTop:
                            axis = AxisType.Y;
                            CurrJogDis = Math.Abs(CurrJogDis) * -1;
                            break;
                        case MoveVecEnum.ToRight:
                            axis = AxisType.X;
                            CurrJogDis = Math.Abs(CurrJogDis) * -1;
                            break;
                        case MoveVecEnum.ToDown:
                            axis = AxisType.Y;
                            CurrJogDis = Math.Abs(CurrJogDis);
                            break;
                        case MoveVecEnum.ZUp:
                            axis = AxisType.Z;
                            CurrJogDis = Math.Abs(CurrJogDis) * -1;
                            break;
                        case MoveVecEnum.ZDown:
                            axis = AxisType.Z;
                            CurrJogDis = Math.Abs(CurrJogDis);
                            break;
                        case MoveVecEnum.RMinus:
                            axis = AxisType.EX0;
                            CurrJogDis = Math.Abs(CurrJogDis) * -1;
                            break;
                        case MoveVecEnum.RPlus:
                            axis = AxisType.EX0;
                            CurrJogDis = Math.Abs(CurrJogDis);
                            break;
                        default:
                            break;
                    }
                }
                AbsMove(axis);
                
                // Avalonia UI Thread Invoke
                Dispatcher.UIThread.Post(() =>
                {
                    button.IsEnabled = true;
                });
            });
        }

        private void AbsMove(AxisType axisType)
        {
            if (AxisFunc == null) return;

            var list = new List<double>();
            list.Add(CurrJogDis);
            list.Add(CurrJogVec);
            int outInt;
            string sErr;
            AxisFunc(AxisOperationType.RelMove, axisType, list, out outInt, out sErr);
        }
    }
}
