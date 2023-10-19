using AdvancedSharpAdbClient;
using Microsoft.AspNetCore.SignalR;
using RemotePhone.Services;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RemotePhone.Hubs
{
    public class Scrcpy
    {
        private ILoggerService _logger;
        public string DeviceName { get; private set; } = "";
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public long Bitrate { get; set; } = 8000000;
        public string ScrcpyServerFile { get; set; } = "ScrcpyNet/scrcpy-server.jar";

        public bool Connected { get; private set; }

        private Thread? videoThread;
        private TcpClient? videoClient;
        private TcpListener? listener;
        private CancellationTokenSource? cts;

        private readonly AdbClient adb;
        private readonly DeviceData device;

        private IHubContext<ImageHub> _hubContext;
        private static readonly ArrayPool<byte> pool = ArrayPool<byte>.Shared;
        private string ConnectionId;
        public Scrcpy(DeviceData device, ILoggerService log, IHubContext<ImageHub> hubContext, string connectionId)
        {
            ConnectionId = connectionId;
            _logger = log;
            _hubContext = hubContext;
            adb = new AdbClient();
            this.device = device;
        }

        public void Start(long timeoutMs = 5000)
        {
            if (Connected)
                throw new Exception("Already connected.");

            int port = GetAvailablePort(IPAddress.Loopback);

            MobileServerSetup(port);

            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            MobileServerStart();

            int waitTimeMs = 0;
            while (!listener.Pending())
            {
                Thread.Sleep(10);
                waitTimeMs += 10;

                if (waitTimeMs > timeoutMs)
                    throw new Exception("Timeout while waiting for server to connect.");
            }

            videoClient = listener.AcceptTcpClient();
            _logger.LogInfo("Video socket connected.");

            if (!listener.Pending())
                throw new Exception("Server is not sending a second connection request. Is 'control' disabled?");

            _logger.LogInfo("Control socket connected.");

            ReadDeviceInfo();

            cts = new CancellationTokenSource();

            videoThread = new Thread(VideoMain) { Name = "ScrcpyNet Video" };

            videoThread.Start();

            Connected = true;

            // ADB forward/reverse is not needed anymore.
            MobileServerCleanup();
        }

        public void Stop()
        {
            if (!Connected)
                throw new Exception("Not connected.");

            cts?.Cancel();

            videoThread?.Join();
            listener?.Stop();
        }

        private void ReadDeviceInfo()
        {
            if (videoClient == null)
                throw new Exception("Can't read device info when videoClient is null.");

            var infoStream = videoClient.GetStream();
            infoStream.ReadTimeout = 2000;

            // Read 68-byte header.
            var deviceInfoBuf = pool.Rent(68);
            int bytesRead = infoStream.Read(deviceInfoBuf, 0, 68);

            if (bytesRead != 68)
                throw new Exception($"Expected to read exactly 68 bytes, but got {bytesRead} bytes.");

            // Decode device name from header.
            var deviceInfoSpan = deviceInfoBuf.AsSpan();
            DeviceName = Encoding.UTF8.GetString(deviceInfoSpan[..64]).TrimEnd(new[] { '\0' });
            _logger.LogInfo("Device name: " + DeviceName);

            Width = BinaryPrimitives.ReadInt16BigEndian(deviceInfoSpan[64..]);
            Height = BinaryPrimitives.ReadInt16BigEndian(deviceInfoSpan[66..]);
            _logger.LogInfo($"Initial texture: {Width}x{Height}");

            pool.Return(deviceInfoBuf);
        }

        private void VideoMain()
        {
            // Both of these should never happen.
            if (videoClient == null) throw new Exception("videoClient is null.");
            if (cts == null) throw new Exception("cts is null.");

            var videoStream = videoClient.GetStream();
            videoStream.ReadTimeout = 2000;

            int bytesRead;
            var metaBuf = pool.Rent(12);

            Stopwatch sw = new();

            while (!cts.Token.IsCancellationRequested)
            {
                // Read metadata (each packet starts with some metadata)
                try
                {
                    bytesRead = videoStream.Read(metaBuf, 0, 12);
                }
                catch (IOException ex)
                {
                    // Ignore timeout errors.
                    if (ex.InnerException is SocketException x && x.SocketErrorCode == SocketError.TimedOut)
                        continue;
                    throw ex;
                }

                if (bytesRead != 12)
                    throw new Exception($"Expected to read exactly 12 bytes, but got {bytesRead} bytes.");

                sw.Restart();

                // Decode metadata
                var metaSpan = metaBuf.AsSpan();
                var presentationTimeUs = BinaryPrimitives.ReadInt64BigEndian(metaSpan);
                var packetSize = BinaryPrimitives.ReadInt32BigEndian(metaSpan[8..]);

                // Read the whole frame, this might require more than one .Read() call.
                var packetBuf = pool.Rent(packetSize);
                var pos = 0;
                var bytesToRead = packetSize;

                while (bytesToRead != 0 && !cts.Token.IsCancellationRequested)
                {
                    bytesRead = videoStream.Read(packetBuf, pos, bytesToRead);

                    if (bytesRead == 0)
                        throw new Exception("Unable to read any bytes.");

                    pos += bytesRead;
                    bytesToRead -= bytesRead;
                }

                if (!cts.Token.IsCancellationRequested)
                {
                    var bytesend = new byte[packetSize + sizeof(long)];
                    Array.Copy(BitConverter.GetBytes(presentationTimeUs), bytesend, sizeof(long));
                    Array.Copy(packetBuf, 0, bytesend, sizeof(long), packetSize);

                    _hubContext.Clients.Client(ConnectionId).SendAsync("VideoBuffer", bytesend);     
                    
                    //_logger.LogInfo($"Received and decoded a packet in {sw.ElapsedMilliseconds} ms");
                }

                sw.Stop();

                pool.Return(packetBuf);
            }
        }

        private void MobileServerSetup(int port)
        {
            MobileServerCleanup();

            // Push scrcpy-server.jar
            UploadMobileServer();

            // Create port reverse rule
            adb.CreateReverseForward(device, "localabstract:scrcpy", $"tcp:{port}", true);
        }

        /// <summary>
        /// Remove ADB forwards/reverses.
        /// </summary>
        private void MobileServerCleanup()
        {
            // Remove any existing network stuff.
            adb.RemoveAllForwards(device);
            adb.RemoveAllReverseForwards(device);
        }

        /// <summary>
        /// Start the scrcpy server on the android device.
        /// </summary>
        /// <param name="bitrate"></param>
        private void MobileServerStart()
        {
            _logger.LogInfo("Starting scrcpy server...");

            var cts = new CancellationTokenSource();
            var receiver = new LogOutputReceiver();

            string version = "1.23";
            int maxFramerate = 0;
            ScrcpyLockVideoOrientation orientation = ScrcpyLockVideoOrientation.Unlocked; // -1 means allow rotate
            bool control = true;
            bool showTouches = false;
            bool stayAwake = false;

            var cmds = new List<string>
            {
                "CLASSPATH=/data/local/tmp/scrcpy-server.jar",
                "app_process",

                // Unused
                "/",

                // App entry point, or something like that.
                "com.genymobile.scrcpy.Server",

                version,
                "log_level=debug",
                $"bit_rate={Bitrate}"
            };

            if (maxFramerate != 0)
                cmds.Add($"max_fps={maxFramerate}");

            if (orientation != ScrcpyLockVideoOrientation.Unlocked)
                cmds.Add($"lock_video_orientation={(int)orientation}");

            cmds.Add("tunnel_forward=false");
            //cmds.Add("crop=-");
            cmds.Add($"control={control}");
            cmds.Add("display_id=0");
            cmds.Add($"show_touches={showTouches}");
            cmds.Add($"stay_awake={stayAwake}");
            cmds.Add("power_off_on_close=false");
            cmds.Add("downsize_on_error=true");
            cmds.Add("cleanup=true");

            string command = string.Join(" ", cmds);

            _logger.LogInfo("Start command: " + command);
            _ = adb.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token);
        }

        private void UploadMobileServer()
        {
            using SyncService service = new(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)), device);
            using Stream stream = File.OpenRead(ScrcpyServerFile);
            service.Push(stream, "/data/local/tmp/scrcpy-server.jar", 444, DateTime.Now, null, CancellationToken.None);
        }

        public static int GetAvailablePort(IPAddress ip)
        {
            TcpListener l = new TcpListener(ip, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
