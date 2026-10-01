using IoTMonitor.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace IoTMonitor.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SystemInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> Get()
    {
        return Ok(new SystemInfoResponse("IoTMonitor.Api", "v1"));
    }
}
