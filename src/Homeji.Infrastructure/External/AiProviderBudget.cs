using Homeji.Application.Common.Exceptions;

namespace Homeji.Infrastructure.External;

public sealed class AiProviderBudget : IDisposable
{
    private readonly SemaphoreSlim _slots = new(2, 2);
    public bool TryEnter() => _slots.Wait(0);
    public void Release() => _slots.Release();
    public void Dispose() => _slots.Dispose();
}

public sealed class AiProviderBudgetHandler(AiProviderBudget budget) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!budget.TryEnter()) throw new ExternalServiceUnavailableException("Gemini",
            "AI đang bận. Vui lòng thử lại sau vài giây.", TimeSpan.FromSeconds(5));
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            try
            {
                await response.Content.LoadIntoBufferAsync(256 * 1024, cancellationToken);
                return response;
            }
            catch { response.Dispose(); throw; }
        }
        finally { budget.Release(); }
    }
}
