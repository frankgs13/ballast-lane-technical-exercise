using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Infrastructure;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Tasks;

namespace TaskManager.Api.Controllers;

/// <summary>CRUD for the authenticated user's tasks. Thin by design: rules live in the Application/Domain layers.</summary>
[ApiController]
[Route("api/tasks")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class TasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>Lists the caller's tasks ordered by due date, optionally filtered by status.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TaskResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> List(
        [FromQuery] TaskItemStatus? status, CancellationToken cancellationToken) =>
        Ok(await taskService.ListAsync(User.GetRequiredUserId(), status, cancellationToken));

    /// <summary>Gets a single task owned by the caller.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await taskService.GetAsync(User.GetRequiredUserId(), id, cancellationToken));

    /// <summary>Creates a task for the caller. New tasks always start as Pending.</summary>
    [HttpPost]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskResponse>> Create(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var created = await taskService.CreateAsync(User.GetRequiredUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Replaces the editable fields of a task (full update).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> Update(
        Guid id, UpdateTaskRequest request, CancellationToken cancellationToken) =>
        Ok(await taskService.UpdateAsync(User.GetRequiredUserId(), id, request, cancellationToken));

    /// <summary>Deletes a task owned by the caller.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await taskService.DeleteAsync(User.GetRequiredUserId(), id, cancellationToken);
        return NoContent();
    }
}
