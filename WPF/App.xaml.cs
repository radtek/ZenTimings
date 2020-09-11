﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using ZenTimings;
using ZenTimings.Properties;

namespace ZenTimings
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string mutexName = "{GUID}";
        private static Mutex instanceMutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            instanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                if (Settings.Default.IsRestarting)
                {
                    instanceMutex.ReleaseMutex();
                    Settings.Default.IsRestarting = true;
                    Settings.Default.Save();
                    instanceMutex = new Mutex(true, mutexName, out _);
                    return;
                }

                //app is already running! Exiting the application
                MessageBox.Show("Another instance is already running.", "Error", MessageBoxButton.OK);
                Application.Current.Shutdown();
            }

            GC.KeepAlive(instanceMutex);

            InteropMethods.PostMessage((IntPtr)InteropMethods.HWND_BROADCAST, InteropMethods.WM_SHOWME, IntPtr.Zero, IntPtr.Zero);

            base.OnStartup(e);
        }
    }
}
