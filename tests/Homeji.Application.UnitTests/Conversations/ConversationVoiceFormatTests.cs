using System.Buffers.Binary;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.Services.Conversations;
using Homeji.Domain.Entities;

namespace Homeji.Application.UnitTests.Conversations;

public sealed class ConversationVoiceFormatTests
{
    internal static byte[] Wav(int samples = 16000)
    {
        var data = new byte[44 + samples * 2];
        "RIFF"u8.CopyTo(data); "WAVEfmt "u8.CopyTo(data.AsSpan(8)); "data"u8.CopyTo(data.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), data.Length - 8);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(24), 16000);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(28), 32000);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(34), 16);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40), samples * 2);
        return data;
    }

    [Fact]
    public void ValidVoice_HasPrivateAttachmentAndCanBeDeletedBySender()
    {
        var content = Wav(); ConversationVoiceFormat.Validate(content);
        var sender = Guid.NewGuid();
        var message = new PostMessage(Guid.NewGuid(), sender, "Tin nhắn thoại", DateTimeOffset.UtcNow);
        var voice = message.AddVoice(sender, content, new string('a', 64), DateTimeOffset.UtcNow);
        Assert.Equal("audio/wav", voice.MimeType); Assert.Equal(0, voice.Width); Assert.Equal(0, voice.Height);
        Assert.Throws<Homeji.Domain.Exceptions.DomainException>(() => voice.Delete(Guid.NewGuid(), DateTimeOffset.UtcNow));
        voice.Delete(sender, DateTimeOffset.UtcNow); Assert.Empty(voice.Content);
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(8)] [InlineData(12)] [InlineData(16)]
    [InlineData(20)] [InlineData(22)] [InlineData(24)] [InlineData(28)]
    [InlineData(32)] [InlineData(34)] [InlineData(36)] [InlineData(40)]
    public void RejectsSpoofedOrMalformedHeaders(int offset)
    {
        var data = Wav(); data[offset] ^= 0xff;
        Assert.Throws<RequestValidationException>(() => ConversationVoiceFormat.Validate(data));
    }

    [Fact]
    public void RejectsTruncationExtraPayloadAndOutOfRangeDuration()
    {
        Assert.Throws<RequestValidationException>(() => ConversationVoiceFormat.Validate([]));
        Assert.Throws<RequestValidationException>(() => ConversationVoiceFormat.Validate(Wav(4799)));
        Assert.Throws<RequestValidationException>(() => ConversationVoiceFormat.Validate(Wav(16000 * 120 + 1)));
        Assert.Throws<RequestValidationException>(() => ConversationVoiceFormat.Validate([.. Wav(), 0, 0]));
        ConversationVoiceFormat.Validate(Wav(4800));
        ConversationVoiceFormat.Validate(Wav(16000 * 120));
    }
}
