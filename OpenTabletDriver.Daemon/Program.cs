﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JKang.IpcServiceFramework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTabletDriver.Contracts;
using OpenTabletDriver.Native;

namespace OpenTabletDriver.Daemon
{
    partial class Program
    {
        static async Task Main(string[] args)
        {
            using (var instance = new Instance("OpenTabletDriver.Daemon"))
            {
                if (instance.AlreadyExists)
                {
                    Console.WriteLine("OpenTabletDriver Daemon is already running.");
                    Thread.Sleep(1000);
                    return;
                }

                AppDomain.CurrentDomain.UnhandledException += (sender, e) => 
                {
                    var exception = (Exception)e.ExceptionObject;
                    File.WriteAllLines(Path.Join(AppInfo.Current.AppDataDirectory, "daemon.log"),
                        new string[]
                        {
                            DateTime.Now.ToString(),
                            exception.GetType().FullName,
                            exception.Message,
                            exception.Source,
                            exception.StackTrace,
                            exception.TargetSite.Name
                        }
                    );
                };

                var rootCommand = new RootCommand("OpenTabletDriver")
                {
                    new Option(new string[] { "--appdata", "-a" }, "Application data directory")
                    {
                        Argument = new Argument<DirectoryInfo>("appdata")
                    },
                    new Option(new string[] { "--config", "-c" }, "Configuration directory")
                    {
                        Argument = new Argument<DirectoryInfo> ("config")
                    },
                    new Option(new string[] { "--service", "-s"}, "Run as a service")
                    {
                        Argument = new Argument<bool>("runAsService")
                    }
                };
                rootCommand.Handler = CommandHandler.Create<DirectoryInfo, DirectoryInfo, bool>((appdata, config, runAsService) => 
                {
                    AppInfo.Current.AppDataDirectory = appdata?.FullName;
                    AppInfo.Current.ConfigurationDirectory = config?.FullName;
                    RunAsService = runAsService;
                });
                rootCommand.Invoke(args);

                Daemon = new DriverDaemon();
                var hostBuilder = CreateHostBuilder();
                if (RunAsService && SystemInfo.CurrentPlatform == RuntimePlatform.Windows)
                    hostBuilder = hostBuilder.UseWindowsService();
                await hostBuilder.Build().RunAsync();
            }
        }

        static IHostBuilder CreateHostBuilder() => 
            Host.CreateDefaultBuilder()
                .ConfigureServices(services => 
                {
                    services.AddSingleton<IDriverDaemon, DriverDaemon>((s) => Daemon);
                })
                .ConfigureIpcHost(builder => 
                {
                    builder.AddNamedPipeEndpoint<IDriverDaemon>("OpenTabletDriver");
                })
                .ConfigureLogging(builder => 
                {
                });

        static DriverDaemon Daemon { set; get; }
        static bool Running { set; get; }
        static bool RunAsService { set; get; }
    }
}
