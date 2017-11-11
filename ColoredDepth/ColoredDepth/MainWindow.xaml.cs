using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Microsoft.Kinect;

namespace ColoredDepth
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        KinectSensor kinectSensor = KinectSensor.GetDefault();

        //Color Parameters
        FrameDescription colorFrameDescription;
        ColorFrameReader colorFrameReader;
        WriteableBitmap colorBitmap;
        int colorStride;
        Int32Rect colorRect;
        byte[] colorBuffer;
        ColorImageFormat colorImageFormat = ColorImageFormat.Bgra;

        //Depth Parameters
        DepthFrameReader depthFrameReader;
        FrameDescription depthFrameDescription;
        WriteableBitmap depthImage;
        ushort[] depthBuffer;
        byte[] depthBitmapBuffer;
        Int32Rect depthRect;
        int depthStride;
        Point depthPoint;
        const int R = 20;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (kinectSensor.IsOpen)
                {
                    kinectSensor.Close();
                    LblStatus.Content = "Kinect Desconnect";
                }
                else
                {
                    kinectSensor.Open();
                    LblStatus.Content = "Kinect Connect";

                    //Color manipulation
                    colorFrameDescription = kinectSensor.ColorFrameSource.CreateFrameDescription(colorImageFormat);

                    colorFrameReader = kinectSensor.ColorFrameSource.OpenReader();
                    colorFrameReader.FrameArrived += ColorFrameReader_colorArrived;

                    colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96, 96, PixelFormats.Bgra32, null);
                    colorStride = colorFrameDescription.Width * (int)colorFrameDescription.BytesPerPixel;
                    colorRect = new Int32Rect(0, 0, colorFrameDescription.Width, colorFrameDescription.Height);
                    colorBuffer = new byte[colorStride * colorFrameDescription.Height];
                    ScreenColor.Source = colorBitmap;

                    //Depth manipulation
                    depthFrameDescription = kinectSensor.DepthFrameSource.FrameDescription;

                    depthImage = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height, 96, 96, PixelFormats.Gray8, null);
                    depthBuffer = new ushort[depthFrameDescription.LengthInPixels];
                    depthBitmapBuffer = new byte[depthFrameDescription.LengthInPixels];
                    depthRect = new Int32Rect(0, 0, depthFrameDescription.Width, depthFrameDescription.Height);
                    depthStride = (int)depthFrameDescription.Width;

                    ScreenDepth.Source = depthImage;

                    depthPoint = new Point(depthFrameDescription.Width / 2, depthFrameDescription.Height / 2);

                    depthFrameReader = kinectSensor.DepthFrameSource.OpenReader();
                    depthFrameReader.FrameArrived += depthFrameReader_FrameArrived;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
                Close();
            }
        }

        private void ColorFrameReader_colorArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (var colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }
                colorFrame.CopyConvertedFrameDataToArray(colorBuffer, colorImageFormat);
            }
            colorBitmap.WritePixels(colorRect, colorBuffer, colorStride, 0);
        }

        private void depthFrameReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            try
            {
                using (var depthFrame = e.FrameReference.AcquireFrame())
                {
                    if (depthFrame == null)
                    {
                        return;
                    }
                    depthFrame.CopyFrameDataToArray(depthBuffer);
                }
                for (int i = 0; i < depthBuffer.Length; i++)
                {
                    depthBitmapBuffer[i] = (byte)(depthBuffer[i] % 255);
                }
                depthImage.WritePixels(depthRect, depthBitmapBuffer, depthStride, 0);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
                Close();
            }
        }

        private void UpdateColorFrame(ColorFrameArrivedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
