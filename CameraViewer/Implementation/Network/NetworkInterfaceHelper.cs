using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CameraViewer.Interfaces.Network;

namespace CameraViewer.Implementation.Network
{
    /// <summary>
    /// Helper for network interface operations.
    /// Provides utilities for getting local network information.
    /// </summary>
    public class NetworkInterfaceHelper : INetworkInterfaceHelper
    {
        public string? GetLocalSubnet()
        {
            var localIP = GetLocalIPAddress();
            if (localIP == null)
                return null;

            var ipParts = localIP.ToString().Split('.');
            if (ipParts.Length < 3)
                return null;

            return $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
        }

        public IPAddress? GetLocalIPAddress()
        {
            var localIP = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(addr =>
                    addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(addr.Address) &&
                    !addr.Address.ToString().StartsWith("169.254"));

            return localIP?.Address;
        }
    }
}
