using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.Specifications;
using System.Linq.Expressions;

namespace Enterprise.Documentation.Core.Application.Specifications;

/// <summary>
/// Specification for documents by approval status.
/// </summary>
public class DocumentsByApprovalStatusSpecification : Specification<Document>
{
    private readonly string _approvalStatus;

    public DocumentsByApprovalStatusSpecification(string approvalStatus)
    {
        _approvalStatus = approvalStatus ?? throw new ArgumentNullException(nameof(approvalStatus));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return document => document.ApprovalStatus != null && 
                          document.ApprovalStatus.Status == _approvalStatus;
    }

    public Expression<Func<Document, object>>? OrderBy { get; }

    public Expression<Func<Document, object>>? OrderByDescending => 
        document => document.CreatedAt;

    public Expression<Func<Document, object>>? GroupBy { get; }

    public int Take { get; }

    public int Skip { get; }

    public bool IsPagingEnabled { get; }
}