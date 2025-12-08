using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace AvaloniaVisionControl
{ 
    /// <summary>
    /// CtlOnlyShowImage 的坐标转换和图元绘制部分（partial class）
    /// </summary>
    public partial class CtlOnlyShowImage
    {
        /// <summary>
        /// 机械坐标到像素坐标的仿射变换矩阵（3×3，存储为 9 元素数组）
        /// </summary>
        private double[] m_9MMToPixMatrix = new double[9];

        /// <summary>
        /// 当前需要显示的图元列表
        /// </summary>
        private List<PaintElement> m_CurrShowElement = new List<PaintElement>();

        /// <summary>
        /// 线宽缩放比例（根据仿射变换计算）
        /// </summary>
        private double m_lineWidthScale = 1;

        /// <summary>
        /// 将机械坐标转换为控件坐标
        /// </summary>
        /// <param name="originData">原始机械坐标列表 [x1, y1, x2, y2, ...]</param>
        /// <param name="type">图元类型</param>
        /// <param name="imageRect">图像在控件中的矩形区域</param>
        /// <returns>转换后的控件坐标列表</returns>
        protected List<float> GetTransedPts(List<double> originData, PaintElementType type, Rect imageRect)
        {
            var ptList = new List<Point>();
            
            // 将坐标转换为 Point 列表
            double tempV = 0;
            for (int i = 0; i < originData.Count; i++)
            {
                if (i % 2 == 1)
                {
                    ptList.Add(new Point(tempV, originData[i]));
                }
                else
                {
                    tempV = originData[i];
                }
            }

            bool IsInImageRect = false; // 指示是否有任何点在视野范围内
            var newPt = new List<Point>();
            var machPos = MotionMgr.Ins.CurrMachPos;

            for (int i = 0; i < ptList.Count; i++)
            {
                // 转换为相对于视野中心坐标系的坐标
                Point machV = new Point(ptList[i].X - machPos.X, ptList[i].Y - machPos.Y);
                
                // 使用仿射变换矩阵转成图像坐标
                Point pixV = TransformPoint(machV, m_9MMToPixMatrix);
                
                // 图像坐标转控件坐标
                Point ctlV = new Point(
                    pixV.X * _currentZoomFactor + _scrollImageLocation.X,
                    pixV.Y * _currentZoomFactor + _scrollImageLocation.Y
                );

                // 判断点是否在可见区域内
                if (type == PaintElementType.Line || type == PaintElementType.Text)
                {
                    IsInImageRect = true;
                }
                else if (!IsInImageRect)
                {
                    if (ctlV.X >= 0 && ctlV.Y >= 0 &&
                        ctlV.X <= Bounds.Width && ctlV.Y <= Bounds.Height)
                    {
                        IsInImageRect = true;
                    }
                }
                newPt.Add(ctlV);
            }

            if (IsInImageRect)
            {
                var list = new List<float>();
                foreach (var pt in newPt)
                {
                    list.Add((float)pt.X);
                    list.Add((float)pt.Y);
                }
                return list;
            }
            else
                return new List<float>();
        }

        /// <summary>
        /// 控件显示图元的状态
        /// </summary>
        public ImageElementCtlStatus CtlShowPaintStatus { get; set; }

        /// <summary>
        /// 控件鼠标状态
        /// </summary>
        public ImageCtlMouseStatus CtlMouseStatus { get; set; }

        /// <summary>
        /// 计算线宽缩放比例
        /// </summary>
        private void CalcLineWidthScale()
        {
            var zero = TransformPoint(new Point(0, 0), m_9MMToPixMatrix);
            var pt1 = TransformPoint(new Point(1, 0), m_9MMToPixMatrix);
            var xOffset = zero.X - pt1.X;
            var yOffset = zero.Y - pt1.Y;
            m_lineWidthScale = Math.Sqrt(xOffset * xOffset + yOffset * yOffset);
        }

        /// <summary>
        /// 设置相机标定参数（通过文件路径）
        /// </summary>
        public int SetCameraCalib(string calibFilePath)
        {
            // TODO: 实现从文件加载标定参数
            return 0;
        }

        /// <summary>
        /// 设置相机标定参数（像素到机械坐标的变换矩阵）
        /// </summary>
        public int SetCameraCalib(double[] matrixPixToMM)
        {
            m_9MMToPixMatrix = CalculateInverseTransform(matrixPixToMM);
            CalcLineWidthScale();
            return 0;
        }

        /// <summary>
        /// 设置相机标定参数（机械坐标到像素的变换矩阵）
        /// </summary>
        public int SetCameraCalibRef(double[] matrixMMToPix)
        {
            m_9MMToPixMatrix = matrixMMToPix;
            CalcLineWidthScale();
            return 0;
        }

        /// <summary>
        /// 简化版标定：只有 X、Y 像素当量
        /// </summary>
        /// <param name="MMpix">像素当量（若为 0.5，则 1pix=0.5mm）</param>
        /// <param name="imgWidth">图像宽度</param>
        /// <param name="imgHeight">图像高度</param>
        public int SetCameraCalib(Point MMpix, int imgWidth, int imgHeight)
        {
            // 计算仿射变换矩阵
            List<Point> mmPoints = new List<Point>();
            List<Point> pixPoints = new List<Point>();
            int halfX = imgWidth / 2;
            int halfY = imgHeight / 2;

            mmPoints.Add(new Point(-1, 1));
            pixPoints.Add(new Point(halfX - 1.0 / MMpix.X, halfY - 1.0 / MMpix.Y));
            mmPoints.Add(new Point(0, 1));
            pixPoints.Add(new Point(halfX, halfY - 1.0 / MMpix.Y));
            mmPoints.Add(new Point(1, 1));
            pixPoints.Add(new Point(halfX + 1.0 / MMpix.X, halfY - 1.0 / MMpix.Y));
            mmPoints.Add(new Point(-1, 0));
            pixPoints.Add(new Point(halfX - 1.0 / MMpix.X, halfY));
            mmPoints.Add(new Point(0, 0));
            pixPoints.Add(new Point(halfX, halfY));
            mmPoints.Add(new Point(1, 0));
            pixPoints.Add(new Point(halfX + 1.0 / MMpix.X, halfY));
            mmPoints.Add(new Point(-1, -1));
            pixPoints.Add(new Point(halfX - 1.0 / MMpix.X, halfY + 1.0 / MMpix.Y));
            mmPoints.Add(new Point(0, -1));
            pixPoints.Add(new Point(halfX, halfY + 1.0 / MMpix.Y));
            mmPoints.Add(new Point(1, -1));
            pixPoints.Add(new Point(halfX + 1.0 / MMpix.X, halfY + 1.0 / MMpix.Y));

            int ret = CalculateAffineTransformMatrix(mmPoints, pixPoints, out m_9MMToPixMatrix);
            CalcLineWidthScale();
            return ret;
        }

        public int SetUpdateCameraPos(Func<Point> getPosFunc)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 设置要显示的图元列表
        /// </summary>
        public int SetPaintElements(List<PaintElement> needShowElement)
        {
            m_CurrShowElement = needShowElement;
            return 0;
        }

        /// <summary>
        /// 改变单个图元的参数
        /// </summary>
        public int ChangePaintElement(int index, PaintElement element)
        {
            if (m_CurrShowElement.Count > index)
                m_CurrShowElement[index] = element;
            return 0;
        }

        /// <summary>
        /// 刷新显示
        /// </summary>
        public void ReFresh()
        {
            InvalidateVisual();
        }

        /// <summary>
        /// 将图像像素坐标转换为机械坐标（绝对坐标，单位：mm）
        /// </summary>
        /// <param name="imagePixelPosition">图像中的像素坐标</param>
        /// <returns>机械坐标（绝对坐标，单位：mm）</returns>
        public Point ConvertImageToMachinePosition(Point imagePixelPosition)
        {
            if (_originImage == null)
                throw new InvalidOperationException("图像未加载，无法进行坐标转换");

            // 计算图像中心（像素坐标）
            double imageCenterX = _originImage.PixelSize.Width / 2.0;
            double imageCenterY = _originImage.PixelSize.Height / 2.0;

            // 将像素坐标转换为相对于图像中心的坐标
            Point relativePixelPos = new Point(
                imagePixelPosition.X - imageCenterX,
                imagePixelPosition.Y - imageCenterY
            );

            // 计算逆变换矩阵（从像素到机械）
            double[] pixToMMMatrix = CalculateInverseTransform(m_9MMToPixMatrix);

            // 将像素坐标转换为相对于视野中心的机械坐标
            Point relativeMachPos = TransformPoint(relativePixelPos, pixToMMMatrix);

            // 加上当前机械位置，得到绝对机械坐标
            Point absoluteMachPos = new Point(
                relativeMachPos.X + MotionMgr.Ins.CurrMachPos.X,
                relativeMachPos.Y + MotionMgr.Ins.CurrMachPos.Y
            );

            return absoluteMachPos;
        }

        /// <summary>
        /// 计算仿射变换矩阵（使用最小二乘法）
        /// </summary>
        private int CalculateAffineTransformMatrix(
            List<Point> sourcePoints, List<Point> targetPoints, out double[] outMatrix)
        {
            outMatrix = new double[9];
            if (sourcePoints == null || targetPoints == null)
                return -1;

            if (sourcePoints.Count != 9 || targetPoints.Count != 9)
                return -2;

            // 构建最小二乘方程组
            double[,] A = new double[18, 6];
            double[] b = new double[18];

            for (int i = 0; i < 9; i++)
            {
                double x = sourcePoints[i].X;
                double y = sourcePoints[i].Y;
                double xp = targetPoints[i].X;
                double yp = targetPoints[i].Y;

                // x' = a*x + b*y + c
                A[2 * i, 0] = x;
                A[2 * i, 1] = y;
                A[2 * i, 2] = 1;
                b[2 * i] = xp;

                // y' = d*x + e*y + f
                A[2 * i + 1, 3] = x;
                A[2 * i + 1, 4] = y;
                A[2 * i + 1, 5] = 1;
                b[2 * i + 1] = yp;
            }

            // 求解最小二乘问题：A^T * A * X = A^T * b
            double[,] ATA = new double[6, 6];
            double[] ATb = new double[6];

            // 计算 A^T * A
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < 18; k++)
                    {
                        sum += A[k, i] * A[k, j];
                    }
                    ATA[i, j] = sum;
                }
            }

            // 计算 A^T * b
            for (int i = 0; i < 6; i++)
            {
                double sum = 0;
                for (int k = 0; k < 18; k++)
                {
                    sum += A[k, i] * b[k];
                }
                ATb[i] = sum;
            }

            // 解线性方程组
            double[] solution = SolveLinearSystem(ATA, ATb);

            // 构建 3×3 仿射变换矩阵
            outMatrix[0] = solution[0]; // a
            outMatrix[1] = solution[1]; // b
            outMatrix[2] = solution[2]; // c
            outMatrix[3] = solution[3]; // d
            outMatrix[4] = solution[4]; // e
            outMatrix[5] = solution[5]; // f
            outMatrix[6] = 0;
            outMatrix[7] = 0;
            outMatrix[8] = 1;

            return 0;
        }

        /// <summary>
        /// 高斯消元法求解线性方程组
        /// </summary>
        private double[] SolveLinearSystem(double[,] A, double[] b)
        {
            int n = b.Length;
            double[] x = new double[n];

            // 高斯消元法
            for (int i = 0; i < n; i++)
            {
                // 寻找主元
                int maxRow = i;
                for (int j = i + 1; j < n; j++)
                {
                    if (Math.Abs(A[j, i]) > Math.Abs(A[maxRow, i]))
                        maxRow = j;
                }

                // 交换行
                for (int k = i; k < n; k++)
                {
                    double temp = A[maxRow, k];
                    A[maxRow, k] = A[i, k];
                    A[i, k] = temp;
                }
                double tempB = b[maxRow];
                b[maxRow] = b[i];
                b[i] = tempB;

                // 消元
                for (int j = i + 1; j < n; j++)
                {
                    double factor = A[j, i] / A[i, i];
                    for (int k = i; k < n; k++)
                    {
                        A[j, k] -= factor * A[i, k];
                    }
                    b[j] -= factor * b[i];
                }
            }

            // 回代
            for (int i = n - 1; i >= 0; i--)
            {
                x[i] = b[i];
                for (int j = i + 1; j < n; j++)
                {
                    x[i] -= A[i, j] * x[j];
                }
                x[i] /= A[i, i];
            }

            return x;
        }

        /// <summary>
        /// 计算仿射变换的逆矩阵
        /// </summary>
        private double[] CalculateInverseTransform(double[] transformMatrix)
        {
            if (transformMatrix == null || transformMatrix.Length != 9)
                throw new ArgumentException("Transform matrix must be a 9-element array");

            // 提取矩阵元素
            double a = transformMatrix[0];
            double b = transformMatrix[1];
            double c = transformMatrix[2];
            double d = transformMatrix[3];
            double e = transformMatrix[4];
            double f = transformMatrix[5];

            // 计算行列式
            double det = a * e - b * d;

            if (Math.Abs(det) < 1e-10)
                throw new InvalidOperationException("Matrix is singular (non-invertible)");

            // 计算逆矩阵
            double invDet = 1.0 / det;
            double[] inverseMatrix = new double[9];

            // 线性部分
            inverseMatrix[0] = e * invDet;
            inverseMatrix[1] = -b * invDet;
            inverseMatrix[3] = -d * invDet;
            inverseMatrix[4] = a * invDet;

            // 平移部分
            inverseMatrix[2] = -(inverseMatrix[0] * c + inverseMatrix[1] * f);
            inverseMatrix[5] = -(inverseMatrix[3] * c + inverseMatrix[4] * f);

            // 最后一行
            inverseMatrix[6] = 0;
            inverseMatrix[7] = 0;
            inverseMatrix[8] = 1;

            return inverseMatrix;
        }

        /// <summary>
        /// 应用仿射变换到点
        /// </summary>
        private Point TransformPoint(Point point, double[] matrix)
        {
            if (matrix == null || matrix.Length < 6)
                throw new ArgumentException("Matrix must have at least 6 elements");

            double x = point.X;
            double y = point.Y;

            // 应用仿射变换公式
            double transformedX = x * matrix[0] + y * matrix[1] + matrix[2];
            double transformedY = x * matrix[3] + y * matrix[4] + matrix[5];

            return new Point(transformedX, transformedY);
        }
    }
}

