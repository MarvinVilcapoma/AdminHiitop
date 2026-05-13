using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.DTOs.Roles;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/roles")]
public sealed class RolesController : BaseApiController
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        IReadOnlyList<RoleListItemResponse> response = await _roleService.GetRolesAsync();
        return Ok(response);
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions()
    {
        IReadOnlyList<PermissionResponse> response = await _roleService.GetPermissionsAsync();
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        RoleDetailResponse response = await _roleService.GetByIdAsync(id);
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RoleUpsertRequest request)
    {
        RoleDetailResponse response = await _roleService.CreateAsync(request);
        return Ok(response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] RoleUpsertRequest request)
    {
        RoleDetailResponse response = await _roleService.UpdateAsync(id, request);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _roleService.DeleteAsync(id);
        return Ok(new SuccessResponse());
    }
}
