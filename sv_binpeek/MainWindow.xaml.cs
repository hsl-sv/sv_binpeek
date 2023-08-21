using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using CheckBox = System.Windows.Controls.CheckBox;

namespace sv_binpeek
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public int nChannels = 4;
        public int nSamplingRate = 2048;
        public string strCSSType = "t4";
        public string chEndian = "l";
        public IPlottable[]? spPlots;

        static private uint fileChunkSize = 104857600;

        public MainWindow()
        {
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            InitializeComponent();

            MainPlot.RightClicked -= MainPlot.DefaultRightClickEvent;
            MainPlot.RightClicked += DeployCustomMenu;

            Loaded += MainWindow_Loaded;
        }

        private void DeployCustomMenu(object sender, RoutedEventArgs e)
        {
            MenuItem item;
            var cm = new ContextMenu();

            item = new() { Header = "Zoom to Fit Data" };
            item.Click += (o, e) => { MainPlot.Plot.AxisAuto(); MainPlot.Refresh(); };
            cm.Items.Add(item);

            cm.Items.Add(new Separator());

            item = new() { Header = "Help" };
            item.Click += (o, e) => new ScottPlot.WPF.HelpWindow().Show();
            cm.Items.Add(item);

            cm.IsOpen = true;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainPlot.Refresh();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Remove stackPanel
            if (FindName("stackPanel") is StackPanel stackPanel) { PlotGrid.Children.Remove(stackPanel); }

            MainPlot.Plot.Clear();
            MainPlot.Refresh();

            GC.Collect();

            byte[] fileContent;
            bool succeeded;

            // Try parse parameters
            succeeded = int.TryParse(TextBoxChannels.Text, out nChannels);

            if (!succeeded)
            {
                nChannels = 4;
                TextBoxChannels.Text = 4.ToString();
            }

            succeeded = int.TryParse(TextBoxSamplingRate.Text, out nSamplingRate);

            if (!succeeded)
            {
                nSamplingRate = 2048;
                TextBoxSamplingRate.Text = 2048.ToString();
            }

            strCSSType = TextBoxCSSType.Text.Substring(0, 2);

            System.Type targetType = typeof(float);

            // CSS 타입 확인
            VerifyType(strCSSType, ref targetType, ref chEndian);

            OpenFileDialog openFileDialog = new();
            openFileDialog.InitialDirectory = "C:\\";
            openFileDialog.Filter = "SVT Binary Data files|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string? filePath = openFileDialog.FileName;

                TextBoxPath.Text = filePath;

                // TODO: 2GB 이상 바이트 할당 (Array 사용인듯)
                long fileLength = new System.IO.FileInfo(filePath).Length;
                fileContent = new byte[fileLength];
                byte[] buf = new byte[fileChunkSize];
                int bytesRead = 0;
                int fcOffset = 0;

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    while ((bytesRead = fs.Read(buf, 0, buf.Length)) > 0)
                    {
                        Buffer.BlockCopy(buf, 0, fileContent, fcOffset, bytesRead);
                        fcOffset += bytesRead;
                    }
                }

                //fileContent = File.ReadAllBytes(filePath);

                if (fileContent.Length > 0)
                {
                    try
                    {
                        if (chEndian == "b")
                        {
                            Array.Reverse(fileContent);
                        }

                        // byte[] to float[]
                        var restoredData = Converter(fileContent, targetType);

                        var data = new List<float[]>(nChannels);

                        // 그림 그리기
                        for (int i = 0; i < nChannels; i++)
                        {
                            float[] ch = new float[restoredData.GetUpperBound(0) + 1];

                            for (int j = 0; j <= restoredData.GetUpperBound(0); j++)
                                ch[j] = restoredData[j, i];

                            if (chEndian == "b")
                            {
                                Array.Reverse(ch);
                            }

                            data.Add(ch);

                            MainPlot.Plot.AddSignal(data[i], nSamplingRate);
                        }

                        // Plots list
                        spPlots = MainPlot.Plot.GetPlottables();

                        MakeCheckbox(data);

                        MainPlot.Plot.Benchmark();
                        MainPlot.Plot.XLabel("Seconds");
                        MainPlot.Render();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Forms.MessageBox.Show(ex.Message, "Error during opening file");

                        MainPlot.Plot.Clear();
                        MainPlot.Refresh();

                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        // CSS 타입 체크
        private static void VerifyType(string type, ref System.Type targetType, ref string endian)
        {
            if (new[] { "t8", "p8" }.Contains(type))
            { endian = "l"; targetType = typeof(double); }
            else if (new[] { "t4", "p4" }.Contains(type))
            { endian = "l"; targetType = typeof(float); }
            else if (new[] { "s4" }.Contains(type))
            { endian = "b"; targetType = typeof(int); }
            else if (new[] { "i4" }.Contains(type))
            { endian = "l"; targetType = typeof(int); }
            else
            { endian = "l"; targetType = typeof(float); }
            return;
        }

        // type 기반으로 오브젝트 어레이 생성, 최종 결과는 float 어레이로 반환
        private float[,] Converter(byte[] bytes, System.Type type)
        {
            // 타입에 맞춰 읽기
            Array objArray = Array.CreateInstance(type, bytes.Length / nChannels);
            float[] floatArray = new float[objArray.Length];

            // byte[] to object array
            Buffer.BlockCopy(bytes, 0, objArray, 0, bytes.Length);

            // float 배열로 Array.Copy()
            Array.Copy(objArray, floatArray, objArray.Length);

            // 채널 수에 따라 Reshape
            var restoredArray = Make2DArray(floatArray, (bytes.Length / Marshal.SizeOf(type)) / nChannels, nChannels);

            return restoredArray;
        }

        // 1차원 어레이 -> 2차원 [h x w] 어레이
        private static T[,] Make2DArray<T>(T[] input, int height, int width)
        {
            T[,] output = new T[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    output[i, j] = input[i * width + j];
                }
            }
            return output;
        }

        private void MainPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            (double mouseCoordX, double mouseCoordY) = MainPlot.GetMouseCoordinates();

            string xytext = $"({mouseCoordX:N6}, {mouseCoordY:N6})";

            PlotXYLabel.Content = xytext;
        }

        private void MakeCheckbox(List<float[]> data)
        {
            var stackPanel = new StackPanel { 
                Orientation = System.Windows.Controls.Orientation.Vertical 
            };

            for (int i = 0; i < data.Count; i++)
            {
                CheckBox cb = new()
                {
                    Name = $"ch{i}",
                    Content = $"Ch {i}",
                    IsChecked = true
                };
                cb.Click += CheckboxClick;
                stackPanel.Children.Add(cb);
            }

            stackPanel.Name = "stackPanel";
            stackPanel.Margin = new Thickness(5, 5, 5, 5);

            PlotGrid.Children.Add(stackPanel);
        }

        private void CheckboxClick(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && spPlots != null)
            {
                try
                {
                    _ = int.TryParse(cb.Name.Split("ch")[1], out int idx);

                    if (cb.IsChecked == true)
                        spPlots[idx].IsVisible = true;
                    else
                        spPlots[idx].IsVisible = false;
                }
                catch
                {
                    cb.IsChecked = false;
                }

                MainPlot.Refresh();
            }
        }
    }
}
