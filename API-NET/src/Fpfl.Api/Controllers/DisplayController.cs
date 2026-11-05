using Fpfl.Api.Dtos.Display;
using Fpfl.Api.Mappers;
using Fpfl.Api.Responses;
using Fpfl.Auth;
using Fpfl.Display.Domain.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fpfl.Api.Controllers;

/// <summary>
///     The forecast ledger behind the Display page's chart and table. Same route and response shape as
///     API-JDK's DisplayController, plus: a signed-in user is required, and a user can only build their own
///     ledger (API-JDK trusted the userId in the body). Ranges are capped at
///     <see cref="ILedgerReadoutService.MaxYears" /> years.
/// </summary>
[ApiController]
[Route("display")]
[Authorize]
[Produces("application/json")]
[Tags("Display")]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
public sealed class DisplayController(
    ILedgerReadoutService ledger,
    DisplayMapper mapper,
    AuthMapper authMapper,
    IApiResponseFactory responses) : ControllerBase
{
    /// <summary>One row per day from ledgerStartDate to ledgerEndDate (inclusive), each with its credits, debits and running balance.</summary>
    [HttpPost("ledger")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LedgerDayResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LedgerDayResponse>>>> BuildLedger(
        LedgerRequest request, CancellationToken cancellationToken)
    {
        var caller = authMapper.GetUserGuid(User);

        var days = await ledger.BuildAsync(
            request.UserId ?? caller,
            request.LedgerStartDate!.Value,
            request.LedgerEndDate!.Value,
            caller,
            User.IsInRole(AuthClaims.AdminRole),
            cancellationToken);

        return Ok(responses.Success(mapper.ToResponse(days), "Ledger retrieved successfully"));
    }

    /// <summary>
    ///     Debt Paydown: each tracked debt's balance per day, its payments, allocations and interest, and the
    ///     projected payoff date, over the same window as the ledger.
    /// </summary>
    [HttpPost("paydown")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DebtPaydownResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DebtPaydownResponse>>>> BuildPaydown(
        LedgerRequest request, CancellationToken cancellationToken)
    {
        var caller = authMapper.GetUserGuid(User);

        var debts = await ledger.BuildPaydownAsync(
            request.UserId ?? caller,
            request.LedgerStartDate!.Value,
            request.LedgerEndDate!.Value,
            caller,
            User.IsInRole(AuthClaims.AdminRole),
            cancellationToken);

        return Ok(responses.Success(mapper.ToResponse(debts), "Paydown retrieved successfully"));
    }

    /// <summary>
    ///     Credit Cards / Loans: each account's balance per day, its purchases, payments and interest, over the
    ///     same window as the ledger. The payments also appear on the ledger as "Card/Loan Payment" debits.
    /// </summary>
    [HttpPost("accounts")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AccountLedgerResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountLedgerResponse>>>> BuildAccounts(
        LedgerRequest request, CancellationToken cancellationToken)
    {
        var caller = authMapper.GetUserGuid(User);

        var accounts = await ledger.BuildAccountsAsync(
            request.UserId ?? caller,
            request.LedgerStartDate!.Value,
            request.LedgerEndDate!.Value,
            caller,
            User.IsInRole(AuthClaims.AdminRole),
            cancellationToken);

        return Ok(responses.Success(mapper.ToResponse(accounts), "Accounts ledger retrieved successfully"));
    }
}
