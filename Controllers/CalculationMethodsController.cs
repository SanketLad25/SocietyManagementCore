using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/calculation-methods")]
[Authorize]
public class CalculationMethodsController(CalculationMethodService calculationMethodService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CalculationMethodResponse>>> List()
    {
        return Ok(await calculationMethodService.ListAsync());
    }
}
