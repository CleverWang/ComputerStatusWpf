using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
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
            memAvailableCounter = new PerformanceCounter("Memory", "Available Bytes");
            netRxCounters = new Dictionary<string, PerformanceCounter>();
            netTxCounters = new Dictionary<string, PerformanceCounter>();

            using (var memMC = new ManagementClass("Win32_PhysicalMemory"))
            {
                using var memMOC = memMC.GetInstances();
                foreach (var memMO in memMOC)
                {
                    memTotal += (ulong)memMO.GetPropertyValue("Capacity");
                }
            }

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

                _ = SetActiveInterfaceAsync();
            }

            timer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 1),
                IsEnabled = true
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private async Task SetActiveInterfaceAsync()
        {
            async Task<bool> IsInterfaceActiveAsync(string interfaceName)
            {
                var rxcounter = netRxCounters[interfaceName];
                var txcounter = netTxCounters[interfaceName];
                _ = rxcounter.NextValue();
                _ = txcounter.NextValue();
                await Task.Delay(3000);
                return rxcounter.NextValue() > 0 || txcounter.NextValue() > 0;
            }

            var tasks = from interfaceName in interfaces
                        select IsInterfaceActiveAsync(interfaceName);

            var results = await Task.WhenAll(tasks);
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i])
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

            var memUsed = memTotal - memAvailableCounter.NextValue();
            MemUsage.Content = $"{memUsed / memTotal:P2} ({memUsed / GB:F2}GB/{memTotal / GB:F2}GB)";

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
