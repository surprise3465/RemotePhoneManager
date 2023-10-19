using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Channels;
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
using System.Windows.Threading;


namespace PhoneClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        HubConnection connection;
        double image_width;
        double image_hight;
        const double container_width = 400.0;
        const double container_height = 740.0;
        System.Windows.Point mouseDown;
        DateTime mouseDownTime;
        string serialId = "";
        string UserId = "";
        bool isConnected = false;
        static HttpClient client = new HttpClient();
        private DispatcherTimer timer = new DispatcherTimer();
        private WriteableBitmap bmp;
        public VideoStreamDecoder videoStreamDecoder;
        public MainWindow()
        {
            InitializeComponent();

            connection = new HubConnectionBuilder()
              .WithUrl("http://localhost:30065/ImageHub")
              //.AddMessagePackProtocol()
              .Build();
            connection.KeepAliveInterval = TimeSpan.FromMilliseconds(15 * 1000);

            videoStreamDecoder = new VideoStreamDecoder();
            videoStreamDecoder.OnFrame += OnFrameData;

            timer.Interval = TimeSpan.FromMilliseconds(5000);
            timer.Tick += HeartBeatTimer;
            timer.Start();
        }

        private unsafe void OnFrameData(object sender, FrameData frameData)
        {
            try
            {
                // The timeout is required. Otherwise this will block forever when the application is about to exit but the videoThread sends a last frame.
                // The DispatcherPriority has been randomly selected, so it might not be the optimal value.
                Dispatcher.Invoke(() =>
                {
                    image_width = frameData.Width;
                    image_hight = frameData.Height;
                    if (bmp == null || bmp.Width != frameData.Width || bmp.Height != frameData.Height)
                    {
                        bmp = new WriteableBitmap(frameData.Width, frameData.Height, 96, 96, PixelFormats.Bgra32, null);
                        ImageContainer.Source = bmp;
                    }

                    try
                    {
                        bmp.Lock();
                        var dest = new Span<byte>(bmp.BackBuffer.ToPointer(), frameData.Data.Length);
                        frameData.Data.CopyTo(dest);
                        bmp.AddDirtyRect(new Int32Rect(0, 0, frameData.Width, frameData.Height));
                    }
                    finally
                    {
                        bmp.Unlock();
                    }
                }/*, DispatcherPriority.Send, default, TimeSpan.FromMilliseconds(200)*/);
            }
            catch (TimeoutException)
            {
                //log.Debug("Ignoring TimeoutException inside OnFrame.");
            }
            catch (TaskCanceledException)
            {
                //log.Debug("Ignoring TaskCanceledException inside OnFrame.");
            }           
        }

        private async void GetSerialId()
        {
            try
            {
                var result = await client.GetAsync("http://localhost:30065/Phone/Port?type=real");
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var res = await result.Content.ReadAsStringAsync();
                    var respounse = JsonConvert.DeserializeObject<PortStatusResponse>(res);
                    serialId = respounse.Message;
                    UserId = respounse.UserId;
                    if (serialId == "No available phone")
                    {
                        isConnected = false;
                        serialId = string.Empty;
                    }
                }
                else
                {
                    isConnected = false;
                }
            }
            catch (Exception ex)
            {

            }
        }

        private async void HeartBeat()
        {
            try
            {
                HeartBeat heartBeat = new HeartBeat()
                {
                    UserId = UserId,
                };
                var str = JsonConvert.SerializeObject(heartBeat);
                HttpContent content = new StringContent(str);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                var result = await client.PostAsync("http://localhost:30065/Phone/HeartBeat", content);
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var res = await result.Content.ReadAsStringAsync();
                    var respounse = JsonConvert.DeserializeObject<StatusResponse>(res);
                    var message = respounse.Message;
                    var status = respounse.Status;
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void HeartBeatTimer(object sender, EventArgs e)
        {
            if (isConnected)
            {
                this.Dispatcher.Invoke(new Action(() => HeartBeat()));
            }
        }

        private async void ExitPhone()
        {
            try
            {
                HeartBeat heartBeat = new HeartBeat()
                {
                    UserId = UserId,
                };
                var str = JsonConvert.SerializeObject(heartBeat);
                HttpContent content = new StringContent(str);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                var result = await client.PostAsync("http://localhost:30065/Phone/Exit", content);
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var res = await result.Content.ReadAsStringAsync();
                    var respounse = JsonConvert.DeserializeObject<StatusResponse>(res);
                    var message = respounse.Message;
                    var status = respounse.Status;
                }
            }
            catch (Exception ex)
            {

            }
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (isConnected)
                {
                    //await connection.SendAsync("SendMessage", serialId);
                    await connection.SendAsync("StartScrcpy", serialId);
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {

            }


            Thread thread = new Thread(async () =>
            {
                Thread.Sleep(5 * 1000);
                while (isConnected)
                {
                    try
                    {
                        List<byte> data = new List<byte>();
                        var cancellationTokenSource = new CancellationTokenSource();
                        var channel = await connection.StreamAsChannelAsync<byte[]>(
                            "DownloadFileAsByteArray", serialId, false, cancellationTokenSource.Token);

                        while (await channel.WaitToReadAsync())
                        {
                            while (channel.TryRead(out var count))
                            {
                                data.AddRange(count);
                            }
                        }

                        this.Dispatcher.Invoke(() =>
                        {
                            ImageBytesToShow(data.ToArray());
                        });
                    }
                    catch (Exception ex)
                    {

                    }
                    finally
                    {

                    }
                    Thread.Sleep(15);
                }
            });

            //thread.Start();
        }

        private void ImageToShow(byte[] imageSource)
        {
            try
            {
                this.Dispatcher.Invoke(new Action(() =>
                {
                    var resImage = BytesToImage(imageSource);

                    if (resImage != null)
                    {
                        image_width = resImage.Width; image_hight = resImage.Height;
                        ImageContainer.Source = resImage;
                    }
                }
                ));
            }
            catch (Exception)
            {

            }
        }

        private unsafe void ImageToShow(FrameDataClass frameData)
        {
            try
            {
                this.Dispatcher.Invoke(new Action(() =>
                {
                    DateTime startT = DateTime.Now;
                    //Console.WriteLine("ImageToShow:" + startT.ToString());
                    if (bmp == null || bmp.Width != frameData.Width || bmp.Height != frameData.Height)
                    {
                        bmp = new WriteableBitmap(frameData.Width, frameData.Height, 96, 96, PixelFormats.Bgr32, null);
                        ImageContainer.Source = bmp;
                    }

                    try
                    {
                        bmp.Lock();
                        var dest = new Span<byte>(bmp.BackBuffer.ToPointer(), frameData.Data.Length);
                        frameData.Data.CopyTo(dest);
                        bmp.AddDirtyRect(new Int32Rect(0, 0, frameData.Width, frameData.Height));
                    }
                    finally
                    {
                        bmp.Unlock();
                    }
                    DateTime endT = DateTime.Now;
                    var time = (endT - startT).TotalMilliseconds;
                    Console.WriteLine("ImageToShow Million:" + time);
                }
                ));
            }
            catch (Exception)
            {

            }
        }

        private unsafe void ImageBytesToShow(byte[] data)
        {
            try
            {
                this.Dispatcher.Invoke(new Action(() =>
                {
                    DateTime startT = DateTime.Now;
                    Console.WriteLine("startT:" + startT.ToString());
                    var Height = BitConverter.ToUInt16(data, 0);
                    var Width = BitConverter.ToUInt16(data, 2);
                    image_width = Width;
                    image_hight = Height;
                    var Length = BitConverter.ToInt32(data, 4);
                    if (bmp == null || bmp.Width != Width || bmp.Height != Height)
                    {
                        bmp = new WriteableBitmap(Width, Height, 96, 96, PixelFormats.Bgr32, null);
                        ImageContainer.Source = bmp;
                    }

                    try
                    {
                        bmp.Lock();
                        var dest = new Span<byte>(bmp.BackBuffer.ToPointer(), Length);
                        data.AsSpan().Slice(8).CopyTo(dest);
                        bmp.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
                    }
                    finally
                    {
                        bmp.Unlock();
                    }
                    DateTime endT = DateTime.Now;
                    var time = (endT - startT).TotalMilliseconds;
                    Console.WriteLine("Image Million:" + time);
                }
                ));
            }
            catch (Exception)
            {

            }
        }

        public static BitmapImage BytesToImage(byte[] buffer)
        {
            using (var bitmap = new Bitmap(new MemoryStream(buffer)))
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    try
                    {
                        bitmap.Save(stream, ImageFormat.Png); // 坑点：格式选Bmp时，不带透明度
                        stream.Position = 0;
                        BitmapImage result = new BitmapImage();
                        result.BeginInit();
                        // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
                        // Force the bitmap to load right now so we can dispose the stream.
                        result.CacheOption = BitmapCacheOption.OnLoad;
                        result.StreamSource = stream;
                        result.EndInit();
                        result.Freeze();
                        return result;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    finally
                    {

                    }
                }
            }
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            connection.On<byte[]>("VideoBuffer", msg =>
            {
                var span = msg.AsSpan();
                var buffer = span.Slice(8).ToArray();
                var pts = BitConverter.ToInt64(span.Slice(0, 8).ToArray(), 0);
                videoStreamDecoder.Decode(buffer, pts);
            });

            try
            {
                GetSerialId();
                if (!isConnected)
                {
                    await connection.StartAsync();
                    isConnected = true;
                }
            }
            catch
            {

            }
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (isConnected)
                {
                    isConnected = false;
                    await connection.StopAsync();
                }
                ExitPhone();
            }
            catch
            {

            }
        }

        private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isConnected)
            {
                mouseDown = e.GetPosition(ImageContainer);
                mouseDownTime = DateTime.Now;
            }
        }

        private void ImageContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isConnected)
            {
                return;
            }

            var mouseUp = e.GetPosition(ImageContainer);
            var mouseUpTime = DateTime.Now;

            double cor_X = (image_width / container_width);
            double cor_y = (image_hight / container_height);

            int realx1 = (int)(cor_X * mouseDown.X);
            int realy1 = (int)(cor_y * mouseDown.Y);
            int realx2 = (int)(cor_X * mouseUp.X);
            int realy2 = (int)(cor_y * mouseUp.Y);

            var timespan = (long)mouseUpTime.Subtract(mouseDownTime).Milliseconds;
            //1.5s 点划
            if (timespan > 1500)
            {
                connection.SendAsync("Swipe", serialId, realx1, realy1, realx2, realy2, timespan);
            }
            else
            {
                if (Math.Abs(realx1 - realx2) < 10 && Math.Abs(realy1 - realy2) < 20)
                {
                    connection.SendAsync("Touch", serialId, realx1, realy1);
                }
                else
                {
                    connection.SendAsync("Swipe", serialId, realx1, realy1, realx2, realy2, timespan);
                }
            }
        }

        public async void SendApkData(string FilePath)
        {
            int chunkSizeInKb = 10;
            int chunkSize = chunkSizeInKb * 1024;

            using (FileStream fs = File.OpenRead(FilePath))
            {
                var channel = Channel.CreateUnbounded<string>();
                await connection.SendAsync("UploadApk", channel.Reader, serialId);
                long chunkLength = fs.Length > chunkSize ? chunkSize : fs.Length;
                byte[] b = new byte[chunkLength];

                try
                {
                    while (fs.Position < fs.Length)
                    {
                        int size = fs.Read(b, 0, b.Length);
                        await channel.Writer.WriteAsync(Encoding.UTF8.GetString(b, 0, size));
                        await Task.Delay(10);
                    }
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    channel.Writer.Complete();
                }
            }
        }

        private void btnUpdateFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            var res = dialog.ShowDialog();
            if (res != null && res == true)
            {
                string filePath = dialog.FileName;
                Task.Run(() => SendApkData(filePath));
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            if (isConnected)
            {
                isConnected = false;
                await connection.StopAsync();
            }
            ExitPhone();
        }
    }
}
