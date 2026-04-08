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
        /// 兼容保留的变换矩阵字段（当前纯图像模式下固定为单位矩阵）
        /// </summary>
        private double[] m_9MMToPixMatrix = new double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        /// <summary>
        /// 当前需要显示的图元列表
        /// </summary>
        private List<PaintElement> m_CurrShowElement = new List<PaintElement>();

        /// <summary>
        /// 线宽缩放比例（纯图像模式固定为 1）
        /// </summary>
        private double m_lineWidthScale = 1;

        private int _selectedElementIndex = -1;

        private const int SuccessCode = 0;
        private const int InvalidParameterCode = -1;
        private const int OutOfRangeCode = -2;
        private const int InvalidStateCode = -3;

        /// <summary>
        /// 将图像像素坐标转换为控件坐标
        /// </summary>
        /// <param name="originData">原始图像像素坐标列表 [x1, y1, x2, y2, ...]</param>
        /// <returns>转换后的控件坐标列表</returns>
        protected List<float> GetTransedPts(List<double> originData)
        {
            if (originData == null || originData.Count < 2)
            {
                return new List<float>();
            }

            var result = new List<float>(originData.Count);

            for (int i = 0; i + 1 < originData.Count; i += 2)
            {
                double imageX = originData[i];
                double imageY = originData[i + 1];

                var ctlV = new Point(
                    imageX * _currentZoomFactor + _scrollImageLocation.X,
                    imageY * _currentZoomFactor + _scrollImageLocation.Y
                );

                result.Add((float)ctlV.X);
                result.Add((float)ctlV.Y);
            }

            return result;
        }

        private bool ShouldRenderElement(int index)
        {
            if (index < 0 || index >= m_CurrShowElement.Count)
            {
                return false;
            }

            return CtlShowPaintStatus switch
            {
                ImageElementCtlStatus.None => false,
                ImageElementCtlStatus.ShowAll => true,
                ImageElementCtlStatus.ShowSelected => index == _selectedElementIndex,
                _ => CtlShowPaintStatus > 0
            };
        }

        /// <summary>
        /// 控件显示图元的状态
        /// </summary>
        public static readonly StyledProperty<ImageElementCtlStatus> CtlShowPaintStatusProperty =
            AvaloniaProperty.Register<CtlOnlyShowImage, ImageElementCtlStatus>(
                nameof(CtlShowPaintStatus),
                ImageElementCtlStatus.None);

        public ImageElementCtlStatus CtlShowPaintStatus
        {
            get => GetValue(CtlShowPaintStatusProperty);
            set => SetValue(CtlShowPaintStatusProperty, value);
        }

        /// <summary>
        /// 控件鼠标状态
        /// </summary>
        private ImageCtlMouseStatus _ctlMouseStatus = ImageCtlMouseStatus.Normal;

        public static readonly DirectProperty<CtlOnlyShowImage, ImageCtlMouseStatus> CtlMouseStatusProperty =
            AvaloniaProperty.RegisterDirect<CtlOnlyShowImage, ImageCtlMouseStatus>(
                nameof(CtlMouseStatus),
                o => o.CtlMouseStatus,
                (o, v) => o.CtlMouseStatus = v,
                ImageCtlMouseStatus.Normal);

        public ImageCtlMouseStatus CtlMouseStatus
        {
            get => _ctlMouseStatus;
            set => SetAndRaise(CtlMouseStatusProperty, ref _ctlMouseStatus, value);
        }

        /// <summary>
        /// 计算线宽缩放比例
        /// </summary>
        private void CalcLineWidthScale()
        {
            m_lineWidthScale = 1;
        }

        /// <summary>
        /// 兼容旧版本：设置相机标定参数（当前纯图像模式下无实际作用）
        /// </summary>
        public int SetCameraCalib(string calibFilePath)
        {
            // 纯图像模式：为兼容旧 API 保留该方法
            return 0;
        }

        /// <summary>
        /// 兼容旧版本：设置相机标定参数（当前纯图像模式下无实际作用）
        /// </summary>
        public int SetCameraCalib(double[] matrixPixToMM)
        {
            // 纯图像模式：为兼容旧 API 保留该方法
            m_9MMToPixMatrix = new double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            CalcLineWidthScale();
            return 0;
        }

        /// <summary>
        /// 兼容旧版本：设置相机标定参数（当前纯图像模式下无实际作用）
        /// </summary>
        public int SetCameraCalibRef(double[] matrixMMToPix)
        {
            // 纯图像模式：为兼容旧 API 保留该方法
            m_9MMToPixMatrix = new double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            CalcLineWidthScale();
            return 0;
        }

        /// <summary>
        /// 兼容旧版本：简化标定参数（当前纯图像模式下无实际作用）
        /// </summary>
        /// <param name="MMpix">历史参数，当前不使用</param>
        /// <param name="imgWidth">图像宽度</param>
        /// <param name="imgHeight">图像高度</param>
        public int SetCameraCalib(Point MMpix, int imgWidth, int imgHeight)
        {
            // 纯图像模式：为兼容旧 API 保留该方法
            m_9MMToPixMatrix = new double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            CalcLineWidthScale();
            return 0;
        }

        public int SetUpdateCameraPos(Func<Point> getPosFunc)
        {
            // 纯图像模式：无需外部更新运控位置
            return SuccessCode;
        }

        /// <summary>
        /// 设置要显示的图元列表
        /// </summary>
        public int SetPaintElements(List<PaintElement> needShowElement)
        {
            if (needShowElement == null)
            {
                return InvalidParameterCode;
            }

            var newElements = new List<PaintElement>(needShowElement.Count);
            foreach (var element in needShowElement)
            {
                if (!TryCloneValidElement(element, out var clone))
                {
                    return InvalidParameterCode;
                }

                newElements.Add(clone!);
            }

            int previousSelected = _selectedElementIndex;
            m_CurrShowElement = newElements;
            if (_selectedElementIndex >= m_CurrShowElement.Count)
            {
                _selectedElementIndex = -1;
            }

            RaiseElementChanged(
                PaintElementChangeAction.Replaced,
                -1,
                null,
                null,
                PaintElementChangeSource.Api,
                PaintElementChangePhase.Committed);

            RaiseSelectionChangedIfNeeded(
                previousSelected,
                _selectedElementIndex,
                PaintElementChangeSource.Api);

            InvalidateVisual();
            return SuccessCode;
        }

        /// <summary>
        /// 改变单个图元的参数
        /// </summary>
        public int ChangePaintElement(int index, PaintElement element)
        {
            if (!IsIndexValid(index))
            {
                return OutOfRangeCode;
            }

            if (!TryCloneValidElement(element, out var clone))
            {
                return InvalidParameterCode;
            }

            var before = m_CurrShowElement[index].DeepCopy();
            m_CurrShowElement[index] = clone!;

            RaiseElementChanged(
                PaintElementChangeAction.Updated,
                index,
                before,
                m_CurrShowElement[index],
                PaintElementChangeSource.Api,
                PaintElementChangePhase.Committed);

            InvalidateVisual();
            return SuccessCode;
        }

        public int AddPaintElement(PaintElement element)
        {
            if (!TryCloneValidElement(element, out var clone))
            {
                return InvalidParameterCode;
            }

            m_CurrShowElement.Add(clone!);
            int index = m_CurrShowElement.Count - 1;
            RaiseElementChanged(
                PaintElementChangeAction.Added,
                index,
                null,
                m_CurrShowElement[index],
                PaintElementChangeSource.Api,
                PaintElementChangePhase.Committed);

            InvalidateVisual();
            return SuccessCode;
        }

        public int InsertPaintElement(int index, PaintElement element)
        {
            if (index < 0 || index > m_CurrShowElement.Count)
            {
                return OutOfRangeCode;
            }

            if (!TryCloneValidElement(element, out var clone))
            {
                return InvalidParameterCode;
            }

            m_CurrShowElement.Insert(index, clone!);
            int previousSelected = _selectedElementIndex;
            if (_selectedElementIndex >= index)
            {
                _selectedElementIndex++;
            }

            RaiseElementChanged(
                PaintElementChangeAction.Added,
                index,
                null,
                m_CurrShowElement[index],
                PaintElementChangeSource.Api,
                PaintElementChangePhase.Committed);

            RaiseSelectionChangedIfNeeded(previousSelected, _selectedElementIndex, PaintElementChangeSource.Api);

            InvalidateVisual();
            return SuccessCode;
        }

        public int RemovePaintElementAt(int index)
        {
            if (!IsIndexValid(index))
            {
                return OutOfRangeCode;
            }

            var before = m_CurrShowElement[index].DeepCopy();
            m_CurrShowElement.RemoveAt(index);

            int previousSelected = _selectedElementIndex;
            if (_selectedElementIndex == index)
            {
                _selectedElementIndex = -1;
            }
            else if (_selectedElementIndex > index)
            {
                _selectedElementIndex--;
            }

            RaiseElementChanged(
                PaintElementChangeAction.Removed,
                index,
                before,
                null,
                PaintElementChangeSource.Api,
                PaintElementChangePhase.Committed);

            RaiseSelectionChangedIfNeeded(previousSelected, _selectedElementIndex, PaintElementChangeSource.Api);

            InvalidateVisual();
            return SuccessCode;
        }

        public int ClearPaintElements()
        {
            if (m_CurrShowElement.Count == 0)
            {
                return SuccessCode;
            }

            int previousSelected = _selectedElementIndex;
            m_CurrShowElement.Clear();
            _selectedElementIndex = -1;

            RaiseElementChanged(
                PaintElementChangeAction.Cleared,
                -1,
                null,
                null,
                PaintElementChangeSource.Api,
                PaintElementChangePhase.Committed);

            RaiseSelectionChangedIfNeeded(previousSelected, _selectedElementIndex, PaintElementChangeSource.Api);

            InvalidateVisual();
            return SuccessCode;
        }

        public IReadOnlyList<PaintElement> GetPaintElementsSnapshot()
        {
            var result = new List<PaintElement>(m_CurrShowElement.Count);
            foreach (var element in m_CurrShowElement)
            {
                result.Add(element.DeepCopy());
            }

            return result;
        }

        public int SetSelectedElementIndex(int index)
        {
            if (index < -1)
            {
                return InvalidParameterCode;
            }

            if (index >= 0 && m_CurrShowElement.Count == 0)
            {
                return InvalidStateCode;
            }

            if (index >= m_CurrShowElement.Count)
            {
                return OutOfRangeCode;
            }

            int previousSelected = _selectedElementIndex;
            _selectedElementIndex = index;
            RaiseSelectionChangedIfNeeded(previousSelected, _selectedElementIndex, PaintElementChangeSource.Api);
            InvalidateVisual();
            return SuccessCode;
        }

        public int GetSelectedElementIndex()
        {
            return _selectedElementIndex;
        }

        /// <summary>
        /// 刷新显示
        /// </summary>
        public void ReFresh()
        {
            InvalidateVisual();
        }

        private bool TrySetSelectedElementIndexInternal(int index, PaintElementChangeSource source)
        {
            if (index < -1 || index >= m_CurrShowElement.Count)
            {
                return false;
            }

            int previousSelected = _selectedElementIndex;
            _selectedElementIndex = index;
            RaiseSelectionChangedIfNeeded(previousSelected, _selectedElementIndex, source);
            return previousSelected != _selectedElementIndex;
        }

        private void RaiseSelectionChangedIfNeeded(int beforeIndex, int afterIndex, PaintElementChangeSource source)
        {
            if (beforeIndex == afterIndex)
            {
                return;
            }

            RaiseElementChanged(
                PaintElementChangeAction.Selected,
                afterIndex,
                CloneElementAt(beforeIndex),
                CloneElementAt(afterIndex),
                source,
                PaintElementChangePhase.Committed);
        }

        private PaintElement? CloneElementAt(int index)
        {
            if (!IsIndexValid(index))
            {
                return null;
            }

            return m_CurrShowElement[index].DeepCopy();
        }

        private void RaiseElementChanged(
            PaintElementChangeAction action,
            int index,
            PaintElement? before,
            PaintElement? after,
            PaintElementChangeSource source,
            PaintElementChangePhase phase)
        {
            ElementChanged?.Invoke(
                this,
                new PaintElementChangedEventArgs(
                    action,
                    index,
                    before?.DeepCopy(),
                    after?.DeepCopy(),
                    source,
                    phase));
        }

        private bool IsIndexValid(int index)
        {
            return index >= 0 && index < m_CurrShowElement.Count;
        }

        private bool TryCloneValidElement(PaintElement? element, out PaintElement? clone)
        {
            clone = null;
            if (element == null || !IsValidElement(element))
            {
                return false;
            }

            clone = element.DeepCopy();
            return true;
        }

        private static bool IsValidElement(PaintElement element)
        {
            if (element.Pts == null || element.Pts.Count < 2 || element.Pts.Count % 2 != 0)
            {
                return false;
            }

            return element.Type switch
            {
                PaintElementType.Point => element.Pts.Count >= 2,
                PaintElementType.Cross => element.Pts.Count >= 2,
                PaintElementType.Text => element.Pts.Count >= 2,
                PaintElementType.Line => element.Pts.Count >= 4,
                PaintElementType.Rectangle => element.Pts.Count >= 4,
                PaintElementType.Circle => element.Pts.Count >= 4,
                PaintElementType.Ellipse => element.Pts.Count >= 4,
                PaintElementType.Arrow => element.Pts.Count >= 4,
                PaintElementType.PolyLine => element.Pts.Count >= 4,
                PaintElementType.Polygon => element.Pts.Count >= 6,
                PaintElementType.Ring => element.Pts.Count >= 6,
                PaintElementType.Arc => element.Pts.Count >= 6,
                _ => false
            };
        }

        /// <summary>
        /// 兼容旧 API：在纯图像模式下返回图像像素坐标
        /// </summary>
        /// <param name="imagePixelPosition">图像中的像素坐标</param>
        /// <returns>图像像素坐标</returns>
        public Point ConvertImageToMachinePosition(Point imagePixelPosition)
        {
            if (_originImage == null)
                return imagePixelPosition;

            return new Point(
                Math.Clamp(imagePixelPosition.X, 0, _originImage.PixelSize.Width),
                Math.Clamp(imagePixelPosition.Y, 0, _originImage.PixelSize.Height));
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
