using TiezhuToolbox.Modules.Ocr;
using TiezhuToolbox.Modules.Recommend;
using TiezhuToolbox.Modules.Automation;
using TiezhuToolbox.Modules.StarForge;
using TiezhuToolbox.Modules.GearScan;
using System.Windows.Forms;

if (args.Contains("--gear-scan-local"))
{
    var optionIndex = Array.IndexOf(args, "--gear-scan-local");
    var capturePath = args.Skip(optionIndex + 1).FirstOrDefault()
        ?? throw new ArgumentException("--gear-scan-local 后必须提供 PCAPNG 路径");
    var baselinePath = args.Length > optionIndex + 2 ? args[optionIndex + 2] : null;
    var minimumEnhance = 6;
    if (baselinePath != null)
    {
        var baseline = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(baselinePath))!.AsObject();
        minimumEnhance = baseline["items"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>()
            .Select(item => item["enhance"]?.GetValue<int>() ?? 0)
            .DefaultIfEmpty(6)
            .Min();
    }
    var result = new EpicSevenLocalGearParser().Parse(capturePath, minimumEnhance);
    Console.WriteLine($"本地解析：装备={result.ItemCount}，英雄={result.HeroCount}，等级0={result.LevelZeroItemCount}，88级推断修复={result.InferredLevelItemCount}");

    if (baselinePath != null)
    {
        var localRoot = System.Text.Json.Nodes.JsonNode.Parse(result.GearText)!.AsObject();
        var baselineRoot = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(baselinePath))!.AsObject();
        static System.Text.Json.Nodes.JsonObject Project(
            System.Text.Json.Nodes.JsonObject source,
            params string[] fields)
        {
            var result = new System.Text.Json.Nodes.JsonObject();
            foreach (var field in fields)
                result[field] = source[field]?.DeepClone();
            return result;
        }
        static Dictionary<string, System.Text.Json.Nodes.JsonObject> IndexBy(
            System.Text.Json.Nodes.JsonArray values,
            string key) => values.OfType<System.Text.Json.Nodes.JsonObject>()
            .Where(value => value[key] != null)
            .ToDictionary(value => value[key]!.ToString(), StringComparer.Ordinal);

        var localItems = IndexBy(localRoot["items"]!.AsArray(), "ingameId");
        var baselineItems = IndexBy(baselineRoot["items"]!.AsArray(), "ingameId");
        static bool IsExpectedLevel88Repair(
            System.Text.Json.Nodes.JsonObject local,
            System.Text.Json.Nodes.JsonObject baseline)
        {
            if (baseline["level"]?.GetValue<int>() != 0 || local["level"]?.GetValue<int>() != 88)
                return false;
            var localMain = local["main"]?.AsObject();
            var baselineMain = baseline["main"]?.AsObject();
            var type = localMain?["type"]?.GetValue<string>();
            var expectedValue = type switch
            {
                "Attack" => 515,
                "Health" => 2765,
                "Defense" => 310,
                "Speed" => 45,
                "CriticalHitChancePercent" => 60,
                "CriticalHitDamagePercent" => 70,
                "AttackPercent" or "HealthPercent" or "DefensePercent"
                    or "EffectivenessPercent" or "EffectResistancePercent" => 65,
                _ => double.NaN,
            };
            return !double.IsNaN(expectedValue)
                && localMain?["value"]?.GetValue<double>() == expectedValue
                && localMain?["type"]?.ToString() == baselineMain?["type"]?.ToString()
                && System.Text.Json.Nodes.JsonNode.DeepEquals(
                    Project(local, "gear", "rank", "set", "enhance", "substats", "ingameEquippedId"),
                    Project(baseline, "gear", "rank", "set", "enhance", "substats", "ingameEquippedId"));
        }
        var repairedIds = localItems.Keys.Intersect(baselineItems.Keys)
            .Where(id => IsExpectedLevel88Repair(localItems[id], baselineItems[id]))
            .ToHashSet(StringComparer.Ordinal);
        var itemMismatch = localItems.Keys.Union(baselineItems.Keys).Count(id =>
            !localItems.TryGetValue(id, out var local)
            || !baselineItems.TryGetValue(id, out var baseline)
            || (!repairedIds.Contains(id) && !System.Text.Json.Nodes.JsonNode.DeepEquals(
                Project(local, "gear", "rank", "set", "level", "enhance", "main", "substats", "ingameEquippedId"),
                Project(baseline, "gear", "rank", "set", "level", "enhance", "main", "substats", "ingameEquippedId"))));
        var localOnly = localItems.Keys.Except(baselineItems.Keys).ToArray();
        var baselineOnly = baselineItems.Keys.Except(localItems.Keys).ToArray();
        Console.WriteLine($"装备 ID：仅本地={localOnly.Length}，仅基准={baselineOnly.Length}，按88级修复={repairedIds.Count}");
        foreach (var id in localOnly.Take(30))
        {
            var item = localItems[id];
            Console.WriteLine($"  仅本地 {id}: code={item["code"]} f={item["f"]} gear={item["gear"]} enhance={item["enhance"]} level={item["level"]}");
        }
        var comparisonFields = new[] { "gear", "rank", "set", "level", "enhance", "main", "substats", "ingameEquippedId" };
        foreach (var field in comparisonFields)
        {
            var count = localItems.Keys.Intersect(baselineItems.Keys).Count(id =>
                !repairedIds.Contains(id)
                && !System.Text.Json.Nodes.JsonNode.DeepEquals(localItems[id][field], baselineItems[id][field]));
            Console.WriteLine($"  字段 {field} 差异={count}");
            foreach (var id in localItems.Keys.Intersect(baselineItems.Keys).Where(id =>
                         !repairedIds.Contains(id)
                         && !System.Text.Json.Nodes.JsonNode.DeepEquals(localItems[id][field], baselineItems[id][field])).Take(5))
                Console.WriteLine($"    {id} code={localItems[id]["code"]} 本地={localItems[id][field]} 基准={baselineItems[id][field]}");
        }

        var localHeroes = IndexBy(localRoot["heroes"]!.AsArray(), "id");
        var baselineHeroes = IndexBy(baselineRoot["heroes"]!.AsArray(), "id");
        var heroMismatch = localHeroes.Keys.Union(baselineHeroes.Keys).Count(id =>
            !localHeroes.TryGetValue(id, out var local)
            || !baselineHeroes.TryGetValue(id, out var baseline)
            || !System.Text.Json.Nodes.JsonNode.DeepEquals(
                Project(local, "code", "name", "stars", "awaken"),
                Project(baseline, "code", "name", "stars", "awaken")));
        Console.WriteLine($"与基准核心字段对比：装备差异={itemMismatch}，英雄差异={heroMismatch}");
        if (itemMismatch != 0 || heroMismatch != 0)
            throw new InvalidOperationException("本地解析结果与远程解析基准不一致");
    }
    return;
}

if (args.Contains("--yuna-script-probe"))
{
    var optionIndex = Array.IndexOf(args, "--yuna-script-probe");
    var sourcePath = args.Skip(optionIndex + 1).FirstOrDefault()
        ?? throw new ArgumentException("--yuna-script-probe 后必须提供 init.bin/game.bin 路径");
    var source = File.ReadAllBytes(sourcePath);
    var signature = Convert.FromHexString("1B5307DF080C");
    var key = Convert.FromHexString("170B060108630148160003229BDA9A2B");
    if (!source.AsSpan().StartsWith(signature))
        throw new InvalidDataException("Yuna 脚本签名不匹配");

    static byte[]? DecryptXxtea(ReadOnlySpan<byte> encrypted, ReadOnlySpan<byte> key)
    {
        if (encrypted.Length < 8 || encrypted.Length % 4 != 0 || key.Length != 16)
            return null;
        var words = new uint[encrypted.Length / 4];
        for (var index = 0; index < words.Length; index++)
            words[index] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(encrypted[(index * 4)..]);
        Span<uint> keyWords = stackalloc uint[4];
        for (var index = 0; index < keyWords.Length; index++)
            keyWords[index] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key[(index * 4)..]);
        const uint delta = 0x9E3779B9;
        var n = words.Length - 1;
        var rounds = 6 + 52 / (n + 1);
        var sum = unchecked((uint)(rounds * delta));
        var y = words[0];
        while (sum != 0)
        {
            var e = (sum >> 2) & 3;
            for (var p = n; p > 0; p--)
            {
                var z = words[p - 1];
                var mix = unchecked((((z >> 5) ^ (y << 2)) + ((y >> 3) ^ (z << 4)))
                    ^ ((sum ^ y) + (keyWords[(p & 3) ^ (int)e] ^ z)));
                y = words[p] = unchecked(words[p] - mix);
            }
            var last = words[n];
            var firstMix = unchecked((((last >> 5) ^ (y << 2)) + ((y >> 3) ^ (last << 4)))
                ^ ((sum ^ y) + (keyWords[(int)e] ^ last)));
            y = words[0] = unchecked(words[0] - firstMix);
            sum = unchecked(sum - delta);
        }
        var plainLength = words[^1];
        var maximumLength = (words.Length - 1) * 4;
        if (plainLength > maximumLength || plainLength < maximumLength - 3)
            return null;
        var result = new byte[plainLength];
        for (var index = 0; index < result.Length; index++)
            result[index] = (byte)(words[index / 4] >> ((index % 4) * 8));
        return result;
    }

    var plain = DecryptXxtea(source.AsSpan(signature.Length), key)
        ?? throw new InvalidDataException("XXTEA 解密失败");
    Console.WriteLine($"源={source.Length}，XXTEA 明文={plain.Length}，前64={Convert.ToHexString(plain.AsSpan(0, Math.Min(64, plain.Length)))}");
    Console.WriteLine("ASCII=" + System.Text.Encoding.ASCII.GetString(plain.AsSpan(0, Math.Min(128, plain.Length))).Replace('\0', '.'));
    if (args.Length > optionIndex + 2)
    {
        var outputPath = Path.GetFullPath(args[optionIndex + 2]);
        File.WriteAllBytes(outputPath, plain);
        Console.WriteLine("已写入：" + outputPath);
    }
    return;
}

