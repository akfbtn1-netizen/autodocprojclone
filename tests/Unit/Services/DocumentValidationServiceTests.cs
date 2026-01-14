using Xunit;
using FluentAssertions;
using Enterprise.Documentation.Core.Domain.Services;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Tests.Unit.Services;

/// <summary>
/// Unit tests for DocumentValidationService domain service.
/// Tests all validation rules and business logic.
/// </summary>
public class DocumentValidationServiceTests
{
    private readonly UserId _testUserId = UserId.New();
    private readonly SecurityClassification _testSecurityClassification;

    public DocumentValidationServiceTests()
    {
        _testSecurityClassification = SecurityClassification.Internal(_testUserId);
    }

    #region ValidateDocumentCreation Tests

    [Fact]
    public void ValidateDocumentCreation_WithValidParameters_ShouldNotThrow()
    {
        // Arrange
        var title = "Valid Title";
        var category = "Valid Category";

        // Act
        Action act = () => DocumentValidationService.ValidateDocumentCreation(
            title,
            category,
            _testSecurityClassification,
            _testUserId);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateDocumentCreation_WithNullTitle_ShouldThrowArgumentException()
    {
        // Arrange
        string? title = null;

        // Act
        Action act = () => DocumentValidationService.ValidateDocumentCreation(
            title!,
            "Category",
            _testSecurityClassification,
            _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("title");
    }

    [Fact]
    public void ValidateDocumentCreation_WithNullSecurityClassification_ShouldThrowArgumentNullException()
    {
        // Arrange
        SecurityClassification? securityClassification = null;

        // Act
        Action act = () => DocumentValidationService.ValidateDocumentCreation(
            "Title",
            "Category",
            securityClassification!,
            _testUserId);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("securityClassification");
    }

    #endregion

    #region ValidateTitle Tests

    [Theory]
    [InlineData("Valid Title")]
    [InlineData("A")]
    [InlineData("Title with numbers 123")]
    [InlineData("Title-with-dashes")]
    public void ValidateTitle_WithValidTitle_ShouldNotThrow(string title)
    {
        // Act
        Action act = () => DocumentValidationService.ValidateTitle(title);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ValidateTitle_WithEmptyTitle_ShouldThrowArgumentException(string? title)
    {
        // Act
        Action act = () => DocumentValidationService.ValidateTitle(title!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void ValidateTitle_WithTitleTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var title = new string('A', 201); // 201 characters (max is 200)

        // Act
        Action act = () => DocumentValidationService.ValidateTitle(title);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*cannot exceed 200 characters*");
    }

    [Fact]
    public void ValidateTitle_WithExactly200Characters_ShouldNotThrow()
    {
        // Arrange
        var title = new string('A', 200); // Exactly 200 characters

        // Act
        Action act = () => DocumentValidationService.ValidateTitle(title);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region ValidateCategory Tests

    [Theory]
    [InlineData("Technical")]
    [InlineData("Business")]
    [InlineData("A")]
    public void ValidateCategory_WithValidCategory_ShouldNotThrow(string category)
    {
        // Act
        Action act = () => DocumentValidationService.ValidateCategory(category);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCategory_WithEmptyCategory_ShouldThrowArgumentException(string? category)
    {
        // Act
        Action act = () => DocumentValidationService.ValidateCategory(category!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("category")
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void ValidateCategory_WithCategoryTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var category = new string('A', 101); // 101 characters (max is 100)

        // Act
        Action act = () => DocumentValidationService.ValidateCategory(category);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("category")
            .WithMessage("*cannot exceed 100 characters*");
    }

    [Fact]
    public void ValidateCategory_WithExactly100Characters_ShouldNotThrow()
    {
        // Arrange
        var category = new string('A', 100); // Exactly 100 characters

        // Act
        Action act = () => DocumentValidationService.ValidateCategory(category);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region ValidateContentType Tests

    [Theory]
    [InlineData("markdown")]
    [InlineData("html")]
    [InlineData("pdf")]
    [InlineData("application/json")]
    public void ValidateContentType_WithValidContentType_ShouldNotThrow(string contentType)
    {
        // Act
        Action act = () => DocumentValidationService.ValidateContentType(contentType);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateContentType_WithEmptyContentType_ShouldThrowArgumentException(string? contentType)
    {
        // Act
        Action act = () => DocumentValidationService.ValidateContentType(contentType!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("contentType")
            .WithMessage("*cannot be empty*");
    }

    #endregion

    #region ValidateNotArchived Tests

    [Fact]
    public void ValidateNotArchived_WhenDocumentIsNotArchived_ShouldNotThrow()
    {
        // Arrange
        var document = new Document(
            DocumentId.New(),
            "Test",
            "Category",
            _testSecurityClassification,
            _testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateNotArchived(document);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateNotArchived_WhenDocumentIsArchived_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = new Document(
            DocumentId.New(),
            "Test",
            "Category",
            _testSecurityClassification,
            _testUserId);
        document.Archive(_testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateNotArchived(document);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot modify archived document*");
    }

    #endregion

    #region ValidateApprovalTransition Tests

    [Fact]
    public void ValidateApprovalTransition_WithValidTransition_ShouldNotThrow()
    {
        // Arrange
        var currentStatus = ApprovalStatus.Pending();
        var newStatus = ApprovalStatus.Approved(_testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateApprovalTransition(currentStatus, newStatus);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateApprovalTransition_WithInvalidTransition_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var currentStatus = ApprovalStatus.Approved(_testUserId);
        var newStatus = ApprovalStatus.Pending();

        // Act
        Action act = () => DocumentValidationService.ValidateApprovalTransition(currentStatus, newStatus);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition from Approved to Pending*");
    }

    #endregion

    #region ValidateCanPublish Tests

    [Fact]
    public void ValidateCanPublish_WhenDocumentIsApproved_ShouldNotThrow()
    {
        // Arrange
        var document = new Document(
            DocumentId.New(),
            "Test",
            "Category",
            _testSecurityClassification,
            _testUserId);
        var approvedStatus = ApprovalStatus.Approved(_testUserId);
        document.UpdateApprovalStatus(approvedStatus, _testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateCanPublish(document);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCanPublish_WhenDocumentIsNotApproved_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = new Document(
            DocumentId.New(),
            "Test",
            "Category",
            _testSecurityClassification,
            _testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateCanPublish(document);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be approved before publishing*");
    }

    [Fact]
    public void ValidateCanPublish_WhenDocumentIsAlreadyPublished_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = new Document(
            DocumentId.New(),
            "Test",
            "Category",
            _testSecurityClassification,
            _testUserId);
        var approvedStatus = ApprovalStatus.Approved(_testUserId);
        document.UpdateApprovalStatus(approvedStatus, _testUserId);
        document.Publish(_testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateCanPublish(document);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already published*");
    }

    #endregion

    #region ValidateCanArchive Tests

    [Fact]
    public void ValidateCanArchive_WhenDocumentIsNotArchived_ShouldNotThrow()
    {
        // Arrange
        var document = new Document(
            DocumentId.New(),
            "Test",
            "Category",
            _testSecurityClassification,
            _testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateCanArchive(document);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCanArchive_WhenDocumentIsAlreadyArchived_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var document = new Document(
            DocumentId.New(),
            "Test",
            "Category",
            _testSecurityClassification,
            _testUserId);
        document.Archive(_testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateCanArchive(document);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already archived*");
    }

    #endregion

    #region ValidateRelatedDocument Tests

    [Fact]
    public void ValidateRelatedDocument_WithDifferentDocumentIds_ShouldNotThrow()
    {
        // Arrange
        var documentId = DocumentId.New();
        var relatedDocumentId = DocumentId.New();

        // Act
        Action act = () => DocumentValidationService.ValidateRelatedDocument(documentId, relatedDocumentId);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateRelatedDocument_WithSameDocumentId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var documentId = DocumentId.New();

        // Act
        Action act = () => DocumentValidationService.ValidateRelatedDocument(documentId, documentId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be related to itself*");
    }

    #endregion

    #region ValidateSecurityClassificationChange Tests

    [Fact]
    public void ValidateSecurityClassificationChange_WithDowngrade_ShouldNotThrow()
    {
        // Arrange
        var currentClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Test" });
        var newClassification = SecurityClassification.Confidential(_testUserId, new List<string> { "Test" });

        // Act
        Action act = () => DocumentValidationService.ValidateSecurityClassificationChange(
            currentClassification,
            newClassification);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateSecurityClassificationChange_WithUpgrade_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var currentClassification = SecurityClassification.Public(_testUserId);
        var newClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Test" });

        // Act
        Action act = () => DocumentValidationService.ValidateSecurityClassificationChange(
            currentClassification,
            newClassification);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot upgrade security classification level*");
    }

    [Fact]
    public void ValidateSecurityClassificationChange_WithNullNewClassification_ShouldThrowArgumentNullException()
    {
        // Arrange
        var currentClassification = SecurityClassification.Public(_testUserId);

        // Act
        Action act = () => DocumentValidationService.ValidateSecurityClassificationChange(
            currentClassification,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("newClassification");
    }

    #endregion
}
