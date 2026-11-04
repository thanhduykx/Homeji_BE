using Homeji.Application.DTOs.RentalPosts;

namespace Homeji.Application.IServices.RentalPosts;

public interface IRentalSourceListingService
{
    Task<IReadOnlyList<RentalSourceListingDto>> SearchAsync(RentalSourceSearchDto query, CancellationToken cancellationToken);
    Task<RentalSourceListingDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<RentalSourceContactDto> GetContactAsync(Guid id, CancellationToken cancellationToken);
    Task<Homeji.Application.DTOs.Chatbot.ChatbotSourceMatchDto> MatchAsync(Homeji.Application.DTOs.AI.AiParsedSearchCriteriaDto criteria, int limit, CancellationToken cancellationToken);
}
