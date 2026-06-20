using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CameraViewer.Configuration;
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
            // ===== Configuration =====
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            // Bind configuration sections to strongly-typed classes
            var appSettings = new AppSettings();
            configuration.Bind(appSettings);
            services.AddSingleton(appSettings);
            services.AddSingleton(appSettings.OnvifDiscovery);
            services.AddSingleton(appSettings.Camera);
            services.AddSingleton(appSettings.Streaming);
            services.AddSingleton(appSettings.PTZ);
            services.AddSingleton(appSettings.UI);
            services.AddSingleton(appSettings.Network);

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
            // IOnvifDiscoveryConfiguration now uses OnvifDiscoverySettings from appsettings.json
            services.AddSingleton<IOnvifDiscoveryConfiguration>(sp =>
            {
                var settings = sp.GetRequiredService<Configuration.OnvifDiscoverySettings>();
                return new OnvifDiscoveryConfiguration(settings);
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

            // ===== ViewModels =====
            services.AddTransient<ViewModels.MainViewModel>();

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
