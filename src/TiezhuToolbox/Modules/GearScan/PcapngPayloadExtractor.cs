using System.Buffers.Binary;

namespace TiezhuToolbox.Modules.GearScan;

/// <summary>
/// 从 pktmon 生成的 PCAPNG 中提取第七史诗 TCP 数据，并按 Fribbels 扫描器的方式按 ACK 分组、SEQ 排序。
/// </summary>
public static class PcapngPayloadExtractor
{
    private const uint SectionHeaderBlock = 0x0A0D0D0A;
    private const uint InterfaceDescriptionBlock = 0x00000001;
    private const uint SimplePacketBlock = 0x00000003;
    private const uint EnhancedPacketBlock = 0x00000006;
    private const uint LittleEndianMagic = 0x1A2B3C4D;
    private const uint BigEndianMagic = 0x4D3C2B1A;

    private const ushort LinkTypeEthernet = 1;
    private const ushort LinkTypeRaw = 101;
    private const ushort LinkTypeLinuxSll = 113;

    private static readonly HashSet<ushort> TargetPorts = [3333, 5222];

    public readonly record struct CapturedTcpSegment(
        int PacketIndex,
        uint SourceAddress,
        ushort SourcePort,
        uint DestinationAddress,
        ushort DestinationPort,
        uint Sequence,
        uint Acknowledgement,
        byte Flags,
        byte[] Payload);

    public static IReadOnlyList<string> ExtractHexStreams(string path)
    {
        var groups = new Dictionary<uint, List<CapturedTcpSegment>>();
        foreach (var segment in ReadTcpSegments(path))
        {
            if (segment.Payload.Length == 0)
                continue;
            if (!groups.TryGetValue(segment.Acknowledgement, out var segments))
            {
                segments = [];
                groups.Add(segment.Acknowledgement, segments);
            }
            segments.Add(segment);
        }

        return groups.Values
            .Select(segments => segments
                .OrderBy(segment => segment.Sequence)
                .SelectMany(segment => segment.Payload)
                .ToArray())
            .Where(payload => payload.Length > 0)
            .Select(Convert.ToHexString)
            .Select(hex => hex.ToLowerInvariant())
            .ToArray();
    }

    public static IReadOnlyList<CapturedTcpSegment> ReadTcpSegments(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        var interfaces = new List<ushort>();
        var segments = new List<CapturedTcpSegment>();
        var seenSegments = new HashSet<string>(StringComparer.Ordinal);
        var littleEndian = true;
        var packetIndex = 0;

        while (stream.Position + 12 <= stream.Length)
        {
            var header = reader.ReadBytes(8);
            if (header.Length != 8)
                break;

            var rawType = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (rawType == SectionHeaderBlock)
            {
                var rawLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                if (rawLength < 28 || rawLength > stream.Length - stream.Position + 8)
                    throw new InvalidDataException("PCAPNG 节头长度无效");

                var body = reader.ReadBytes(checked((int)rawLength - 12));
                var trailer = reader.ReadBytes(4);
                if (body.Length < 4 || trailer.Length != 4)
                    throw new InvalidDataException("PCAPNG 节头不完整");

                var magic = BinaryPrimitives.ReadUInt32LittleEndian(body);
                littleEndian = magic switch
                {
                    LittleEndianMagic => true,
                    BigEndianMagic => false,
                    _ => throw new InvalidDataException("PCAPNG 字节序标记无效"),
                };
                interfaces.Clear();
                continue;
            }

            var type = ReadUInt32(header, littleEndian);
            var blockLength = ReadUInt32(header.AsSpan(4), littleEndian);
            if (blockLength < 12 || blockLength > stream.Length - stream.Position + 8 || blockLength > int.MaxValue)
                throw new InvalidDataException($"PCAPNG 数据块长度无效：{blockLength}");

            var blockBody = reader.ReadBytes(checked((int)blockLength - 12));
            var blockTrailer = reader.ReadBytes(4);
            if (blockBody.Length != blockLength - 12 || blockTrailer.Length != 4
                || ReadUInt32(blockTrailer, littleEndian) != blockLength)
                throw new InvalidDataException("PCAPNG 数据块不完整或首尾长度不一致");

            switch (type)
            {
                case InterfaceDescriptionBlock when blockBody.Length >= 8:
                    interfaces.Add(ReadUInt16(blockBody, littleEndian));
                    break;
                case EnhancedPacketBlock when blockBody.Length >= 20:
                {
                    var interfaceId = ReadUInt32(blockBody, littleEndian);
                    var capturedLength = ReadUInt32(blockBody.AsSpan(12), littleEndian);
                    if (interfaceId >= interfaces.Count || capturedLength > blockBody.Length - 20)
                        break;
                    AddPacket(blockBody.AsSpan(20, checked((int)capturedLength)), interfaces[(int)interfaceId], packetIndex++, segments, seenSegments);
                    break;
                }
                case SimplePacketBlock when blockBody.Length >= 4 && interfaces.Count > 0:
                {
                    var originalLength = ReadUInt32(blockBody, littleEndian);
                    var capturedLength = Math.Min(checked((int)originalLength), blockBody.Length - 4);
                    AddPacket(blockBody.AsSpan(4, capturedLength), interfaces[0], packetIndex++, segments, seenSegments);
                    break;
                }
            }
        }

        return segments;
    }

