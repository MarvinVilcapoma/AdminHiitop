using AdminHiitop.Api.Application.DTOs.Common;
using AdminHiitop.Api.Application.DTOs.Users;
using AdminHiitop.Api.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdminHiitop.Api.Controllers;

[Route("api/users")]
public sealed class UsersController : BaseApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? search)
    {
        IReadOnlyList<UserResponse> response = await _userService.GetUsersAsync(search);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        UserResponse response = await _userService.GetByIdAsync(id);
        return Ok(response);
    }

    [HttpGet("roles-list")]
    public async Task<IActionResult> RolesList()
    {
        IReadOnlyList<UserRoleLookupResponse> response = await _userService.GetRolesLookupAsync();
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserUpsertRequest request)
    {
        UserResponse response = await _userService.CreateAsync(request);
        return Ok(response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpsertRequest request)
    {
        UserResponse response = await _userService.UpdateAsync(id, request);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _userService.DeleteAsync(id);
        return Ok(new SuccessResponse());
    }
}
