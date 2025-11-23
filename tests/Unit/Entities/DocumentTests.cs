using Xunit;
using FluentAssertions;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Events;

namespace Tests.Unit.Entities;

/// <summary>
/// Unit tests for Document entity.
/// Tests document creation, updates, approval workflows, publishing, and business rules.
/// </summary>
public class DocumentTests
{
    private readonly UserId _testUserId = UserId.New();
    private readonly DocumentId _testDocumentId = DocumentId.New();
    private readonly SecurityClassification _testSecurityClassification;

    public DocumentTests()
    {
        _testSecurityClassification = SecurityClassification.Internal(_testUserId);
    }

    #region Creation Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateDocument()
    {
        // Arrange
        var title = "Test Document";
        var category = "Technical";
        var description = "Test description";
        var tags = new List<string> { "test", "documentation" };

        // Act
        var document = new Document(
            _testDocumentId,
            title,
            category,
            _testSecurityClassification,
            _testUserId,
            description,
            tags);

        // Assert
        document.Should().NotBeNull();
        document.Id.Should().Be(_testDocumentId);
        document.Title.Should().Be(title);
        document.Category.Should().Be(category);
        document.Description.Should().Be(description);
        document.Tags.Should().BeEquivalentTo(tags);
        document.SecurityClassification.Should().Be(_testSecurityClassification);
        document.CreatedBy.Should().Be(_testUserId);
        document.Status.Should().Be(DocumentStatus.Draft);
        document.DocumentVersion.Should().Be("1.0");
        document.ApprovalStatus.Status.Should().Be("NotRequired");
        document.ContainsPII.Should().BeFalse();
        document.RelatedDocuments.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullTitle_ShouldThrowArgumentException()
    {
        // Arrange
        string? title = null;

        // Act
        Action act = () => new Document(
            _testDocumentId,
            title!,
            "Technical",
            _testSecurityClassification,
            _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyTitle_ShouldThrowArgumentException()
    {
        // Arrange
        var title = "   ";

        // Act
        Action act = () => new Document(
            _testDocumentId,
            title,
            "Technical",
            _testSecurityClassification,
            _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Constructor_WithTitleTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var title = new string('A', 201); // 201 characters

        // Act
        Action act = () => new Document(
            _testDocumentId,
            title,
            "Technical",
            _testSecurityClassification,
            _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*cannot exceed 200 characters*");
    }

    [Fact]
    public void Constructor_WithEmptyCategory_ShouldThrowArgumentException()
    {
        // Arrange
        var category = "";

        // Act
        Action act = () => new Document(
            _testDocumentId,
            "Test Document",
            category,
            _testSecurityClassification,
            _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("category")
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Constructor_WithCategoryTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var category = new string('A', 101); // 101 characters

        // Act
        Action act = () => new Document(
            _testDocumentId,
            "Test Document",
            category,
            _testSecurityClassification,
            _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("category")
            .WithMessage("*cannot exceed 100 characters*");
    }

    [Fact]
    public void Constructor_ShouldRaiseDocumentCreatedEvent()
    {
        // Arrange & Act
        var document = new Document(
            _testDocumentId,
            "Test Document",
            "Technical",
            _testSecurityClassification,
            _testUserId);

        // Assert
        var domainEvents = document.DomainEvents;
        domainEvents.Should().HaveCount(1);
        domainEvents.First().Should().BeOfType<DocumentCreatedEvent>();

        var createdEvent = (DocumentCreatedEvent)domainEvents.First();
        createdEvent.DocumentId.Should().Be(_testDocumentId);
        createdEvent.Title.Should().Be("Test Document");
        createdEvent.Category.Should().Be("Technical");
        createdEvent.CreatedBy.Should().Be(_testUserId);
    }

    #endregion

    #region UpdateContent Tests

    [Fact]
    public void UpdateContent_WithValidParameters_ShouldUpdateDocumentContent()
    {
        // Arrange
        var document = CreateTestDocument();
        var content = "# Test Content\n\nThis is test content.";
        var sizeBytes = 1024L;
        var storagePath = "/documents/test.md";
        var containsPII = true;

        // Act
        document.UpdateContent(content, sizeBytes, storagePath, containsPII, _testUserId);

        // Assert
        document.Content.Should().Be(content);
        document.SizeBytes.Should().Be(sizeBytes);
        document.StoragePath.Should().Be(storagePath);
        document.ContainsPII.Should().BeTrue();
        // document.LastModifiedBy.Should().Be(_testUserId); // Property not implemented yet
    }

    [Fact]
    public void UpdateContent_ShouldRaiseDocumentContentUpdatedEvent()
    {
        // Arrange
        var document = CreateTestDocument();
        document.ClearDomainEvents(); // Clear creation event

        // Act
        document.UpdateContent("content", 100, "/path", false, _testUserId);

        // Assert
        var events = document.DomainEvents;
        events.Should().HaveCount(1);
        events.First().Should().BeOfType<DocumentContentUpdatedEvent>();
    }

    #endregion

    #region UpdateMetadata Tests

    [Fact]
    public void UpdateMetadata_WithNewTitle_ShouldUpdateTitle()
    {
        // Arrange
        var document = CreateTestDocument();
        var newTitle = "Updated Title";

        // Act
        document.UpdateMetadata(title: newTitle, updatedBy: _testUserId);

        // Assert
        document.Title.Should().Be(newTitle);
    }

    [Fact]
    public void UpdateMetadata_WithNewDescription_ShouldUpdateDescription()
    {
        // Arrange
        var document = CreateTestDocument();
        var newDescription = "Updated description";

        // Act
        document.UpdateMetadata(description: newDescription, updatedBy: _testUserId);

        // Assert
        document.Description.Should().Be(newDescription);
    }

    [Fact]
    public void UpdateMetadata_WithNewCategory_ShouldUpdateCategory()
    {
        // Arrange
        var document = CreateTestDocument();
        var newCategory = "Updated Category";

        // Act
        document.UpdateMetadata(category: newCategory, updatedBy: _testUserId);

        // Assert
        document.Category.Should().Be(newCategory);
    }

    [Fact]
    public void UpdateMetadata_WithNewTags_ShouldUpdateTags()
    {
        // Arrange
        var document = CreateTestDocument();
        var newTags = new List<string> { "updated", "tags" };

        // Act
        document.UpdateMetadata(tags: newTags, updatedBy: _testUserId);

        // Assert
        document.Tags.Should().BeEquivalentTo(newTags);
    }

    [Fact]
    public void UpdateMetadata_WithNullUpdatedBy_ShouldThrowArgumentNullException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        Action act = () => document.UpdateMetadata(title: "New Title", updatedBy: null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("updatedBy");
    }

    [Fact]
    public void UpdateMetadata_ShouldRaiseDocumentMetadataUpdatedEvent()
    {
        // Arrange
        var document = CreateTestDocument();
        document.ClearDomainEvents();

        // Act
        document.UpdateMetadata(title: "New Title", updatedBy: _testUserId);

        // Assert
        document.DomainEvents.Should().HaveCount(1);
        document.DomainEvents.First().Should().BeOfType<DocumentMetadataUpdatedEvent>();
    }

    #endregion

    #region UpdateTitle Tests

    [Fact]
    public void UpdateTitle_WithValidTitle_ShouldUpdateTitle()
    {
        // Arrange
        var document = CreateTestDocument();
        var newTitle = "New Valid Title";

        // Act
        document.UpdateTitle(newTitle, _testUserId);

        // Assert
        document.Title.Should().Be(newTitle);
    }

    [Fact]
    public void UpdateTitle_OnArchivedDocument_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Archive(_testUserId);

        // Act
        Action act = () => document.UpdateTitle("New Title", _testUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public void UpdateTitle_WithEmptyTitle_ShouldThrowArgumentException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        Action act = () => document.UpdateTitle("", _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("title");
    }

    #endregion

    #region UpdateCategory Tests

    [Fact]
    public void UpdateCategory_WithValidCategory_ShouldUpdateCategory()
    {
        // Arrange
        var document = CreateTestDocument();
        var newCategory = "New Category";

        // Act
        document.UpdateCategory(newCategory, _testUserId);

        // Assert
        document.Category.Should().Be(newCategory);
    }

    [Fact]
    public void UpdateCategory_OnArchivedDocument_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Archive(_testUserId);

        // Act
        Action act = () => document.UpdateCategory("New Category", _testUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*archived*");
    }

    #endregion

    #region Approval Workflow Tests

    [Fact]
    public void UpdateApprovalStatus_WithValidTransition_ShouldUpdateStatus()
    {
        // Arrange
        var document = CreateTestDocument();
        var pendingStatus = ApprovalStatus.Pending();

        // Act
        document.UpdateApprovalStatus(pendingStatus, _testUserId);

        // Assert
        document.ApprovalStatus.Should().Be(pendingStatus);
    }

    [Fact]
    public void UpdateApprovalStatus_ShouldRaiseApprovalStatusChangedEvent()
    {
        // Arrange
        var document = CreateTestDocument();
        document.ClearDomainEvents();
        var pendingStatus = ApprovalStatus.Pending();

        // Act
        document.UpdateApprovalStatus(pendingStatus, _testUserId);

        // Assert
        var events = document.DomainEvents;
        events.Should().Contain(e => e is DocumentApprovalStatusChangedEvent);
    }

    [Fact]
    public void Approve_WhenUnderReview_ShouldApproveAndPublishDocument()
    {
        // Arrange
        var document = CreateTestDocument();
        // First set to UnderReview status
        typeof(Document)
            .GetProperty("Status")!
            .SetValue(document, DocumentStatus.UnderReview);

        var approverId = UserId.New();
        var comments = "Approved for publication";

        // Act
        document.Approve(approverId, comments);

        // Assert
        document.Status.Should().Be(DocumentStatus.Published);
        document.PublishedAt.Should().NotBeNull();
        document.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        document.ApprovalStatus.IsApproved.Should().BeTrue();
        document.ApprovalStatus.Comments.Should().Be(comments);
    }

    [Fact]
    public void Approve_WhenNotUnderReview_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument(); // Status is Draft

        // Act
        Action act = () => document.Approve(_testUserId, "comments");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be under review*");
    }

    #endregion

    #region Publish Tests

    [Fact]
    public void Publish_WhenApproved_ShouldPublishDocument()
    {
        // Arrange
        var document = CreateTestDocument();
        var approvedStatus = ApprovalStatus.Approved(_testUserId);
        document.UpdateApprovalStatus(approvedStatus, _testUserId);

        // Act
        document.Publish(_testUserId);

        // Assert
        document.Status.Should().Be(DocumentStatus.Published);
        document.PublishedAt.Should().NotBeNull();
        document.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Publish_WhenNotApproved_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument(); // ApprovalStatus is NotRequired (not approved)

        // Act
        Action act = () => document.Publish(_testUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be approved*");
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        var approvedStatus = ApprovalStatus.Approved(_testUserId);
        document.UpdateApprovalStatus(approvedStatus, _testUserId);
        document.Publish(_testUserId);

        // Act
        Action act = () => document.Publish(_testUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already published*");
    }

    [Fact]
    public void Publish_ShouldRaiseDocumentPublishedEvent()
    {
        // Arrange
        var document = CreateTestDocument();
        var approvedStatus = ApprovalStatus.Approved(_testUserId);
        document.UpdateApprovalStatus(approvedStatus, _testUserId);
        document.ClearDomainEvents();

        // Act
        document.Publish(_testUserId);

        // Assert
        document.DomainEvents.Should().Contain(e => e is DocumentPublishedEvent);
    }

    #endregion

    #region Archive Tests

    [Fact]
    public void Archive_WhenNotArchived_ShouldArchiveDocument()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        document.Archive(_testUserId);

        // Assert
        document.Status.Should().Be(DocumentStatus.Archived);
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.Archive(_testUserId);

        // Act
        Action act = () => document.Archive(_testUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already archived*");
    }

    [Fact]
    public void Archive_ShouldRaiseDocumentArchivedEvent()
    {
        // Arrange
        var document = CreateTestDocument();
        document.ClearDomainEvents();

        // Act
        document.Archive(_testUserId);

        // Assert
        document.DomainEvents.Should().Contain(e => e is DocumentArchivedEvent);
    }

    #endregion

    #region Related Documents Tests

    [Fact]
    public void AddRelatedDocument_WithValidDocumentId_ShouldAddRelation()
    {
        // Arrange
        var document = CreateTestDocument();
        var relatedDocId = DocumentId.New();

        // Act
        document.AddRelatedDocument(relatedDocId, _testUserId);

        // Assert
        document.RelatedDocuments.Should().Contain(relatedDocId);
    }

    [Fact]
    public void AddRelatedDocument_WithSameDocumentId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        Action act = () => document.AddRelatedDocument(document.Id, _testUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be related to itself*");
    }

    [Fact]
    public void AddRelatedDocument_WhenAlreadyRelated_ShouldNotAddDuplicate()
    {
        // Arrange
        var document = CreateTestDocument();
        var relatedDocId = DocumentId.New();
        document.AddRelatedDocument(relatedDocId, _testUserId);

        // Act
        document.AddRelatedDocument(relatedDocId, _testUserId);

        // Assert
        document.RelatedDocuments.Should().ContainSingle(id => id == relatedDocId);
    }

    [Fact]
    public void RemoveRelatedDocument_WhenRelationExists_ShouldRemoveRelation()
    {
        // Arrange
        var document = CreateTestDocument();
        var relatedDocId = DocumentId.New();
        document.AddRelatedDocument(relatedDocId, _testUserId);

        // Act
        document.RemoveRelatedDocument(relatedDocId, _testUserId);

        // Assert
        document.RelatedDocuments.Should().NotContain(relatedDocId);
    }

    #endregion

    #region Security Classification Tests

    [Fact]
    public void UpdateSecurityClassification_WithDowngrade_ShouldUpdateClassification()
    {
        // Arrange
        var restrictedClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Executive" });
        var document = new Document(
            _testDocumentId,
            "Test Document",
            "Technical",
            restrictedClassification,
            _testUserId);

        var newClassification = SecurityClassification.Confidential(_testUserId, new List<string> { "Engineering" });

        // Act
        document.UpdateSecurityClassification(newClassification, _testUserId);

        // Assert
        document.SecurityClassification.Should().Be(newClassification);
    }

    [Fact]
    public void UpdateSecurityClassification_WithUpgrade_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = CreateTestDocument(); // Internal classification
        var restrictedClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Executive" });

        // Act
        Action act = () => document.UpdateSecurityClassification(restrictedClassification, _testUserId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot upgrade security classification*");
    }

    [Fact]
    public void UpdateSecurityClassification_ShouldRaiseSecurityClassificationChangedEvent()
    {
        // Arrange
        var restrictedClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Executive" });
        var document = new Document(
            _testDocumentId,
            "Test Document",
            "Technical",
            restrictedClassification,
            _testUserId);
        document.ClearDomainEvents();

        var newClassification = SecurityClassification.Public(_testUserId);

        // Act
        document.UpdateSecurityClassification(newClassification, _testUserId);

        // Assert
        document.DomainEvents.Should().Contain(e => e is DocumentSecurityClassificationChangedEvent);
    }

    #endregion

    #region Helper Methods

    private Document CreateTestDocument()
    {
        return new Document(
            _testDocumentId,
            "Test Document",
            "Technical",
            _testSecurityClassification,
            _testUserId);
    }

    #endregion
}
