namespace Homeji.Application.DTOs.Payments;

public sealed record PayPalSandboxCapture(string Status, string? CaptureId);
