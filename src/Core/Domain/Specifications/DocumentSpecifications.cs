
using System.Linq.Expressions;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Specifications;

/// <summary>
/// Specification for documents that require approval.
/// </summary>
public class DocumentsRequiringApprovalSpecification : Specification<Document>
{
    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.ApprovalStatus.Status == "Pending";
    }
}

/// <summary>
/// Specification for documents accessible by a specific user based on security clearance.
/// </summary>
public class DocumentAccessibleByUserSpecification : Specification<Document>
{
    private readonly User _user;

    public DocumentAccessibleByUserSpecification(User user)
    {
        _user = user ?? throw new ArgumentNullException(nameof(user));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        var userClearanceLevel = GetClearanceLevel(_user.SecurityClearance);
        
        return doc => GetDocumentClearanceLevel(doc.SecurityClassification.Level) <= userClearanceLevel;
    }

    public override bool IsSatisfiedBy(Document entity)
    {
        return _user.CanAccessSecurityLevel(entity.SecurityClassification);
    }

    private static int GetClearanceLevel(SecurityClearanceLevel level)
    {
        return level switch
        {
            SecurityClearanceLevel.Public => 0,
            SecurityClearanceLevel.Internal => 1,
            SecurityClearanceLevel.Confidential => 2,
            SecurityClearanceLevel.Restricted => 3,
            _ => 0
        };
    }

    private static int GetDocumentClearanceLevel(string level)
    {
        return level switch
        {
            "Public" => 0,
            "Internal" => 1,
            "Confidential" => 2,
            "Restricted" => 3,
            _ => 0
        };
    }
}

/// <summary>
/// Specification for published documents.
/// </summary>
public class PublishedDocumentsSpecification : Specification<Document>
{
    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => !doc.IsDeleted && doc.ApprovalStatus.Status == "Approved";
    }
}

/// <summary>
/// Specification for documents in a specific category.
/// </summary>
public class DocumentsByCategorySpecification : Specification<Document>
{
    private readonly string _category;

    public DocumentsByCategorySpecification(string category)
    {
        _category = category ?? throw new ArgumentNullException(nameof(category));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.Category == _category;
    }
}

/// <summary>
/// Specification for documents created by a specific user.
/// </summary>
public class DocumentsByAuthorSpecification : Specification<Document>
{
    private readonly UserId _authorId;

    public DocumentsByAuthorSpecification(UserId authorId)
    {
        _authorId = authorId ?? throw new ArgumentNullException(nameof(authorId));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.CreatedBy == _authorId;
    }
}

/// <summary>
/// Specification for documents generated from a specific template.
/// </summary>
public class DocumentsByTemplateSpecification : Specification<Document>
{
    private readonly TemplateId _templateId;

    public DocumentsByTemplateSpecification(TemplateId templateId)
    {
        _templateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.TemplateId == _templateId;
    }
}

/// <summary>
/// Specification for documents created within a date range.
/// </summary>
public class DocumentsByDateRangeSpecification : Specification<Document>
{
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;

    public DocumentsByDateRangeSpecification(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate;
        _endDate = endDate;

        if (startDate > endDate)
            throw new ArgumentException("Start date must be before end date");
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.CreatedAt >= _startDate && doc.CreatedAt <= _endDate;
    }
}

/// <summary>
/// Specification for documents with specific tags.
/// </summary>
public class DocumentsWithTagsSpecification : Specification<Document>
{
    private readonly List<string> _tags;
    private readonly bool _requireAllTags;

    public DocumentsWithTagsSpecification(List<string> tags, bool requireAllTags = false)
    {
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
        _requireAllTags = requireAllTags;
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        if (_requireAllTags)
        {
            return doc => _tags.All(tag => doc.Tags.Contains(tag));
        }
        else
        {
            return doc => _tags.Any(tag => doc.Tags.Contains(tag));
        }
    }
}

/// <summary>
/// Specification for large documents that may require special handling.
/// </summary>
public class LargeDocumentsSpecification : Specification<Document>
{
    private readonly int _sizeThreshold;

    public LargeDocumentsSpecification(int sizeThreshold = 50000) // 50KB default
    {
        _sizeThreshold = sizeThreshold;
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.Content != null && doc.Content.Length > _sizeThreshold;
    }
}

/// <summary>
/// Specification for documents accessible by a specific user (alias for backward compatibility).
/// </summary>
public class DocumentsAccessibleByUserSpecification : DocumentAccessibleByUserSpecification
{
    public DocumentsAccessibleByUserSpecification(User user) : base(user) { }
}

/// <summary>
/// Specification for documents containing specific text in title, description, or tags.
/// </summary>
public class DocumentsContainingTextSpecification : Specification<Document>
{
    private readonly string _searchTerm;

    public DocumentsContainingTextSpecification(string searchTerm)
    {
        _searchTerm = searchTerm?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(searchTerm));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.Title.ToLower().Contains(_searchTerm) ||
                     (doc.Description != null && doc.Description.ToLower().Contains(_searchTerm)) ||
                     doc.Tags.Any(tag => tag.ToLower().Contains(_searchTerm));
    }
}

/// <summary>
/// Specification for documents in a specific category.
/// </summary>
public class DocumentsInCategorySpecification : Specification<Document>
{
    private readonly string _category;

    public DocumentsInCategorySpecification(string category)
    {
        _category = category ?? throw new ArgumentNullException(nameof(category));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.Category == _category;
    }
}

/// <summary>
/// Specification for documents with a specific status.
/// </summary>
public class DocumentsWithStatusSpecification : Specification<Document>
{
    private readonly DocumentStatus _status;

    public DocumentsWithStatusSpecification(DocumentStatus status)
    {
        _status = status;
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.Status == _status;
    }
}

/// <summary>
/// Specification for documents with a specific security level.
/// </summary>
public class DocumentsWithSecurityLevelSpecification : Specification<Document>
{
    private readonly string _securityLevel;

    public DocumentsWithSecurityLevelSpecification(string securityLevel)
    {
        _securityLevel = securityLevel ?? throw new ArgumentNullException(nameof(securityLevel));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.SecurityClassification.Level == _securityLevel;
    }
}

/// <summary>
/// Specification for documents created by a specific user.
/// </summary>
public class DocumentsCreatedByUserSpecification : Specification<Document>
{
    private readonly UserId _userId;

    public DocumentsCreatedByUserSpecification(UserId userId)
    {
        _userId = userId ?? throw new ArgumentNullException(nameof(userId));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        return doc => doc.CreatedBy.Equals(_userId);
    }
}

/// <summary>
/// Specification for documents assigned to a specific user for approval or review.
/// </summary>
public class DocumentsAssignedToUserSpecification : Specification<Document>
{
    private readonly UserId _userId;

    public DocumentsAssignedToUserSpecification(UserId userId)
    {
        _userId = userId ?? throw new ArgumentNullException(nameof(userId));
    }

    public override Expression<Func<Document, bool>> ToExpression()
    {
        // For now, documents under review that could be approved by the user
        // In a real implementation, this would check assignment tables
        return doc => doc.Status == DocumentStatus.UnderReview;
    }
}