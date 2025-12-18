using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;

namespace AvaloniaVisionControl
{
    public struct PointD
    {
        public double X;
        public double Y;

        public PointD(double x, double y) : this()
        {
            this.X = x;
            this.Y = y;
        }
    }
    public struct BoardCalibData
    {
        public double m_dStartX;
        public double m_dStartY;
        public double m_dStepX;
        public double m_dStepY;
        public double m_dEndX;
        public double m_dEndY;
        public double m_dXRange;
        public double m_dYRange;
        public int m_iTotalXNum;
        public int m_iTotalYNum;
        public int m_MovType;
        public List<Tuple<int, double, double>> m_VisionResultMap;
        public List<Tuple<int, double, double>> m_IndexShotPosMap;
    }

    // 逆变换参数结构（替代元组）
    public struct InverseParams
    {
        public double InvA { get; set; }
        public double InvB { get; set; }
        public double InvC { get; set; }
        public double InvD { get; set; }
        public double InvE { get; set; }
        public double InvF { get; set; }
    }
    /// <summary>
    /// 仿射变换参数类
    /// </summary>
    public class AffineTransformParams
    {
        public double A { get; set; }  // x' = a*x + b*y + c
        public double B { get; set; }
        public double C { get; set; }
        public double D { get; set; }  // y' = d*x + e*y + f
        public double E { get; set; }
        public double F { get; set; }

        // 逆变换参数（缓存，避免重复计算）
        private double? _invDet = null;
        private double? _invA = null;
        private double? _invB = null;
        private double? _invC = null;
        private double? _invD = null;
        private double? _invE = null;
        private double? _invF = null;

        public void CalculateInverseParameters()
        {
            double det = A * E - B * D;

            if (Math.Abs(det) < 1e-15)
                throw new InvalidOperationException("变换矩阵不可逆，无法计算逆变换");

            _invDet = det;
            _invA = E / det;
            _invB = -B / det;
            _invC = (B * F - C * E) / det;
            _invD = -D / det;
            _invE = A / det;
            _invF = (C * D - A * F) / det;
        }

        public InverseParams GetInverseParameters()
        {
            if (!_invDet.HasValue)
                CalculateInverseParameters();

            return new InverseParams
            {
                InvA = _invA.Value,
                InvB = _invB.Value,
                InvC = _invC.Value,
                InvD = _invD.Value,
                InvE = _invE.Value,
                InvF = _invF.Value
            };
        }
    }
    // 仿射变换工具类
    internal static class AffineTransform
    {
        // 接口1：计算仿射变换参数
        internal static AffineTransformParams CalculateTransform(
            List<PointD> sourcePoints,
            List<PointD> targetPoints)
        {
            if (sourcePoints == null || targetPoints == null)
                throw new ArgumentNullException("点列表不能为null");

            if (sourcePoints.Count != targetPoints.Count)
                throw new ArgumentException("原始点和目标点数量必须相同");

            if (sourcePoints.Count < 3)
                throw new ArgumentException("至少需要3个点对来计算仿射变换");

            // 构建最小二乘法的矩阵方程：A * x = b
            int n = sourcePoints.Count;
            double[,] A = new double[2 * n, 6];
            double[] b = new double[2 * n];

            for (int i = 0; i < n; i++)
            {
                PointD src = sourcePoints[i];
                PointD tgt = targetPoints[i];

                // x' = a*x + b*y + c
                A[2 * i, 0] = src.X;
                A[2 * i, 1] = src.Y;
                A[2 * i, 2] = 1;
                A[2 * i, 3] = 0;
                A[2 * i, 4] = 0;
                A[2 * i, 5] = 0;
                b[2 * i] = tgt.X;

                // y' = d*x + e*y + f
                A[2 * i + 1, 0] = 0;
                A[2 * i + 1, 1] = 0;
                A[2 * i + 1, 2] = 0;
                A[2 * i + 1, 3] = src.X;
                A[2 * i + 1, 4] = src.Y;
                A[2 * i + 1, 5] = 1;
                b[2 * i + 1] = tgt.Y;
            }

            // 使用最小二乘法求解：x = (A^T * A)^-1 * A^T * b
            try
            {
                double[,] result = SolveLeastSquares(A, b);

                return new AffineTransformParams
                {
                    A = result[0, 0],
                    B = result[1, 0],
                    C = result[2, 0],
                    D = result[3, 0],
                    E = result[4, 0],
                    F = result[5, 0]
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("计算仿射变换参数失败", ex);
            }
        }

        // 接口2：正变换
        public static PointD TransformPoint(PointD point, AffineTransformParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            double x = point.X * parameters.A + point.Y * parameters.B + parameters.C;
            double y = point.X * parameters.D + point.Y * parameters.E + parameters.F;

            return new PointD(x, y);
        }

        // 接口3：逆变换
        public static PointD InverseTransformPoint(PointD point, AffineTransformParams parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            // 获取逆变换参数
            InverseParams inverseParams = parameters.GetInverseParameters();

            double x = point.X * inverseParams.InvA + point.Y * inverseParams.InvB + inverseParams.InvC;
            double y = point.X * inverseParams.InvD + point.Y * inverseParams.InvE + inverseParams.InvF;

            return new PointD(x, y);
        }

        // 最小二乘法求解
        private static double[,] SolveLeastSquares(double[,] A, double[] b)
        {
            int rows = A.GetLength(0);
            int cols = A.GetLength(1);

            // 计算 A^T * A
            double[,] ATA = new double[cols, cols];
            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < rows; k++)
                    {
                        sum += A[k, i] * A[k, j];
                    }
                    ATA[i, j] = sum;
                }
            }

