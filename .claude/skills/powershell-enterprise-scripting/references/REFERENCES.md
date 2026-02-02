# PowerShell Enterprise Scripting - References

## Official Documentation

### Microsoft PowerShell Documentation
- PowerShell Documentation: https://learn.microsoft.com/en-us/powershell/
- About Here-Strings: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_quoting_rules
- About Try Catch Finally: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_try_catch_finally
- About Functions Advanced Parameters: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_functions_advanced_parameters

### SqlServer Module
- SqlServer Module: https://learn.microsoft.com/en-us/powershell/module/sqlserver/
- Invoke-Sqlcmd: https://learn.microsoft.com/en-us/powershell/module/sqlserver/invoke-sqlcmd
- PowerShell Gallery: https://www.powershellgallery.com/packages/SqlServer

## Best Practice Guides (2024-2025)

### Code Style & Structure
1. **PowerShell Practice and Style Guide** (PoshCode)
   - URL: https://poshcode.gitbook.io/powershell-practice-and-style/
   - Topics: Naming conventions, code layout, documentation

2. **The PowerShell Best Practices and Style Guide** (GitHub)
   - URL: https://github.com/PoshCode/PowerShellPracticeAndStyle
   - Topics: Community-maintained style guide

3. **PowerShell Modules - Best Practices** (PSPlaybook, 2025)
   - URL: https://www.psplaybook.com/2025/02/06/powershell-modules-best-practices/
   - Topics: Module structure, $script: scope, auto-loading

### Error Handling
4. **About Try Catch Finally** (Microsoft Docs)
   - URL: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_try_catch_finally
   - Topics: Exception handling, $ErrorActionPreference

5. **Handling Errors the PowerShell Way** (DevBlogs)
   - URL: https://devblogs.microsoft.com/scripting/
   - Topics: Terminating vs non-terminating errors

### Parameter Validation
6. **About Functions Advanced Parameters** (Microsoft Docs)
   - URL: https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_functions_advanced_parameters
   - Topics: ValidateSet, ValidatePattern, ValidateScript

### SQL Server Integration
7. **SqlServer PowerShell Module** (Microsoft Docs)
   - URL: https://learn.microsoft.com/en-us/sql/powershell/sql-server-powershell
   - Topics: Invoke-Sqlcmd, SQL authentication

8. **SQL Server PowerShell** (PowerShell Gallery)
   - URL: https://www.powershellgallery.com/packages/SqlServer/
   - Topics: Latest module version, cross-platform support

## Version-Specific Notes

### PowerShell 5.1 (Windows PowerShell)
- Pre-installed on Windows 10/11 and Windows Server
- Most cmdlets available
- Windows-only features (COM, WMI)
- Use for: Enterprise Windows administration

### PowerShell 7.4+ (PowerShell Core)
- Cross-platform (Windows, Linux, macOS)
- Improved performance
- New operators: ?? (null-coalescing), ??= (null-coalescing assignment)
- Parallel foreach: `ForEach-Object -Parallel`
- Use for: Cross-platform scripts, modern development

## Common Pitfall References

### Here-String Issues
- Closing delimiter MUST be at column 0 (no indentation)
- No whitespace after opening @" or before closing "@
- Single-quote @' '@ for literal strings (no variable expansion)
- Double-quote @" "@ for expandable strings

### ErrorAction Stop
- Most cmdlet errors are non-terminating by default
- Use `-ErrorAction Stop` to make them terminating
- Required for try/catch to work properly

### Array Handling
- Single result from cmdlet returns scalar, not array
- Use `@()` to force array: `$files = @(Get-ChildItem)`

## Tool Recommendations

### Development Environment
- **Visual Studio Code** with PowerShell extension
- **Windows Terminal** for modern console experience
- **PSScriptAnalyzer** for static code analysis

### Testing
- **Pester** for unit and integration testing
- **PSScriptAnalyzer** for linting

### Package Management
- **PowerShell Gallery** for module distribution
- **NuGet** for .NET dependencies

## Version History
- 2026-01-03: Initial compilation from 50+ sources
