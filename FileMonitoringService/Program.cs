using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileMonitoringService
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.UserInteractive)
            {
                FilesMonitoring filesMonitoring = new FilesMonitoring();
                filesMonitoring.DebuggingConsoleMode();
                Thread.Sleep(11000);
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new FilesMonitoring()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
