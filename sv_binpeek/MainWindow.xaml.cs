using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace sv_binpeek
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// TODO: Get fs/channel number, select channel
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainPlot.Refresh();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainPlot.Plot.Clear();
            MainPlot.Refresh();

            byte[]? fileContent = null;
            var filePath = string.Empty;

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

                    float[] ch0 = new float[restoredData.GetUpperBound(0) + 1];
                    for (int i = 0; i <= restoredData.GetUpperBound(0); i++)
                        ch0[i] = restoredData[i, 0];

                    MainPlot.Plot.AddSignal(ch0, 2048);
                    MainPlot.Plot.Benchmark();
                    MainPlot.Plot.XLabel("Seconds");
                    MainPlot.Render();
                }
                catch 
                { 
                    // Exception
                }
            }
            // If null
        }

        private float[,] Converter(byte[] bytes)
        {
            var floatArray = new float[(bytes.Length / 4)];

            Buffer.BlockCopy(bytes, 0, floatArray, 0, bytes.Length);

            var restoredArray = Make2DArray(floatArray, (bytes.Length / 4) / 4, 4);

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

            Title = $"binpeek XY: ({mouseCoordX:N2}, {mouseCoordY:N2})";
        }
    }
}
