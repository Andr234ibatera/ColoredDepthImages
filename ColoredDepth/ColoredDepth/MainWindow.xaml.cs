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
using Microsoft.Kinect.Face;
using System.Globalization;
using System.IO;

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

        //Body parameters
        BodyFrameReader bodyFrameReader;
        Body[] bodies = null;
        int bodyCount;
        FaceFrameSource[] faceFrameSources = null;
        FaceFrameReader[] faceFrameReaders = null;
        FaceFrameResult[] faceFrameResults = null;
        List<Brush> faceBrush;
        //Draw
        DrawingGroup drawingGroup;
        DrawingImage imageSource;
        Rect displayRect;

        //Global Control Variable
        bool capturing = false;
        double count = 0;
        string path = "";


        public MainWindow()
        {
            drawingGroup = new DrawingGroup();
            imageSource = new DrawingImage(drawingGroup);
            this.DataContext = this;
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
                    BtStart.Content = "Start";
                }
                else
                {
                    kinectSensor.Open();
                    LblStatus.Content = "Kinect Connect";
                    BtStart.Content = "Stop";

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

                    //Face manipulation
                    FrameDescription frameDescription = kinectSensor.ColorFrameSource.FrameDescription;
                    displayRect = new Rect( 0, 0, frameDescription.Width, frameDescription.Height);
                    bodyFrameReader = kinectSensor.BodyFrameSource.OpenReader();
                    bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;
                    bodyCount = kinectSensor.BodyFrameSource.BodyCount;
                    bodies = new Body[bodyCount];
                    InitializeFace();
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
                Close();
            }
        }
        
        private void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            try
            {
                using (var bodyFrame = e.FrameReference.AcquireFrame())
                {
                    if (bodyFrame == null)
                    {
                        return;
                    }
                    bodyFrame.GetAndRefreshBodyData(bodies);
                    for (int i = 0; i < bodyCount; i++)
                    {
                        Body body = bodies[i];
                        if (!body.IsTracked)
                        {
                            continue;
                        }
                        ulong trackingId = body.TrackingId;
                        faceFrameReaders[i].FaceFrameSource.TrackingId = trackingId;
                    }
                }
                using (DrawingContext dc = drawingGroup.Open())
                {
                    dc.DrawRectangle(Brushes.Black, null, displayRect);
                    for (int i = 0; i < bodyCount; i++)
                    {
                        if (faceFrameReaders[i].FaceFrameSource.IsTrackingIdValid)
                        {
                            if (faceFrameResults[i] != null)
                            {
                                DrawFaceFrameResult(i, faceFrameResults[i], dc);
                            }
                        }
                    }
                    drawingGroup.ClipGeometry = new RectangleGeometry(displayRect);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
                Close();
            }
        }
        
        void DrawFaceFrameResult(int faceIndex, FaceFrameResult faceResult, DrawingContext drawingContext)
        {
            //Brush/Pen
            Brush drawingBrush = faceBrush[0];
            if (faceIndex < bodyCount)
            {
                drawingBrush = faceBrush[faceIndex];
            }
            Pen drawingPen = new Pen(drawingBrush, 5);

            //Face Points
            var facePoints = faceResult.FacePointsInColorSpace;
            foreach (PointF pointF in facePoints.Values)
            {
                Point points = new Point(pointF.X, pointF.Y);
                
                RectI box = faceResult.FaceBoundingBoxInColorSpace;

                Target.Width = box.Right - box.Left;
                Target.Height = box.Bottom - box.Top;

                Canvas.SetLeft(Target, (points.X / 4) - Target.Width / 2);
                Canvas.SetTop(Target, points.Y / 4 - Target.Height / 2);

            }
        }

        private void CaptureImage(WriteableBitmap bitmap)
        {
            try
            {
                if (path=="")
                {
                    var currentDirectory = System.IO.Directory.GetCurrentDirectory();
                    string[] parts = currentDirectory.Split('\\');

                    for (int i = 0; i < (parts.Length - 5); i++)
                    {
                        path += parts[i] + "\\";
                    }
                    path += "data";

                    if (!File.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                }                
                //var time = DateTimeOffset.Now.ToUnixTimeSeconds();
                using (FileStream fileStream = new FileStream(path + "\\" + bitmap+count+ ".png", FileMode.Create))
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }

                //LblStatus.Content = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
                Close();
            }
        }

        private void BtCapture_Click(object sender, RoutedEventArgs e)
        {
            if (capturing==true)
            {
                capturing = false;
                //LblStatus.Content = "Record is over";
                path = "";
                BtCapture.Content = "Start Cap.";
            }
            else
            {
                capturing = true;
                //LblStatus.Content = "Recording";
                BtCapture.Content = "Stop Cap.";
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
            

            if (capturing == true)
            {
                count += 1;
                LblStatus.Content = count.ToString();
                CaptureImage(colorBitmap);
            }
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

        private void FaceFrameReader_FrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            try
            {
                using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
                {
                    if (faceFrame == null)
                    {
                        return;
                    }
                    bool tracked;
                    tracked = faceFrame.IsTrackingIdValid;
                    if (!tracked)
                    {
                        return;
                    }

                    FaceFrameResult faceResult = faceFrame.FaceFrameResult;
                    int index = GetFaceSourceIndex(faceFrame.FaceFrameSource);
                    faceFrameResults[index] = faceResult;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
                Close();
            }
        }

        int GetFaceSourceIndex(FaceFrameSource source)
        {
            int index = -1;
            for (int i = 0; i < bodyCount; i++)
            {
                if (faceFrameSources[i] == source)
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        void InitializeFace()
        {
            FaceFrameFeatures faceFrameFeatures =
                    FaceFrameFeatures.BoundingBoxInColorSpace
                    | FaceFrameFeatures.PointsInColorSpace
                    | FaceFrameFeatures.RotationOrientation
                    | FaceFrameFeatures.FaceEngagement
                    | FaceFrameFeatures.Glasses
                    | FaceFrameFeatures.Happy
                    | FaceFrameFeatures.LeftEyeClosed
                    | FaceFrameFeatures.RightEyeClosed
                    | FaceFrameFeatures.LookingAway
                    | FaceFrameFeatures.MouthMoved
                    | FaceFrameFeatures.MouthOpen;
            faceFrameSources = new FaceFrameSource[bodyCount];
            faceFrameReaders = new FaceFrameReader[bodyCount];
            for (int i = 0; i < bodyCount; i++)
            {
                faceFrameSources[i] = new FaceFrameSource(kinectSensor, 0, faceFrameFeatures);
                faceFrameReaders[i] = faceFrameSources[i].OpenReader();
                faceFrameReaders[i].FrameArrived += FaceFrameReader_FrameArrived;
            }
            faceFrameResults = new FaceFrameResult[bodyCount];
            faceBrush = new List<Brush>()
                {
                    Brushes.White,
                    Brushes.Orange,
                    Brushes.Green,
                    Brushes.Red,
                    Brushes.LightBlue,
                    Brushes.Yellow
                };
        }

        
    }
}
