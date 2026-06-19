using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CameraViewer.Interfaces.Configuration;
using CameraViewer.Interfaces.Discovery;
using CameraViewer.Interfaces.Logging;
using CameraViewer.Interfaces.Network;
using CameraViewer.Interfaces.Parsing;
using CameraViewer.Implementation.Configuration;
using CameraViewer.Implementation.Discovery;
using CameraViewer.Implementation.Logging;
using CameraViewer.Implementation.Network;
using CameraViewer.Implementation.Parsing;
using CameraViewer.Services;

namespace CameraViewer
{
    /// <summary>
    /// Application with Dependency Injection configured.
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        /// <summary>
        /// Gets the service provider for dependency injection.
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider 
            ?? throw new InvalidOperationException("ServiceProvider not initialized");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configure Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Create and show main window using DI
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // ===== Logging =====
            services.AddSingleton<ILogger>(sp => new DebugLogger("[ONVIF]"));

            // ===== Network =====
            services.AddTransient<INetworkClient, UdpNetworkClient>();
            services.AddTransient<ITcpClient, TcpClientAdapter>();
            services.AddSingleton<ITcpClientFactory, TcpClientFactory>();
            services.AddSingleton<INetworkInterfaceHelper, NetworkInterfaceHelper>();

            // ===== Parsing =====
            services.AddSingleton<IOnvifMessageBuilder, OnvifSoapMessageBuilder>();
            services.AddSingleton<IOnvifResponseParser, OnvifResponseParser>();

            // ===== Configuration =====
            services.AddSingleton<IOnvifDiscoveryConfiguration>(sp => new OnvifDiscoveryConfiguration
            {
                MulticastAddress = "239.255.255.250",
                MulticastPort = 3702,
                DiscoveryTimeoutMs = 5000,
                ProbeRetries = 3,
                ProbeDelayMs = 100,
                AlternativePorts = new[] { 10080, 8080, 8899 },
                IpRanges = new[]
                {
                    (64, 27),   // 64-90
                    (100, 21),  // 100-120
                    (200, 11)   // 200-210
                },
                TcpConnectionTimeoutMs = 500
            });

            // ===== Discovery Services =====
            services.AddTransient<IOnvifMulticastDiscovery, OnvifMulticastDiscovery>();
            services.AddTransient<IOnvifPortScanner, OnvifPortScanner>();
            
            // Legacy service for backward compatibility
            services.AddTransient<OnvifDiscovery>(sp =>
            {
                var multicast = sp.GetRequiredService<IOnvifMulticastDiscovery>();
                var portScanner = sp.GetRequiredService<IOnvifPortScanner>();
                return new OnvifDiscovery(multicast, portScanner);
            });

            // ===== Other Services =====
            services.AddTransient<V380Discovery>();
            services.AddTransient<OnvifPtzService>();

            // ===== Views =====
            services.AddTransient<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
