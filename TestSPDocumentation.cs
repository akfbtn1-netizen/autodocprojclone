// Simple test to check if SP documentation files are generated
using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Application.Services.Documentation;

namespace Enterprise.Documentation.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 SP DOCUMENTATION TEST - PHASE 6");
            Console.WriteLine("=====================================");

            // Configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("src/Api/appsettings.json", optional: false)
                .AddJsonFile("src/Api/appsettings.Development.json", optional: true)
                .Build();

            // Test data from our previous setup
            var procedureName = "usp_TestDocumentation_V1";
            var changeDocumentId = "74a2d18b-277d-4cdb-bcb9-86864ac760c9";

            Console.WriteLine($"Testing SP: {procedureName}");
            Console.WriteLine($"ChangeDocumentId: {changeDocumentId}");
            
            // Check if documentation directory exists
            var docBasePath = @"C:\Users\Alexander.Kirby\Desktop\Doctest\Documentation-Catalog\Database\IRFS1\DaQa\StoredProcedures\";
            Console.WriteLine($"\n📁 Checking documentation directory: {docBasePath}");
            
            if (Directory.Exists(docBasePath))
            {
                Console.WriteLine("✅ Documentation directory exists");
                
                // Look for our SP documentation file
                var searchPattern = $"*{procedureName}*.docx";
                var files = Directory.GetFiles(docBasePath, searchPattern, SearchOption.AllDirectories);
                
                Console.WriteLine($"\n🔍 Searching for files matching: {searchPattern}");
                
                if (files.Length > 0)
                {
                    Console.WriteLine("✅ SP DOCUMENTATION FILES FOUND:");
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        Console.WriteLine($"   📄 {file}");
                        Console.WriteLine($"      Size: {fileInfo.Length} bytes");
                        Console.WriteLine($"      Created: {fileInfo.CreationTime}");
                        Console.WriteLine($"      Modified: {fileInfo.LastWriteTime}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ No SP documentation files found yet");
                    Console.WriteLine("\nListing all .docx files in directory:");
                    var allDocx = Directory.GetFiles(docBasePath, "*.docx", SearchOption.AllDirectories);
                    if (allDocx.Length > 0)
                    {
                        foreach (var file in allDocx)
                        {
                            Console.WriteLine($"   📄 {Path.GetFileName(file)}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("   No .docx files found in directory");
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ Documentation directory does not exist");
                Console.WriteLine("This suggests the StoredProcedureDocumentationService hasn't run yet");
            }

            // Check MasterIndex for entries
            Console.WriteLine("\n📊 Checking DaQa.MasterIndex table...");
            
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                try
                {
                    using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                    await connection.OpenAsync();
                    
                    var query = @"
                        SELECT TOP 10 
                            ObjectName,
                            DocumentPath,
                            ObjectType,
                            LastUpdated,
                            Version
                        FROM DaQa.MasterIndex 
                        WHERE ObjectName LIKE @ProcedureName 
                        ORDER BY LastUpdated DESC";
                    
                    using var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ProcedureName", $"%{procedureName}%");
                    
                    using var reader = await command.ExecuteReaderAsync();
                    
                    if (reader.HasRows)
                    {
                        Console.WriteLine("✅ MasterIndex entries found:");
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine($"   📋 {reader["ObjectName"]}");
                            Console.WriteLine($"      Path: {reader["DocumentPath"]}");
                            Console.WriteLine($"      Type: {reader["ObjectType"]}");
                            Console.WriteLine($"      Version: {reader["Version"]}");
                            Console.WriteLine($"      Updated: {reader["LastUpdated"]}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ No MasterIndex entries found for this SP");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Database error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌ No connection string found");
            }

            Console.WriteLine("\n🔍 SUMMARY:");
            Console.WriteLine("If no files or MasterIndex entries were found, the StoredProcedureDocumentationService");
            Console.WriteLine("may not have been triggered yet, or there might be an issue with the workflow.");
            Console.WriteLine("\nNext steps:");
            Console.WriteLine("1. Check API logs for any StoredProcedureDocumentationService execution");
            Console.WriteLine("2. Verify the ApprovalTrackingService is monitoring ApprovalWorkflow table");
            Console.WriteLine("3. Manually trigger the service if needed");
        }
    }
}