if (args.Contains("--gear-scan-probe"))
{
    var capturePath = args.SkipWhile(arg => arg != "--gear-scan-probe").Skip(1).FirstOrDefault()
        ?? throw new ArgumentException("--gear-scan-probe 后必须提供 PCAPNG 路径");
    var streams = PcapngPayloadExtractor.ExtractHexStreams(capturePath);
    var payloads = streams.Select(Convert.FromHexString).Where(bytes => bytes.Length > 0).ToArray();
    var lengths = payloads.Select(bytes => bytes.Length).Order().ToArray();
    var totalBytes = lengths.Sum(length => (long)length);
    var byteCounts = new long[256];
    foreach (var payload in payloads)
        foreach (var value in payload)
            byteCounts[value]++;
    var entropy = byteCounts.Where(count => count > 0).Sum(count =>
    {
        var probability = count / (double)totalBytes;
        return -probability * Math.Log2(probability);
    });
    var printableBytes = byteCounts.Skip(0x20).Take(0x7F - 0x20).Sum();
    var zeroBytes = byteCounts[0];
    var exactLengthPrefixes = payloads.Count(bytes => bytes.Length >= 4 &&
        (System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes) == bytes.Length - 4
         || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) == bytes.Length - 4));
    var knownCompressionHeaders = payloads.Count(bytes => bytes.Length >= 2 &&
        ((bytes[0] == 0x1F && bytes[1] == 0x8B)
         || (bytes[0] == 0x78 && bytes[1] is 0x01 or 0x5E or 0x9C or 0xDA)
         || (bytes[0] == 0x50 && bytes[1] == 0x4B)));
    var jsonStarts = payloads.Count(bytes => bytes[0] is (byte)'{' or (byte)'[');
    var commonPrefixLength = payloads.Length == 0 ? 0 : Enumerable.Range(0, payloads.Min(bytes => bytes.Length))
        .TakeWhile(index => payloads.All(bytes => bytes[index] == payloads[0][index]))
        .Count();
    Console.WriteLine($"流数量={payloads.Length}，载荷总量={totalBytes} 字节");
    if (lengths.Length > 0)
        Console.WriteLine($"长度：最小={lengths[0]}，中位={lengths[lengths.Length / 2]}，P90={lengths[(int)((lengths.Length - 1) * 0.9)]}，最大={lengths[^1]}");
    Console.WriteLine($"字节熵={entropy:F4} bit/byte，零字节={zeroBytes / (double)Math.Max(1, totalBytes):P2}，可打印 ASCII={printableBytes / (double)Math.Max(1, totalBytes):P2}");
    Console.WriteLine($"整流长度前缀={exactLengthPrefixes}，已知压缩头={knownCompressionHeaders}，JSON 起始={jsonStarts}，全体公共前缀={commonPrefixLength} 字节");
    foreach (var entry in payloads.Select((bytes, index) => new
             {
                 Index = index,
                 Length = bytes.Length,
                 Mod16 = bytes.Length % 16,
                 Prefix = Convert.ToHexString(bytes.AsSpan(0, Math.Min(16, bytes.Length))),
             }).OrderByDescending(entry => entry.Length))
        Console.WriteLine($"  #{entry.Index:D2} 长度={entry.Length,7} mod16={entry.Mod16,2} 前16={entry.Prefix}");

    var tcpSegments = PcapngPayloadExtractor.ReadTcpSegments(capturePath);
    static string Endpoint(uint address, ushort port) => $"{address:X8}:{port}";
    static string DirectionKey(PcapngPayloadExtractor.CapturedTcpSegment segment) =>
        $"{Endpoint(segment.SourceAddress, segment.SourcePort)}>{Endpoint(segment.DestinationAddress, segment.DestinationPort)}";
    static (byte[] Data, int GapCount, int OverlapBytes) Reassemble(IEnumerable<PcapngPayloadExtractor.CapturedTcpSegment> source)
    {
        var ordered = source.Where(segment => segment.Payload.Length > 0).OrderBy(segment => segment.Sequence).ToArray();
        if (ordered.Length == 0)
            return ([], 0, 0);
        using var output = new MemoryStream();
        var next = ordered[0].Sequence;
        var gaps = 0;
        var overlaps = 0;
        foreach (var segment in ordered)
        {
            if (segment.Sequence > next)
            {
                gaps++;
                next = segment.Sequence;
            }
            var consumed = next > segment.Sequence ? checked((int)(next - segment.Sequence)) : 0;
            overlaps += Math.Min(consumed, segment.Payload.Length);
            if (consumed < segment.Payload.Length)
            {
                output.Write(segment.Payload, consumed, segment.Payload.Length - consumed);
                next = segment.Sequence + checked((uint)segment.Payload.Length);
            }
        }
        return (output.ToArray(), gaps, overlaps);
    }

    Console.WriteLine($"TCP 段={tcpSegments.Count}（含握手/纯 ACK），方向流如下：");
    foreach (var direction in tcpSegments.GroupBy(DirectionKey).OrderByDescending(group => group.Sum(segment => segment.Payload.Length)))
    {
        var (data, gaps, overlaps) = Reassemble(direction);
        var first = Convert.ToHexString(data.AsSpan(0, Math.Min(24, data.Length)));
        var firstPacket = direction.Min(segment => segment.PacketIndex);
        var lastPacket = direction.Max(segment => segment.PacketIndex);
        Console.WriteLine($"  {direction.Key} 段={direction.Count(),4} 载荷={data.Length,7} gaps={gaps} overlaps={overlaps} packet={firstPacket}-{lastPacket} 前24={first}");
    }

    static void DescribeCollections(object? value, string path, int depth = 0)
    {
        if (depth > 8)
            return;
        if (value is Dictionary<string, object?> map)
        {
            var childMaps = map.Values.OfType<Dictionary<string, object?>>().ToArray();
            var interesting = path.Contains("equip", StringComparison.OrdinalIgnoreCase)
                || path.Contains("unit", StringComparison.OrdinalIgnoreCase)
                || path.Contains("hero", StringComparison.OrdinalIgnoreCase)
                || map.Count >= 100;
            if (interesting && map.Count > 0)
            {
                var sampleKeys = childMaps.Take(5).SelectMany(child => child.Keys).Distinct().Take(30);
                Console.WriteLine($"    {path} map={map.Count} 子项字段=[{string.Join(',', sampleKeys)}]");
            }
            if (childMaps.Length >= 10 && !path.EndsWith("account_data", StringComparison.Ordinal))
                return;
            foreach (var (key, child) in map)
                DescribeCollections(child, path + "." + key, depth + 1);
            return;
        }
        if (value is List<object?> list)
        {
            var childMaps = list.OfType<Dictionary<string, object?>>().ToArray();
            var interesting = path.Contains("equip", StringComparison.OrdinalIgnoreCase)
                || path.Contains("unit", StringComparison.OrdinalIgnoreCase)
                || path.Contains("hero", StringComparison.OrdinalIgnoreCase)
                || list.Count >= 100;
            if (interesting)
            {
                var sampleKeys = childMaps.Take(5).SelectMany(child => child.Keys).Distinct().Take(30);
                Console.WriteLine($"    {path} array={list.Count} 子项字段=[{string.Join(',', sampleKeys)}]");
            }
            for (var index = 0; index < list.Count; index++)
                DescribeCollections(list[index], path + "[]", depth + 1);
        }
    }

    Console.WriteLine("本地完整解码（TCP XOR → LZ4 → MessagePack）：");
    var localPayloads = EpicSevenTransportDecoder.DecodeServerPayloads(capturePath);
    for (var index = 0; index < localPayloads.Count; index++)
    {
        var message = Lz4BlockDecoder.DecodeGamePayload(localPayloads[index]);
        var document = new MessagePackReader(message).ReadDocument();
        var rootKeys = document is Dictionary<string, object?> rootMap
            ? string.Join(',', rootMap.Keys)
            : document?.GetType().Name ?? "null";
        Console.WriteLine($"  #{index:D2} 压缩={localPayloads[index].Length}，解压={message.Length}，根={rootKeys}");
        if (document is Dictionary<string, object?> response
            && response.TryGetValue("account_data", out var accountValue)
            && accountValue is Dictionary<string, object?> account)
        {
            foreach (var (key, child) in account.Where(entry =>
                         entry.Key.Contains("equip", StringComparison.OrdinalIgnoreCase)
                         || entry.Key.Contains("unit", StringComparison.OrdinalIgnoreCase)))
            {
                var count = child switch
                {
                    Dictionary<string, object?> childMap => childMap.Count,
                    List<object?> childList => childList.Count,
                    _ => -1,
                };
                var fields = child is Dictionary<string, object?> childDictionary
                    ? childDictionary.Values.OfType<Dictionary<string, object?>>().Take(5)
                        .SelectMany(item => item.Keys).Distinct().ToArray()
                    : [];
                Console.WriteLine($"    account_data.{key}: count={count} fields=[{string.Join(',', fields)}]");
            }
        }
        DescribeCollections(document, "$" + index);
    }

    static byte[]? TryXxteaDecrypt(ReadOnlySpan<byte> encrypted, ReadOnlySpan<byte> key)
    {
        if (encrypted.Length < 8 || encrypted.Length % 4 != 0 || key.Length != 16)
            return null;
        var words = new uint[encrypted.Length / 4];
        for (var index = 0; index < words.Length; index++)
            words[index] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(encrypted[(index * 4)..]);
        Span<uint> keyWords = stackalloc uint[4];
        for (var index = 0; index < keyWords.Length; index++)
            keyWords[index] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(key[(index * 4)..]);
        const uint delta = 0x9E3779B9;
        var n = words.Length - 1;
        var rounds = 6 + 52 / (n + 1);
        var sum = unchecked((uint)(rounds * delta));
        var y = words[0];
        while (sum != 0)
        {
            var e = (sum >> 2) & 3;
            for (var p = n; p > 0; p--)
            {
                var z = words[p - 1];
                var mix = unchecked((((z >> 5) ^ (y << 2)) + ((y >> 3) ^ (z << 4)))
                    ^ ((sum ^ y) + (keyWords[(p & 3) ^ (int)e] ^ z)));
                y = words[p] = unchecked(words[p] - mix);
            }
            var last = words[n];
            var firstMix = unchecked((((last >> 5) ^ (y << 2)) + ((y >> 3) ^ (last << 4)))
                ^ ((sum ^ y) + (keyWords[(int)e] ^ last)));
            y = words[0] = unchecked(words[0] - firstMix);
            sum = unchecked(sum - delta);
        }
        var plainLength = words[^1];
        var maximumLength = (words.Length - 1) * 4;
        if (plainLength > maximumLength || plainLength < maximumLength - 3)
            return null;
        var result = new byte[plainLength];
        for (var index = 0; index < result.Length; index++)
            result[index] = (byte)(words[index / 4] >> ((index % 4) * 8));
        return result;
    }

    var baseKey = System.Text.Encoding.ASCII.GetBytes("89ABCDEF01234567");
    var xxteaMatches = 0;
    foreach (var payload in payloads)
    {
        for (var offset = 0; offset < Math.Min(32, payload.Length - 7); offset++)
        {
            var plain = TryXxteaDecrypt(payload.AsSpan(offset), baseKey);
            if (plain is null)
                continue;
            xxteaMatches++;
            Console.WriteLine($"BASE_ENCRYPT_KEY 命中：密文={payload.Length} offset={offset} 明文={plain.Length} 前24={Convert.ToHexString(plain.AsSpan(0, Math.Min(24, plain.Length)))}");
        }
    }
    Console.WriteLine($"BASE_ENCRYPT_KEY 标准 XXTEA 有效候选={xxteaMatches}");
    return;
}

