#!/usr/bin/env python3
"""
Analizador de capturas .pcap para ingenieria inversa del protocolo V380/ONVIF,
sin dependencias externas (no requiere scapy/pyshark/tshark - solo stdlib).

Soporta formato pcap clasico (magic 0xa1b2c3d4 / 0xd4c3b2a1) sobre Ethernet
(linktype 1) o Linux cooked capture (linktype 113, usado por PCAPdroid en
Android). pcapng (0x0A0D0D0A) tambien se detecta pero no se parsea (Wireshark
puede reexportar un pcapng como pcap clasico: File > Save As > formato
"Wireshark/tcpdump... - pcap").

Uso:
    python analyze_pcap.py <archivo.pcap> [--host 192.168.1.81] [--port 32108]
                            [--hexdump N] [--out resumen.txt]

Sin --host/--port: imprime un resumen de todas las conversaciones
(IP origen, IP destino, protocolo, puerto, cantidad de paquetes, bytes).
Con --host y/o --port: ademas imprime, por paquete que matchee el filtro,
timestamp + direccion + hex dump de los primeros N bytes del payload L4
(TCP/UDP), para poder comparar con PROTOCOL_NOTES.txt a mano.
"""

import argparse
import struct
import sys
from collections import defaultdict

PCAP_MAGIC_LE = 0xa1b2c3d4
PCAP_MAGIC_BE = 0xd4c3b2a1
PCAP_MAGIC_NS_LE = 0xa1b23c4d  # nanosecond-resolution variant
PCAPNG_MAGIC = 0x0A0D0D0A

LINKTYPE_ETHERNET = 1
LINKTYPE_LINUX_SLL = 113  # "Linux cooked capture" - comun en capturas Android (PCAPdroid)
LINKTYPE_LINUX_SLL2 = 276
LINKTYPE_RAW = 101  # IP puro, sin header L2


def read_pcap_packets(path):
    """Generador: produce (timestamp_float, raw_packet_bytes) para cada paquete."""
    with open(path, "rb") as f:
        magic_bytes = f.read(4)
        if len(magic_bytes) < 4:
            raise ValueError("Archivo vacio o truncado")

        magic = struct.unpack("<I", magic_bytes)[0]
        if magic == PCAPNG_MAGIC or struct.unpack(">I", magic_bytes)[0] == PCAPNG_MAGIC:
            raise ValueError(
                "Este archivo es pcapng, no pcap clasico. En Wireshark: "
                "File > Save As > elegir '...pcap' (no pcapng) y volver a intentar."
            )

        if magic in (PCAP_MAGIC_LE, PCAP_MAGIC_NS_LE):
            endian = "<"
        elif magic == PCAP_MAGIC_BE:
            endian = ">"
        else:
            raise ValueError(f"Magic number desconocido: {magic:#010x} (no es un .pcap clasico valido)")

        nanosecond = magic == PCAP_MAGIC_NS_LE

        header_rest = f.read(20)
        if len(header_rest) < 20:
            raise ValueError("Global header truncado")
        version_major, version_minor, thiszone, sigfigs, snaplen, linktype = struct.unpack(
            endian + "HHiIII", header_rest
        )

        while True:
            rec_header = f.read(16)
            if len(rec_header) < 16:
                break
            ts_sec, ts_usec, incl_len, orig_len = struct.unpack(endian + "IIII", rec_header)
            data = f.read(incl_len)
            if len(data) < incl_len:
                break
            ts = ts_sec + (ts_usec / 1_000_000_000.0 if nanosecond else ts_usec / 1_000_000.0)
            yield ts, data, linktype


def strip_link_layer(data, linktype):
    """Devuelve (ip_payload_bytes) saltando el header de capa de enlace, o None si no es IP."""
    if linktype == LINKTYPE_ETHERNET:
        if len(data) < 14:
            return None
        ethertype = struct.unpack(">H", data[12:14])[0]
        offset = 14
        # VLAN tag (802.1Q)
        while ethertype == 0x8100:
            if len(data) < offset + 4:
                return None
            ethertype = struct.unpack(">H", data[offset + 2:offset + 4])[0]
            offset += 4
        if ethertype == 0x0800:  # IPv4
            return data[offset:]
        return None  # IPv6 / ARP / etc. - no nos interesa para este analisis

    if linktype == LINKTYPE_LINUX_SLL:
        # SLL header: 16 bytes fijos, bytes 14-15 = protocol type (igual que ethertype)
        if len(data) < 16:
            return None
        proto = struct.unpack(">H", data[14:16])[0]
        if proto == 0x0800:
            return data[16:]
        return None

    if linktype == LINKTYPE_LINUX_SLL2:
        if len(data) < 20:
            return None
        proto = struct.unpack(">H", data[0:2])[0]
        if proto == 0x0800:
            return data[20:]
        return None

    if linktype == LINKTYPE_RAW:
        return data

    return None


def parse_ipv4(ip_data):
    """Devuelve dict con src, dst, proto, payload (L4) o None si no es IPv4 valido."""
    if len(ip_data) < 20:
        return None
    version_ihl = ip_data[0]
    version = version_ihl >> 4
    if version != 4:
        return None
    ihl = (version_ihl & 0x0F) * 4
    if len(ip_data) < ihl:
        return None
    total_len = struct.unpack(">H", ip_data[2:4])[0]
    proto = ip_data[9]
    src = ".".join(str(b) for b in ip_data[12:16])
    dst = ".".join(str(b) for b in ip_data[16:20])
    payload = ip_data[ihl:total_len] if total_len and total_len <= len(ip_data) else ip_data[ihl:]
    return {"src": src, "dst": dst, "proto": proto, "payload": payload}


