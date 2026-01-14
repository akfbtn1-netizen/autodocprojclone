
using System.Linq.Expressions;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Domain.Specifications;

/// <summary>
/// Specification for active users.
/// </summary>
public class ActiveUsersSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.IsActive && !user.IsDeleted;
    }
}

/// <summary>
/// Specification for users with specific roles.
/// </summary>
public class UsersWithRoleSpecification : Specification<User>
{
    private readonly UserRole _role;

    public UsersWithRoleSpecification(UserRole role)
    {
        _role = role;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.Roles.Contains(_role);
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.HasRole(_role);
    }
}

/// <summary>
/// Specification for users with sufficient security clearance.
/// </summary>
public class UsersWithSecurityClearanceSpecification : Specification<User>
{
    private readonly SecurityClearanceLevel _minimumClearance;

    public UsersWithSecurityClearanceSpecification(SecurityClearanceLevel minimumClearance)
    {
        _minimumClearance = minimumClearance;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.SecurityClearance >= _minimumClearance;
    }
}

/// <summary>
/// Specification for users who can approve documents.
/// </summary>
public class DocumentApproversSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.IsActive && 
                      (user.Roles.Contains(UserRole.Approver) || 
                       user.Roles.Contains(UserRole.Manager) || 
                       user.Roles.Contains(UserRole.Administrator));
    }

    public override bool IsSatisfiedBy(User entity)
    {
        return entity.IsActive && 
               entity.HasAnyRole(UserRole.Approver, UserRole.Manager, UserRole.Administrator);
    }
}

/// <summary>
/// Specification for users in a specific department.
/// </summary>
public class UsersByDepartmentSpecification : Specification<User>
{
    private readonly string _department;

    public UsersByDepartmentSpecification(string department)
    {
        _department = department ?? throw new ArgumentNullException(nameof(department));
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.Department == _department;
    }
}

/// <summary>
/// Specification for users who have accessed the system recently.
/// </summary>
public class RecentlyActiveUsersSpecification : Specification<User>
{
    private readonly DateTime _cutoffDate;

    public RecentlyActiveUsersSpecification(TimeSpan lookbackPeriod)
    {
        _cutoffDate = DateTime.UtcNow - lookbackPeriod;
    }

    public RecentlyActiveUsersSpecification(DateTime cutoffDate)
    {
        _cutoffDate = cutoffDate;
    }

    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.LastAccessAt.HasValue && user.LastAccessAt.Value >= _cutoffDate;
    }
}