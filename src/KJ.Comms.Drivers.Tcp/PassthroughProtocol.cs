using KJ.Comms.Abstractions;
using System.Buffers;

namespace KJ.Comms.Drivers.Tcp;

public sealed class PassthroughProtocol : IProtocol
{
    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> appPayload) => appPayload;

    public bool TryDecode(ref ReadOnlySequence<byte> buffer, out ReadOnlyMemory<byte> appPayload)
    {
        if (buffer.Length <= 0)
        {
            appPayload = default;
            return false;
        }

        if (buffer.IsSingleSegment)
        {
            appPayload = buffer.First;
        }
        else
        {
            appPayload = buffer.ToArray();
        }

        buffer = buffer.Slice(buffer.End);
        return true;
    }
}