def parse_l4(proto, payload):
    """Devuelve dict con sport, dport, flags(si TCP), data (payload L7) o None."""
    if proto == 6:  # TCP
        if len(payload) < 20:
            return None
        sport, dport = struct.unpack(">HH", payload[0:4])
        data_offset = (payload[12] >> 4) * 4
        flags = payload[13]
        data = payload[data_offset:] if data_offset <= len(payload) else b""
        return {"proto": "TCP", "sport": sport, "dport": dport, "flags": flags, "data": data}
    if proto == 17:  # UDP
        if len(payload) < 8:
            return None
        sport, dport, length = struct.unpack(">HHH", payload[0:6])
        data = payload[8:length] if length >= 8 else payload[8:]
        return {"proto": "UDP", "sport": sport, "dport": dport, "flags": None, "data": data}
    return None


def tcp_flags_str(flags):
    names = []
    if flags & 0x02: names.append("SYN")
    if flags & 0x10: names.append("ACK")
    if flags & 0x01: names.append("FIN")
    if flags & 0x04: names.append("RST")
    if flags & 0x08: names.append("PSH")
    return ",".join(names) if names else "-"


def hexdump(data, length=64):
    data = data[:length]
    lines = []
    for i in range(0, len(data), 16):
        chunk = data[i:i + 16]
        hex_part = " ".join(f"{b:02x}" for b in chunk)
        ascii_part = "".join(chr(b) if 32 <= b < 127 else "." for b in chunk)
        lines.append(f"    {i:04x}  {hex_part:<47}  {ascii_part}")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("pcap", help="Ruta al archivo .pcap")
    parser.add_argument("--host", help="Filtrar por IP (origen o destino)")
    parser.add_argument("--port", type=int, help="Filtrar por puerto (origen o destino)")
    parser.add_argument("--hexdump", type=int, default=64, help="Bytes de payload a mostrar por paquete filtrado (default 64)")
    parser.add_argument("--out", help="Guardar la salida tambien en este archivo de texto")
    parser.add_argument("--max-detail", type=int, default=200, help="Maximo de paquetes a detallar con hexdump (default 200)")
    args = parser.parse_args()

    conversations = defaultdict(lambda: {"packets": 0, "bytes": 0})
    detail_lines = []
    summary_lines = []
    matched = 0
    total = 0
    linktype_seen = None

    try:
        for ts, raw, linktype in read_pcap_packets(args.pcap):
            total += 1
            linktype_seen = linktype
            ip_data = strip_link_layer(raw, linktype)
            if ip_data is None:
                continue
            ip = parse_ipv4(ip_data)
            if ip is None:
                continue
            proto_name = {6: "TCP", 17: "UDP"}.get(ip["proto"])
            if proto_name is None:
                key = (ip["src"], ip["dst"], f"proto{ip['proto']}", "-")
                conversations[key]["packets"] += 1
                conversations[key]["bytes"] += len(raw)
                continue

            l4 = parse_l4(ip["proto"], ip["payload"])
            if l4 is None:
                continue

            key = (ip["src"], ip["dst"], l4["proto"], f"{l4['sport']}->{l4['dport']}")
            conversations[key]["packets"] += 1
            conversations[key]["bytes"] += len(raw)

            host_match = args.host is None or args.host in (ip["src"], ip["dst"])
            port_match = args.port is None or args.port in (l4["sport"], l4["dport"])
            if host_match and port_match:
                matched += 1
                if matched <= args.max_detail:
                    flags = f" flags={tcp_flags_str(l4['flags'])}" if l4["proto"] == "TCP" else ""
                    detail_lines.append(
                        f"[{ts:.6f}] {ip['src']}:{l4['sport']} -> {ip['dst']}:{l4['dport']} "
                        f"{l4['proto']}{flags} payload={len(l4['data'])}B"
                    )
                    if l4["data"]:
                        detail_lines.append(hexdump(l4["data"], args.hexdump))
    except ValueError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    linktype_name = {1: "Ethernet", 113: "Linux cooked (SLL)", 276: "Linux cooked v2 (SLL2)", 101: "Raw IP"}.get(
        linktype_seen, f"linktype {linktype_seen}"
    )
    summary_lines.append(f"Archivo: {args.pcap}")
    summary_lines.append(f"Link type: {linktype_name}")
    summary_lines.append(f"Paquetes totales en el archivo: {total}")
    summary_lines.append(f"Conversaciones IPv4 distintas (src, dst, proto, puertos): {len(conversations)}")
    summary_lines.append("")
    summary_lines.append("=== Resumen de conversaciones (ordenado por cantidad de paquetes) ===")
    for key, stats in sorted(conversations.items(), key=lambda kv: -kv[1]["packets"]):
        src, dst, proto, ports = key
        summary_lines.append(
            f"  {src:15s} -> {dst:15s}  {proto:5s} {ports:15s}  "
            f"{stats['packets']:6d} paquetes  {stats['bytes']:10d} bytes"
        )

    if args.host or args.port:
        summary_lines.append("")
        summary_lines.append(
            f"=== Paquetes que matchean host={args.host!r} port={args.port!r}: {matched} "
            f"(mostrando hasta {args.max_detail}) ==="
        )
        summary_lines.extend(detail_lines)

    output = "\n".join(summary_lines)
    print(output)

    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(output + "\n")
        print(f"\n(tambien guardado en {args.out})", file=sys.stderr)


if __name__ == "__main__":
    main()
