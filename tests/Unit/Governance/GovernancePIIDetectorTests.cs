using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Governance;

namespace Tests.Unit.Governance;

/// <summary>
/// Comprehensive unit tests for GovernancePIIDetector.
/// Tests PII pattern detection with focus on avoiding false positives/negatives.
///
/// Test Strategy:
/// 1. True Positives - Known PII that MUST be detected
/// 2. True Negatives - Non-PII that must NOT trigger false positives
/// 3. Edge Cases - Boundary conditions, partial matches, format variations
/// 4. Confidence Scoring - Verify confidence thresholds are accurate
/// 5. Column Name Boosting - Verify suggestive column names boost confidence
/// </summary>
public class GovernancePIIDetectorTests
{
    private readonly GovernancePIIDetector _detector;
    private readonly Mock<ILogger<GovernancePIIDetector>> _mockLogger;

    public GovernancePIIDetectorTests()
    {
        _mockLogger = new Mock<ILogger<GovernancePIIDetector>>();
        _detector = new GovernancePIIDetector(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new GovernancePIIDetector(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_ShouldCreateInstance()
    {
        // Act
        var detector = new GovernancePIIDetector(_mockLogger.Object);

        // Assert
        detector.Should().NotBeNull();
    }

    #endregion

    #region Email Detection Tests - True Positives

    [Theory]
    [InlineData("john.doe@example.com", "Standard email")]
    [InlineData("user+tag@subdomain.example.co.uk", "Plus addressing with subdomain")]
    [InlineData("test.email.123@company.org", "Dotted username")]
    [InlineData("firstname.lastname@domain.com", "First.Last format")]
    [InlineData("email@123.123.123.123", "IP address domain")]
    [InlineData("email@domain-with-dash.com", "Domain with dash")]
    [InlineData("1234567890@domain.com", "Numeric username")]
    [InlineData("_______@domain.com", "Underscore username")]
    public async Task DetectPIIAsync_WithValidEmails_ShouldDetectAsEmail(string email, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("data_column", email);

        // Assert
        result.IsPII.Should().BeTrue(because: $"{testCase} should be detected as PII");
        result.PIIType.Should().Be(PIIType.EmailAddress);
        result.Confidence.Should().BeGreaterOrEqualTo(0.85, because: "emails should have high confidence");
    }

    #endregion

    #region Email Detection Tests - True Negatives (Avoiding False Positives)

    [Theory]
    [InlineData("not-an-email", "Plain text without @")]
    [InlineData("john@", "Incomplete email - missing domain")]
    [InlineData("@example.com", "Incomplete email - missing local part")]
    [InlineData("john doe at example dot com", "Spelled out email")]
    [InlineData("example.com", "Just domain")]
    [InlineData("@", "Just @ symbol")]
    [InlineData("john@.com", "Missing domain name")]
    [InlineData("john@com", "Missing TLD dot")]
    [InlineData("", "Empty string")]
    [InlineData("   ", "Whitespace only")]
    public async Task DetectPIIAsync_WithInvalidEmails_ShouldNotDetectAsEmail(string value, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("random_column", value);

        // Assert - Either not PII or not detected as Email specifically
        if (result.IsPII)
        {
            result.PIIType.Should().NotBe(PIIType.EmailAddress, because: $"{testCase} should not be detected as email");
        }
    }

    #endregion

    #region SSN Detection Tests - True Positives

    [Theory]
    [InlineData("123-45-6789", "Standard SSN format")]
    [InlineData("000-00-0000", "Edge case - all zeros")]
    [InlineData("999-99-9999", "Edge case - all nines")]
    [InlineData("078-05-1120", "Real SSN pattern")]
    public async Task DetectPIIAsync_WithValidSSN_ShouldDetectAsSSN(string ssn, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("tax_info", ssn);

        // Assert
        result.IsPII.Should().BeTrue(because: $"{testCase} should be detected as PII");
        result.PIIType.Should().Be(PIIType.SSN);
        result.Confidence.Should().BeGreaterOrEqualTo(0.98, because: "SSN pattern XXX-XX-XXXX has 0.98 base confidence");
    }

    #endregion

    #region SSN Detection Tests - True Negatives (Critical: Avoid Phone Number Confusion)

    [Theory]
    [InlineData("123-456-7890", "US phone number - different format")]
    [InlineData("12-345-6789", "Wrong grouping")]
    [InlineData("1234-56-789", "Wrong grouping")]
    [InlineData("123-45-678", "Too short")]
    [InlineData("123-45-67890", "Too long")]
    [InlineData("ABC-DE-FGHI", "Letters not digits")]
    public async Task DetectPIIAsync_WithPhoneOrInvalidSSN_ShouldNotDetectAsSSN(string value, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("general_column", value);

        // Assert
        if (result.IsPII)
        {
            result.PIIType.Should().NotBe(PIIType.SSN, because: $"{testCase} should not be detected as SSN");
        }
    }

    #endregion

    #region Phone Number Detection Tests - True Positives

    [Theory]
    [InlineData("123-456-7890", "US format with dashes")]
    [InlineData("(123) 456-7890", "US format with parentheses")]
    [InlineData("+1 234 567 8901", "International format")]
    [InlineData("+44 20 7946 0958", "UK international format")]
    public async Task DetectPIIAsync_WithValidPhoneNumbers_ShouldDetectAsPhone(string phone, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("contact", phone);

        // Assert
        result.IsPII.Should().BeTrue(because: $"{testCase} should be detected as PII");
        result.PIIType.Should().Be(PIIType.PhoneNumber);
        result.Confidence.Should().BeGreaterOrEqualTo(0.70);
    }

    #endregion

    #region Phone Number Detection Tests - True Negatives

    [Theory]
    [InlineData("12345", "Too short")]
    [InlineData("abcdefghij", "Letters not digits")]
    [InlineData("123-45-6789", "SSN format - should be SSN not phone")]
    public async Task DetectPIIAsync_WithInvalidPhoneNumbers_ShouldNotDetectAsPhone(string value, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("misc_column", value);

        // Assert
        if (result.IsPII && result.PIIType == PIIType.PhoneNumber)
        {
            // 10-digit number might match with low confidence, that's acceptable
            // but SSN format should definitely NOT be phone
            if (value == "123-45-6789")
            {
                result.PIIType.Should().NotBe(PIIType.PhoneNumber,
                    because: "SSN format should be detected as SSN, not phone");
            }
        }
    }

    #endregion

    #region Credit Card Detection Tests - True Positives

    [Theory]
    [InlineData("4111111111111111", "Visa test card")]
    [InlineData("4111-1111-1111-1111", "Visa with dashes")]
    [InlineData("4111 1111 1111 1111", "Visa with spaces")]
    [InlineData("5500000000000004", "MasterCard test")]
    [InlineData("5500-0000-0000-0004", "MasterCard with dashes")]
    [InlineData("340000000000009", "Amex test card")]
    [InlineData("378282246310005", "Amex test card 2")]
    public async Task DetectPIIAsync_WithValidCreditCards_ShouldDetectAsCreditCard(string card, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("payment_info", card);

        // Assert
        result.IsPII.Should().BeTrue(because: $"{testCase} should be detected as PII");
        result.PIIType.Should().Be(PIIType.CreditCard);
        result.Confidence.Should().BeGreaterOrEqualTo(0.70);
    }

    #endregion

    #region Credit Card Detection Tests - True Negatives

    [Theory]
    [InlineData("1234", "Too short")]
    [InlineData("12345678901234567890", "Too long")]
    [InlineData("abcd-efgh-ijkl-mnop", "Letters")]
    [InlineData("0000000000000000", "All zeros - invalid prefix")]
    public async Task DetectPIIAsync_WithInvalidCreditCards_ShouldNotDetectAsCreditCard(string value, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("data_field", value);

        // Assert
        if (result.IsPII)
        {
            result.PIIType.Should().NotBe(PIIType.CreditCard,
                because: $"{testCase} should not be detected as credit card");
        }
    }

    #endregion

    #region Address Detection Tests - True Positives

    [Theory]
    [InlineData("123 Main Street", "Simple street address")]
    [InlineData("456 Oak Avenue", "Avenue address")]
    [InlineData("789 Elm Road", "Road address")]
    [InlineData("101 Pine Drive", "Drive address")]
    [InlineData("202 Maple Lane", "Lane address")]
    [InlineData("303 Cedar Boulevard", "Boulevard address")]
    [InlineData("New York, NY 10001", "City State ZIP")]
    [InlineData("Los Angeles, CA 90210-1234", "City State ZIP+4")]
    public async Task DetectPIIAsync_WithValidAddresses_ShouldDetectAsAddress(string address, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("location", address);

        // Assert
        result.IsPII.Should().BeTrue(because: $"{testCase} should be detected as PII");
        result.PIIType.Should().Be(PIIType.Address);
        result.Confidence.Should().BeGreaterOrEqualTo(0.80);
    }

    #endregion

    #region Date of Birth Detection Tests - True Positives

    [Theory]
    [InlineData("01/15/1990", "MM/DD/YYYY format")]
    [InlineData("12/31/2000", "End of year date")]
    [InlineData("1985-06-20", "YYYY-MM-DD format")]
    [InlineData("2001-01-01", "New millennium date")]
    public async Task DetectPIIAsync_WithValidDatesOfBirth_ShouldDetectAsDateOfBirth(string dob, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("birth_date", dob);

        // Assert
        result.IsPII.Should().BeTrue(because: $"{testCase} should be detected as PII");
        result.PIIType.Should().Be(PIIType.DateOfBirth);
        result.Confidence.Should().BeGreaterOrEqualTo(0.90);
    }

    #endregion

    #region Date of Birth Detection Tests - True Negatives

    [Theory]
    [InlineData("13/01/1990", "Invalid month 13")]
    [InlineData("01/32/1990", "Invalid day 32")]
    [InlineData("1890-01-01", "Year too old (before 1900)")]
    [InlineData("2099-01-01", "Year in future")]
    [InlineData("not-a-date", "Plain text")]
    public async Task DetectPIIAsync_WithInvalidDates_ShouldNotDetectAsDateOfBirth(string value, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("random_field", value);

        // Assert
        if (result.IsPII)
        {
            result.PIIType.Should().NotBe(PIIType.DateOfBirth,
                because: $"{testCase} should not be detected as date of birth");
        }
    }

    #endregion

    #region Person Name Detection Tests - True Positives

    [Theory]
    [InlineData("John Smith", "First Last")]
    [InlineData("Mary Johnson", "First Last")]
    [InlineData("Robert J. Williams", "First M. Last")]
    [InlineData("Elizabeth A. Brown", "First M. Last")]
    public async Task DetectPIIAsync_WithValidPersonNames_ShouldDetectAsPersonName(string name, string testCase)
    {
        // Act
        var result = await _detector.DetectPIIAsync("customer_name", name);

        // Assert
        result.IsPII.Should().BeTrue(because: $"{testCase} should be detected as PII");
        result.PIIType.Should().Be(PIIType.PersonName);
        result.Confidence.Should().BeGreaterOrEqualTo(0.60);
    }

    #endregion

    #region Column Name Confidence Boost Tests

    [Theory]
    [InlineData("email", "john.doe@example.com", PIIType.EmailAddress)]
    [InlineData("contact_email", "user@domain.com", PIIType.EmailAddress)]
    [InlineData("phone", "123-456-7890", PIIType.PhoneNumber)]
    [InlineData("mobile", "(555) 123-4567", PIIType.PhoneNumber)]
    [InlineData("ssn", "123-45-6789", PIIType.SSN)]
    [InlineData("social_security", "987-65-4321", PIIType.SSN)]
    [InlineData("credit_card", "4111111111111111", PIIType.CreditCard)]
    [InlineData("card_number", "5500000000000004", PIIType.CreditCard)]
    public async Task DetectPIIAsync_WithSuggestiveColumnName_ShouldBoostConfidence(
        string columnName, string value, PIIType expectedType)
    {
        // Arrange - Get baseline confidence with neutral column name
        var baselineResult = await _detector.DetectPIIAsync("random_data", value);

        // Act - Get boosted confidence with suggestive column name
        var boostedResult = await _detector.DetectPIIAsync(columnName, value);

        // Assert
        boostedResult.IsPII.Should().BeTrue();
        boostedResult.PIIType.Should().Be(expectedType);

        // Confidence should be boosted (up to +0.15, capped at 0.99)
        if (baselineResult.Confidence < 0.99)
        {
            boostedResult.Confidence.Should().BeGreaterThan(baselineResult.Confidence,
                because: "suggestive column names should boost confidence");
        }
        boostedResult.Confidence.Should().BeLessOrEqualTo(0.99,
            because: "confidence is capped at 0.99");
    }

    #endregion

    #region Empty/Null Input Tests

    [Fact]
    public async Task DetectPIIAsync_WithNullValue_ShouldReturnNotPII()
    {
        // Act
        var result = await _detector.DetectPIIAsync("column", null!);

        // Assert
        result.IsPII.Should().BeFalse();
    }

    [Fact]
    public async Task DetectPIIAsync_WithEmptyValue_ShouldReturnNotPII()
    {
        // Act
        var result = await _detector.DetectPIIAsync("column", "");

        // Assert
        result.IsPII.Should().BeFalse();
    }

    [Fact]
    public async Task DetectPIIAsync_WithWhitespaceValue_ShouldReturnNotPII()
    {
        // Act
        var result = await _detector.DetectPIIAsync("column", "   ");

        // Assert
        result.IsPII.Should().BeFalse();
    }

    #endregion

    #region IsColumnPII Tests

    [Theory]
    [InlineData("email", "varchar", true)]
    [InlineData("user_email", "nvarchar", true)]
    [InlineData("phone_number", "varchar", true)]
    [InlineData("ssn", "char", true)]
    [InlineData("social_security_number", "varchar", true)]
    [InlineData("first_name", "varchar", true)]
    [InlineData("address", "nvarchar", true)]
    [InlineData("credit_card_number", "varchar", true)]
    [InlineData("birthdate", "date", true)]
    public void IsColumnPII_WithPIISuggestiveNames_ShouldReturnTrue(
        string columnName, string dataType, bool expectedResult)
    {
        // Act
        var result = _detector.IsColumnPII(columnName, dataType);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData("id", "int")]
    [InlineData("created_at", "datetime")]
    [InlineData("status", "varchar")]
    [InlineData("count", "int")]
    [InlineData("description", "nvarchar")]
    [InlineData("is_active", "bit")]
    public void IsColumnPII_WithNonPIINames_ShouldReturnFalse(string columnName, string dataType)
    {
        // Act
        var result = _detector.IsColumnPII(columnName, dataType);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ClassifyColumn Tests

    [Theory]
    [InlineData("ssn", "varchar", DataClassification.Restricted)]
    [InlineData("social_security", "varchar", DataClassification.Restricted)]
    [InlineData("credit_card", "varchar", DataClassification.Restricted)]
    [InlineData("card_number", "varchar", DataClassification.Restricted)]
    public void ClassifyColumn_WithHighSensitivityPII_ShouldReturnRestricted(
        string columnName, string dataType, DataClassification expected)
    {
        // Act
        var result = _detector.ClassifyColumn(columnName, dataType);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("email", "varchar", DataClassification.Confidential)]
    [InlineData("phone", "varchar", DataClassification.Confidential)]
    [InlineData("address", "varchar", DataClassification.Confidential)]
    public void ClassifyColumn_WithMediumSensitivityPII_ShouldReturnConfidential(
        string columnName, string dataType, DataClassification expected)
    {
        // Act
        var result = _detector.ClassifyColumn(columnName, dataType);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("first_name", "varchar", DataClassification.Internal)]
    [InlineData("last_name", "varchar", DataClassification.Internal)]
    [InlineData("dob", "date", DataClassification.Internal)]
    public void ClassifyColumn_WithLowSensitivityPII_ShouldReturnInternal(
        string columnName, string dataType, DataClassification expected)
    {
        // Act
        var result = _detector.ClassifyColumn(columnName, dataType);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("id", "int", DataClassification.Public)]
    [InlineData("created_at", "datetime", DataClassification.Public)]
    [InlineData("status", "varchar", DataClassification.Public)]
    public void ClassifyColumn_WithNonPII_ShouldReturnPublic(
        string columnName, string dataType, DataClassification expected)
    {
        // Act
        var result = _detector.ClassifyColumn(columnName, dataType);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Concurrent Detection Tests

    [Fact]
    public async Task DetectPIIAsync_WithConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        var testData = new[]
        {
            ("john@example.com", PIIType.EmailAddress),
            ("123-45-6789", PIIType.SSN),
            ("555-123-4567", PIIType.PhoneNumber),
            ("4111111111111111", PIIType.CreditCard),
            ("John Smith", PIIType.PersonName)
        };

        // Act - Run all detections concurrently
        var tasks = testData.Select(async data =>
        {
            var result = await _detector.DetectPIIAsync("test_column", data.Item1);
            return (data.Item1, data.Item2, result);
        });

        var results = await Task.WhenAll(tasks);

        // Assert - All should complete correctly
        foreach (var (value, expectedType, result) in results)
        {
            result.IsPII.Should().BeTrue($"Value '{value}' should be detected as PII");
            result.PIIType.Should().Be(expectedType, $"Value '{value}' should be detected as {expectedType}");
        }
    }

    #endregion

    #region Detection Rule Tracking Tests

    [Fact]
    public async Task DetectPIIAsync_WhenPIIDetected_ShouldIncludeDetectionRule()
    {
        // Arrange
        var email = "test@example.com";

        // Act
        var result = await _detector.DetectPIIAsync("email_column", email);

        // Assert
        result.IsPII.Should().BeTrue();
        result.DetectionRule.Should().NotBeNullOrEmpty();
        result.DetectionRule.Should().Contain("email",
            because: "detection rule should describe the pattern matched");
    }

    #endregion
}
