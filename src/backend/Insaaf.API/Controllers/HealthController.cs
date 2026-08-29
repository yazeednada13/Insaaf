using Insaaf.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Insaaf.API.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthStatusDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<HealthStatusDto>> Get()
    {
        var meta = new ApiMeta { RequestId = HttpContext.TraceIdentifier };
        return Ok(ApiResponse<HealthStatusDto>.Ok(new HealthStatusDto("healthy"), meta));
    }
}

public sealed record HealthStatusDto(string Status);
