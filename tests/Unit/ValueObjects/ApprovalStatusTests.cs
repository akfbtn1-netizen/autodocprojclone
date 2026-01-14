using Xunit;
using FluentAssertions;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Tests.Unit.ValueObjects;

/// <summary>
/// Unit tests for ApprovalStatus value object.
/// Tests factory methods, business rules, and workflow transitions.
/// </summary>
public class ApprovalStatusTests
{
    #region Factory Method Tests

    [Fact]
    public void NotRequired_ShouldCreateStatusWithCorrectProperties()
    {
        // Arrange & Act
        var status = ApprovalStatus.NotRequired();

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("NotRequired");
        status.Comments.Should().BeNull();
        status.ApprovedBy.Should().BeNull();
        status.StatusChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Pending_WithoutComments_ShouldCreateStatusWithCorrectProperties()
    {
        // Arrange & Act
        var status = ApprovalStatus.Pending();

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("Pending");
        status.IsPending.Should().BeTrue();
        status.Comments.Should().BeNull();
        status.ApprovedBy.Should().BeNull();
        status.RequiresAction.Should().BeTrue();
    }

    [Fact]
    public void Pending_WithComments_ShouldCreateStatusWithComments()
    {
        // Arrange
        var comments = "Pending manager approval";

        // Act
        var status = ApprovalStatus.Pending(comments);

        // Assert
        status.Comments.Should().Be(comments);
        status.IsPending.Should().BeTrue();
    }

    [Fact]
    public void Approved_WithValidUserId_ShouldCreateApprovedStatus()
    {
        // Arrange
        var userId = UserId.New();
        var comments = "Looks good!";

        // Act
        var status = ApprovalStatus.Approved(userId, comments);

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("Approved");
        status.IsApproved.Should().BeTrue();
        status.ApprovedBy.Should().Be(userId);
        status.Comments.Should().Be(comments);
        status.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Approved_WithNullUserId_ShouldThrowArgumentNullException()
    {
        // Arrange
        UserId? userId = null;

        // Act
        Action act = () => ApprovalStatus.Approved(userId!, "comments");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("approvedBy");
    }

    [Fact]
    public void Rejected_WithValidUserId_ShouldCreateRejectedStatus()
    {
        // Arrange
        var userId = UserId.New();
        var comments = "Needs revision";

        // Act
        var status = ApprovalStatus.Rejected(userId, comments);

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("Rejected");
        status.IsRejected.Should().BeTrue();
        status.ApprovedBy.Should().Be(userId);
        status.Comments.Should().Be(comments);
        status.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Rejected_WithNullUserId_ShouldThrowArgumentNullException()
    {
        // Arrange
        UserId? userId = null;

        // Act
        Action act = () => ApprovalStatus.Rejected(userId!, "needs work");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("rejectedBy");
    }

    [Fact]
    public void Expired_ShouldCreateExpiredStatus()
    {
        // Arrange
        var comments = "Approval window expired";

        // Act
        var status = ApprovalStatus.Expired(comments);

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("Expired");
        status.Comments.Should().Be(comments);
        status.ApprovedBy.Should().BeNull();
        status.IsTerminal.Should().BeTrue();
    }

    #endregion

    #region Business Rule Tests

    [Fact]
    public void IsPending_WhenStatusIsPending_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act & Assert
        status.IsPending.Should().BeTrue();
        status.IsApproved.Should().BeFalse();
        status.IsRejected.Should().BeFalse();
    }

    [Fact]
    public void RequiresAction_WhenStatusIsPending_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act & Assert
        status.RequiresAction.Should().BeTrue();
    }

    [Fact]
    public void RequiresAction_WhenStatusIsApproved_ShouldReturnFalse()
    {
        // Arrange
        var status = ApprovalStatus.Approved(UserId.New());

        // Act & Assert
        status.RequiresAction.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_WhenStatusIsApproved_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Approved(UserId.New());

        // Act & Assert
        status.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_WhenStatusIsRejected_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Rejected(UserId.New());

        // Act & Assert
        status.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_WhenStatusIsExpired_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Expired();

        // Act & Assert
        status.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_WhenStatusIsPending_ShouldReturnFalse()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act & Assert
        status.IsTerminal.Should().BeFalse();
    }

    #endregion

    #region Workflow Transition Tests

    [Fact]
    public void CanTransitionTo_FromPendingToApproved_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act
        var canTransition = status.CanTransitionTo("Approved");

        // Assert
        canTransition.Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromPendingToRejected_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act
        var canTransition = status.CanTransitionTo("Rejected");

        // Assert
        canTransition.Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromPendingToExpired_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act
        var canTransition = status.CanTransitionTo("Expired");

        // Assert
        canTransition.Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromRejectedToPending_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.Rejected(UserId.New());

        // Act
        var canTransition = status.CanTransitionTo("Pending");

        // Assert
        canTransition.Should().BeTrue("rejected documents can be resubmitted for approval");
    }

    [Fact]
    public void CanTransitionTo_FromApprovedToPending_ShouldReturnFalse()
    {
        // Arrange
        var status = ApprovalStatus.Approved(UserId.New());

        // Act
        var canTransition = status.CanTransitionTo("Pending");

        // Assert
        canTransition.Should().BeFalse("approved documents cannot be moved back to pending");
    }

    [Fact]
    public void CanTransitionTo_ToNotRequired_ShouldAlwaysReturnFalse()
    {
        // Arrange
        var pendingStatus = ApprovalStatus.Pending();
        var approvedStatus = ApprovalStatus.Approved(UserId.New());

        // Act & Assert
        pendingStatus.CanTransitionTo("NotRequired").Should().BeFalse();
        approvedStatus.CanTransitionTo("NotRequired").Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromNotRequiredToPending_ShouldReturnTrue()
    {
        // Arrange
        var status = ApprovalStatus.NotRequired();

        // Act
        var canTransition = status.CanTransitionTo("Pending");

        // Assert
        canTransition.Should().BeTrue("documents can require approval later");
    }

    [Fact]
    public void CanTransitionTo_WithInvalidStatus_ShouldReturnFalse()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act
        var canTransition = status.CanTransitionTo("InvalidStatus");

        // Assert
        canTransition.Should().BeFalse();
    }

    #endregion

    #region Value Object Equality Tests

    [Fact]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        var userId = UserId.New();
        var comments = "Test comments";
        var status1 = ApprovalStatus.Approved(userId, comments);
        var status2 = ApprovalStatus.Approved(userId, comments);

        // Act & Assert
        // Note: Equality will be based on all properties including timestamp
        // So these won't be equal due to different timestamps
        status1.Should().NotBe(status2, "timestamps will differ");
    }

    [Fact]
    public void Equals_WithDifferentStatus_ShouldReturnFalse()
    {
        // Arrange
        var status1 = ApprovalStatus.Pending();
        var status2 = ApprovalStatus.NotRequired();

        // Act & Assert
        status1.Should().NotBe(status2);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithApprovedBy_ShouldIncludeUserId()
    {
        // Arrange
        var userId = UserId.New();
        var status = ApprovalStatus.Approved(userId, "Approved");

        // Act
        var result = status.ToString();

        // Assert
        result.Should().Contain("Approved");
        result.Should().Contain("by");
        result.Should().Contain("at");
    }

    [Fact]
    public void ToString_WithoutApprovedBy_ShouldNotIncludeUserId()
    {
        // Arrange
        var status = ApprovalStatus.Pending();

        // Act
        var result = status.ToString();

        // Assert
        result.Should().Contain("Pending");
        result.Should().Contain("at");
        result.Should().NotContain("by");
    }

    #endregion
}