if (args.Contains("--gear-scan"))
{
    var testRoot = Path.Combine(Path.GetTempPath(), "TiezhuToolbox-gear-scan-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testRoot);
    try
    {
        var pcapngPath = Path.Combine(testRoot, "synthetic.pcapng");
        static byte[] TcpFrame(uint sequence, uint acknowledgement, ushort sourcePort, byte[] payload)
        {
            var frame = new byte[14 + 20 + 20 + payload.Length];
            frame[12] = 0x08;
            frame[13] = 0x00;
            var ip = frame.AsSpan(14);
            ip[0] = 0x45;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(ip[2..], (ushort)(20 + 20 + payload.Length));
            ip[8] = 64;
            ip[9] = 6;
            ip[12] = 10;
            ip[15] = 1;
            ip[16] = 10;
            ip[19] = 2;
            var tcp = ip[20..];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(tcp, sourcePort);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(tcp[2..], 50000);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(tcp[4..], sequence);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(tcp[8..], acknowledgement);
            tcp[12] = 0x50;
            tcp[13] = 0x18;
            payload.CopyTo(tcp[20..]);
            return frame;
        }

        using (var stream = File.Create(pcapngPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0x0A0D0D0Au);
            writer.Write(28u);
            writer.Write(0x1A2B3C4Du);
            writer.Write((ushort)1);
            writer.Write((ushort)0);
            writer.Write(ulong.MaxValue);
            writer.Write(28u);

            writer.Write(1u);
            writer.Write(20u);
            writer.Write((ushort)1);
            writer.Write((ushort)0);
            writer.Write(65535u);
            writer.Write(20u);

            void WritePacket(byte[] packet)
            {
                var paddedLength = (packet.Length + 3) & ~3;
                var blockLength = (uint)(32 + paddedLength);
                writer.Write(6u);
                writer.Write(blockLength);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write((uint)packet.Length);
                writer.Write((uint)packet.Length);
                writer.Write(packet);
                writer.Write(new byte[paddedLength - packet.Length]);
                writer.Write(blockLength);
            }

            // 国际服 3333：故意乱序并重复尾段，验证 TCP 重组与去重。
            WritePacket(TcpFrame(104, 900, 3333, [0xAA, 0xBB, 0xCC, 0xDD]));
            WritePacket(TcpFrame(100, 900, 3333, [0x00, 0x00, 0x00, 0x04]));
            WritePacket(TcpFrame(104, 900, 3333, [0xAA, 0xBB, 0xCC, 0xDD]));

            // 中国服 5222：与国际服使用相同查询层协议，应由默认端口集合一并解码。
            WritePacket(TcpFrame(200, 901, 5222, [0x00, 0x00, 0x00, 0x02, 0xEE, 0xFF]));
            WritePacket(TcpFrame(300, 902, 3333, []));
        }

        var streams = PcapngPayloadExtractor.ExtractHexStreams(pcapngPath);
        var expectedStreams = new HashSet<string>(StringComparer.Ordinal)
        {
            "00000004aabbccdd",
            "00000002eeff",
        };
        if (streams.Count != 2 || !streams.ToHashSet(StringComparer.Ordinal).SetEquals(expectedStreams))
            throw new InvalidOperationException("PCAPNG TCP 重组错误：" + string.Join(",", streams));

        var serverPayloads = EpicSevenTransportDecoder.DecodeServerPayloads(pcapngPath)
            .Select(Convert.ToHexString)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!serverPayloads.SetEquals(["AABBCCDD", "EEFF"]))
            throw new InvalidOperationException("国际服/中国服端口解码错误：" + string.Join(",", serverPayloads));

        const string parserResponse = """
            {
              "status":"SUCCESS",
              "data":[
                {
                  "id":123456,"p":9988,"g":5,"f":"set_speed","type":"weapon","level":85,
                  "name":"测试之剑","mainStatValue":500,
                  "op":[
                    ["att",500],
                    ["att_rate",0.08],
                    ["speed",4],
                    ["cri",0.05],
                    ["cri_dmg",0.07],
                    ["acc",0.08],
                    ["att_rate",0.07,"r"]
                  ]
                },
                {
                  "id":654321,"p":0,"g":5,"f":"set_cri","type":"helm","level":88,
                  "name":"应被过滤","mainStatValue":100,
                  "op":[["max_hp",100],["speed",3],["cri",0.04],["att_rate",0.05],["def_rate",0.06]]
                }
              ],
              "units":[
                [{"id":1,"name":"短列表","g":5,"z":3}],
                [{"id":42,"name":"测试英雄","g":6,"z":6},{"id":43,"name":"第二英雄","g":5,"z":4},{"id":44,"name":"五星五觉英雄","g":5,"z":5}]
              ]
            }
            """;
        var result = FribbelsGearExporter.ConvertParserResponse(parserResponse, 6);
        if (result.ItemCount != 1 || result.HeroCount != 3 || result.LevelZeroItemCount != 0)
            throw new InvalidOperationException("gear.txt 汇总数量错误");
        using var export = System.Text.Json.JsonDocument.Parse(result.GearText);
        var item = export.RootElement.GetProperty("items")[0];
        if (item.GetProperty("gear").GetString() != "Weapon"
            || item.GetProperty("rank").GetString() != "Epic"
            || item.GetProperty("set").GetString() != "SpeedSet"
            || item.GetProperty("enhance").GetInt32() != 6
            || item.GetProperty("main").GetProperty("type").GetString() != "Attack"
            || item.GetProperty("substats")[0].GetProperty("value").GetDouble() != 15
            || item.GetProperty("substats")[0].GetProperty("rolls").GetInt32() != 2
            || item.GetProperty("ingameId").GetInt32() != 123456
            || item.GetProperty("ingameEquippedId").GetString() != "9988")
            throw new InvalidOperationException("gear.txt 装备字段转换错误");
        var hero = export.RootElement.GetProperty("heroes")[0];
        if (hero.GetProperty("stars").GetInt32() != 6 || hero.GetProperty("awaken").GetInt32() != 6)
            throw new InvalidOperationException("gear.txt 英雄字段转换错误");
        var fiveStarResult = FribbelsGearExporter.ConvertParserResponse(
            parserResponse,
            6,
            GearScanHeroFilter.AtLeastFiveStarsFiveAwakened);
        var sixStarResult = FribbelsGearExporter.ConvertParserResponse(
            parserResponse,
            6,
            GearScanHeroFilter.SixStarsSixAwakened);
        if (fiveStarResult.HeroCount != 2 || sixStarResult.HeroCount != 1)
            throw new InvalidOperationException(
                $"英雄星级/觉醒过滤错误：5星5觉={fiveStarResult.HeroCount}，6星6觉={sixStarResult.HeroCount}");

        byte[] messagePack = [0x81, 0xA3, (byte)'r', (byte)'e', (byte)'s', 0xA2, (byte)'o', (byte)'k'];
        var lz4Payload = new byte[8 + 1 + messagePack.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lz4Payload, messagePack.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lz4Payload.AsSpan(4), 1 + messagePack.Length);
        lz4Payload[8] = (byte)(messagePack.Length << 4);
        messagePack.CopyTo(lz4Payload.AsSpan(9));
        var transportStream = new byte[4 + lz4Payload.Length];
        transportStream[2] = (byte)(lz4Payload.Length >> 8);
        transportStream[3] = (byte)lz4Payload.Length;
        lz4Payload.CopyTo(transportStream.AsSpan(4));
        var decodedPayloads = EpicSevenTransportDecoder.DecodeStream(transportStream);
        if (decodedPayloads.Count != 1 || !decodedPayloads[0].AsSpan().SequenceEqual(lz4Payload))
            throw new InvalidOperationException("游戏 TCP 查询层解码错误");
        var decodedMessage = Lz4BlockDecoder.DecodeGamePayload(decodedPayloads[0]);
        if (!decodedMessage.AsSpan().SequenceEqual(messagePack)
            || new MessagePackReader(decodedMessage).ReadDocument() is not Dictionary<string, object?> message
            || message.GetValueOrDefault("res") as string != "ok")
            throw new InvalidOperationException("游戏 LZ4/MessagePack 解码错误");

        Console.WriteLine("装备扫描自检通过：国际服3333/中国服5222、PCAPNG/TCP重组、传输解码、LZ4、MessagePack、装备转换、英雄过滤与+6过滤均正常");
    }
    finally
    {
        Directory.Delete(testRoot, recursive: true);
    }
    return;
}

if (args.Contains("--star-forge"))
{
    var imagePath = args.SkipWhile(arg => arg != "--star-forge").Skip(1).FirstOrDefault()
        ?? throw new ArgumentException("--star-forge 后必须提供星之铁匠铺截图路径");
    using var bitmap = new Bitmap(imagePath);
    using var ocr = new StarForgeOcrEngine();
    var result = await ocr.RecognizeAsync(bitmap, CancellationToken.None);
    Console.WriteLine(result.RawText);
    foreach (var stat in result.Stats)
        Console.WriteLine($"  {stat.StatName} {stat.DisplayValue}");
    if (!result.IsReliable)
        throw new InvalidOperationException(
            $"星之铁匠铺识别不完整：screen={result.IsForgeScreen}, button={result.CanChange}, stats={result.Stats.Count}");
    using var resized = new Bitmap(1920, 1080);
    using (var graphics = Graphics.FromImage(resized))
        graphics.DrawImage(bitmap, new Rectangle(0, 0, resized.Width, resized.Height));
    var resizedResult = await ocr.RecognizeAsync(resized, CancellationToken.None);
    if (!resizedResult.IsReliable
        || !result.Stats.Select(stat => (stat.StatName, stat.Value))
            .SequenceEqual(resizedResult.Stats.Select(stat => (stat.StatName, stat.Value))))
        throw new InvalidOperationException("星之铁匠铺 OCR 未通过 1920×1080 分辨率缩放测试");
    Console.WriteLine("星之铁匠铺 OCR 测试通过：原始截图与 1920×1080 缩放结果一致");
    return;
}

if (args.Contains("--custom-demand"))
{
    var testRoot = Path.Combine(Path.GetTempPath(), "TiezhuToolbox-custom-demand-test-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable("TIEZHU_TOOLBOX_USER_ROOT", testRoot);
    var database = DemandDatabase.Instance;
    if (!database.IsLoaded)
        throw new InvalidOperationException($"静态需求数据未加载：{database.ErrorMessage}");
    var set = database.Sets.First(item => item.Profiles.Count > 0);
    var store = CustomDemandProfileStore.Instance;
    var custom = new CustomDemandProfile
    {
        SetCode = set.Code,
        Name = "生命值·速度",
        Stats = { "生命值", "速度" },
        Weights = new Dictionary<string, double>
        {
            ["生命值"] = 2,
            ["速度"] = 4,
        },
    };
    store.Upsert(custom);
    var saved = store.GetProfiles(set.Code).Single();
    var customPath = Path.Combine(testRoot, "custom-demand-profiles.json");
    if (!File.Exists(customPath) || File.Exists(Path.Combine(testRoot, "settings.json"))
        || !saved.Id.StartsWith("custom-", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("手动需求没有保存到独立文件");
    }

    var equipment = new EquipmentInfo
    {
        SetName = set.Name,
        Level = 85,
        Quality = "传说武器",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "4" },
        },
    };
    if (SetProfileMatcher.Match(equipment, top: int.MaxValue)
        .All(result => result.ProfileId != saved.Id))
    {
        throw new InvalidOperationException("启用的手动需求没有参与套装匹配");
    }

    store.SetEnabled(set.Code, saved.Id, false);
    if (SetProfileMatcher.Match(equipment, top: int.MaxValue)
        .Any(result => result.ProfileId == saved.Id))
    {
        throw new InvalidOperationException("停用的手动需求仍参与套装匹配");
    }

    saved.Name = "生命值";
    saved.Stats = new List<string> { "生命值" };
    saved.Weights = new Dictionary<string, double> { ["生命值"] = 3 };
    saved.Enabled = true;
    store.Upsert(saved);
    var edited = store.Find(set.Code, saved.Id);
    if (edited?.Name != "生命值" || edited.Stats.Count != 1 || !edited.Enabled)
        throw new InvalidOperationException("手动需求编辑结果不正确");

    store.Remove(set.Code, saved.Id);
    if (store.GetProfiles(set.Code).Count != 0)
        throw new InvalidOperationException("手动需求删除失败");
    Console.WriteLine("手动需求独立保存、添加、编辑、启停、删除及匹配测试通过");
    return;
}

