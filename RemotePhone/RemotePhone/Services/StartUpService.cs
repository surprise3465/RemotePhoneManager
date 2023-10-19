using AdvancedSharpAdbClient;

using RemotePhone.Database;
using RemotePhone.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RemotePhone.Services
{

    public interface IStartUpService
    {
        public void StartScan();
        public Task<VirtualPhone?> GetVirtualPhonePort();
        public Task<RealPhone?> GetRealPhoneSerial();
        public Task<VirtualPhone?> UpdateVirtualVisit(string UserId);
        public Task<RealPhone?> UpdateRealVisit(string UserId);
        public Task<VirtualPhone?> ExitVirtualPhone(string UserId);
        public Task<RealPhone?> ExitRealPhone(string UserId);
    }

    public class StartUpService : IStartUpService
    {
        protected readonly ApplicationDbContext _context;
        private static ILoggerService _logger;
        private Thread? scanThread;
        public static object locker = new object();

        public static List<DeviceData> Devices = new List<DeviceData>();

        public StartUpService(IServiceScopeFactory scopeFactory, ILoggerService logger)
        {
            _context = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _logger = logger;
        }

        public List<RealPhone> AddRealPhone()
        {
            var phones = new List<RealPhone>();

            var adb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScrcpyNet", "adb.exe");

            if (!File.Exists(adb))
            {
                _logger.LogError("adb.exe not exist");
            }

            if (!AdbServer.Instance.GetStatus().IsRunning)
            {
                AdbServer server = new AdbServer();
                StartServerResult result = server.StartServer(adb, false);
                if (result != StartServerResult.Started)
                {
                    _logger.LogError("Can't start adb server");
                }
            }

            AdbClient client = new AdbClient();

            Monitor.Enter(locker);
            try
            {
                Devices = client.GetDevices();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }

            foreach (var dev in Devices)
            {
                if (!dev.Serial.Contains("emul"))
                {
                    RealPhone phone = new RealPhone()
                    {
                        InUse = false,
                        Serial = dev.Serial,
                        UserId = string.Empty,
                        Lastvisit = DateTime.Now,
                    };

                    phones.Add(phone);
                }
            }

            return phones;
        }

        public List<VirtualPhone> AddVirtualPhone()
        {
            var phones = new List<VirtualPhone>();
            Process pro = new Process();
            pro.StartInfo.FileName = "cmd";
            pro.StartInfo.UseShellExecute = false;
            pro.StartInfo.RedirectStandardInput = true;
            pro.StartInfo.RedirectStandardOutput = true;
            pro.StartInfo.RedirectStandardError = true;
            pro.StartInfo.CreateNoWindow = true;
            pro.Start();
            pro.StandardInput.WriteLine("netstat -no -p TCP");
            pro.StandardInput.WriteLine("exit");
            Regex reg = new Regex("\\s+", RegexOptions.Compiled);
            string? line = null;
            List<string> infoList = new List<string>();
            while ((line = pro.StandardOutput.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Contains("TCP"))
                {
                    line = reg.Replace(line, ",");
                    infoList.Add(line);
                }
            }
            pro.Close();

            var PidList = Process.GetProcesses().Where(t => t.ProcessName.Contains("BoxHeadless")).Select(t => t.Id).ToList();
            foreach (var info in infoList)
            {
                string[] infos = info.Split(",");

                if (infos.Length == 5)
                {
                    var Port = Convert.ToInt32(infos[4]);
                    if (PidList.Contains(Port) && infos[1].Contains("127.0.0.1:"))
                    {
                        VirtualPhone phone = new VirtualPhone()
                        {
                            InUse = false,
                            Port = Convert.ToInt32(infos[1].Replace("127.0.0.1:", "")),
                            UserId = string.Empty,
                            Lastvisit = DateTime.Now,
                        };
                        phones.Add(phone);
                    }
                }
            }

            return phones;
        }

        public void StartScan()
        {
            scanThread = new Thread(ProcessScanThread);
            scanThread.IsBackground = true;
            scanThread.Start();
        }

        private void ProcessScanThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(5000);
                    RefreshRealPhone();
                    RefreshVirtualPhone();
                    RefreshInuseStatus();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex.Message);
                }
            }
        }

        private void RefreshRealPhone()
        {
            Monitor.Enter(locker);
            try
            {
                var NewList = AddRealPhone();
                var ExistList = _context.RealPhones.ToList();
                var MissList = ExistList.Except(NewList);
                var AddList = NewList.Except(ExistList);


                if (AddList != null && AddList.Count() > 0)
                {
                    _context.RealPhones.AddRange(AddList);
                    _context.SaveChanges();
                }

                if (MissList != null && MissList.Count() > 0)
                {
                    _context.RealPhones.RemoveRange(MissList);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }
        }


        private void RefreshVirtualPhone()
        {
            Monitor.Enter(locker);
            try
            {
                var NewList = AddVirtualPhone();
                var ExistList = _context.VirtualPhones.ToList();
                var MissList = ExistList.Except(NewList);
                var AddList = NewList.Except(ExistList);
                if (AddList != null && AddList.Count() > 0)
                {
                    _context.VirtualPhones.AddRange(AddList);
                    _context.SaveChanges();
                }

                if (MissList != null && MissList.Count() > 0)
                {
                    _context.VirtualPhones.RemoveRange(MissList);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }
        }

        private void RefreshInuseStatus()
        {
            Monitor.Enter(locker);
            try
            {
                var rPhones = _context.RealPhones.ToList();
                var vPhones = _context.VirtualPhones.ToList();
                DateTime dateTime = DateTime.Now;
                foreach (var phone in rPhones)
                {
                    if (phone.Lastvisit.AddMinutes(2) < dateTime)
                    {
                        phone.InUse = false;
                        _context.RealPhones.Update(phone);
                        _context.SaveChanges();
                    }
                }

                dateTime = DateTime.Now;
                foreach (var phone in vPhones)
                {
                    if (phone.Lastvisit.AddMinutes(2) < dateTime)
                    {
                        phone.InUse = false;
                        _context.VirtualPhones.Update(phone);
                        _context.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }
        }

        public async Task<VirtualPhone?> GetVirtualPhonePort()
        {
            Monitor.Enter(locker);
            try
            {
                var phone = _context.VirtualPhones.FirstOrDefault(t => t.InUse == false);
                if (phone != null)
                {
                    phone.InUse = true;
                    phone.UserId = Guid.NewGuid().ToString("N").ToUpper();
                    _context.VirtualPhones.Update(phone);
                    await _context.SaveChangesAsync();
                }
                return phone;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }
            return null;
        }

        public async Task<RealPhone?> GetRealPhoneSerial()
        {
            Monitor.Enter(locker);
            try
            {
                var phone = _context.RealPhones?.FirstOrDefault(t => t.InUse == false);
                if (phone != null)
                {
                    phone.InUse = true;
                    phone.UserId = Guid.NewGuid().ToString("N").ToUpper();
                    _context.RealPhones?.Update(phone);
                    await _context.SaveChangesAsync();
                }
                return phone;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }
            return null;
        }

        public async Task<VirtualPhone?> UpdateVirtualVisit(string UserId)
        {
            Monitor.Enter(locker);
            try
            {
                var phone = _context.VirtualPhones?.SingleOrDefault(t => t.UserId == UserId);
                if (phone != null)
                {
                    phone.Lastvisit = DateTime.Now;
                    _context.VirtualPhones?.Update(phone);
                    await _context.SaveChangesAsync();
                }
                return phone;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }
            return null;
        }

        public async Task<RealPhone?> UpdateRealVisit(string UserId)
        {
            Monitor.Enter(locker);
            try
            {
                var phone = _context.RealPhones.SingleOrDefault(t => t.UserId == UserId);
                if (phone != null)
                {
                    phone.Lastvisit = DateTime.Now;
                    _context.RealPhones?.Update(phone);
                    await _context.SaveChangesAsync();
                }

                return phone;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }
            return null;
        }

        public async Task<VirtualPhone?> ExitVirtualPhone(string UserId)
        {
            Monitor.Enter(locker);
            try
            {
                var phone = _context.VirtualPhones?.SingleOrDefault(t => t.UserId == UserId);
                if (phone != null)
                {
                    phone.InUse = false;
                    phone.UserId = string.Empty;
                    _context.VirtualPhones?.Update(phone);
                    await _context.SaveChangesAsync();
                }
                return phone;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }

            return null;
        }

        public async Task<RealPhone?> ExitRealPhone(string UserId)
        {
            Monitor.Enter(locker);
            try
            {
                var phone = _context.RealPhones?.SingleOrDefault(t => t.UserId == UserId);
            if (phone != null)
            {
                phone.InUse = false;
                phone.UserId = string.Empty;
                _context.RealPhones?.Update(phone);
                await _context.SaveChangesAsync();
            }
                return phone;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message);
            }
            finally
            {
                Monitor.Exit(locker);
            }

            return null;
        }
    }
}
