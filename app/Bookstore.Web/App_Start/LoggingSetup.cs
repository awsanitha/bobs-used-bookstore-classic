using Bookstore.Common;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Targets;

namespace Bookstore.Web
{
    public static class LoggingSetup
    {
        public static void ConfigureLogging(string loggingService)
        {
            var config = new LoggingConfiguration();

            Target loggingTarget;

            if (loggingService == "aws")
            {
                loggingTarget = new AWSTarget { LogGroup = Constants.AppName };
            }
            else
            {
                loggingTarget = new DebuggerTarget();
            }

            config.AddTarget("logging", loggingTarget);
            config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));

            LogManager.Configuration = config;
        }
    }
}
