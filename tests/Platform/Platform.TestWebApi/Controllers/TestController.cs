using MediatR;

using Microsoft.AspNetCore.Mvc;

using Platform.TestWebApi.Commands;

using Sirius.Platform;

namespace Platform.TestWebApi.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly IMediator _mediator;

    public TestController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost]
    public async Task<ActionResult<Result<int>>> CreateTestEntity(
        [FromBody] CreateTestEntityRequest request,
        [FromHeader(Name = "x-requestid")] Guid requestId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
            return BadRequest("RequestId is required");

        var command = new CreateTestEntityCommand(request.Name, request.Value, requestId);

        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Result<bool>>> UpdateTestEntity(
        Guid id,
        [FromBody] UpdateTestEntityRequest request,
        [FromHeader(Name = "x-requestid")] Guid requestId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
            return BadRequest("RequestId is required");

        var command = new UpdateTestEntityCommand(id, request.NewValue, requestId);

        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result) : BadRequest(result.Error);
    }
}

public record CreateTestEntityRequest(string Name, int Value);
public record UpdateTestEntityRequest(int NewValue);
