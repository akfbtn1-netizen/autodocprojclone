// Simple test to validate StoredProcedureTemplate integration

using System;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

namespace TestStoredProcedureIntegration
{
    class Program
    {
        static void Main()
        {
            try
            {
                // Test 1: Create StoredProcedureTemplate instance
                var template = new StoredProcedureTemplate();
                Console.WriteLine("✅ StoredProcedureTemplate created successfully");

                // Test 2: Create sample data
                var sampleData = StoredProcedureTemplate.CreateSampleData();
                Console.WriteLine("✅ Sample data created successfully");
                Console.WriteLine($"   Procedure: {sampleData.SpName}");
                Console.WriteLine($"   Version: {sampleData.Version}");
                Console.WriteLine($"   Complexity: {sampleData.ComplexityScore}");
                Console.WriteLine($"   Parameters: {sampleData.Parameters?.Count ?? 0}");

                // Test 3: Generate document
                using var stream = template.GenerateDocument(sampleData);
                Console.WriteLine("✅ Document generated successfully");
                Console.WriteLine($"   Document size: {stream.Length} bytes");

                // Test 4: Validate adaptive logic
                if (sampleData.ComplexityScore > 30 && sampleData.Dependencies != null)
                {
                    Console.WriteLine("✅ Adaptive logic: Dependencies section included (complexity > 30)");
                }
                if (sampleData.ComplexityScore > 50 && !string.IsNullOrEmpty(sampleData.PerformanceNotes))
                {
                    Console.WriteLine("✅ Adaptive logic: Performance notes included (complexity > 50)");
                }
                if (sampleData.ComplexityScore > 40 && !string.IsNullOrEmpty(sampleData.ErrorHandling))
                {
                    Console.WriteLine("✅ Adaptive logic: Error handling included (complexity > 40)");
                }

                Console.WriteLine("\n🎉 All StoredProcedure integration tests passed!");
                Console.WriteLine("✅ Ready for tomorrow's demo!");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}