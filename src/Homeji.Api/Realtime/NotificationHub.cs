using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Homeji.Api.Realtime;

[Authorize]
public sealed class NotificationHub : Hub;
