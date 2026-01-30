using GMSDK;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace TrendLineAnalyzer
{
    public partial class Form1 : Form
    {
        private List<PricePoint> priceData = new List<PricePoint>();
        private List<TrendLine> trendLines = new List<TrendLine>();

        public Form1()
        {
            InitializeComponent();
            InitializeChart();
            SetupDefaultValues();
        }

        private void InitializeChart()
        {
            chart1.Series.Clear();

            // 主价格序列
            var priceSeries = chart1.Series.Add("Price");
            priceSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            priceSeries.Color = Color.Blue;
            priceSeries.BorderWidth = 2;

            // 高点序列
            var highPointSeries = chart1.Series.Add("HighPoints");
            highPointSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Point;
            highPointSeries.Color = Color.Red;
            highPointSeries.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;
            highPointSeries.MarkerSize = 8;

            // 低点序列
            var lowPointSeries = chart1.Series.Add("LowPoints");
            lowPointSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Point;
            lowPointSeries.Color = Color.Green;
            lowPointSeries.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;
            lowPointSeries.MarkerSize = 8;

            // 上升趋势线
            var uptrendSeries = chart1.Series.Add("UptrendLines");
            uptrendSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            uptrendSeries.Color = Color.Green;
            uptrendSeries.BorderWidth = 2;
            uptrendSeries.BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dash;

            // 下降趋势线
            var downtrendSeries = chart1.Series.Add("DowntrendLines");
            downtrendSeries.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            downtrendSeries.Color = Color.Red;
            downtrendSeries.BorderWidth = 2;
            downtrendSeries.BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dash;

            // 设置X轴属性
            chart1.ChartAreas[0].AxisX.Interval = 5;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "{0}";
            chart1.ChartAreas[0].AxisY.Title = "价格";
            chart1.ChartAreas[0].AxisX.Title = "交易日序号";
        }

        private void SetupDefaultValues()
        {
            txtSymbol.Text = "SZSE.159915";
            dtpStartDate.Value = DateTime.Now.AddMonths(-6);
            dtpEndDate.Value = DateTime.Now;
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSymbol.Text))
            {
                MessageBox.Show("请输入股票代码！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (dtpStartDate.Value > dtpEndDate.Value)
            {
                MessageBox.Show("开始日期不能晚于结束日期！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnDownload.Enabled = false;
            btnDownload.Text = "下载中...";
            progressBar1.Visible = true;

            try
            {
                string startTime = $"{dtpStartDate.Value:yyyy-MM-dd} 09:30:00";
                string endTime = $"{dtpEndDate.Value:yyyy-MM-dd} 15:00:00";

                lblStatus.Text = $"正在下载 {txtSymbol.Text} 数据...";
                lblStatus.ForeColor = Color.Blue;

                // 获取历史数据
                var result = GMApi.HistoryBars(
                    symbols: txtSymbol.Text,
                    frequency: "1d",
                    startTime: startTime,
                    endTime: endTime,
                    adjust: Adjust.ADJUST_PREV
                );

                if (result.status != 0 || result.data == null)
                {
                    MessageBox.Show($"获取数据失败！状态码: {result.status}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 清空数据
                priceData.Clear();
                trendLines.Clear();
                chart1.Series["Price"].Points.Clear();
                chart1.Series["HighPoints"].Points.Clear();
                chart1.Series["LowPoints"].Points.Clear();
                chart1.Series["UptrendLines"].Points.Clear();
                chart1.Series["DowntrendLines"].Points.Clear();

                // 处理数据
                int pointIndex = 0;
                foreach (var bar in result.data)
                {
                    var pricePoint = new PricePoint
                    {
                        Index = pointIndex,
                        Date = bar.eob,
                        Open = (double)bar.open,
                        Close = (double)bar.close,
                        High = (double)bar.high,
                        Low = (double)bar.low
                    };

                    priceData.Add(pricePoint);

                    // 添加到图表 - 使用索引作为X值，确保正确显示
                    int idx = chart1.Series["Price"].Points.AddXY(pointIndex, (double)bar.close);
                    DataPoint dataPoint = chart1.Series["Price"].Points[idx];

                    // 设置悬停提示显示完整日期
                    dataPoint.ToolTip = $"日期: {bar.eob:yyyy-MM-dd}\n开盘: {bar.open:F2}\n最高: {bar.high:F2}\n最低: {bar.low:F2}\n收盘: {bar.close:F2}";

                    // 每隔10个点或最后一个点显示日期标签
                    if (pointIndex % 10 == 0 || pointIndex == result.data.Count - 1)
                    {
                        dataPoint.AxisLabel = bar.eob.ToString("MM-dd");
                    }

                    pointIndex++;
                }

                // 设置X轴范围
                if (result.data.Count > 0)
                {
                    chart1.ChartAreas[0].AxisX.Minimum = 0;
                    chart1.ChartAreas[0].AxisX.Maximum = result.data.Count - 1;
                    chart1.ChartAreas[0].AxisX.Interval = Math.Max(1, result.data.Count / 10);
                }

                // 更新状态
                lblStatus.Text = $"下载完成！共获取 {result.data.Count} 条数据";
                lblStatus.ForeColor = Color.Green;

                // 自动识别高低点
                btnAnalyze.PerformClick();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生错误: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "下载失败！";
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnDownload.Enabled = true;
                btnDownload.Text = "下载数据";
                progressBar1.Visible = false;
            }
        }

        private void btnAnalyze_Click(object sender, EventArgs e)
        {
            if (priceData.Count == 0)
            {
                MessageBox.Show("请先下载数据！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 清空之前的点
            chart1.Series["HighPoints"].Points.Clear();
            chart1.Series["LowPoints"].Points.Clear();
            chart1.Series["UptrendLines"].Points.Clear();
            chart1.Series["DowntrendLines"].Points.Clear();
            trendLines.Clear();

            // 获取参数
            int lookbackDays = (int)nudLookback.Value;
            double minChangePercent = (double)nudMinChange.Value / 100;

            // 识别高低点
            var highPoints = FindHighPoints(lookbackDays, minChangePercent);
            var lowPoints = FindLowPoints(lookbackDays, minChangePercent);

            // 在图表上标记高低点
            foreach (var point in highPoints)
            {
                int idx = chart1.Series["HighPoints"].Points.AddXY(point.Index, point.High);
                DataPoint dataPoint = chart1.Series["HighPoints"].Points[idx];
                dataPoint.ToolTip = $"高点日期: {point.Date:yyyy-MM-dd}\n价格: {point.High:F2}";
            }

            foreach (var point in lowPoints)
            {
                int idx = chart1.Series["LowPoints"].Points.AddXY(point.Index, point.Low);
                DataPoint dataPoint = chart1.Series["LowPoints"].Points[idx];
                dataPoint.ToolTip = $"低点日期: {point.Date:yyyy-MM-dd}\n价格: {point.Low:F2}";
            }

            // 绘制趋势线
            DrawTrendLines(highPoints, lowPoints);

            // 显示统计信息
            lblStats.Text = $"识别到 {highPoints.Count} 个高点，{lowPoints.Count} 个低点，{trendLines.Count} 条趋势线";
        }

        private List<PricePoint> FindHighPoints(int lookbackDays, double minChangePercent)
        {
            List<PricePoint> highPoints = new List<PricePoint>();

            for (int i = lookbackDays; i < priceData.Count - lookbackDays; i++)
            {
                var currentPoint = priceData[i];
                bool isHighPoint = true;

                // 检查前lookbackDays天
                for (int j = 1; j <= lookbackDays; j++)
                {
                    if (priceData[i - j].High >= currentPoint.High ||
                        priceData[i + j].High >= currentPoint.High)
                    {
                        isHighPoint = false;
                        break;
                    }
                }

                if (isHighPoint)
                {
                    // 检查价格变化幅度
                    double avgBefore = 0;
                    double avgAfter = 0;

                    for (int j = 1; j <= lookbackDays; j++)
                    {
                        avgBefore += priceData[i - j].Close;
                        avgAfter += priceData[i + j].Close;
                    }

                    avgBefore /= lookbackDays;
                    avgAfter /= lookbackDays;

                    double changeFromBefore = (currentPoint.High - avgBefore) / avgBefore;
                    double changeToAfter = (currentPoint.High - avgAfter) / currentPoint.High;

                    if (Math.Abs(changeFromBefore) >= minChangePercent &&
                        Math.Abs(changeToAfter) >= minChangePercent)
                    {
                        highPoints.Add(currentPoint);
                    }
                }
            }

            return highPoints;
        }

        private List<PricePoint> FindLowPoints(int lookbackDays, double minChangePercent)
        {
            List<PricePoint> lowPoints = new List<PricePoint>();

            for (int i = lookbackDays; i < priceData.Count - lookbackDays; i++)
            {
                var currentPoint = priceData[i];
                bool isLowPoint = true;

                // 检查前lookbackDays天
                for (int j = 1; j <= lookbackDays; j++)
                {
                    if (priceData[i - j].Low <= currentPoint.Low ||
                        priceData[i + j].Low <= currentPoint.Low)
                    {
                        isLowPoint = false;
                        break;
                    }
                }

                if (isLowPoint)
                {
                    // 检查价格变化幅度
                    double avgBefore = 0;
                    double avgAfter = 0;

                    for (int j = 1; j <= lookbackDays; j++)
                    {
                        avgBefore += priceData[i - j].Close;
                        avgAfter += priceData[i + j].Close;
                    }

                    avgBefore /= lookbackDays;
                    avgAfter /= lookbackDays;

                    double changeFromBefore = (avgBefore - currentPoint.Low) / avgBefore;
                    double changeToAfter = (avgAfter - currentPoint.Low) / currentPoint.Low;

                    if (Math.Abs(changeFromBefore) >= minChangePercent &&
                        Math.Abs(changeToAfter) >= minChangePercent)
                    {
                        lowPoints.Add(currentPoint);
                    }
                }
            }

            return lowPoints;
        }

        private void DrawTrendLines(List<PricePoint> highPoints, List<PricePoint> lowPoints)
        {
            trendLines.Clear();

            // 绘制上升趋势线（连接低点）
            if (lowPoints.Count >= 2)
            {
                // 按时间排序
                lowPoints = lowPoints.OrderBy(p => p.Index).ToList();

                // 尝试找到至少3个低点形成的上升趋势线
                for (int i = 0; i < lowPoints.Count - 1; i++)
                {
                    for (int j = i + 1; j < lowPoints.Count; j++)
                    {
                        var line = new TrendLine
                        {
                            Type = TrendLineType.Uptrend,
                            StartPoint = lowPoints[i],
                            EndPoint = lowPoints[j],
                            Slope = (lowPoints[j].Low - lowPoints[i].Low) / (lowPoints[j].Index - lowPoints[i].Index)
                        };

                        // 检查有多少低点在这条线上
                        int pointsOnLine = 2; // 起点和终点
                        for (int k = 0; k < lowPoints.Count; k++)
                        {
                            if (k != i && k != j)
                            {
                                double expectedY = lowPoints[i].Low + line.Slope * (lowPoints[k].Index - lowPoints[i].Index);
                                double tolerance = lowPoints[k].Low * 0.02; // 2%容差

                                if (Math.Abs(expectedY - lowPoints[k].Low) <= tolerance)
                                {
                                    pointsOnLine++;
                                }
                            }
                        }

                        if (pointsOnLine >= 3) // 至少3个点确认趋势线
                        {
                            trendLines.Add(line);

                            // 在图表上绘制
                            chart1.Series["UptrendLines"].Points.AddXY(
                                lowPoints[i].Index,
                                lowPoints[i].Low
                            );
                            chart1.Series["UptrendLines"].Points.AddXY(
                                lowPoints[j].Index,
                                lowPoints[j].Low
                            );
                        }
                    }
                }
            }

            // 绘制下降趋势线（连接高点）
            if (highPoints.Count >= 2)
            {
                // 按时间排序
                highPoints = highPoints.OrderBy(p => p.Index).ToList();

                // 尝试找到至少3个高点形成的下降趋势线
                for (int i = 0; i < highPoints.Count - 1; i++)
                {
                    for (int j = i + 1; j < highPoints.Count; j++)
                    {
                        var line = new TrendLine
                        {
                            Type = TrendLineType.Downtrend,
                            StartPoint = highPoints[i],
                            EndPoint = highPoints[j],
                            Slope = (highPoints[j].High - highPoints[i].High) / (highPoints[j].Index - highPoints[i].Index)
                        };

                        // 检查有多少高点在这条线上
                        int pointsOnLine = 2; // 起点和终点
                        for (int k = 0; k < highPoints.Count; k++)
                        {
                            if (k != i && k != j)
                            {
                                double expectedY = highPoints[i].High + line.Slope * (highPoints[k].Index - highPoints[i].Index);
                                double tolerance = highPoints[k].High * 0.02; // 2%容差

                                if (Math.Abs(expectedY - highPoints[k].High) <= tolerance)
                                {
                                    pointsOnLine++;
                                }
                            }
                        }

                        if (pointsOnLine >= 3 && line.Slope < 0) // 至少3个点且斜率为负（下降）
                        {
                            trendLines.Add(line);

                            // 在图表上绘制
                            chart1.Series["DowntrendLines"].Points.AddXY(
                                highPoints[i].Index,
                                highPoints[i].High
                            );
                            chart1.Series["DowntrendLines"].Points.AddXY(
                                highPoints[j].Index,
                                highPoints[j].High
                            );
                        }
                    }
                }
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (trendLines.Count == 0)
            {
                MessageBox.Show("没有趋势线数据可导出！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV文件 (*.csv)|*.csv";
            saveFileDialog.FileName = $"{txtSymbol.Text.Replace(".", "_")}_趋势线_{DateTime.Now:yyyyMMddHHmmss}";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExportTrendLinesToCsv(saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportTrendLinesToCsv(string filePath)
        {
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // 写入标题
                writer.WriteLine("趋势线类型,起始日期,起始价格,结束日期,结束价格,斜率,持续时间(天)");

                // 写入数据
                foreach (var line in trendLines)
                {
                    int duration = (int)(line.EndPoint.Date - line.StartPoint.Date).TotalDays;
                    writer.WriteLine($"{line.Type},{line.StartPoint.Date:yyyy-MM-dd},{line.StartPoint.GetPrice():F3}," +
                                     $"{line.EndPoint.Date:yyyy-MM-dd},{line.EndPoint.GetPrice():F3}," +
                                     $"{line.Slope:F6},{duration}");
                }
            }

            MessageBox.Show($"趋势线数据已导出到: {filePath}", "成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            lblStatus.Text = "就绪";
            lblStats.Text = "统计信息: 暂无数据";
            var setResult = GMApi.SetToken("d2be33f639f0ce9502b8cf527419010f8a53bd64");
            if (setResult != 0)
            {
                MessageBox.Show($"SetToken 失败，错误码：{setResult}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // 价格点类
    public class PricePoint
    {
        public int Index { get; set; }
        public DateTime Date { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }

        public double GetPrice()
        {
            return Close;
        }
    }

    // 趋势线类型
    public enum TrendLineType
    {
        Uptrend,
        Downtrend
    }

    // 趋势线类
    public class TrendLine
    {
        public TrendLineType Type { get; set; }
        public PricePoint StartPoint { get; set; }
        public PricePoint EndPoint { get; set; }
        public double Slope { get; set; }
    }
}