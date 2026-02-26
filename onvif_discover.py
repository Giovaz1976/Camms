#!/usr/bin/env python3
"""
Script para descubrir cámaras ONVIF y obtener sus URLs de stream
Requiere: pip install onvif-zeep
"""

from onvif import ONVIFCamera
import sys

def get_stream_uri(ip, port, user, password):
    """Obtiene la URI del stream de una cámara ONVIF"""
    try:
        print(f"Conectando a {ip}:{port}...")
        
        # Crear cliente ONVIF
        mycam = ONVIFCamera(ip, port, user, password)
        
        # Obtener servicio de medios
        media_service = mycam.create_media_service()
        
        # Obtener perfiles disponibles
        profiles = media_service.GetProfiles()
        
        print(f"\nPerfiles encontrados: {len(profiles)}")
        
        # Obtener URI de stream para cada perfil
        for i, profile in enumerate(profiles):
            print(f"\n--- Perfil {i+1}: {profile.Name} ---")
            
            # Solicitar URI del stream
            token = profile.token
            stream_setup = {
                'Stream': 'RTP-Unicast',
                'Transport': {'Protocol': 'RTSP'}
            }
            
            uri = media_service.GetStreamUri({
                'StreamSetup': stream_setup,
                'ProfileToken': token
            })
            
            print(f"URI: {uri.Uri}")
            print(f"Timeout: {uri.Timeout}")
            
        return True
        
    except Exception as e:
        print(f"Error: {e}")
        return False

if __name__ == "__main__":
    # Configuración de la cámara
    CAMERA_IP = "192.168.1.81"
    ONVIF_PORT = 80  # Prueba también: 8080, 8899, 8000
    USERNAME = "admin"
    PASSWORD = ""  # Prueba también: "admin", "12345", ""
    
    print("=== Descubrimiento ONVIF ===")
    print(f"Cámara: {CAMERA_IP}:{ONVIF_PORT}")
    print(f"Usuario: {USERNAME}")
    print(f"Password: {'(vacío)' if not PASSWORD else PASSWORD}")
    print()
    
    success = get_stream_uri(CAMERA_IP, ONVIF_PORT, USERNAME, PASSWORD)
    
    if not success:
        print("\n--- Probando puertos alternativos ---")
        for port in [8080, 8899, 8000, 554]:
            print(f"\nProbando puerto {port}...")
            if get_stream_uri(CAMERA_IP, port, USERNAME, PASSWORD):
                break
