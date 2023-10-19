using AdvancedSharpAdbClient;
using NLog;

namespace RemotePhone.Hubs
{
    public class LogOutputReceiver : MultiLineReceiver
    {
        private static NLog.ILogger logger = LogManager.GetCurrentClassLogger();
        protected override void ProcessNewLines(IEnumerable<string> lines)
        {
            foreach (var line in lines)
                logger.Info($"[server] {line}");
        }
    }
}
