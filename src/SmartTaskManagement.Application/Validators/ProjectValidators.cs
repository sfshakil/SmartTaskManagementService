using FluentValidation;
using SmartTaskManagement.Application.Common;
using SmartTaskManagement.Application.DTOs;

namespace SmartTaskManagement.Application.Validators;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public abstract class PagedQueryValidator<T> : AbstractValidator<T> where T : PagedQuery
{
    protected PagedQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagedQuery.MaxPageSize);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.SortBy).MaximumLength(50);
    }
}

public class ProjectQueryValidator : PagedQueryValidator<ProjectQuery> { }

public class UserQueryValidator : PagedQueryValidator<UserQuery> { }
