using Homeji.Application.IRepositories.Conversations;
using Homeji.Application.IRepositories.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Homeji.Api.Realtime;

[Authorize]
public sealed class VoiceCallHub(VoiceCallRegistry calls, IPostConversationRepository conversations,
    IUserProfileRepository profiles, VoiceIceService iceService) : Hub
{
    private Guid UserId => Guid.TryParse(Context.UserIdentifier, out var id) ? id : throw new HubException("Bạn cần đăng nhập.");

    public async Task<Guid> StartCall(Guid conversationId)
    {
        if (!iceService.IsConfigured) throw new HubException("Gọi thoại đang chờ kích hoạt dịch vụ.");
        var conversation = await conversations.GetByIdAsync(conversationId, Context.ConnectionAborted);
        if (conversation is null || !conversation.Includes(UserId)) throw new HubException("Không có quyền gọi trong cuộc trò chuyện này.");
        var recipient = conversation.GetOtherParticipantId(UserId);
        var profile = await profiles.GetByIdAsync(UserId, Context.ConnectionAborted);
        var call = calls.Create(conversationId, UserId, recipient, Context.ConnectionId)
            ?? throw new HubException("Bạn hoặc người nhận đang có cuộc gọi khác.");
        await Clients.User(recipient.ToString()).SendAsync("incomingCall", new {
            callId = call.Id, conversationId, callerName = profile?.DisplayName ?? "Người dùng", callerId = UserId
        });
        return call.Id;
    }

    public async Task AcceptCall(Guid callId)
    {
        var call = calls.Accept(callId, UserId, Context.ConnectionId)
            ?? throw new HubException("Cuộc gọi đã kết thúc hoặc được trả lời ở thiết bị khác.");
        await Clients.Client(call.CallerConnectionId).SendAsync("callAccepted", callId);
        await Clients.User(UserId.ToString()).SendAsync("callClaimed", new { callId, connectionId = Context.ConnectionId });
    }

    public async Task Signal(Guid callId, string kind, string payload)
    {
        var call = calls.Get(callId, UserId) ?? throw new HubException("Cuộc gọi đã hết hạn.");
        var isCaller = call.Caller == UserId;
        if ((isCaller ? call.CallerConnectionId : call.RecipientConnectionId) != Context.ConnectionId ||
            call.RecipientConnectionId is null || payload.Length > 16000 || !call.AllowSignal() ||
            !(kind == "candidate" || (kind == "offer" && isCaller) || (kind == "answer" && !isCaller)))
            throw new HubException("Tín hiệu cuộc gọi không hợp lệ.");
        await Clients.Client(isCaller ? call.RecipientConnectionId : call.CallerConnectionId)
            .SendAsync("callSignal", new { callId, kind, payload });
    }

    public async Task EndCall(Guid callId)
    {
        var call = calls.Remove(callId, UserId);
        if (call is not null) await NotifyEnded(call);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var call in calls.Disconnect(Context.ConnectionId)) await NotifyEnded(call);
        await base.OnDisconnectedAsync(exception);
    }
    private Task NotifyEnded(VoiceCallSession call) => Clients.Users(call.Caller.ToString(), call.Recipient.ToString()).SendAsync("callEnded", call.Id);
}
