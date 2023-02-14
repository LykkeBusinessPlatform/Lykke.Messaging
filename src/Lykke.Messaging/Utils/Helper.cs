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

        [Obsolete("Assumes 1 session per destination which is not efficient. Use BuildSessionDisplayName instead.")]
        public static string BuildSessionDisplayName(Destination destination)
        {
            var appName = PlatformServices.Default.Application.ApplicationName;
            var appVersion = PlatformServices.Default.Application.ApplicationVersion;
            
            return $"{appName} {appVersion} {destination}";
        }

        /// <summary>
        /// Builds a sessions display name. Assumes 1 session per host but
        /// doesn't limit the number of sessions per host. In case of multiple
        /// sessions, the display name will be the same for all of them.
        /// </summary>
        /// <returns></returns>
        public static string BuildSessionDisplayName()
        {
            var appName = PlatformServices.Default.Application.ApplicationName;
            var appVersion = PlatformServices.Default.Application.ApplicationVersion;
            
            return $"{appName} {appVersion}";
        }
    }
}