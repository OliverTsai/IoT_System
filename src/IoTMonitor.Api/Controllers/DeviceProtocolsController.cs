using IoTMonitor.Api.Messaging.Protocols;
using Microsoft.AspNetCore.Mvc;

namespace IoTMonitor.Api.Controllers;

[ApiController]
[Route("api/device-protocols")]
public sealed class DeviceProtocolsController(DeviceProtocolRegistry registry) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeviceProtocolDescriptor>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DeviceProtocolDescriptor>> GetAll()
    {
        return Ok(registry.Descriptors);
    }
}
