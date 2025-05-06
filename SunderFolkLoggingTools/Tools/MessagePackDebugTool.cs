using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MessagePack;

namespace SunderFolkLoggingTools.Tools;

[MessagePackObject]
public class BufferPair
{
    [Key(0)] public byte[] Data { get; set; }

    [Key(1)] public int Port { get; set; }
}

[MessagePackObject]
public class JoinData
{
    [Key(0)] public List<List<BufferPair>> ConnectionGroups { get; set; }

    [Key(1)] public List<object> Unknown1 { get; set; }

    [Key(2)] public object Unknown2 { get; set; }

    [Key(3)] public int SessionToken { get; set; }

    [Key(4)] public object Unknown3 { get; set; }

    [Key(5)] public int Flags { get; set; }
}

public static class MessagePackDebugTool
{
    public static JoinData DecodeFromBase64(string base64)
    {
        var b64 = base64.Replace('-', '+').Replace('_', '/');
        var padding = 4 - b64.Length % 4;
        if (padding < 4) b64 += new string('=', padding);

        var bytes = Convert.FromBase64String(b64);
        return MessagePackSerializer.Deserialize<JoinData>(bytes);
    }

    public static string EncodeToBase64(JoinData data)
    {
        var bytes = MessagePackSerializer.Serialize(data);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static void DumpToJson(JoinData data, string path)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }

    public static bool IsPrivateIP(byte[] ipBytes)
    {
        if (ipBytes.Length != 4) return false;

        return
            ipBytes[0] == 10 ||
            (ipBytes[0] == 192 && ipBytes[1] == 168) ||
            (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31);
    }
}