    private static void AddPacket(
        ReadOnlySpan<byte> frame,
        ushort linkType,
        int packetIndex,
        List<CapturedTcpSegment> segments,
        HashSet<string> seenSegments)
    {
        if (!TryLocateIpPacket(frame, linkType, out var ipPacket) || ipPacket.Length < 20 || (ipPacket[0] >> 4) != 4)
            return;

        var ipHeaderLength = (ipPacket[0] & 0x0F) * 4;
        if (ipHeaderLength < 20 || ipPacket.Length < ipHeaderLength + 20 || ipPacket[9] != 6)
            return;

        var fragment = BinaryPrimitives.ReadUInt16BigEndian(ipPacket.Slice(6, 2));
        if ((fragment & 0x1FFF) != 0)
            return;

        var totalLength = BinaryPrimitives.ReadUInt16BigEndian(ipPacket.Slice(2, 2));
        var boundedLength = Math.Min(ipPacket.Length, totalLength);
        var tcp = ipPacket.Slice(ipHeaderLength, boundedLength - ipHeaderLength);
        var sourcePort = BinaryPrimitives.ReadUInt16BigEndian(tcp);
        var destinationPort = BinaryPrimitives.ReadUInt16BigEndian(tcp.Slice(2));
        if (!TargetPorts.Contains(sourcePort) && !TargetPorts.Contains(destinationPort))
            return;

        var tcpHeaderLength = (tcp[12] >> 4) * 4;
        if (tcpHeaderLength < 20 || tcp.Length < tcpHeaderLength)
            return;

        var payload = tcp[tcpHeaderLength..].ToArray();
        var sourceAddress = BinaryPrimitives.ReadUInt32BigEndian(ipPacket.Slice(12, 4));
        var destinationAddress = BinaryPrimitives.ReadUInt32BigEndian(ipPacket.Slice(16, 4));
        var sequence = BinaryPrimitives.ReadUInt32BigEndian(tcp.Slice(4, 4));
        var acknowledgement = BinaryPrimitives.ReadUInt32BigEndian(tcp.Slice(8, 4));
        var segmentKey = $"{sourceAddress:X8}:{sourcePort}>{destinationAddress:X8}:{destinationPort}/{sequence:X8}/{Convert.ToHexString(payload)}";
        if (!seenSegments.Add(segmentKey))
            return;

        segments.Add(new CapturedTcpSegment(
            packetIndex,
            sourceAddress,
            sourcePort,
            destinationAddress,
            destinationPort,
            sequence,
            acknowledgement,
            tcp[13],
            payload));
    }

    private static bool TryLocateIpPacket(ReadOnlySpan<byte> frame, ushort linkType, out ReadOnlySpan<byte> ipPacket)
    {
        switch (linkType)
        {
            case LinkTypeEthernet when frame.Length >= 14:
            {
                var offset = 14;
                var etherType = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(12, 2));
                while (etherType is 0x8100 or 0x88A8 && frame.Length >= offset + 4)
                {
                    etherType = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(offset + 2, 2));
                    offset += 4;
                }
                ipPacket = etherType == 0x0800 && frame.Length > offset ? frame[offset..] : default;
                return !ipPacket.IsEmpty;
            }
            case LinkTypeRaw:
                ipPacket = frame;
                return true;
            case LinkTypeLinuxSll when frame.Length >= 16:
                ipPacket = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(14, 2)) == 0x0800 ? frame[16..] : default;
                return !ipPacket.IsEmpty;
            default:
                ipPacket = default;
                return false;
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> value, bool littleEndian) => littleEndian
        ? BinaryPrimitives.ReadUInt16LittleEndian(value)
        : BinaryPrimitives.ReadUInt16BigEndian(value);

    private static uint ReadUInt32(ReadOnlySpan<byte> value, bool littleEndian) => littleEndian
        ? BinaryPrimitives.ReadUInt32LittleEndian(value)
        : BinaryPrimitives.ReadUInt32BigEndian(value);
}
