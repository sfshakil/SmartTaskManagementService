using SmartTaskManagement.Application.Abstractions;

namespace SmartTaskManagement.Application.Common;

public static class CurrentUserExtensions
{
    public static AccessScope ToScope(this ICurrentUser user)
    {
        if (!user.IsAuthenticated)
        {
            throw new AuthenticationFailedException("Authentication is required.");
        }

        return new AccessScope(user.UserId, user.Role);
    }
}
