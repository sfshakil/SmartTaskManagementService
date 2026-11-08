using FluentValidation;
using SmartTaskManagement.Application.DTOs;

namespace SmartTaskManagement.Application.Validators;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.DueDateUtc)
            .Must(d => d!.Value.ToUniversalTime().Date >= DateTime.UtcNow.Date)
            .When(x => x.DueDateUtc.HasValue)
            .WithMessage("Due date cannot be in the past.");
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class AssignTaskRequestValidator : AbstractValidator<AssignTaskRequest>
{
    public AssignTaskRequestValidator()
    {
        RuleFor(x => x.AssignedToId).NotEqual(Guid.Empty).When(x => x.AssignedToId.HasValue);
    }
}

public class TaskQueryValidator : PagedQueryValidator<TaskQuery>
{
    public TaskQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
        RuleFor(x => x.DueToUtc)
            .GreaterThanOrEqualTo(x => x.DueFromUtc!.Value)
            .When(x => x.DueFromUtc.HasValue && x.DueToUtc.HasValue)
            .WithMessage("DueToUtc must be on or after DueFromUtc.");
    }
}

public class ImproveDescriptionRequestValidator : AbstractValidator<ImproveDescriptionRequest>
{
    public const int MaxDescriptionLength = 1000;

    public ImproveDescriptionRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(MaxDescriptionLength)
            .WithMessage($"Description must not exceed {MaxDescriptionLength} characters.");
    }
}
