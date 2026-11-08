using Microsoft.AspNetCore.Mvc;
using SmartTaskManagement.Application.Services;

namespace SmartTaskManagement.Api.Controllers;

public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        OkResponse(await _dashboard.GetAsync(ct));
}