if (args.Contains("--config-smoke"))
{
    var testRoot = Path.Combine(Path.GetTempPath(), "TiezhuToolbox-config-test-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable("TIEZHU_TOOLBOX_USER_ROOT", testRoot);
    try
    {
        var persistedProfileKey = DemandDatabase.Instance.Sets
            .SelectMany(set => set.Profiles.Select(profile =>
                SetProfileMatcher.CreateProfileKey(set.Code, profile.Id)))
            .First();
        Exception? settingsError = null;
        var settingsThread = new Thread(() =>
        {
            try
            {
                using (var firstForm = new TiezhuToolbox.MainForm())
                {
                    var threshold = typeof(TiezhuToolbox.MainForm).GetField("numLeftThreshold",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    threshold.GetType().GetProperty("Value")!.SetValue(threshold, 31M);
                    var level88Threshold = typeof(TiezhuToolbox.MainForm).GetField("numLevel88Threshold",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    var defaultLevel88Value = (decimal)level88Threshold.GetType().GetProperty("Value")!.GetValue(level88Threshold)!;
                    if (defaultLevel88Value != 28M)
                        throw new InvalidOperationException($"88级默认阈值错误：{defaultLevel88Value}");
                    level88Threshold.GetType().GetProperty("Value")!.SetValue(level88Threshold, 33M);
                    var address = (Control)typeof(TiezhuToolbox.MainForm).GetField("txtAddress",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    address.Text = "127.0.0.1:5555";
                    var gearScanMinimum = typeof(TiezhuToolbox.MainForm).GetField("_comboGearScanMinimumEnhance",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    gearScanMinimum.GetType().GetProperty("SelectedValue")!.SetValue(gearScanMinimum, "+12");
                    var gearScanHeroFilter = typeof(TiezhuToolbox.MainForm).GetField("_comboGearScanHeroFilter",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    gearScanHeroFilter.GetType().GetProperty("SelectedValue")!
                        .SetValue(gearScanHeroFilter, "仅6星6觉醒");
                    var maxAutoEquipment = typeof(TiezhuToolbox.MainForm).GetField("_numAutoMaxEquipment",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    maxAutoEquipment.GetType().GetProperty("Value")!.SetValue(maxAutoEquipment, 17M);
                    var disposalMethod = typeof(TiezhuToolbox.MainForm).GetField("_comboAutoDisposalMethod",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    disposalMethod.GetType().GetProperty("SelectedValue")!.SetValue(disposalMethod, "分解");
                    var matchThreshold = typeof(TiezhuToolbox.MainForm).GetField("_numHeroMatchThreshold",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    matchThreshold.GetType().GetProperty("Value")!.SetValue(matchThreshold, 82M);
                    var stopOnValuable = typeof(TiezhuToolbox.MainForm).GetField("_chkAutoStopOnValuableEquipment",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    stopOnValuable.GetType().GetProperty("Checked")!.SetValue(stopOnValuable, false);
                    var heroicOnlyGambleSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkHeroicOnlyGambleSpeed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    heroicOnlyGambleSpeed.GetType().GetProperty("Checked")!.SetValue(heroicOnlyGambleSpeed, true);
                    var speedSetRequiresSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkSpeedSetRequiresSpeed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    var criticalNecklaceMainStatRule = typeof(TiezhuToolbox.MainForm).GetField("_chkCriticalNecklaceMainStatRule",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    if (!(bool)speedSetRequiresSpeed.GetType().GetProperty("Checked")!.GetValue(speedSetRequiresSpeed)!
                        || !(bool)criticalNecklaceMainStatRule.GetType().GetProperty("Checked")!.GetValue(criticalNecklaceMainStatRule)!)
                        throw new InvalidOperationException("两项特殊强化规则没有默认开启");
                    speedSetRequiresSpeed.GetType().GetProperty("Checked")!.SetValue(speedSetRequiresSpeed, false);
                    criticalNecklaceMainStatRule.GetType().GetProperty("Checked")!.SetValue(criticalNecklaceMainStatRule, false);
                    var starForgeMaximum = typeof(TiezhuToolbox.MainForm).GetField("_numStarForgeMaximumChanges",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(firstForm)!;
                    starForgeMaximum.GetType().GetProperty("Value")!.SetValue(starForgeMaximum, 77M);
                    var starForgeRows = (System.Collections.IList)typeof(TiezhuToolbox.MainForm)
                        .GetField("_starForgeRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .GetValue(firstForm)!;
                    var secondTarget = starForgeRows[1]!;
                    secondTarget.GetType().GetProperty("Enabled")!.GetValue(secondTarget)!.GetType()
                        .GetProperty("Checked")!.SetValue(secondTarget.GetType().GetProperty("Enabled")!.GetValue(secondTarget), true);
                    secondTarget.GetType().GetProperty("Stat")!.GetValue(secondTarget)!.GetType()
                        .GetProperty("SelectedValue")!.SetValue(secondTarget.GetType().GetProperty("Stat")!.GetValue(secondTarget), "生命值");
                    secondTarget.GetType().GetProperty("Minimum")!.GetValue(secondTarget)!.GetType()
                        .GetProperty("Value")!.SetValue(secondTarget.GetType().GetProperty("Minimum")!.GetValue(secondTarget), 250M);
                    typeof(TiezhuToolbox.MainForm).GetMethod("SetDemandProfileEnabled",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                        .Invoke(firstForm, new object[] { persistedProfileKey, false });
                    typeof(TiezhuToolbox.MainForm).GetMethod("SaveSettingsFromControls",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(firstForm, null);
                }
                using var secondForm = new TiezhuToolbox.MainForm();
                var loadedThreshold = typeof(TiezhuToolbox.MainForm).GetField("numLeftThreshold",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var value = (decimal)loadedThreshold.GetType().GetProperty("Value")!.GetValue(loadedThreshold)!;
                var loadedLevel88Threshold = typeof(TiezhuToolbox.MainForm).GetField("numLevel88Threshold",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var level88Value = (decimal)loadedLevel88Threshold.GetType().GetProperty("Value")!.GetValue(loadedLevel88Threshold)!;
                var loadedAddress = (Control)typeof(TiezhuToolbox.MainForm).GetField("txtAddress",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var loadedGearScanMinimum = typeof(TiezhuToolbox.MainForm).GetField("_comboGearScanMinimumEnhance",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var gearScanMinimumValue = loadedGearScanMinimum.GetType().GetProperty("SelectedValue")!
                    .GetValue(loadedGearScanMinimum) as string;
                var loadedGearScanHeroFilter = typeof(TiezhuToolbox.MainForm).GetField("_comboGearScanHeroFilter",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var gearScanHeroFilterValue = loadedGearScanHeroFilter.GetType().GetProperty("SelectedValue")!
                    .GetValue(loadedGearScanHeroFilter) as string;
                var loadedMaxAutoEquipment = typeof(TiezhuToolbox.MainForm).GetField("_numAutoMaxEquipment",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var maxAutoValue = (decimal)loadedMaxAutoEquipment.GetType().GetProperty("Value")!.GetValue(loadedMaxAutoEquipment)!;
                var loadedDisposalMethod = typeof(TiezhuToolbox.MainForm).GetField("_comboAutoDisposalMethod",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var disposalValue = loadedDisposalMethod.GetType().GetProperty("SelectedValue")!.GetValue(loadedDisposalMethod) as string;
                var loadedMatchThreshold = typeof(TiezhuToolbox.MainForm).GetField("_numHeroMatchThreshold",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var matchValue = (decimal)loadedMatchThreshold.GetType().GetProperty("Value")!.GetValue(loadedMatchThreshold)!;
                var loadedStopOnValuable = typeof(TiezhuToolbox.MainForm).GetField("_chkAutoStopOnValuableEquipment",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var stopOnValuableValue = (bool)loadedStopOnValuable.GetType().GetProperty("Checked")!.GetValue(loadedStopOnValuable)!;
                var loadedHeroicOnlyGambleSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkHeroicOnlyGambleSpeed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var heroicOnlyGambleSpeedValue = (bool)loadedHeroicOnlyGambleSpeed.GetType().GetProperty("Checked")!
                    .GetValue(loadedHeroicOnlyGambleSpeed)!;
                var loadedSpeedSetRequiresSpeed = typeof(TiezhuToolbox.MainForm).GetField("_chkSpeedSetRequiresSpeed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var speedSetRequiresSpeedValue = (bool)loadedSpeedSetRequiresSpeed.GetType().GetProperty("Checked")!
                    .GetValue(loadedSpeedSetRequiresSpeed)!;
                var loadedCriticalNecklaceMainStatRule = typeof(TiezhuToolbox.MainForm).GetField("_chkCriticalNecklaceMainStatRule",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var criticalNecklaceMainStatRuleValue = (bool)loadedCriticalNecklaceMainStatRule.GetType().GetProperty("Checked")!
                    .GetValue(loadedCriticalNecklaceMainStatRule)!;
                var loadedDisabledProfiles = (IReadOnlySet<string>)typeof(TiezhuToolbox.MainForm)
                    .GetField("_disabledDemandProfiles",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(secondForm)!;
                var loadedStarForgeMaximum = typeof(TiezhuToolbox.MainForm).GetField("_numStarForgeMaximumChanges",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(secondForm)!;
                var starForgeMaximumValue = (decimal)loadedStarForgeMaximum.GetType().GetProperty("Value")!
                    .GetValue(loadedStarForgeMaximum)!;
                var loadedStarForgeRows = (System.Collections.IList)typeof(TiezhuToolbox.MainForm)
                    .GetField("_starForgeRows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(secondForm)!;
                var loadedSecondTarget = loadedStarForgeRows[1]!;
                var loadedSecondEnabledControl = loadedSecondTarget.GetType().GetProperty("Enabled")!.GetValue(loadedSecondTarget)!;
                var loadedSecondStatControl = loadedSecondTarget.GetType().GetProperty("Stat")!.GetValue(loadedSecondTarget)!;
                var loadedSecondMinimumControl = loadedSecondTarget.GetType().GetProperty("Minimum")!.GetValue(loadedSecondTarget)!;
                var loadedSecondEnabled = (bool)loadedSecondEnabledControl.GetType().GetProperty("Checked")!.GetValue(loadedSecondEnabledControl)!;
                var loadedSecondStat = loadedSecondStatControl.GetType().GetProperty("SelectedValue")!.GetValue(loadedSecondStatControl) as string;
                var loadedSecondMinimum = (decimal)loadedSecondMinimumControl.GetType().GetProperty("Value")!.GetValue(loadedSecondMinimumControl)!;
                if (value != 31M || level88Value != 33M || maxAutoValue != 17M
                    || disposalValue != "分解" || matchValue != 82M || stopOnValuableValue
                    || !heroicOnlyGambleSpeedValue
                    || speedSetRequiresSpeedValue || criticalNecklaceMainStatRuleValue
                    || starForgeMaximumValue != 77M || !loadedSecondEnabled
                    || loadedSecondStat != "生命值" || loadedSecondMinimum != 250M
                    || !loadedDisabledProfiles.Contains(persistedProfileKey)
                    || loadedAddress.Text != "127.0.0.1:5555" || gearScanMinimumValue != "+12"
                    || gearScanHeroFilterValue != "仅6星6觉醒")
                    throw new InvalidOperationException("软件设置重载结果不一致");
            }
            catch (Exception ex)
            {
                settingsError = ex;
            }
        });
        settingsThread.SetApartmentState(ApartmentState.STA);
        settingsThread.Start();
        settingsThread.Join();
        if (settingsError != null)
            throw new InvalidOperationException("软件设置持久化测试失败", settingsError);
        Console.WriteLine("配置持久化测试通过：强化规则与停用需求子类均可保存并恢复");
    }
    finally
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, recursive: true);
    }
    return;
}

if (args.Contains("--automation-smoke"))
{
    var imagePaths = args.Where(arg => arg != "--automation-smoke").ToArray();
    if (imagePaths.Length != 7 || imagePaths.Any(path => !File.Exists(path)))
        throw new ArgumentException("--automation-smoke 后需依次提供背包、强化、等级弹窗、已登记材料、出售确认、分解确认、经验溢出奖励截图");

    using var matcher = new AutomationScreenMatcher();
    using var backpack = new Bitmap(imagePaths[0]);
    using var enhance = new Bitmap(imagePaths[1]);
    using var popup = new Bitmap(imagePaths[2]);
    using var registered = new Bitmap(imagePaths[3]);
    using var sellConfirmation = new Bitmap(imagePaths[4]);
    using var extractConfirmation = new Bitmap(imagePaths[5]);
    using var rewardPopup = new Bitmap(imagePaths[6]);

    void AssertScreen(Bitmap image, AutomationGameScreen expected)
    {
        var actual = matcher.DetectScreen(image, out var confidence);
        Console.WriteLine($"  界面：期望 {expected}，实际 {actual}，置信度 {confidence:P1}");
        if (actual != expected)
            throw new InvalidOperationException($"自动强化界面识别失败：期望 {expected}，实际 {actual}（{confidence:P1}）");
    }

    AssertScreen(backpack, AutomationGameScreen.Backpack);
    AssertScreen(enhance, AutomationGameScreen.EnhanceEquipment);
    AssertScreen(popup, AutomationGameScreen.AutoRegisterPopup);
    AssertScreen(registered, AutomationGameScreen.EnhanceEquipment);
    AssertScreen(sellConfirmation, AutomationGameScreen.SellConfirmation);
    AssertScreen(extractConfirmation, AutomationGameScreen.ExtractConfirmation);
    AssertScreen(rewardPopup, AutomationGameScreen.EnhancementRewardPopup);

    var expectedButtons = new[]
    {
        (backpack, AutomationTemplate.BackpackEnhance),
        (enhance, AutomationTemplate.AutoRegister),
        (enhance, AutomationTemplate.Sell),
        (enhance, AutomationTemplate.Extract),
        (popup, AutomationTemplate.Target3),
        (popup, AutomationTemplate.Target6),
        (popup, AutomationTemplate.Target9),
        (popup, AutomationTemplate.Target12),
        (popup, AutomationTemplate.Target15),
        (registered, AutomationTemplate.ReadyEnhance),
        (sellConfirmation, AutomationTemplate.SellConfirmButton),
        (extractConfirmation, AutomationTemplate.ExtractConfirmButton),
        (rewardPopup, AutomationTemplate.RewardClose),
    };
    foreach (var (image, template) in expectedButtons)
    {
        var match = matcher.Find(image, template);
        Console.WriteLine($"  按钮：{template} {match.Confidence:P1} @ {match.Center}");
        if (!match.IsMatch())
            throw new InvalidOperationException($"自动强化按钮识别失败：{template}（{match.Confidence:P1}）");
    }

    var targetRows = new[]
    {
        AutomationTemplate.Target15,
        AutomationTemplate.Target12,
        AutomationTemplate.Target9,
        AutomationTemplate.Target6,
        AutomationTemplate.Target3,
    }.Select(template => matcher.Find(popup, template).Center.Y).ToArray();
    if (!targetRows.Zip(targetRows.Skip(1), (upper, lower) => lower - upper)
            .All(gap => gap is >= 55 and <= 90))
    {
        throw new InvalidOperationException(
            $"强化等级按钮行定位异常：{string.Join("/", targetRows)}");
    }

    if (matcher.HasRegisteredMaterials(enhance) || matcher.HasRegisteredMaterials(popup))
        throw new InvalidOperationException("空材料槽被误判为已登记材料");
    if (!matcher.HasRegisteredMaterials(registered))
        throw new InvalidOperationException("已登记的强化材料未被识别");

    var targets = new[] { 0, 3, 6, 9, 12 }.Select(level => AutomationScreenMatcher.NextTargetLevel(level)).ToArray();
    if (!targets.SequenceEqual(new int?[] { 3, 6, 9, 12, 15 })
        || AutomationScreenMatcher.NextTargetLevel(15) != null)
        throw new InvalidOperationException("下一强化档位计算错误");

    using var resized = new Bitmap(1280, 720);
    using (var graphics = Graphics.FromImage(resized))
        graphics.DrawImage(backpack, new Rectangle(0, 0, resized.Width, resized.Height));
    AssertScreen(resized, AutomationGameScreen.Backpack);

    var automationTemplateDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "TiezhuToolbox", "Assets", "Templates"));
    using (var ocr = new OcrEngine(automationTemplateDir))
    {
        var info = await ocr.RecognizeAsync(imagePaths[1]);
        var advice = EnhancementAdvisor.Analyze(info, 24, 24, 28);
        Console.WriteLine($"  OCR：{info.Level}级 {info.Quality}，+{info.EnhanceLevel}，{info.Score:0.##}分，建议={advice.Advice}");
        if (info.Level != 85 || info.Quality != "英雄鞋子" || info.EnhanceLevel != 0
            || advice.Advice != EnhanceAdvice.GiveUp)
            throw new InvalidOperationException("自动强化样例的 OCR 或强化建议结果不符合预期");
    }

    Console.WriteLine("自动强化测试通过：7 个界面、13 个按钮、材料槽、分辨率缩放、OCR 与强化建议均正常");
    return;
}

if (args.Contains("--ui-smoke"))
{
    var uiTestRoot = Path.Combine(Path.GetTempPath(), "TiezhuToolbox-ui-test-" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable("TIEZHU_TOOLBOX_USER_ROOT", uiTestRoot);
    var uiCustomSet = DemandDatabase.Instance.Sets.First(set => set.Profiles.Count > 0);
    var uiCustomProfile = new CustomDemandProfile
    {
        SetCode = uiCustomSet.Code,
        Name = "攻击力·速度",
        Stats = { "攻击力", "速度" },
        Weights = new Dictionary<string, double> { ["攻击力"] = 3, ["速度"] = 4 },
    };
    CustomDemandProfileStore.Instance.Upsert(uiCustomProfile);
    Exception? uiError = null;
    var thread = new Thread(() =>
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            using var form = new TiezhuToolbox.MainForm();
            form.Show();
            Application.DoEvents();
            var dpiScale = form.DeviceDpi / 96D;
            int DpiPixel(int logicalPixel) => (int)Math.Round(logicalPixel * dpiScale);
            if (form.AutoScaleMode != AutoScaleMode.Dpi)
                throw new InvalidOperationException($"主窗体未启用 DPI 缩放：{form.AutoScaleMode}");

            var tabsField = typeof(TiezhuToolbox.MainForm).GetField("_mainTabs",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("未找到主页签");
            var tabs = tabsField.GetValue(form) ?? throw new InvalidOperationException("主页签未初始化");
            var pages = tabs.GetType().GetProperty("Pages")?.GetValue(tabs) as System.Collections.ICollection;
            if (pages?.Count != 6)
                throw new InvalidOperationException($"页签数量错误：{pages?.Count}");

            var selectedIndex = tabs.GetType().GetProperty("SelectedIndex")!;
            void CaptureTab(string name)
            {
                var directory = Environment.GetEnvironmentVariable("TIEZHU_UI_CAPTURE_DIR");
                if (string.IsNullOrWhiteSpace(directory))
                    return;
                Directory.CreateDirectory(directory);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save(Path.Combine(directory, name + ".png"));
            }
            var deviceSelect = typeof(TiezhuToolbox.MainForm).GetField("comboDevices",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var deviceReadOnly = (bool)deviceSelect.GetType().GetProperty("ReadOnly")!.GetValue(deviceSelect)!;
            if (deviceReadOnly)
                throw new InvalidOperationException("设备下拉框被完全禁用，无法展开选择");
            var deviceListMode = (bool)deviceSelect.GetType().GetProperty("List")!.GetValue(deviceSelect)!;
            if (!deviceListMode)
                throw new InvalidOperationException("设备下拉框仍允许文字输入");
            var expandDrop = deviceSelect.GetType().GetProperty("ExpandDrop")!;
            expandDrop.SetValue(deviceSelect, true);
            Application.DoEvents();
            if (!(bool)expandDrop.GetValue(deviceSelect)!)
                throw new InvalidOperationException("设备下拉框无法展开");
            expandDrop.SetValue(deviceSelect, false);
            Application.DoEvents();
            var addressInput = (Control)typeof(TiezhuToolbox.MainForm).GetField("txtAddress",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (addressInput.Width < DpiPixel(200))
                throw new InvalidOperationException($"ADB 地址输入框宽度不足：{addressInput.Width}");
            var showDemand = typeof(TiezhuToolbox.MainForm).GetMethod("ShowDemandRecommendations",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("找不到套装需求展示方法");
            showDemand.Invoke(form,
            [
                new EquipmentInfo
                {
                    Level = 88,
                    Quality = "传说鞋子",
                    SetName = "速度套装",
                    MainStatName = "速度",
                    MainStatValue = "1",
                    SubStats =
                    {
                        new SubStat { Name = "生命值", Value = "8%" },
                        new SubStat { Name = "防御力", Value = "8%" },
                        new SubStat { Name = "效果命中", Value = "8%" },
                        new SubStat { Name = "效果抗性", Value = "8%" },
                    },
                },
            ]);
            Application.DoEvents();
            var demandResults = (FlowLayoutPanel)typeof(TiezhuToolbox.MainForm).GetField("flowHeroes",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (demandResults.Controls.Count == 0 || demandResults.Controls.Count > 5)
                throw new InvalidOperationException($"装备页需求子类卡片数量错误：{demandResults.Controls.Count}");
            var firstCard = demandResults.Controls[0];
            var collapsedHeight = firstCard.Height;
            var header = firstCard.Controls.Cast<Control>().OfType<Panel>()
                .First(panel => panel.Cursor == Cursors.Hand);
            typeof(Control).GetMethod("OnClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(header, new object[] { EventArgs.Empty });
            Application.DoEvents();
            if (firstCard.Height <= collapsedHeight)
                throw new InvalidOperationException("装备页需求子类卡片无法展开英雄配装");
            CaptureTab("equipment");
            var timer = (System.Windows.Forms.Timer)(typeof(TiezhuToolbox.MainForm)
                .GetField("continuousRecognitionTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(form) ?? throw new InvalidOperationException("持续识别计时器未初始化"));
            timer.Interval = 60000;
            var loadingField = typeof(TiezhuToolbox.MainForm).GetField("_isLoadingSettings",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var continuousCheck = typeof(TiezhuToolbox.MainForm).GetField("chkContinuousRecognition",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            loadingField.SetValue(form, true);
            continuousCheck.GetType().GetProperty("Checked")!.SetValue(continuousCheck, true);
            loadingField.SetValue(form, false);
            typeof(TiezhuToolbox.MainForm).GetMethod("ApplyRecognitionAvailability",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form, new object[] { false });
            if (!timer.Enabled)
                throw new InvalidOperationException("装备页未恢复持续识别");

            selectedIndex.SetValue(tabs, 1);
            Application.DoEvents();
            CaptureTab("gear-scan");
            var gearScanStart = (Control)typeof(TiezhuToolbox.MainForm).GetField("_btnGearScanStart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var gearScanStop = (Control)typeof(TiezhuToolbox.MainForm).GetField("_btnGearScanStop",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var gearScanExport = (Control)typeof(TiezhuToolbox.MainForm).GetField("_btnGearScanExport",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var gearScanLog = (RichTextBox)typeof(TiezhuToolbox.MainForm).GetField("_gearScanLog",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var gearScanMinimum = typeof(TiezhuToolbox.MainForm).GetField("_comboGearScanMinimumEnhance",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var gearScanMinimumValue = gearScanMinimum.GetType().GetProperty("SelectedValue")!.GetValue(gearScanMinimum) as string;
            var gearScanHeroFilter = typeof(TiezhuToolbox.MainForm).GetField("_comboGearScanHeroFilter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var gearScanHeroFilterValue = gearScanHeroFilter.GetType().GetProperty("SelectedValue")!
                .GetValue(gearScanHeroFilter) as string;
            if (!gearScanStart.Enabled || gearScanStop.Enabled || gearScanExport.Enabled
                || !gearScanLog.ReadOnly || gearScanMinimumValue != "+6"
                || gearScanHeroFilterValue != "全部英雄" || timer.Enabled)
                throw new InvalidOperationException("装备扫描页初始状态不正确");
            if (gearScanLog.Right < gearScanLog.Parent!.ClientSize.Width - gearScanLog.Parent.Padding.Right - 2)
                throw new InvalidOperationException("装备扫描日志未填满内容区");

            selectedIndex.SetValue(tabs, 2);
            Application.DoEvents();
            CaptureTab("auto-enhance");
            var autoStart = (Control)typeof(TiezhuToolbox.MainForm).GetField("_btnAutoStart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var autoLog = (RichTextBox)typeof(TiezhuToolbox.MainForm).GetField("_autoLog",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (!autoStart.Enabled || !autoLog.ReadOnly || timer.Enabled)
                throw new InvalidOperationException("自动强化页初始状态不正确");
            if (autoLog.Right < autoLog.Parent!.ClientSize.Width - autoLog.Parent.Padding.Right - 2)
                throw new InvalidOperationException(
                    $"自动强化日志未填满内容区：日志={autoLog.Bounds}，父容器={autoLog.Parent.ClientSize}");

            selectedIndex.SetValue(tabs, 3);
            Application.DoEvents();
            CaptureTab("star-forge");
            var starForgeStart = (Control)typeof(TiezhuToolbox.MainForm).GetField("_btnStarForgeStart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var starForgeLog = (RichTextBox)typeof(TiezhuToolbox.MainForm).GetField("_starForgeLog",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var starForgeMaximum = typeof(TiezhuToolbox.MainForm).GetField("_numStarForgeMaximumChanges",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var starForgeMaximumValue = (decimal)starForgeMaximum.GetType().GetProperty("Value")!.GetValue(starForgeMaximum)!;
            if (!starForgeStart.Enabled || !starForgeLog.ReadOnly || starForgeMaximumValue != 100M || timer.Enabled)
                throw new InvalidOperationException("星之铁匠铺页初始状态不正确");
            if (starForgeLog.Right < starForgeLog.Parent!.ClientSize.Width - starForgeLog.Parent.Padding.Right - 2)
                throw new InvalidOperationException("星之铁匠铺日志未填满内容区");

            selectedIndex.SetValue(tabs, 4);
            Application.DoEvents();
            CaptureTab("demand-analysis");
            var demandBrowser = typeof(TiezhuToolbox.MainForm).GetField("_demandBrowserControl",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var setList = (ListBox)demandBrowser.GetType().GetField("_setList",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(demandBrowser)!;
            var profilesPanel = (FlowLayoutPanel)demandBrowser.GetType().GetField("_profiles",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(demandBrowser)!;
            var addProfileButton = (AntdUI.Button)demandBrowser.GetType().GetField("_addProfileButton",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(demandBrowser)!;
            if (setList.Items.Count != 23)
                throw new InvalidOperationException($"需求分析套装数量错误：{setList.Items.Count}");
            var populatedIndex = Enumerable.Range(0, setList.Items.Count)
                .First(index => ((DemandSet)setList.Items[index]!).Profiles.Count > 0);
            setList.SelectedIndex = populatedIndex;
            Application.DoEvents();
            if (profilesPanel.Controls.Count == 0)
                throw new InvalidOperationException("需求分析页未显示属性子类");
            var profileCards = profilesPanel.Controls.Cast<Control>()
                .Where(control => Equals(control.Tag, "profile-card"))
                .ToList();
            static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>()
                .SelectMany(control => new[] { control }.Concat(Descendants(control)));
            var profileSwitches = profileCards
                .SelectMany(Descendants)
                .OfType<AntdUI.Switch>()
                .ToList();
            var selectedSet = (DemandSet)setList.SelectedItem!;
            var expectedCustomCount = selectedSet.Code == uiCustomSet.Code ? 1 : 0;
            if (!addProfileButton.Enabled
                || profileSwitches.Count != selectedSet.Profiles.Count + expectedCustomCount
                || profileSwitches.Any(profileSwitch => !profileSwitch.Checked))
                throw new InvalidOperationException("需求子类参与匹配开关数量或默认状态错误");
            if (expectedCustomCount > 0)
            {
                var customCard = profileCards.Single(card => Descendants(card)
                    .OfType<AntdUI.Switch>()
                    .Any(profileSwitch => profileSwitch.Tag is string key
                                          && key.Contains("/custom-", StringComparison.Ordinal)));
                var customActions = Descendants(customCard).OfType<AntdUI.Button>()
                    .Where(button => button.Text is "编辑" or "删除")
                    .ToList();
                var customActionPanel = customActions.FirstOrDefault()?.Parent;
                if (customActions.Count != 2 || customActions.Any(button =>
                        !button.Visible || button.Parent == null || button.Right > button.Parent.ClientSize.Width)
                    || customActionPanel?.Parent == null
                    || customActionPanel.Parent.GetChildAtPoint(new Point(
                        customActionPanel.Left + 2, customActionPanel.Top + 2)) != customActionPanel)
                {
                    throw new InvalidOperationException("手动需求的编辑或删除按钮未显示在卡片范围内");
                }
            }
            var analysisCard = profileCards.First(card => card.Controls.Cast<Control>()
                .OfType<Panel>().Any(panel => panel.Cursor == Cursors.Hand));
            var analysisCollapsedHeight = analysisCard.Height;
            var analysisHeader = analysisCard.Controls.Cast<Control>().OfType<Panel>()
                .First(panel => panel.Cursor == Cursors.Hand);
            var analysisBuilds = analysisCard.Controls.Cast<Control>().OfType<Panel>()
                .First(panel => panel != analysisHeader);
            if (analysisBuilds.Visible)
                throw new InvalidOperationException("需求分析页角色列表没有默认折叠");
            typeof(Control).GetMethod("OnClick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(analysisHeader, new object[] { EventArgs.Empty });
            Application.DoEvents();
            if (!analysisBuilds.Visible || analysisCard.Height <= analysisCollapsedHeight)
                throw new InvalidOperationException("需求分析页角色列表无法展开");
            CaptureTab("demand-analysis-expanded");
            var firstProfileSwitch = profileSwitches.First(profileSwitch =>
                profileSwitch.Tag is string key && !key.Contains("/custom-", StringComparison.Ordinal));
            firstProfileSwitch.Checked = false;
            Application.DoEvents();
            var disabledProfiles = (IReadOnlySet<string>)typeof(TiezhuToolbox.MainForm)
                .GetField("_disabledDemandProfiles",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(form)!;
            if (firstProfileSwitch.Tag is not string disabledKey
                || !disabledProfiles.Contains(disabledKey))
                throw new InvalidOperationException("需求子类开关没有更新匹配过滤配置");
            if (timer.Enabled)
                throw new InvalidOperationException("离开装备页后持续识别仍在运行");
            selectedIndex.SetValue(tabs, 5);
            Application.DoEvents();
            var settingInputs = new[] { "numLeftThreshold", "numRightThreshold", "numLevel88Threshold", "comboRecognitionHotKey", "numRecognitionInterval" }
                .Select(name => (Control)typeof(TiezhuToolbox.MainForm).GetField(name,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!);
            if (settingInputs.Any(control =>
                    control.Height < DpiPixel(32) || control.Width < DpiPixel(70)))
                throw new InvalidOperationException("软件设置输入框尺寸不足");
            var settingRowLabels = new[]
                {
                    "lblThresholdGroup", "lblThLeft", "lblThRight", "lblTh88",
                    "lblRecognitionGroup", "lblRecognitionHotKey", "lblRecognitionInterval", "lblIntervalUnit",
                }
                .Select(name => (Label)typeof(TiezhuToolbox.MainForm).GetField(name,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!);
            var clippedSettingLabel = settingRowLabels.FirstOrDefault(label =>
                label.GetPreferredSize(Size.Empty).Width > label.ClientSize.Width);
            if (clippedSettingLabel != null)
                throw new InvalidOperationException(
                    $"软件设置标签被裁剪：{clippedSettingLabel.Text}，"
                    + $"需要 {clippedSettingLabel.GetPreferredSize(Size.Empty).Width}，"
                    + $"实际 {clippedSettingLabel.ClientSize.Width}");
            var thresholdPanel = (Control)typeof(TiezhuToolbox.MainForm).GetField("thresholdPanel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var level88Input = (Control)typeof(TiezhuToolbox.MainForm).GetField("numLevel88Threshold",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (level88Input.Right > thresholdPanel.ClientSize.Width || level88Input.Bottom > thresholdPanel.ClientSize.Height)
                throw new InvalidOperationException(
                    $"88级阈值输入框被裁剪：输入框={level88Input.Bounds}，容器={thresholdPanel.ClientSize}");
            var settingsRulesLabel = (Label)typeof(TiezhuToolbox.MainForm).GetField("_settingsRulesLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var githubLink = (LinkLabel)typeof(TiezhuToolbox.MainForm).GetField("_githubLink",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var checkUpdateButton = (Control)typeof(TiezhuToolbox.MainForm).GetField("_btnCheckUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            var updateStatus = (Label)typeof(TiezhuToolbox.MainForm).GetField("_lblUpdateStatus",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (!githubLink.Text.Contains("GitHub", StringComparison.Ordinal)
                || !checkUpdateButton.Enabled
                || checkUpdateButton.Text != "检查更新"
                || !updateStatus.Text.Contains("GitHub", StringComparison.Ordinal))
                throw new InvalidOperationException("软件设置页 GitHub 链接或更新控件初始化错误");
            var heroicOnlySpeedCheck = (Control)typeof(TiezhuToolbox.MainForm).GetField("_chkHeroicOnlyGambleSpeed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            if (heroicOnlySpeedCheck.Width < DpiPixel(400) || heroicOnlySpeedCheck.Height < DpiPixel(32))
                throw new InvalidOperationException("紫装只赌速度设置项尺寸不足");
            var requiredRuleTexts = new[]
                {
                    "红装赌速度", "紫装只赌速度", "速度套速度规则", "暴击项链规则",
                    "套装子类", "右三主属性", "强化分数", "固定主属性",
                };
            if (requiredRuleTexts.Any(text => !settingsRulesLabel.Text.Contains(text)))
                throw new InvalidOperationException("软件设置页缺少自动规则说明");
            var preferredRulesHeight = settingsRulesLabel.GetPreferredSize(
                new Size(settingsRulesLabel.ClientSize.Width, 0)).Height;
            if (preferredRulesHeight > settingsRulesLabel.ClientSize.Height)
                throw new InvalidOperationException(
                    $"自动规则说明被裁剪：需要 {preferredRulesHeight}，实际 {settingsRulesLabel.ClientSize.Height}");
            CaptureTab("software-settings");
            var settingsHost = settingsRulesLabel.Parent?.Parent?.Parent as ScrollableControl
                ?? throw new InvalidOperationException("找不到软件设置滚动容器");
            settingsHost.AutoScrollPosition = new Point(0, settingsHost.VerticalScroll.Maximum);
            Application.DoEvents();
            CaptureTab("software-settings-rules");
            selectedIndex.SetValue(tabs, 0);
            Application.DoEvents();
            if (!timer.Enabled)
                throw new InvalidOperationException("返回装备页后持续识别未恢复");
            loadingField.SetValue(form, true);
            continuousCheck.GetType().GetProperty("Checked")!.SetValue(continuousCheck, false);
            loadingField.SetValue(form, false);
            typeof(TiezhuToolbox.MainForm).GetMethod("ApplyRecognitionAvailability",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form, new object[] { false });

            if (!DemandDatabase.Instance.IsLoaded || DemandDatabase.Instance.Sets.Count != 23)
                throw new InvalidOperationException("静态需求数据未加载");
            var topPanel = (Control)typeof(TiezhuToolbox.MainForm).GetField("topPanel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(form)!;
            Console.WriteLine($"  布局尺寸：窗体={form.ClientSize.Width}，页签={((Control)tabs).ClientSize.Width}，工具栏={topPanel.ClientSize.Width}，DPI={form.DeviceDpi}");
            form.Close();
        }
        catch (Exception ex)
        {
            uiError = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join(TimeSpan.FromSeconds(20));
    if (Directory.Exists(uiTestRoot))
        Directory.Delete(uiTestRoot, recursive: true);
    if (thread.IsAlive)
        throw new TimeoutException("界面冒烟测试超时");
    if (uiError != null)
        throw new InvalidOperationException("界面冒烟测试失败", uiError);
    Console.WriteLine("界面冒烟测试通过：6 个页签，23 个套装需求");
    return;
}

var screenshotsDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\bin\Release\net9.0-windows\win-x64\publish\screenshots";
var templateDir = @"E:\coding\TiezhuToolboxPRO\src\TiezhuToolbox\Assets\Templates";

// 新旧两种分辨率的截图
var imageNames = args.Length > 0
    ? args
    : new[] { "MuMuNxDevice_20260717_031029.png", "MuMuNxDevice_20260717_041111.png" };

// 合成样例自检（无需截图）：
// 样例一：速度套 + 速度主属性鞋 + 副属性{防御,生命,命中,抗性} → 调香师维波里丝(c5154) 应为 100%
// 样例二：暴击套 + 暴击率主属性项链 + 同样副属性 → c5154 主属性/套装均不符，不得出现
// 样例三：速度套速度鞋，副属性{生命,防御,命中,暴击率}但强化全跳暴击率 → c5154 应出现但匹配度大降（<50%）
if (args.Contains("--demand-data"))
{
    var database = DemandDatabase.Instance;
    if (!database.IsLoaded)
        throw new InvalidOperationException($"静态需求数据未加载：{database.ErrorMessage}");
    var profiles = database.Sets.SelectMany(set => set.Profiles).ToList();
    var builds = profiles.SelectMany(profile => profile.Heroes).ToList();
    var uniqueHeroes = builds.Select(hero => hero.Code).Distinct(StringComparer.Ordinal).Count();
    if (database.Sets.Count != 23 || database.Sets.Count(set => set.Profiles.Count > 0) != 21
        || profiles.Count != 171 || builds.Count != 644 || uniqueHeroes != 100)
    {
        throw new InvalidOperationException(
            $"需求数据规模错误：套装 {database.Sets.Count}/有数据 {database.Sets.Count(set => set.Profiles.Count > 0)}/子类 {profiles.Count}/配装 {builds.Count}/英雄 {uniqueHeroes}");
    }
    var duplicateBuildPreserved = profiles.Any(profile => profile.Heroes
        .GroupBy(hero => hero.Code, StringComparer.Ordinal)
        .Any(group => group.Select(hero => hero.ComboName).Distinct(StringComparer.Ordinal).Count() > 1));
    if (!duplicateBuildPreserved)
        throw new InvalidOperationException("同英雄不同完整套装组合未保留");
    var missingSetIcons = database.Sets
        .Where(set => DemandDatabase.GetSetIconPath(set.Code) == null)
        .Select(set => set.Code)
        .ToList();
    var missingAvatars = builds.Select(hero => hero.Code)
        .Distinct(StringComparer.Ordinal)
        .Where(code => DemandDatabase.GetAvatarPath(code) == null)
        .ToList();
    if (missingSetIcons.Count > 0 || missingAvatars.Count > 0)
        throw new InvalidOperationException(
            $"静态图片缺失：套装[{string.Join(",", missingSetIcons)}] 英雄[{string.Join(",", missingAvatars)}]");
    var invalidDocument = new DemandDataDocument
    {
        SchemaVersion = DemandDatabase.CurrentSchemaVersion,
        UpdatedAt = "test",
        Sets =
        {
            new DemandSet
            {
                Code = "invalid",
                Name = "无效套装",
                Profiles =
                {
                    new DemandProfile
                    {
                        Id = "bad",
                        Name = "错误权重",
                        Stats = { "速度" },
                        Weights = new Dictionary<string, double> { ["速度"] = 11 },
                    },
                },
            },
        },
    };
    if (DemandDatabase.Validate(invalidDocument).Count == 0)
        throw new InvalidOperationException("非法需求权重未被数据校验拒绝");
    var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "HeroData", "demand-profiles.json");
    var json = File.ReadAllText(dataPath);
    var forbidden = new[] { "gear_path", "supply_pieces", "inventory", "C:\\Users\\" };
    if (forbidden.Any(json.Contains))
        throw new InvalidOperationException("静态需求数据仍包含个人库存或供给字段");
    Console.WriteLine("需求数据校验通过：23 套装 / 21 有数据 / 171 子类 / 644 配装 / 100 英雄");
    return;
}

// 合成样例自检（无需截图）：验证套装子类权重、右三满值、固定主属性与强化规则。
if (args.Contains("--synthetic"))
{
    Dictionary<string, double> Weights(
        double speed = 0,
        double hp = 0,
        double crit = 0,
        double critDamage = 0,
        double attack = 0,
        double defense = 0,
        double effectHit = 0,
        double effectResistance = 0)
        => new()
        {
            ["攻击力"] = attack, ["生命值"] = hp, ["防御力"] = defense, ["速度"] = speed,
            ["暴击率"] = crit, ["暴击伤害"] = critDamage,
            ["效果命中"] = effectHit, ["效果抗性"] = effectResistance,
        };

    var weightedSet = new DemandSet
    {
        Code = "set_speed",
        Name = "速度套装",
        Profiles =
        {
            new DemandProfile
            {
                Id = "hp-spd",
                Name = "生命值·速度",
                Stats = { "生命值", "速度" },
                Weights = Weights(speed: 4, hp: 2),
                DemandWeight = 10,
                Heroes =
                {
                    new DemandHeroBuild
                    {
                        Code = "c5154", Name = "调香师维波里丝", ComboName = "速度+生命值",
                        SampleShare = 0.4, DemandContribution = 2, Weights = Weights(speed: 4, hp: 2),
                    },
                    new DemandHeroBuild
                    {
                        Code = "c5154", Name = "调香师维波里丝", ComboName = "速度+免疫",
                        SampleShare = 0.2, DemandContribution = 1, Weights = Weights(speed: 3.5, hp: 2.5),
                    },
                },
            },
        },
    };

    EquipmentInfo RightGear(string mainName, string mainValue, int level = 88) => new()
    {
        Level = level,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = mainName,
        MainStatValue = mainValue,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "防御力", Value = "8%" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    };

    var speedShoe = SetProfileMatcher.Match(RightGear("速度", "1"), weightedSet, int.MaxValue).Single();
    var hpShoe = SetProfileMatcher.Match(RightGear("生命值", "1%"), weightedSet, int.MaxValue).Single();
    Console.WriteLine($"  速度鞋匹配 {speedShoe.Score}% / 生命鞋匹配 {hpShoe.Score}%");
    if (speedShoe.Score <= hpShoe.Score)
        throw new InvalidOperationException("高速度权重子类未优先推荐速度鞋");
    if (speedShoe.Heroes.Count != 2 || speedShoe.Heroes.Select(hero => hero.ComboName).Distinct().Count() != 2)
        throw new InvalidOperationException("同英雄不同完整套装组合未分别返回");
    var disabledWeightedProfile = new HashSet<string>(StringComparer.Ordinal)
    {
        SetProfileMatcher.CreateProfileKey(weightedSet.Code, weightedSet.Profiles[0].Id),
    };
    if (SetProfileMatcher.Match(
            RightGear("速度", "1"), weightedSet, int.MaxValue, disabledWeightedProfile).Count != 0)
        throw new InvalidOperationException("已停用需求子类仍参与装备匹配");

    string MainContribution(string mainName, string mainValue, string stat, double weight)
    {
        var set = new DemandSet
        {
            Code = "test", Name = "测试套装",
            Profiles =
            {
                new DemandProfile
                {
                    Id = stat, Name = stat, Stats = { stat }, Weights = Weights(
                        speed: stat == "速度" ? weight : 0,
                        hp: stat == "生命值" ? weight : 0,
                        crit: stat == "暴击率" ? weight : 0,
                        critDamage: stat == "暴击伤害" ? weight : 0),
                },
            },
        };
        var part = stat is "暴击率" or "暴击伤害" ? "项链" : "鞋子";
        var info = new EquipmentInfo
        {
            Level = 88, Quality = "传说" + part, MainStatName = mainName, MainStatValue = mainValue,
            SubStats = { new SubStat { Name = "效果命中", Value = "8%" } },
        };
        return SetProfileMatcher.Match(info, set, 1).Single().MainStatContribution;
    }
    if (!MainContribution("暴击率", "1%", "暴击率", 1).Contains("价值 90")
        || !MainContribution("暴击伤害", "1%", "暴击伤害", 1).Contains("价值 78.75")
        || !MainContribution("生命值", "1%", "生命值", 1).Contains("价值 65"))
        throw new InvalidOperationException("右三满值换算错误");

    var score85 = SetProfileMatcher.Match(RightGear("速度", "1", 85), weightedSet, 1).Single().Score;
    var score88 = SetProfileMatcher.Match(RightGear("速度", "1", 88), weightedSet, 1).Single().Score;
    var score90 = SetProfileMatcher.Match(RightGear("速度", "1", 90), weightedSet, 1).Single().Score;
    if (score85 != score88 || score88 != score90)
        throw new InvalidOperationException("85→90预估与88/90满值结果不一致");
    if (SetProfileMatcher.Match(RightGear("生命值", "500", 88), weightedSet, 1).Count != 0)
        throw new InvalidOperationException("固定值右三仍匹配需求子类");
    if (SetProfileMatcher.Match(RightGear("速度", "1", 75), weightedSet, 1).Count != 0
        || SetProfileMatcher.Match(RightGear(string.Empty, string.Empty, 88), weightedSet, 1).Count != 0)
        throw new InvalidOperationException("不支持等级或未识别右三主属性仍返回完整匹配");
    if (SetProfileMatcher.Match(RightGear("速度", "1"), new DemandSet
        {
            Code = "empty", Name = "空套装",
        }, 1).Count != 0)
        throw new InvalidOperationException("无数据套装错误回退到旧算法");

    EquipmentInfo LeftGear(string mainValue) => new()
    {
        Level = 88, Quality = "传说武器", MainStatName = "攻击力", MainStatValue = mainValue,
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "效果命中", Value = "8%" },
            new SubStat { Name = "效果抗性", Value = "8%" },
        },
    };
    var leftA = SetProfileMatcher.Match(LeftGear("100"), weightedSet, 1).Single().Score;
    var leftB = SetProfileMatcher.Match(LeftGear("999"), weightedSet, 1).Single().Score;
    if (leftA != leftB)
        throw new InvalidOperationException("左三固定主属性改变了匹配度");

    var subScoreA = EquipmentScoreCalculator.Calculate(RightGear("速度", "1").SubStats);
    var subScoreB = EquipmentScoreCalculator.Calculate(RightGear("生命值", "1%").SubStats);
    if (subScoreA != subScoreB)
        throw new InvalidOperationException("主属性错误加入副属性装备分");

    EquipmentInfo WeightedWeapon(bool includeEnhanceText = true) => new()
    {
        Level = 88,
        EnhanceLevel = 15,
        Quality = "传说武器",
        SetName = "速度套装",
        MainStatName = "攻击力",
        MainStatValue = "515",
        SubStats =
        {
            new SubStat { Name = "生命值", Value = "8%" },
            new SubStat
            {
                Name = "速度", Value = "13", RollCount = 2,
                EnhanceValue = includeEnhanceText ? "+8" : null,
            },
            new SubStat { Name = "效果抗性", Value = "8%" },
            new SubStat
            {
                Name = "效果命中", Value = "31%", RollCount = 3,
                EnhanceValue = includeEnhanceText ? "+23%" : null,
            },
        },
    };
    var allocationSet = new DemandSet
    {
        Code = "set_speed",
        Name = "速度套装",
        Profiles =
        {
            new DemandProfile
            {
                Id = "hp-spd-hit-res",
                Name = "生命值·速度·效果命中·效果抗性",
                Stats = { "生命值", "速度", "效果命中", "效果抗性" },
                Weights = Weights(speed: 3.1, hp: 2.2, effectHit: 1.2, effectResistance: 2.3),
            },
            new DemandProfile
            {
                Id = "atk-hp-def-spd-hit",
                Name = "攻击力·生命值·防御力·速度·效果命中",
                Stats = { "攻击力", "生命值", "防御力", "速度", "效果命中" },
                Weights = Weights(
                    speed: 2.6, hp: 2, attack: 1.7, defense: 1.2, effectHit: 2.5),
            },
            new DemandProfile
            {
                Id = "spd-hit",
                Name = "速度·效果命中",
                Stats = { "速度", "效果命中" },
                Weights = Weights(speed: 4.1, effectHit: 1.8),
            },
            new DemandProfile
            {
                Id = "hp-spd",
                Name = "生命值·速度",
                Stats = { "生命值", "速度" },
                Weights = Weights(speed: 4, hp: 2),
            },
        },
    };
    var allocationResults = SetProfileMatcher.Match(
            WeightedWeapon(), allocationSet, int.MaxValue)
        .ToDictionary(result => result.ProfileId, StringComparer.Ordinal);
    var fourStatScore = allocationResults["hp-spd-hit-res"].Score;
    var fiveStatScore = allocationResults["atk-hp-def-spd-hit"].Score;
    var speedHitScore = allocationResults["spd-hit"].Score;
    var speedHpScore = allocationResults["hp-spd"].Score;
    Console.WriteLine(
        $"  新需求匹配：四项全中 {fourStatScore}% / 五项缺攻防 {fiveStatScore}% / "
        + $"速度命中 {speedHitScore}% / 速度生命 {speedHpScore}%");
    if (Math.Abs(fourStatScore - 90.1) > 0.1 || fourStatScore <= fiveStatScore)
        throw new InvalidOperationException("四项完全命中子类未按权重分布得到高匹配");
    if (Math.Abs(speedHitScore - 81.9) > 0.1
        || speedHpScore >= 60
        || speedHitScore - speedHpScore < 25)
        throw new InvalidOperationException("双属性子类未区分初始歪词条与歪强化");

    var estimatedResults = SetProfileMatcher.Match(
            WeightedWeapon(includeEnhanceText: false), allocationSet, int.MaxValue)
        .ToDictionary(result => result.ProfileId, StringComparer.Ordinal);
    if (Math.Abs(estimatedResults["hp-spd"].Score - speedHpScore) > 1)
        throw new InvalidOperationException("强化增量漏识别时的 RollCount 估算偏差过大");

    void AssertAdvice(
        string title,
        EquipmentInfo info,
        EnhanceAdvice expected,
        bool heroicOnly = false,
        bool speedSetRequiresSpeed = true,
        bool criticalNecklaceMainStatRule = true)
    {
        var result = EnhancementAdvisor.Analyze(
            info,
            24,
            24,
            28,
            heroicOnlyGambleSpeed: heroicOnly,
            speedSetRequiresSpeed: speedSetRequiresSpeed,
            criticalNecklaceMainStatRule: criticalNecklaceMainStatRule);
        Console.WriteLine($"  {title} → {result.Text}（{result.Detail}）");
        if (result.Advice != expected)
            throw new InvalidOperationException($"强化建议回归失败：{title}，期望 {expected}，实际 {result.Advice}");
    }
    AssertAdvice("传说武器 +3 第一跳歪但可赌速度", new EquipmentInfo
    {
        Level = 85, Quality = "传说武器", EnhanceLevel = 3,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GambleSpeed);
    AssertAdvice("传说武器 +6 连歪两跳", new EquipmentInfo
    {
        Level = 85, Quality = "传说武器", EnhanceLevel = 6,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUp);
    AssertAdvice("紫装只赌速度 +0 无速度", new EquipmentInfo
    {
        Level = 85, Quality = "英雄武器", EnhanceLevel = 0,
        SubStats = { new SubStat { Name = "攻击力", Value = "20%" } },
    }, EnhanceAdvice.GiveUp, heroicOnly: true);
    AssertAdvice("紫装只赌速度 +0 速度3", new EquipmentInfo
    {
        Level = 85, Quality = "英雄武器", EnhanceLevel = 0,
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GambleSpeed, heroicOnly: true);
    AssertAdvice("紫装只赌速度不包含鞋子", new EquipmentInfo
    {
        Level = 85, Quality = "英雄鞋子", EnhanceLevel = 0,
        MainStatName = "生命值", MainStatValue = "65%",
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUp, heroicOnly: true);
    AssertAdvice("88级 +15 高分保留", new EquipmentInfo
    {
        Level = 88, Quality = "传说武器", EnhanceLevel = 15,
        SubStats =
        {
            new SubStat { Name = "速度", Value = "15" },
            new SubStat { Name = "攻击力", Value = "20%" },
            new SubStat { Name = "暴击率", Value = "12%" },
            new SubStat { Name = "暴击伤害", Value = "20%" },
        },
    }, EnhanceAdvice.Keep);
    AssertAdvice("固定防御鞋", new EquipmentInfo
    {
        Level = 88, Quality = "传说鞋子", MainStatName = "防御力", MainStatValue = "500",
        SubStats = { new SubStat { Name = "速度", Value = "3" } },
    }, EnhanceAdvice.GiveUpFixedMain);

    EquipmentInfo SpeedSetHpBoots() => new()
    {
        Level = 85,
        Quality = "传说鞋子",
        SetName = "速度套装",
        MainStatName = "生命值",
        MainStatValue = "65%",
        SubStats =
        {
            new SubStat { Name = "速度", Value = "4" },
            new SubStat { Name = "防御力", Value = "20%" },
            new SubStat { Name = "效果命中", Value = "20%" },
            new SubStat { Name = "效果抗性", Value = "20%" },
        },
    };
    AssertAdvice(
        "速度套生命鞋特殊规则",
        SpeedSetHpBoots(),
        EnhanceAdvice.GiveUp);
    AssertAdvice(
        "关闭速度套特殊规则",
        SpeedSetHpBoots(),
        EnhanceAdvice.Continue,
        speedSetRequiresSpeed: false);

    EquipmentInfo AttackSetNecklace(string mainStat)
    {
        var info = new EquipmentInfo
        {
            Level = 85,
            Quality = "传说项链",
            SetName = "攻击套装",
            MainStatName = mainStat,
            MainStatValue = mainStat == "暴击伤害" ? "70%" : "65%",
            SubStats =
            {
                new SubStat { Name = "速度", Value = "5" },
                new SubStat { Name = "暴击率", Value = "12%" },
                new SubStat { Name = "效果命中", Value = "8%" },
            },
        };
        info.SubStats.Add(mainStat == "暴击伤害"
            ? new SubStat { Name = "攻击力", Value = "20%" }
            : new SubStat { Name = "暴击伤害", Value = "20%" });
        return info;
    }
    AssertAdvice(
        "双爆需求攻击项链特殊规则",
        AttackSetNecklace("攻击力"),
        EnhanceAdvice.GiveUp);
    AssertAdvice(
        "关闭暴击项链特殊规则",
        AttackSetNecklace("攻击力"),
        EnhanceAdvice.Continue,
        criticalNecklaceMainStatRule: false);
    AssertAdvice(
        "双爆需求暴伤项链",
        AttackSetNecklace("暴击伤害"),
        EnhanceAdvice.Continue);

    var classifyCriticalWeights = typeof(EnhancementAdvisor).GetMethod(
        "GetHighCriticalWeights",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("找不到暴击项链权重分类方法");
    string ClassifyCriticalWeights(IEnumerable<string> stats, double crit, double critDamage)
    {
        var result = classifyCriticalWeights.Invoke(null, new object[]
        {
            stats.ToList(),
            Weights(speed: 3, crit: crit, critDamage: critDamage),
        });
        return result?.ToString() ?? string.Empty;
    }
    if (ClassifyCriticalWeights(new[] { "速度", "暴击率" }, 1.5, 0) != "CriticalChance"
        || ClassifyCriticalWeights(new[] { "速度", "暴击伤害" }, 0, 1.5) != "CriticalDamage"
        || ClassifyCriticalWeights(new[] { "速度", "暴击率", "暴击伤害" }, 1.5, 1.5)
        != "CriticalChance, CriticalDamage")
    {
        throw new InvalidOperationException("单暴击、单暴伤和双爆需求未被分别识别");
    }

    var inferEnhanceLevel = typeof(OcrEngine).GetMethod(
        "InferEnhanceLevelByRolls",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("找不到强化等级推导方法");
    var inferred = (int?)inferEnhanceLevel.Invoke(null, new object[] { "英雄铠甲", 3, 2 });
    if (inferred != 6)
        throw new InvalidOperationException($"强化等级推导失败：期望6，实际{inferred}");

    var forgeLines = new[]
    {
        ("速度5", "速度", 5D, false),
        ("暴击率5%", "暴击率", 5D, true),
        ("暴击伤害7％", "暴击伤害", 7D, true),
        ("生命值200", "生命值", 200D, false),
        ("防御力30", "防御力", 30D, false),
        ("攻击力40", "攻击力", 40D, false),
        ("效果抗性8%", "效果抗性", 8D, true),
        ("生命值8%", "生命值%", 8D, true),
    };
    foreach (var (text, name, value, percent) in forgeLines)
    {
        if (!StarForgeRules.TryParseStatLine(text, out var stat)
            || stat.StatName != name || stat.Value != value || stat.IsPercent != percent)
            throw new InvalidOperationException($"星之铁匠铺属性解析失败：{text}");
    }
    var forgeStats = new[]
    {
        new StarForgeStat("速度", 5, false),
        new StarForgeStat("暴击率", 5, true),
        new StarForgeStat("生命值%", 8, true),
        new StarForgeStat("防御力", 30, false),
    };
    if (!StarForgeRules.Match(forgeStats,
        [new StarForgeTarget("速度", 5), new StarForgeTarget("生命值%", 8)]).IsMatch)
        throw new InvalidOperationException("星之铁匠铺没有在全部目标达标时停止");
    if (StarForgeRules.Match(forgeStats,
        [new StarForgeTarget("速度", 6), new StarForgeTarget("生命值%", 8)]).IsMatch)
        throw new InvalidOperationException("星之铁匠铺在目标未全部达标时错误停止");
    if (StarForgeRules.GetDefaultMinimum("速度") != 5
        || StarForgeRules.GetDefaultMinimum("暴击率") != 5
        || StarForgeRules.GetDefaultMinimum("暴击伤害") != 7
        || StarForgeRules.GetDefaultMinimum("效果命中") != 8
        || StarForgeRules.GetDefaultMinimum("生命值") != 200
        || StarForgeRules.GetDefaultMinimum("防御力") != 30
        || StarForgeRules.GetDefaultMinimum("攻击力") != 40)
        throw new InvalidOperationException("星之铁匠铺默认属性阈值错误");

    Console.WriteLine("套装子类匹配、主属性量化、强化建议与星之铁匠铺规则合成测试通过");
    return;
}

using var engine = new OcrEngine(templateDir);

foreach (var name in imageNames)
{
    var imagePath = Path.Combine(screenshotsDir, name);
    if (!File.Exists(imagePath))
    {
        Console.WriteLine($"截图不存在: {imagePath}");
        continue;
    }

    Console.WriteLine($"===== 测试图片: {name} =====");

    var info = await engine.RecognizeAsync(imagePath);

    Console.WriteLine("识别结果:");
    Console.WriteLine($"  装备等级: {info.Level}");
    Console.WriteLine($"  强化等级: +{info.EnhanceLevel}");
    Console.WriteLine($"  装备品质: {info.Quality}");
    Console.WriteLine($"  主属性: {info.MainStatName} {info.MainStatValue}");
    Console.WriteLine($"  副属性:");
    foreach (var sub in info.SubStats)
    {
        var rollText = sub.RollCount > 0 ? $"({sub.RollCount})" : string.Empty;
        Console.WriteLine($"    - {sub.Name}{rollText} {sub.Value}" + (string.IsNullOrEmpty(sub.EnhanceValue) ? "" : $" ({sub.EnhanceValue})"));
    }
    Console.WriteLine($"  套装: {info.SetName}");
    Console.WriteLine($"  装备分数: {info.Score}");

    // 强化建议（阈值 24/24）
    var advice = EnhancementAdvisor.Analyze(info, 24, 24);
    Console.WriteLine($"  强化建议: {advice.Text}（{advice.Detail}）");

    // 装备 → 当前套装属性子类推荐
    var recommendations = SetProfileMatcher.Match(info);
    Console.WriteLine("  适用子类:");
    foreach (var rec in recommendations)
        Console.WriteLine($"    - {rec.ProfileName} 匹配度 {rec.Score}%  命中=[{string.Join(",", rec.MatchedStats)}] {rec.MainStatContribution}");
    if (recommendations.Count == 0)
        Console.WriteLine("    （无匹配或静态需求数据缺失）");

    Console.WriteLine();
    Console.WriteLine("原始文本:");
    Console.WriteLine(info.RawText);
    Console.WriteLine();
}
