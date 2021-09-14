using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WpfUsage
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PerformanceCounter cpuCounter;
        private readonly PerformanceCounter memAvailableCounter;
        private readonly ulong memTotal = 0;
        private readonly Dictionary<string, PerformanceCounter> netRxCounters;
        private readonly Dictionary<string, PerformanceCounter> netTxCounters;

        private readonly string[] interfaces;
        private int interfaceIdx = -1;

        private readonly DispatcherTimer timer;

        private const int KB = 1024;
        private const int MB = KB * 1024;
        private const int GB = MB * 1024;

        public MainWindow()
        {
            InitializeComponent();

            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            //memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            memAvailableCounter = new PerformanceCounter("Memory", "Available Bytes");
            netRxCounters = new Dictionary<string, PerformanceCounter>();
            netTxCounters = new Dictionary<string, PerformanceCounter>();

            var memMC = new ManagementClass("Win32_PhysicalMemory");
            foreach (var memMo in memMC.GetInstances())
            {
                memTotal += (ulong)memMo.GetPropertyValue("Capacity");
                memMo.Dispose();
            }
            memMC.Dispose();

            interfaces = PerformanceCounterCategory.GetCategories().Where(category => category.CategoryName == "Network Interface").FirstOrDefault()?.GetInstanceNames();
            if (interfaces != null && interfaces.Length > 0)
            {
                Array.Sort(interfaces);

                ComBoxInterfaces.ItemsSource = interfaces;

                foreach (string interfaceName in interfaces)
                {
                    netRxCounters.Add(interfaceName, new PerformanceCounter("Network Interface", "Bytes Received/sec", interfaceName));
                    netTxCounters.Add(interfaceName, new PerformanceCounter("Network Interface", "Bytes Sent/sec", interfaceName));
                }

                SetActiveInterface();
            }

            timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 1),
                IsEnabled = true
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void SetActiveInterface()
        {
            for (int i = 0; i < interfaces.Length; i++)
            {
                // simple check: not good enough
                if (netRxCounters[interfaces[i]].NextValue() > 0)
                {
                    interfaceIdx = i;
                    ComBoxInterfaces.SelectedIndex = i;
                    return;
                }
            }

            interfaceIdx = 0;
            ComBoxInterfaces.SelectedIndex = 0;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            CpuUsage.Content = $"{cpuCounter.NextValue():F2}%";
            //MemUsage.Content = $"{memCounter.NextValue():F2}% (Available: {memAvailableCounter.NextValue() / GB:F2}GB)";
            var memAvailable = memAvailableCounter.NextValue();
            MemUsage.Content = $"{(memTotal - memAvailable) / memTotal:P2} (Available: {memAvailable / GB:F2}GB)";

            if (interfaceIdx != -1)
            {
                NetRxUsage.Content = FormatRxTxRate(netRxCounters[interfaces[interfaceIdx]].NextValue());
                NetTxUsage.Content = FormatRxTxRate(netTxCounters[interfaces[interfaceIdx]].NextValue());
            }
        }

        private static string FormatRxTxRate(float rate)
        {
            if (rate < KB)
            {
                return $"{rate:F2}B/s";
            }
            else if (rate < MB)
            {
                return $"{rate / KB:F2}KB/s";
            }
            else if (rate < GB)
            {
                return $"{rate / MB:F2}MB/s";
            }
            else
            {
                return $"{rate / GB:F2}GB/s";
            }
        }

        private void ComBoxInterfaces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            interfaceIdx = ComBoxInterfaces.SelectedIndex;
        }
    }
}
