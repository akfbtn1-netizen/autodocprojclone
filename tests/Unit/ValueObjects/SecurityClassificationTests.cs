using Xunit;
using FluentAssertions;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Tests.Unit.ValueObjects;

/// <summary>
/// Unit tests for SecurityClassification value object.
/// Tests security levels, access control, and classification business rules.
/// </summary>
public class SecurityClassificationTests
{
    private readonly UserId _testUserId = UserId.New();

    #region Factory Method Tests

    [Fact]
    public void Public_ShouldCreatePublicClassification()
    {
        // Arrange & Act
        var classification = SecurityClassification.Public(_testUserId);

        // Assert
        classification.Should().NotBeNull();
        classification.Level.Should().Be("Public");
        classification.IsPublic.Should().BeTrue();
        classification.AccessGroups.Should().ContainSingle().Which.Should().Be("Everyone");
        classification.RequiresPIIHandling.Should().BeFalse();
        classification.ClassifiedBy.Should().Be(_testUserId);
        classification.SecurityLevel.Should().Be(0);
    }

    [Fact]
    public void Internal_WithoutAccessGroups_ShouldCreateInternalClassificationWithDefaultGroups()
    {
        // Arrange & Act
        var classification = SecurityClassification.Internal(_testUserId);

        // Assert
        classification.Should().NotBeNull();
        classification.Level.Should().Be("Internal");
        classification.IsInternal.Should().BeTrue();
        classification.AccessGroups.Should().ContainSingle().Which.Should().Be("Employees");
        classification.RequiresPIIHandling.Should().BeFalse();
        classification.SecurityLevel.Should().Be(1);
    }

    [Fact]
    public void Internal_WithCustomAccessGroups_ShouldUseProvidedGroups()
    {
        // Arrange
        var customGroups = new List<string> { "Engineering", "Product" };

        // Act
        var classification = SecurityClassification.Internal(_testUserId, customGroups);

        // Assert
        classification.AccessGroups.Should().BeEquivalentTo(customGroups);
    }

    [Fact]
    public void Confidential_WithValidAccessGroups_ShouldCreateConfidentialClassification()
    {
        // Arrange
        var accessGroups = new List<string> { "Management", "Legal" };

        // Act
        var classification = SecurityClassification.Confidential(_testUserId, accessGroups);

        // Assert
        classification.Should().NotBeNull();
        classification.Level.Should().Be("Confidential");
        classification.IsConfidential.Should().BeTrue();
        classification.AccessGroups.Should().BeEquivalentTo(accessGroups);
        classification.RequiresPIIHandling.Should().BeFalse();
        classification.SecurityLevel.Should().Be(2);
    }

    [Fact]
    public void Confidential_WithPIIHandling_ShouldSetPIIHandlingTrue()
    {
        // Arrange
        var accessGroups = new List<string> { "HR" };

        // Act
        var classification = SecurityClassification.Confidential(_testUserId, accessGroups, requiresPIIHandling: true);

        // Assert
        classification.RequiresPIIHandling.Should().BeTrue();
    }

