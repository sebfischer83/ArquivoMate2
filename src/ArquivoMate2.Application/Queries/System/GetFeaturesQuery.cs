using ArquivoMate2.Shared.Models;
using MediatR;

namespace ArquivoMate2.Application.Queries.Features
{
    public record GetFeaturesQuery() : IRequest<FeaturesDto>;
}