            // 计算 A^T * b
            double[] ATb = new double[cols];
            for (int i = 0; i < cols; i++)
            {
                double sum = 0;
                for (int k = 0; k < rows; k++)
                {
                    sum += A[k, i] * b[k];
                }
                ATb[i] = sum;
            }

            // 求解线性方程组 ATA * x = ATb
            return SolveLinearSystem(ATA, ATb);
        }

        // 求解线性方程组（高斯消元法）
        private static double[,] SolveLinearSystem(double[,] matrix, double[] vector)
        {
            int n = vector.Length;
            double[,] result = new double[n, 1];

            // 增广矩阵
            double[,] augmented = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    augmented[i, j] = matrix[i, j];
                }
                augmented[i, n] = vector[i];
            }

            // 前向消元
            for (int i = 0; i < n; i++)
            {
                // 寻找主元
                int maxRow = i;
                for (int k = i + 1; k < n; k++)
                {
                    if (Math.Abs(augmented[k, i]) > Math.Abs(augmented[maxRow, i]))
                    {
                        maxRow = k;
                    }
                }

                // 交换行
                for (int k = i; k <= n; k++)
                {
                    double temp = augmented[i, k];
                    augmented[i, k] = augmented[maxRow, k];
                    augmented[maxRow, k] = temp;
                }

                // 检查主元是否为0
                if (Math.Abs(augmented[i, i]) < 1e-15)
                {
                    throw new InvalidOperationException("矩阵奇异，无法求解线性方程组");
                }

                // 消元
                for (int k = i + 1; k < n; k++)
                {
                    double factor = augmented[k, i] / augmented[i, i];
                    for (int j = i; j <= n; j++)
                    {
                        augmented[k, j] -= factor * augmented[i, j];
                    }
                }
            }

            // 回代
            for (int i = n - 1; i >= 0; i--)
            {
                result[i, 0] = augmented[i, n];
                for (int j = i + 1; j < n; j++)
                {
                    result[i, 0] -= augmented[i, j] * result[j, 0];
                }
                result[i, 0] /= augmented[i, i];
            }

            return result;
        }
    }


    /// <summary>
    /// 用来做棋盘校正标定数据的转换
    /// </summary>
    public class AffineCalib
    {
        public AffineCalib()
        {

        }
        private BoardCalibData Data;
        private Dictionary<int, Point> VisionRstDic;
        private Dictionary<int, Point> ShotPosDic;
        /// <summary>
        /// 读取标定文件，注意里面的视觉坐标，都是相对于视野中心的机械坐标偏差
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public int Read(string path)
        {
            try
            {
                string jsonStr = File.ReadAllText(path);
                Data = JsonConvert.DeserializeObject<BoardCalibData>(jsonStr);
                VisionRstDic = new Dictionary<int, Point>();
                ShotPosDic = new Dictionary<int, Point>();
                foreach (var rst in Data.m_VisionResultMap)
                {
                    VisionRstDic[rst.Item1] = new Point(rst.Item2, rst.Item3);
                }
                foreach (var rst in Data.m_IndexShotPosMap)
                {
                    ShotPosDic[rst.Item1] = new Point(rst.Item2, rst.Item3);
                }
            }
            catch (Exception e) { return -1; }

            return 0;
        }
        /// <summary>
        /// 从设备坐标系转到棋盘坐标系，输出相对偏差，需要先调用 AngleNormalization
        /// </summary>
        /// <param name="machPos">补偿之前的机械坐标</param>
        /// <param name="boardCoordOffset">补偿之后的坐标</param>
        /// <returns></returns>
        public int MachToBoardCoord(PointD machPos, out PointD boardCoordOffset)
        {
            boardCoordOffset = new PointD(0, 0);
            List<int> outIndexList;
            if (Get4NearIndex(out outIndexList, machPos))
            {
                //检查对应的值是否存在
                if (outIndexList.Count < 4)
                    return -3;
                //检查结束，开始正式计算偏差
                double scale0y = 0.5;
                double scale0x = 0.5;
                //计算与0.Y轴的比例
                scale0y = Math.Abs(ShotPosDic[outIndexList[1]].X - machPos.X) / Data.m_dStepX;
                //计算与0.X轴的比例
                scale0x = Math.Abs(ShotPosDic[outIndexList[2]].Y - machPos.Y) / Data.m_dStepY;
                //安全判断，比例最大为1
                if (scale0y > 1)
                    scale0y = 0.999;
                if (scale0x > 1)
                    scale0x = 0.999;
                //计算X方向总偏差
                var p0 = VisionRstDic[outIndexList[0]];
                var p1 = VisionRstDic[outIndexList[1]];
                var p2 = VisionRstDic[outIndexList[2]];
                var p3 = VisionRstDic[outIndexList[3]];
                double xOffset = scale0y * (scale0x * p0.X + (1 - scale0x) * p2.X)
                    + (1 - scale0y) * (scale0x * p1.X + (1 - scale0x) * p3.X);
                double yOffset = scale0y * (scale0x * p0.Y + (1 - scale0x) * p2.Y)
                    + (1 - scale0y) * (scale0x * p1.Y + (1 - scale0x) * p3.Y);
                boardCoordOffset.X = (float)xOffset;
                boardCoordOffset.Y = (float)yOffset;
            }
            else
                return -2;
            //获得
            return 0;
        }

        public AffineTransformParams AngleNormalization(bool distanceNormal = false)
        {
            var sourceList = new List<PointD>();
            var targetList = new List<PointD>();
            for (int i = 0; i < Data.m_VisionResultMap.Count; i++)
            {
                var rst = Data.m_VisionResultMap[i];
                var mach = Data.m_IndexShotPosMap[i];
                sourceList.Add(new PointD(rst.Item2 + mach.Item2, rst.Item3 + mach.Item3));
                targetList.Add(new PointD(mach.Item2, mach.Item3));
            }

            // 接口1：计算变换参数
            AffineTransformParams parameters = AffineTransform.CalculateTransform(
                sourceList, targetList);
            //A和E固定设为1
            if (distanceNormal)
            {
                parameters.A = 1;
                parameters.E = 1;
            }
            // 接口2：正变换测试
            for (int i = 0; i < Data.m_VisionResultMap.Count; i++)
            {
                var rst = Data.m_VisionResultMap[i];
                var mach = Data.m_IndexShotPosMap[i];
                var testPoint = new PointD(rst.Item2 + mach.Item2, rst.Item3 + mach.Item3);
                PointD transformed = AffineTransform.TransformPoint(testPoint, parameters);
                var lastPt = new PointD(transformed.X - mach.Item2, transformed.Y - mach.Item3);
                VisionRstDic[i] = new Point(lastPt.X, lastPt.Y);
            }

            return parameters;
        }
        /// <summary>
        /// 获得所给的机械坐标最近的4个点下标，如果在范围外，这4个下标会有重复的情况出现
        /// </summary>
        /// <param name="outIndexVec"></param>
        /// <param name="inPos"></param>
        /// <returns></returns>
        private bool Get4NearIndex(out List<int> outIndexVec, PointD inPos)
        {
            outIndexVec = new List<int>();
            //计算第几行第几列
            double currXRange = inPos.X - Data.m_dStartX;
            double currYRange = inPos.Y - Data.m_dStartY;
            bool fixX = false;
            int fixXIndex = 0;
            bool fixY = false;
            int fixYIndex = 0;
            if (currXRange < 0 || currXRange > Data.m_dXRange)
            {
                fixX = true;
                if (currXRange < 0)
                {
                    fixXIndex = 0;
                }
                else
                {
                    fixXIndex = Data.m_iTotalXNum - 1;
                }
            }
            if (currYRange < 0 || currYRange > Data.m_dYRange)
            {
                fixY = true;
                if (currYRange < 0)
                {
                    fixYIndex = 0;
                }
                else
                {
                    fixYIndex = Data.m_iTotalYNum - 1;
                }
            }
            int colIndex = (int)(currXRange / Data.m_dStepX);
            int rowIndex = (int)(currYRange / Data.m_dStepY);
            int colIndexNext = colIndex + 1;
            int rowIndexNext = rowIndex + 1;
            if (fixX)
            {
                colIndex = fixXIndex;
                colIndexNext = fixXIndex;
            }
            if (fixY)
            {
                rowIndex = fixYIndex;
                rowIndexNext = fixYIndex;
            }
            outIndexVec.Clear();

            outIndexVec.Add(colIndex + rowIndex * Data.m_iTotalXNum);
            outIndexVec.Add(colIndexNext + rowIndex * Data.m_iTotalXNum);
            outIndexVec.Add(colIndex + rowIndexNext * Data.m_iTotalXNum);
            outIndexVec.Add(colIndexNext + rowIndexNext * Data.m_iTotalXNum);
            return true;
        }
    }
}
