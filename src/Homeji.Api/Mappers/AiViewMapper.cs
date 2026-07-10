using Homeji.Api.Views.AI;
using Homeji.Application.DTOs.AI;

namespace Homeji.Api.Mappers;

public static class AiViewMapper
{
    public static AiParseSearchRequestDto ToDto(AiParseSearchViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return new AiParseSearchRequestDto(viewModel.Text);
    }

    public static AiHighlightRequestDto ToDto(AiHighlightRentalPostsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return new AiHighlightRequestDto(viewModel.Text, viewModel.MaxResults);
    }
}
