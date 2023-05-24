using ScottPlot;
using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.IO;
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
        public IPlottable[]? spPlots;

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

            item = new() { Header = "Spectrogram" };
            item.Click += (o, e) => new ScottPlot.WpfPlotViewer(MainPlot.Plot).Show();
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
            StackPanel? stackPanel = FindName("stackPanel") as StackPanel;

            if (stackPanel != null) { PlotGrid.Children.Remove(stackPanel); }

            MainPlot.Plot.Clear();
            MainPlot.Refresh();

            byte[]? fileContent = null;
            var filePath = string.Empty;
            bool succeeded = false;

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

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "C:\\";
                openFileDialog.Filter = "SVT Binary Data files (*.bin)|*.bin";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;

                    TextBoxPath.Text = filePath;

                    fileContent = File.ReadAllBytes(filePath);
                }
            }

            if (fileContent != null)
            {
                try
                {

                    var restoredData = Converter(fileContent);

                    var data = new List<float[]>(nChannels);

                    for (int i = 0; i < nChannels; i++)
                    {
                        float[] ch = new float[restoredData.GetUpperBound(0) + 1];

                        for (int j = 0; j <= restoredData.GetUpperBound(0); j++)
                            ch[j] = restoredData[j, i];

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

        private float[,] Converter(byte[] bytes)
        {
            var floatArray = new float[(bytes.Length / nChannels)];

            Buffer.BlockCopy(bytes, 0, floatArray, 0, bytes.Length);

            var restoredArray = Make2DArray(floatArray, (bytes.Length / sizeof(float)) / nChannels, nChannels);

            return restoredArray;
        }

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
                CheckBox cb = new();
                cb.Name = $"ch{i}";
                cb.Content = $"Ch {i}";
                cb.IsChecked = true;
                cb.Click += CheckboxClick;
                stackPanel.Children.Add(cb);
            }

            stackPanel.Name = "stackPanel";
            stackPanel.Margin = new Thickness(5, 5, 5, 5);

            PlotGrid.Children.Add(stackPanel);
        }

        private void CheckboxClick(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;

            if (cb != null && spPlots != null)
            {
                int idx = 0;
                _ = int.TryParse(cb.Name.Split("ch")[1], out idx);

                if (cb.IsChecked == true)
                    spPlots[idx].IsVisible = true;
                else
                    spPlots[idx].IsVisible = false;

                MainPlot.Refresh();
            }
        }
    }
}
