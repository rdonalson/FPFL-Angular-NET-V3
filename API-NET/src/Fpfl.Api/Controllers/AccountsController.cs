using Fpfl.Api.Dtos.Accounts;
using Fpfl.Api.Mappers;
using Fpfl.Api.Responses;
using Fpfl.Auth;
using Fpfl.Items.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fpfl.Api.Controllers;

/// <summary>
///     Credit Cards / Loans: standalone accounts with their own purchases, paid by the user's credits.
///     Not part of API-JDK. Signed-in users only; each user sees and changes only their own accounts.
/// </summary>
[ApiController]
[Route("accounts")]
[Authorize]
[Produces("application/json")]
[Tags("Credit Cards / Loans")]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
public sealed class AccountsController(
    ICreditAccountService service,
    AccountMapper mapper,
    AuthMapper authMapper,
    IApiResponseFactory responses) : ControllerBase
{
    /// <summary>Lists the caller's cards and loans with their purchases and payments.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AccountResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountResponse>>>> List(CancellationToken cancellationToken)
    {
        var accounts = await service.ListAsync(authMapper.GetUserGuid(User), cancellationToken);
        IReadOnlyList<AccountResponse> body = accounts.Select(mapper.ToResponse).ToList();

        return Ok(responses.Success(body, "Accounts fetched successfully"));
    }

    /// <summary>Gets one of the caller's cards or loans.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType<ApiResponse<AccountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountResponse>>> Get(long id, CancellationToken cancellationToken)
    {
        var account = await service.GetAsync(id, authMapper.GetUserGuid(User), IsAdmin, cancellationToken);

        return Ok(responses.Success(mapper.ToResponse(account), $"Account {id} retrieved successfully"));
    }

    /// <summary>Creates a card or loan, with any paying credits.</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse<AccountResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AccountResponse>>> Create(AccountRequest request, CancellationToken cancellationToken)
    {
        var account = await service.CreateAsync(mapper.ToDraft(request), authMapper.GetUserGuid(User), cancellationToken);
        var location = $"/accounts/{account.Id}";

        return Created(location, responses.Created(mapper.ToResponse(account), "Account created successfully", location));
    }

    /// <summary>Changes a card or loan's details and its paying credits.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType<ApiResponse<AccountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AccountResponse>>> Update(
        long id, AccountRequest request, CancellationToken cancellationToken)
    {
        var account = await service.UpdateAsync(id, mapper.ToDraft(request), authMapper.GetUserGuid(User), IsAdmin, cancellationToken);

        return Ok(responses.Success(mapper.ToResponse(account), "Account updated successfully"));
    }

    /// <summary>Deletes a card or loan with its purchases and payments (the credits themselves stay).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, authMapper.GetUserGuid(User), IsAdmin, cancellationToken);

        return Ok(responses.Success("Account deleted successfully"));
    }

    /// <summary>Lists an account's purchases.</summary>
    [HttpGet("{id:long}/purchases")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PurchaseResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PurchaseResponse>>>> ListPurchases(long id, CancellationToken cancellationToken)
    {
        var account = await service.GetAsync(id, authMapper.GetUserGuid(User), IsAdmin, cancellationToken);
        IReadOnlyList<PurchaseResponse> body = mapper.ToResponse(account).Purchases;

        return Ok(responses.Success(body, "Purchases fetched successfully"));
    }

    /// <summary>Adds a purchase (One Time, Daily or Weekly) to an account.</summary>
    [HttpPost("{id:long}/purchases")]
    [ProducesResponseType<ApiResponse<PurchaseResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PurchaseResponse>>> AddPurchase(
        long id, PurchaseRequest request, CancellationToken cancellationToken)
    {
        var purchase = await service.AddPurchaseAsync(id, mapper.ToDraft(request), authMapper.GetUserGuid(User), IsAdmin, cancellationToken);
        var location = $"/accounts/{id}/purchases/{purchase.Id}";

        return Created(location, responses.Created(mapper.ToResponse(purchase), "Purchase created successfully", location));
    }

    /// <summary>Changes one of an account's purchases.</summary>
    [HttpPut("{id:long}/purchases/{purchaseId:long}")]
    [ProducesResponseType<ApiResponse<PurchaseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PurchaseResponse>>> UpdatePurchase(
        long id, long purchaseId, PurchaseRequest request, CancellationToken cancellationToken)
    {
        var purchase = await service.UpdatePurchaseAsync(id, purchaseId, mapper.ToDraft(request), authMapper.GetUserGuid(User), IsAdmin, cancellationToken);

        return Ok(responses.Success(mapper.ToResponse(purchase), "Purchase updated successfully"));
    }

    /// <summary>Deletes one of an account's purchases.</summary>
    [HttpDelete("{id:long}/purchases/{purchaseId:long}")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object?>>> DeletePurchase(long id, long purchaseId, CancellationToken cancellationToken)
    {
        await service.DeletePurchaseAsync(id, purchaseId, authMapper.GetUserGuid(User), IsAdmin, cancellationToken);

        return Ok(responses.Success("Purchase deleted successfully"));
    }

    private bool IsAdmin => User.IsInRole(AuthClaims.AdminRole);
}
