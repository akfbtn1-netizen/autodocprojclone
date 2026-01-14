using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using Enterprise.Documentation.Core.Application.Services.ExcelSync;

// Test program to directly test Excel DocId writeback
var config = new ConfigurationBuilder().Build();
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<ExcelChangeIntegratorService>();

// Create the service with the actual Excel file path
var service = new ExcelChangeIntegratorService(logger, config);

try 
{
    Console.WriteLine("Testing WriteDocIdToExcelAsync...");
    
    // Test writing DocID Test456 to row 153
    // We need to find the JIRA number for row 153 first
    var excelPath = @"C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx";
    
    if (!File.Exists(excelPath))
    {
        Console.WriteLine($"Excel file not found: {excelPath}");
        return;
    }
    
    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    
    using var package = new ExcelPackage(new FileInfo(excelPath));
    var worksheet = package.Workbook.Worksheets[0];
    
    Console.WriteLine($"Worksheet: {worksheet.Name}");
    Console.WriteLine($"Dimensions: {worksheet.Dimension.Rows} rows x {worksheet.Dimension.Columns} columns");
    
    // Find JIRA column
    int jiraColumn = -1;
    for (int col = 1; col <= worksheet.Dimension.Columns; col++)
    {
        var header = worksheet.Cells[1, col].Value?.ToString() ?? "";
        Console.WriteLine($"Column {col}: '{header}'");
        
        if (header.Contains("JIRA", StringComparison.OrdinalIgnoreCase))
        {
            jiraColumn = col;
            Console.WriteLine($"Found JIRA column at {col}");
            break;
        }
    }
    
    if (jiraColumn == -1)
    {
        Console.WriteLine("JIRA column not found");
        return;
    }
    
    // Get the JIRA number from row 153
    if (worksheet.Dimension.Rows >= 153)
    {
        var jiraNumber = worksheet.Cells[153, jiraColumn].Value?.ToString() ?? "";
        Console.WriteLine($"JIRA number at row 153: '{jiraNumber}'");
        
        if (string.IsNullOrEmpty(jiraNumber))
        {
            Console.WriteLine("No JIRA number found at row 153");
            return;
        }
        
        // Now call the actual service method
        Console.WriteLine($"Calling WriteDocIdToExcelAsync with JIRA: {jiraNumber}, DocId: Test456");
        
        await service.WriteDocIdToExcelAsync(jiraNumber, "Test456", CancellationToken.None);
        
        Console.WriteLine("WriteDocIdToExcelAsync completed!");
    }
    else
    {
        Console.WriteLine($"Row 153 doesn't exist (only {worksheet.Dimension.Rows} rows)");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}