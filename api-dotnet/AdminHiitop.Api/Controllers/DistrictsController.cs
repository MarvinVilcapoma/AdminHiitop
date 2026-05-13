using AdminHiitop.Api.Application.Interfaces.Services;
using AdminHiitop.Api.Domain.Catalog.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/districts")]
public sealed class DistrictsController : BaseApiController
{
    private readonly IDistrictService _districtService;

    public DistrictsController(IDistrictService districtService)
    {
        _districtService = districtService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "per_page")] int perPage = 15, [FromQuery] int page = 1, [FromQuery(Name = "province_id")] int? provinceId = null)
    {
        return Ok(await _districtService.GetAsync(perPage, page, provinceId));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) => await _districtService.GetByIdAsync(id) is { } entity ? Ok(entity) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] District request) => Ok(await _districtService.CreateAsync(request));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] District request) => Ok(await _districtService.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _districtService.DeleteAsync(id);
        return Ok(new { success = true });
    }
}
