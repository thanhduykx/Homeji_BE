using System.Buffers.Binary;
using Homeji.Application.Common.Exceptions;

namespace Homeji.Application.Services.Conversations;

/// <summary>Accepts only canonical 16 kHz, mono, signed 16-bit PCM WAV from the recorder.</summary>
public static class ConversationVoiceFormat
{
    public const int MaximumBytes = 44 + 16000 * 2 * 120;

    public static void Validate(byte[] content)
    {
        if (content is null || content.Length < 44 + 9600 || content.Length > MaximumBytes)
        {
            Reject();
        }

        var data = content.AsSpan();
        if (!data[..4].SequenceEqual("RIFF"u8) ||
            !data.Slice(8, 8).SequenceEqual("WAVEfmt "u8) ||
            !data.Slice(36, 4).SequenceEqual("data"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) != content.Length - 8 ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[16..]) != 16 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data[20..]) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data[22..]) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[24..]) != 16000 ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[28..]) != 32000 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data[32..]) != 2 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data[34..]) != 16 ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[40..]) != content.Length - 44 ||
            (content.Length - 44) % 2 != 0)
        {
            Reject();
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Reject() => throw new RequestValidationException(new Dictionary<string, string[]>
    {
        ["file"] = ["Bản ghi không hợp lệ. Hãy ghi âm trực tiếp từ 0,3 giây đến 2 phút trên Homeji."],
    });
}
