// <copyright file="OpenXmlTemplateTests.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>
using System.IO;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Tests.Unit.Services;

/// <summary>
/// Unit tests for OpenXML template generation.
/// </summary>
public class OpenXmlTemplateTests
{
    private readonly DocGeneratorService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenXmlTemplateTests"/> class.
    /// </summary>
    public OpenXmlTemplateTests()
    {
        var logger = new NullLogger<DocGeneratorService>();
        _service = new DocGeneratorService(logger);
    }

    /// <summary>
    /// Test that business request template generates without errors.
    /// </summary>
    [Fact]
    public async Task BusinessRequestTemplate_Generate_ShouldCreateDocument()
    {
        // Arrange
        var data = (BusinessRequestTemplate.BusinessRequestData)_service.CreateSampleData("BusinessRequest");

        // Act
        using var stream = await _service.GenerateDocumentAsync("BusinessRequest", data);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
        stream.Position = 0;
        
        // Verify it's a valid Word document by checking for OpenXML headers
        var buffer = new byte[4];
        await stream.ReadAsync(buffer, 0, 4);
        Assert.Equal(0x50, buffer[0]); // 'P' - ZIP file signature (Word documents are ZIP files)
        Assert.Equal(0x4B, buffer[1]); // 'K'
    }

    /// <summary>
    /// Test that enhancement template generates without errors.
    /// </summary>
    [Fact]
    public async Task EnhancementTemplate_Generate_ShouldCreateDocument()
    {
        // Arrange
        var data = (EnhancementTemplate.EnhancementData)_service.CreateSampleData("Enhancement");

        // Act
        using var stream = await _service.GenerateDocumentAsync("Enhancement", data);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }

    /// <summary>
    /// Test that defect template generates without errors.
    /// </summary>
    [Fact]
    public async Task DefectTemplate_Generate_ShouldCreateDocument()
    {
        // Arrange
        var data = (DefectTemplate.DefectData)_service.CreateSampleData("Defect");

        // Act
        using var stream = await _service.GenerateDocumentAsync("Defect", data);

        // Assert
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }

    /// <summary>
    /// Test template data validation.
    /// </summary>
    [Fact]
    public void ValidateTemplateData_ValidData_ShouldReturnTrue()
    {
        // Arrange
        var data = _service.CreateSampleData("BusinessRequest");

        // Act
        var isValid = _service.ValidateTemplateData("BusinessRequest", data);

        // Assert
        Assert.True(isValid);
    }

    /// <summary>
    /// Test available template types.
    /// </summary>
    [Fact]
    public void GetAvailableTemplateTypes_ShouldReturnExpectedTypes()
    {
        // Act
        var types = _service.GetAvailableTemplateTypes();

        // Assert
        Assert.Contains("BusinessRequest", types);
        Assert.Contains("Enhancement", types);
        Assert.Contains("Defect", types);
    }
}