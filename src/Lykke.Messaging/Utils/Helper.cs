using System;
using System.Threading;
using Lykke.Messaging.Contract;
using Microsoft.Extensions.PlatformAbstractions;

namespace Lykke.Messaging.Utils
{
    public class Helper
    {
        public static Action CallOnlyOnce(Action action)
        {
            var alreadyCalled = 0;
            Action ret = () =>
            {
                if (Interlocked.Exchange(ref alreadyCalled, 1) != 0)
                    return;
                action();
            };

            return ret;
        }

        public static string BuildSessionDisplayName(Destination destination)
        {
            var appName = PlatformServices.Default.Application.ApplicationName;
            var appVersion = PlatformServices.Default.Application.ApplicationVersion;
            
            return $"{appName} {appVersion} {destination}";
        }
    }
}