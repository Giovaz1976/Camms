using System;
using CameraViewer.Interfaces.Parsing;

namespace CameraViewer.Implementation.Parsing
{
    /// <summary>
    /// Builder for ONVIF SOAP messages.
    /// Constructs WS-Discovery and ONVIF protocol messages.
    /// </summary>
    public class OnvifSoapMessageBuilder : IOnvifMessageBuilder
    {
        public string BuildProbeMessage()
        {
            var messageId = Guid.NewGuid().ToString();
            
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" 
            xmlns:a=""http://schemas.xmlsoap.org/ws/2004/08/addressing"">
    <s:Header>
        <a:Action s:mustUnderstand=""1"">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</a:Action>
        <a:MessageID>uuid:{messageId}</a:MessageID>
        <a:ReplyTo>
            <a:Address>http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous</a:Address>
        </a:ReplyTo>
        <a:To s:mustUnderstand=""1"">urn:schemas-xmlsoap-org:ws:2005:04:discovery</a:To>
    </s:Header>
    <s:Body>
        <Probe xmlns=""http://schemas.xmlsoap.org/ws/2005/04/discovery"">
            <d:Types xmlns:d=""http://schemas.xmlsoap.org/ws/2005/04/discovery"" 
                     xmlns:dp0=""http://www.onvif.org/ver10/network/wsdl"">dp0:NetworkVideoTransmitter</d:Types>
        </Probe>
    </s:Body>
</s:Envelope>";
        }

        public string BuildGetDeviceInformationMessage()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" 
            xmlns:tds=""http://www.onvif.org/ver10/device/wsdl"">
    <s:Body>
        <tds:GetDeviceInformation/>
    </s:Body>
</s:Envelope>";
        }
    }
}
