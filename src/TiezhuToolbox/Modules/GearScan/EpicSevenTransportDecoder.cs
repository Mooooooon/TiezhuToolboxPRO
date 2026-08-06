namespace TiezhuToolbox.Modules.GearScan;

/// <summary>
/// 解码第七史诗 TCP 查询层。协议头为“1 字节 XOR 偏移 + 3 字节大端载荷长度”，
/// 头部后三字节与载荷使用引擎内置的 256 字节表循环 XOR。
/// </summary>
public static class EpicSevenTransportDecoder
{
    private static readonly HashSet<ushort> DefaultServerPorts = [3333, 5222];
    private static readonly byte[] XorKey = Convert.FromHexString(
        "91AE4ED4644F585162EC1BD5EF24ADDBAF838242AEF51E97804B134FFD8CE5BB" +
        "4F6E3E6451147CDF56C318E5E964C999C0D95CC860822E6B418BE465D79A036D" +
        "BF67AB3DA72AB1023A4561F444E5CE858D23EA10FEB4899151AD7E43FF3E2419" +
        "A97B4DD3AF4EF5C829E5AF4ACE9436F6B6B6382E9DFD26642099011A4899089C" +
        "9D4B9F80BBB00A4CC73255CE1F78646E91C9C12313F5D840DC51457010D37D19" +
        "615BB69888B42B19E749F993C00337E9332F89B320C173A5653848788798A7717" +
        "39E72DBC84C7946597149BDDAE4E3BD1A17856C85A555CFA24F6352D005933B50" +
        "042BE0BA4C708DE8EBB52059B2059C9BFE90D8923DF74B43911BBC00BB6BFA");

    public static IReadOnlyList<byte[]> DecodeServerPayloads(string pcapngPath, params ushort[] serverPorts)
    {
        IReadOnlySet<ushort> ports = serverPorts.Length == 0
            ? DefaultServerPorts
            : new HashSet<ushort>(serverPorts);
        var segments = PcapngPayloadExtractor.ReadTcpSegments(pcapngPath)
            .Where(segment => ports.Contains(segment.SourcePort) && segment.Payload.Length > 0)
            .GroupBy(segment => new Direction(
                segment.SourceAddress,
                segment.DestinationAddress,
                segment.SourcePort,
                segment.DestinationPort));

        var result = new List<byte[]>();
        foreach (var direction in segments)
        {
            var stream = Reassemble(direction);
            result.AddRange(DecodeStream(stream));
        }
        return result;
    }

    public static IReadOnlyList<byte[]> DecodeStream(ReadOnlySpan<byte> stream)
    {
        var packets = new List<byte[]>();
        var cursor = 0;
        while (cursor + 4 <= stream.Length)
        {
            var xorOffset = stream[cursor];
            var lengthHigh = Transform(stream[cursor + 1], xorOffset, 0);
            var lengthMiddle = Transform(stream[cursor + 2], xorOffset, 1);
            var lengthLow = Transform(stream[cursor + 3], xorOffset, 2);
            var length = (lengthHigh << 16) | (lengthMiddle << 8) | lengthLow;
            if (cursor + 4 + length > stream.Length)
                throw new InvalidDataException($"TCP 查询包不完整：声明 {length} 字节，实际仅剩 {stream.Length - cursor - 4} 字节。");

            var payload = stream.Slice(cursor + 4, length).ToArray();
            if (xorOffset != 0)
            {
                for (var index = 0; index < payload.Length; index++)
                    payload[index] ^= XorKey[(xorOffset + 3 + index) % XorKey.Length];
            }
            packets.Add(payload);
            cursor += 4 + length;
        }

        if (cursor != stream.Length)
            throw new InvalidDataException($"TCP 查询流末尾残留 {stream.Length - cursor} 字节。");
        return packets;
    }

    private static int Transform(byte value, byte xorOffset, int relativeOffset) =>
        xorOffset == 0 ? value : value ^ XorKey[(xorOffset + relativeOffset) % XorKey.Length];

    private static byte[] Reassemble(IEnumerable<PcapngPayloadExtractor.CapturedTcpSegment> source)
    {
        var ordered = source.OrderBy(segment => segment.Sequence).ToArray();
        if (ordered.Length == 0)
            return [];

        using var output = new MemoryStream();
        var nextSequence = ordered[0].Sequence;
        foreach (var segment in ordered)
        {
            if (segment.Sequence > nextSequence)
                throw new InvalidDataException($"TCP 响应存在缺口：期望序号 {nextSequence}，实际为 {segment.Sequence}。");

            var overlap = nextSequence > segment.Sequence
                ? checked((int)(nextSequence - segment.Sequence))
                : 0;
            if (overlap >= segment.Payload.Length)
                continue;

            output.Write(segment.Payload, overlap, segment.Payload.Length - overlap);
            nextSequence = segment.Sequence + checked((uint)segment.Payload.Length);
        }
        return output.ToArray();
    }

    private readonly record struct Direction(
        uint SourceAddress,
        uint DestinationAddress,
        ushort SourcePort,
        ushort DestinationPort);
}
