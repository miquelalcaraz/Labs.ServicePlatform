using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Qlt.Infrastructure.Identity.Commands;

namespace Qlt.Infrastructure.Identity.Pages.Account.ConfirmEmail;

[SecurityHeaders]
[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly IMediator _mediator;

    public ConfirmEmailStatus Status { get; private set; } = ConfirmEmailStatus.Error;
    public string? UserEmail { get; private set; }

    public IndexModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<IActionResult> OnGetAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            Status = ConfirmEmailStatus.Error;
            return Page();
        }

        var result = await _mediator.Send(new ConfirmEmailCommand(userId));

        Status = result.Value.Status;
        UserEmail = result.Value.UserEmail;

        return Page();
    }
}
