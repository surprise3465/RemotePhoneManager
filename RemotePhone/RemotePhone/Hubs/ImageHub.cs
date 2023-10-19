using AdvancedSharpAdbClient;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using RemotePhone.Database;
using RemotePhone.Services;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading.Channels;
using FFmpeg.AutoGen;

namespace RemotePhone.Hubs
{
    public class SerialState
    {
        public string? Serial { set; get; }
        public bool IsConnected { set; get; }
    }


    public partial class FrameDataClass
    {
        public int Width { set; get; }
        public int Height { set; get; }
        public int FrameNumber { set; get; }
        public byte[]? Data { set; get; }
        public int Length { set; get; }
        public int AVPixelFormat { set; get; }
    }

    public class ImageHub : Hub
    {
        private ILoggerService _logger;
        private IServiceProvider _sp;
        public ImageHub(IServiceProvider sp, ILoggerService logger)
        {
            _sp = sp;
            _logger = logger;
        }

        static ConcurrentDictionary<string, Scrcpy> scrcpyDic = new ConcurrentDictionary<string, Scrcpy>();
        static ConcurrentDictionary<string, DeviceData> deviceDic = new ConcurrentDictionary<string, DeviceData>();

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {

            if (scrcpyDic.ContainsKey(Context.ConnectionId))
            {
                scrcpyDic[Context.ConnectionId].Stop();
            }

            var res = scrcpyDic.TryRemove(Context.ConnectionId, out var value);
            await base.OnDisconnectedAsync(exception);
        }

        public Task StartScrcpy(string serialId)
        {
            DeviceData? deviceData = null;
            if (!deviceDic.ContainsKey(serialId))
            {
                deviceData = StartUpService.Devices.FirstOrDefault(t => t.Serial == serialId);
                if (deviceData == null)
                {
                    throw new Exception("deviceData not found");
                }
                deviceDic.TryAdd(serialId, deviceData);
            }
            else
            {
                deviceData = deviceDic[serialId];
            }
            IHubContext<ImageHub>? context = _sp.GetRequiredService<IHubContext<ImageHub>>();
            if (context != null)
            {
                var scrcpy = new Scrcpy(deviceData, _logger, context, Context.ConnectionId);
                scrcpyDic.TryAdd(Context.ConnectionId, scrcpy);
                scrcpy.Start();
                //scrcpy.VideoStreamDecoder.OnFrame += OnFrameData;
            }

            return Task.CompletedTask;
        }

        //public ChannelReader<byte[]> DownloadFileAsByteArray(string serialId, bool throwException, CancellationToken cancellationToken)
        //{
        //    var channel = Channel.CreateUnbounded<byte[]>();
        //    _ = WriteToChannelAsByteArray(channel.Writer, serialId, throwException, cancellationToken);
        //    return channel.Reader;
        //}
        //private async Task WriteToChannelAsByteArray(ChannelWriter<byte[]> writer, string serialId, bool throwException, CancellationToken cancellationToken)
        //{
        //    Exception? localException = null;
        //    int numOfChunks = 0;
        //    int chunkSizeInKb = 10;
        //    try
        //    {
        //        DateTime startT = DateTime.Now;
        //        Console.WriteLine("startT:" + startT.ToString());

        //        if (!scrcpyDic.ContainsKey(Context.ConnectionId))
        //        {
        //            throw new Exception("deviceData not found");
        //        }

        //        int chunkSize = (int)(chunkSizeInKb * 1024);

