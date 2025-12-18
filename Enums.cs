using System;

namespace AvaloniaVisionControl
{
    /// <summary>
    /// 轴操作类型
    /// </summary>
    public enum AxisOperationType
    {
        Null,
        GoHome,
        Move,
        RelMove,
        WaitMoveEnd,
        MoveAndWait,
        RelMoveAndWait,
        ArcMove,
        CircleMove,
        SetIO,
        GetIO,
        StopMotion,
    }

    /// <summary>
    /// 轴类型
    /// </summary>
    public enum AxisType
    {
        Null,
        X,
        Y,
        Z,
        XY,
        EX0,
        EX1,
        EX2,
        EX3,
        EX4
    }
}
