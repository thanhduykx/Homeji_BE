using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.RentalPosts;

public interface IRentalPublicationStore
{
    Task SaveApprovalAsync(RentalPost post, CancellationToken cancellationToken);
}
