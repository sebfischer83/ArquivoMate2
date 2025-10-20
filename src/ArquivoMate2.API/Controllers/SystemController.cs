using ArquivoMate2.Application.Queries.Features;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Shared.ApiModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/system")] 
    public class SystemController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SystemController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("features")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFeatures()
        {
            var result = await _mediator.Send(new GetFeaturesQuery());
            var response = new ApiResponse<FeaturesDto>(result, true);
            return Ok(response);
        }
    }
}