    [Fact]
    public void Confidential_WithNullAccessGroups_ShouldThrowArgumentException()
    {
        // Arrange
        List<string>? accessGroups = null;

        // Act
        Action act = () => SecurityClassification.Confidential(_testUserId, accessGroups!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("accessGroups")
            .WithMessage("*must specify access groups*");
    }

    [Fact]
    public void Confidential_WithEmptyAccessGroups_ShouldThrowArgumentException()
    {
        // Arrange
        var accessGroups = new List<string>();

        // Act
        Action act = () => SecurityClassification.Confidential(_testUserId, accessGroups);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("accessGroups")
            .WithMessage("*must specify access groups*");
    }

    [Fact]
    public void Restricted_WithValidAccessGroups_ShouldCreateRestrictedClassification()
    {
        // Arrange
        var accessGroups = new List<string> { "Executive", "Compliance" };

        // Act
        var classification = SecurityClassification.Restricted(_testUserId, accessGroups);

        // Assert
        classification.Should().NotBeNull();
        classification.Level.Should().Be("Restricted");
        classification.IsRestricted.Should().BeTrue();
        classification.AccessGroups.Should().BeEquivalentTo(accessGroups);
        classification.RequiresPIIHandling.Should().BeTrue("restricted always requires PII handling");
        classification.SecurityLevel.Should().Be(3);
    }

    [Fact]
    public void Restricted_WithNullAccessGroups_ShouldThrowArgumentException()
    {
        // Arrange
        List<string>? accessGroups = null;

        // Act
        Action act = () => SecurityClassification.Restricted(_testUserId, accessGroups!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("accessGroups")
            .WithMessage("*must specify access groups*");
    }

    #endregion

    #region Security Level Tests

    [Theory]
    [InlineData("Public", 0)]
    [InlineData("Internal", 1)]
    [InlineData("Confidential", 2)]
    [InlineData("Restricted", 3)]
    public void SecurityLevel_ShouldReturnCorrectNumericValue(string expectedLevel, int expectedNumericLevel)
    {
        // Arrange
        SecurityClassification classification = expectedLevel switch
        {
            "Public" => SecurityClassification.Public(_testUserId),
            "Internal" => SecurityClassification.Internal(_testUserId),
            "Confidential" => SecurityClassification.Confidential(_testUserId, new List<string> { "Test" }),
            "Restricted" => SecurityClassification.Restricted(_testUserId, new List<string> { "Test" }),
            _ => throw new InvalidOperationException()
        };

        // Act & Assert
        classification.SecurityLevel.Should().Be(expectedNumericLevel);
    }

    [Fact]
    public void SecurityLevel_IsOrdered_PublicLessThanRestricted()
    {
        // Arrange
        var publicClassification = SecurityClassification.Public(_testUserId);
        var restrictedClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Test" });

        // Act & Assert
        publicClassification.SecurityLevel.Should().BeLessThan(restrictedClassification.SecurityLevel);
    }

    #endregion

    #region Access Control Tests

    [Fact]
    public void CanAccess_PublicDocument_WithNoUserGroups_ShouldReturnTrue()
    {
        // Arrange
        var classification = SecurityClassification.Public(_testUserId);
        var userGroups = new List<string>();

        // Act
        var canAccess = classification.CanAccess(userGroups);

        // Assert
        canAccess.Should().BeTrue("public documents are accessible to everyone");
    }

    [Fact]
    public void CanAccess_PublicDocument_WithAnyUserGroups_ShouldReturnTrue()
    {
        // Arrange
        var classification = SecurityClassification.Public(_testUserId);
        var userGroups = new List<string> { "Engineering" };

        // Act
        var canAccess = classification.CanAccess(userGroups);

        // Assert
        canAccess.Should().BeTrue("public documents are accessible to everyone");
    }

    [Fact]
    public void CanAccess_InternalDocument_WithNoUserGroups_ShouldReturnFalse()
    {
        // Arrange
        var classification = SecurityClassification.Internal(_testUserId);
        var userGroups = new List<string>();

        // Act
        var canAccess = classification.CanAccess(userGroups);

        // Assert
        canAccess.Should().BeFalse("internal documents require group membership");
    }

    [Fact]
    public void CanAccess_InternalDocument_WithMatchingGroup_ShouldReturnTrue()
    {
        // Arrange
        var classification = SecurityClassification.Internal(_testUserId);
        var userGroups = new List<string> { "Employees", "Engineering" };

        // Act
        var canAccess = classification.CanAccess(userGroups);

        // Assert
        canAccess.Should().BeTrue("user is in Employees group");
    }

    [Fact]
    public void CanAccess_ConfidentialDocument_WithMatchingGroup_ShouldReturnTrue()
    {
        // Arrange
        var classification = SecurityClassification.Confidential(
            _testUserId,
            new List<string> { "Legal", "Compliance" });
        var userGroups = new List<string> { "Legal" };

        // Act
        var canAccess = classification.CanAccess(userGroups);

        // Assert
        canAccess.Should().BeTrue("user is in Legal group");
    }

    [Fact]
    public void CanAccess_ConfidentialDocument_WithoutMatchingGroup_ShouldReturnFalse()
    {
        // Arrange
        var classification = SecurityClassification.Confidential(
            _testUserId,
            new List<string> { "Legal", "Compliance" });
        var userGroups = new List<string> { "Engineering", "Product" };

        // Act
        var canAccess = classification.CanAccess(userGroups);

        // Assert
        canAccess.Should().BeFalse("user is not in required groups");
    }

    [Fact]
    public void CanAccess_ShouldBeCaseInsensitive()
    {
        // Arrange
        var classification = SecurityClassification.Confidential(
            _testUserId,
            new List<string> { "Legal" });
        var userGroups = new List<string> { "legal" }; // lowercase

        // Act
        var canAccess = classification.CanAccess(userGroups);

        // Assert
        canAccess.Should().BeTrue("access check should be case-insensitive");
    }

    #endregion

    #region Downgrade Tests

    [Fact]
    public void CanDowngradeTo_FromRestrictedToConfidential_ShouldReturnTrue()
    {
        // Arrange
        var restrictedClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Test" });
        var confidentialClassification = SecurityClassification.Confidential(_testUserId, new List<string> { "Test" });

        // Act
        var canDowngrade = restrictedClassification.CanDowngradeTo(confidentialClassification);

        // Assert
        canDowngrade.Should().BeTrue("can downgrade from Restricted (3) to Confidential (2)");
    }

    [Fact]
    public void CanDowngradeTo_FromConfidentialToPublic_ShouldReturnTrue()
    {
        // Arrange
        var confidentialClassification = SecurityClassification.Confidential(_testUserId, new List<string> { "Test" });
        var publicClassification = SecurityClassification.Public(_testUserId);

        // Act
        var canDowngrade = confidentialClassification.CanDowngradeTo(publicClassification);

        // Assert
        canDowngrade.Should().BeTrue("can downgrade from Confidential (2) to Public (0)");
    }

    [Fact]
    public void CanDowngradeTo_FromPublicToRestricted_ShouldReturnFalse()
    {
        // Arrange
        var publicClassification = SecurityClassification.Public(_testUserId);
        var restrictedClassification = SecurityClassification.Restricted(_testUserId, new List<string> { "Test" });

        // Act
        var canDowngrade = publicClassification.CanDowngradeTo(restrictedClassification);

        // Assert
        canDowngrade.Should().BeFalse("cannot downgrade from Public (0) to Restricted (3) - this is an upgrade");
    }

    [Fact]
    public void CanDowngradeTo_SameLevel_ShouldReturnFalse()
    {
        // Arrange
        var classification1 = SecurityClassification.Confidential(_testUserId, new List<string> { "Test1" });
        var classification2 = SecurityClassification.Confidential(_testUserId, new List<string> { "Test2" });

        // Act
        var canDowngrade = classification1.CanDowngradeTo(classification2);

        // Assert
        canDowngrade.Should().BeFalse("same level is not a downgrade");
    }

    #endregion

    #region WithAccessGroups Tests

    [Fact]
    public void WithAccessGroups_WithValidGroups_ShouldCreateNewClassificationWithUpdatedGroups()
    {
        // Arrange
        var originalClassification = SecurityClassification.Confidential(
            _testUserId,
            new List<string> { "Legal" });
        var newGroups = new List<string> { "Legal", "Compliance", "Executive" };
        var modifiedBy = UserId.New();

        // Act
        var updatedClassification = originalClassification.WithAccessGroups(newGroups, modifiedBy);

        // Assert
        updatedClassification.Should().NotBeNull();
        updatedClassification.Level.Should().Be(originalClassification.Level);
        updatedClassification.AccessGroups.Should().BeEquivalentTo(newGroups);
        updatedClassification.RequiresPIIHandling.Should().Be(originalClassification.RequiresPIIHandling);
        updatedClassification.ClassifiedBy.Should().Be(modifiedBy);
    }

    [Fact]
    public void WithAccessGroups_WithEmptyGroups_ShouldThrowArgumentException()
    {
        // Arrange
        var classification = SecurityClassification.Confidential(_testUserId, new List<string> { "Test" });
        var emptyGroups = new List<string>();

        // Act
        Action act = () => classification.WithAccessGroups(emptyGroups, _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newAccessGroups")
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void WithAccessGroups_WithNullGroups_ShouldThrowArgumentException()
    {
        // Arrange
        var classification = SecurityClassification.Confidential(_testUserId, new List<string> { "Test" });

        // Act
        Action act = () => classification.WithAccessGroups(null!, _testUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("newAccessGroups");
    }

    #endregion

    #region Value Object Equality Tests

    [Fact]
    public void Equals_WithDifferentLevels_ShouldReturnFalse()
    {
        // Arrange
        var publicClassification = SecurityClassification.Public(_testUserId);
        var internalClassification = SecurityClassification.Internal(_testUserId);

        // Act & Assert
        publicClassification.Should().NotBe(internalClassification);
    }

    [Fact]
    public void Equals_WithDifferentAccessGroups_ShouldReturnFalse()
    {
        // Arrange
        var classification1 = SecurityClassification.Confidential(_testUserId, new List<string> { "Legal" });
        var classification2 = SecurityClassification.Confidential(_testUserId, new List<string> { "HR" });

        // Act & Assert
        classification1.Should().NotBe(classification2, "different access groups");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldIncludeLevelGroupsAndPIIFlag()
    {
        // Arrange
        var classification = SecurityClassification.Restricted(
            _testUserId,
            new List<string> { "Executive", "Compliance" });

        // Act
        var result = classification.ToString();

        // Assert
        result.Should().Contain("Restricted");
        result.Should().Contain("Executive");
        result.Should().Contain("Compliance");
        result.Should().Contain("PII:");
        result.Should().Contain("True");
    }

    #endregion
}
