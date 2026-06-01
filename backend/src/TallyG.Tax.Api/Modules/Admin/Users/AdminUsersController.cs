using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Admin.Users;

/// <summary>
/// Back-office user board (docs 04 §"Admin/CRM", docs 07 §7.8). Admin/Ops act within their tenant;
/// SuperAdmin across all. Status uses the canonical ":status" action sub-resource convention
/// (Decision Log D-3); role grant/revoke is a POST to the /roles sub-collection.
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = AdminScope.Roles)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _users;

    public AdminUsersController(IAdminUserService users) => _users = users;

    /// <summary>List/search users (paged). <paramref name="search"/> matches name/email/mobile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminUserListItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResult<AdminUserListItemDto>> List(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AdminPaging.DefaultPageSize,
        CancellationToken ct = default)
        => _users.ListAsync(search, page, pageSize, ct);

    /// <summary>Get one user with activity counters.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
    public Task<AdminUserDetailDto> Get([FromRoute] Guid id, CancellationToken ct)
        => _users.GetAsync(id, ct);

    /// <summary>Set the account state (Active/Locked/Disabled).</summary>
    [HttpPatch("{id:guid}:status")]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
    public Task<AdminUserDetailDto> SetStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken ct)
        => _users.SetStatusAsync(id, request, ct);

    /// <summary>Assign or remove a role for the user.</summary>
    [HttpPost("{id:guid}/roles")]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
    public Task<AdminUserDetailDto> ModifyRole(
        [FromRoute] Guid id,
        [FromBody] ModifyUserRoleRequest request,
        CancellationToken ct)
        => _users.ModifyRoleAsync(id, request, ct);
}
