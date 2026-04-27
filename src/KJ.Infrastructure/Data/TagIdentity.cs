using System.Security.Cryptography;
using System.Text;

namespace KJ.Infrastructure.Data;

public static class TagIdentity
{
    public static readonly Guid SimulatedDeviceId =
        Guid.Parse("8b2a1ce1-39cf-4e2a-8c4a-2a38f7b1f2d0");

    private static readonly Guid TagNamespace =
        Guid.Parse("a3c9d4d9-3d6a-4b44-9a51-1d0aa2a1c0a5");

    public static Guid GetTagId(string tagKey) => CreateDeterministicGuid(TagNamespace, tagKey);

    // UUID v5-like deterministic GUID (namespace + name, SHA1 -> 16 bytes)
    private static Guid CreateDeterministicGuid(Guid ns, string name)
    {
        var nsBytes = ns.ToByteArray();
        SwapByteOrder(nsBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var data = new byte[nsBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(nsBytes, 0, data, 0, nsBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, data, nsBytes.Length, nameBytes.Length);

        var hash = SHA1.HashData(data);
        var newGuid = new byte[16];
        Array.Copy(hash, 0, newGuid, 0, 16);

        newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
        newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

        SwapByteOrder(newGuid);
        return new Guid(newGuid);
    }

    private static void SwapByteOrder(byte[] guid)
    {
        static void Swap(byte[] b, int a, int c) => (b[a], b[c]) = (b[c], b[a]);
        Swap(guid, 0, 3);
        Swap(guid, 1, 2);
        Swap(guid, 4, 5);
        Swap(guid, 6, 7);
    }
}

