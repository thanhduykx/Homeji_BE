using System.Buffers.Binary;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.Chatbot;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotMediaValidationTests
{
    [Theory]
    [InlineData("image/svg+xml", "PHN2Zz4=")]
    [InlineData("image/png", "not-base64")]
    [InlineData("audio/wav", "UkVGRg==")]
    [InlineData("audio/mp3", "AA==")]
    [InlineData(null, "AA==")]
    [InlineData("image/jpeg", null)]
    public void RejectsUnsupportedMalformedOrFakeAudio(string? mime, string? data) =>
        Assert.Throws<RequestValidationException>(() => ChatbotMediaValidation.Decode([new(mime, data)]));

    [Fact]
    public void EnforcesPerImageCountAndCombinedByteBudgets()
    {
        var small = new ChatbotAttachmentDto("image/png", "AA==");
        Assert.Throws<RequestValidationException>(() => ChatbotMediaValidation.Decode([small, small, small, small]));
        var large = new ChatbotAttachmentDto("image/png", Convert.ToBase64String(new byte[ChatbotMediaValidation.MaxImageBytes + 1]));
        Assert.Throws<RequestValidationException>(() => ChatbotMediaValidation.Decode([large]));
        var full = new ChatbotAttachmentDto("image/png", Convert.ToBase64String(new byte[ChatbotMediaValidation.MaxImageBytes]));
        Assert.Throws<RequestValidationException>(() => ChatbotMediaValidation.Decode([full, full, small]));
    }

    [Fact]
    public void AcceptsCanonicalRecorderAudioButRejectsDuplicateAndSpoofedHeaders()
    {
        var audio = CreateAudio();
        var decoded = ChatbotMediaValidation.Decode([audio]);
        Assert.Equal("audio/wav", Assert.Single(decoded).MimeType);
        Assert.Throws<RequestValidationException>(() => ChatbotMediaValidation.Decode([audio, audio]));
        var bytes = decoded[0].Content;
        bytes[24] = 0;
        Assert.Throws<RequestValidationException>(() => ChatbotMediaValidation.Decode([audio with { Data = Convert.ToBase64String(bytes) }]));
    }

    internal static ChatbotAttachmentDto CreateAudio()
    {
        var bytes = new byte[44 + 16000 * 2];
        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)bytes.Length - 8);
        "WAVEfmt "u8.CopyTo(bytes.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 16000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 32000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), 16);
        "data"u8.CopyTo(bytes.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), (uint)bytes.Length - 44);
        return new("audio/wav", Convert.ToBase64String(bytes));
    }
}