        //        var frame = scrcpyDic[Context.ConnectionId].VideoStreamDecoder.GetLastFrame();
        //        if (frame != null)
        //        {
        //            ushort Height = (ushort)frame.Height;
        //            ushort Width = (ushort)frame.Width;
        //            var bin = new byte[frame.length + 8];
        //            int position = 0;
        //            await writer.WriteAsync(BitConverter.GetBytes(Height), cancellationToken);
        //            await writer.WriteAsync(BitConverter.GetBytes(Width), cancellationToken);
        //            await writer.WriteAsync(BitConverter.GetBytes(frame.length), cancellationToken);
        //            //Array.Copy(BitConverter.GetBytes(Height), 0, bin, 0, 2);
        //            //Array.Copy(BitConverter.GetBytes(Width), 0, bin, 2, 2);
        //            //Array.Copy(BitConverter.GetBytes(frame.length), 0, bin, 4, 4);
        //            //Array.Copy(frame.Data.ToArray(), 0, bin, 8, frame.length);
        //            while (position < frame.length)
        //            {
        //                int res = frame.length - position;
        //                int chunkLength = res > chunkSize ? chunkSize : res;
        //                var byteSend = frame.Data.Slice(position, chunkLength).ToArray();
        //                if (cancellationToken.IsCancellationRequested)
        //                {
        //                    break;
        //                }
        //                position += chunkLength;
        //                //_logger.LogInfo($"chunk sent: {Convert.ToBase64String(b)}");
        //                await writer.WriteAsync(byteSend, cancellationToken);
        //                numOfChunks++;
        //            }
        //        }

        //        DateTime endT = DateTime.Now;
        //        var time = (endT - startT).TotalMilliseconds;
        //        Console.WriteLine("Write Million:" + time);
        //        //_logger.LogInfo($"Downstream: Total {numOfChunks} chunks written to channel");
        //    }
        //    catch (Exception ex)
        //    {
        //        localException = ex;
        //        _logger.LogError("Error occurred while writing to channel, Exception: " + ex.Message);
        //    }
        //    finally
        //    {
        //        if (throwException && localException == null)
        //        {
        //            /* Due to this issue: https://github.com/dotnet/aspnetcore/issues/33753, currently only OperationCanceledException
        //             are caught by SignalR.*/
        //            localException = new OperationCanceledException("Custom Exception thrown from service");
        //        }
        //        writer.TryComplete(localException);
        //    }
        //}

        public Task Touch(string serialId, int x, int y)
        {
            DeviceData? deviceData = null;
            if (!deviceDic.ContainsKey(serialId))
            {
                deviceData = StartUpService.Devices.FirstOrDefault(t => t.Serial == serialId);
                if (deviceData == null)
                {
                    throw new Exception("deviceData not found");
                }
                deviceDic.TryAdd(serialId, deviceData);
            }
            else
            {
                deviceData = deviceDic[serialId];
            }

            AdbClient client = new AdbClient();
            client.Click(deviceData, new Cords(x, y));
            return Task.CompletedTask;
        }

        public Task Swipe(string serialId, int x, int y, int x2, int y2, long speed)
        {
            DeviceData? deviceData = null;
            if (!deviceDic.ContainsKey(serialId))
            {
                deviceData = StartUpService.Devices.FirstOrDefault(t => t.Serial == serialId);
                if (deviceData == null)
                {
                    throw new Exception("deviceData not found");
                }
                deviceDic.TryAdd(serialId, deviceData);
            }
            else
            {
                deviceData = deviceDic[serialId];
            }

            AdbClient client = new AdbClient();
            client.Swipe(deviceData, x, y, x2, y2, speed);
            return Task.CompletedTask;
        }

        public async Task UploadApk(ChannelReader<string> stream, string serialId)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                while (await stream.WaitToReadAsync())
                {
                    while (stream.TryRead(out var item))
                    {
                        if (item is not null)
                        {
                            ms.Write(Encoding.UTF8.GetBytes(item));
                        }
                    }
                }

                ms.Position = 0;

                try
                {
                    DeviceData? deviceData = null;
                    if (!deviceDic.ContainsKey(serialId))
                    {
                        deviceData = StartUpService.Devices.FirstOrDefault(t => t.Serial == serialId);
                        if (deviceData == null)
                        {
                            throw new Exception("deviceData not found");
                        }
                        deviceDic.TryAdd(serialId, deviceData);
                    }
                    else
                    {
                        deviceData = deviceDic[serialId];
                    }
                    AdbClient client = new AdbClient();
                    client.Install(deviceData, ms, new string[] { "-r", "-d", "-g" });
                }
                catch (Exception)
                {

                }
            }
        }
    }
}
