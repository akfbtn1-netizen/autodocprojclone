# ============================================================================
# SETUP ROSLYN ANALYZERS
# ============================================================================
# Adds analyzer packages and creates .editorconfig with rules
# ============================================================================

param(
    [string]$ProjectPath = "C:\Projects\EnterpriseDocumentationPlatform.V2"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  ROSLYN ANALYZER SETUP" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# 1. ADD ANALYZER PACKAGES
# ============================================================================
Write-Host "[1/3] Adding Roslyn Analyzer Packages..." -ForegroundColor Yellow

$analyzerPackages = @(
    @{ Name = "Microsoft.CodeAnalysis.NetAnalyzers"; Version = "8.0.0" },
    @{ Name = "StyleCop.Analyzers"; Version = "1.1.118" },
    @{ Name = "Roslynator.Analyzers"; Version = "4.7.0" },
    @{ Name = "SonarAnalyzer.CSharp"; Version = "9.16.0.82469" },
    @{ Name = "AsyncFixer"; Version = "1.6.0" },
    @{ Name = "SecurityCodeScan.VS2019"; Version = "5.6.7" }
)

# Find all project files
$projectFiles = Get-ChildItem -Path $ProjectPath -Filter "*.csproj" -Recurse

Write-Host ""
Write-Host "  Packages to install:" -ForegroundColor White
foreach ($pkg in $analyzerPackages) {
    Write-Host "    - $($pkg.Name) v$($pkg.Version)" -ForegroundColor Cyan
}
Write-Host ""

Write-Host "  Run these commands in each project directory:" -ForegroundColor Yellow
Write-Host ""

foreach ($pkg in $analyzerPackages) {
    Write-Host "  dotnet add package $($pkg.Name) --version $($pkg.Version)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  Or add to Directory.Build.props for all projects:" -ForegroundColor Yellow
Write-Host ""

# ============================================================================
# 2. CREATE DIRECTORY.BUILD.PROPS
# ============================================================================
Write-Host "[2/3] Creating Directory.Build.props..." -ForegroundColor Yellow

$directoryBuildProps = @"
<Project>
  <PropertyGroup>
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>Recommended</AnalysisMode>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Microsoft .NET Analyzers (built-in, just enable) -->

    <!-- StyleCop for code style consistency -->
    <PackageReference Include="StyleCop.Analyzers" Version="1.1.118">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>

    <!-- Roslynator for 500+ analyzers and refactorings -->
    <PackageReference Include="Roslynator.Analyzers" Version="4.7.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>

    <!-- AsyncFixer for async/await best practices -->
    <PackageReference Include="AsyncFixer" Version="1.6.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>

    <!-- Security Code Scan for security vulnerabilities -->
    <PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
"@

$propsPath = Join-Path $ProjectPath "Directory.Build.props"
[System.IO.File]::WriteAllText($propsPath, $directoryBuildProps, [System.Text.UTF8Encoding]::new($false))
Write-Host "  Created: $propsPath" -ForegroundColor Green

# ============================================================================
# 3. CREATE .EDITORCONFIG
# ============================================================================
Write-Host "[3/3] Creating .editorconfig with analyzer rules..." -ForegroundColor Yellow

$editorconfig = @"
# EditorConfig - Enterprise Documentation Platform
# https://editorconfig.org

root = true

# All files
[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# C# files
[*.cs]

#### .NET Coding Conventions ####

# Organize usings
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

# this. and Me. preferences
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_event = false:suggestion

# Language keywords vs BCL types preferences
dotnet_style_predefined_type_for_locals_parameters_members = true:suggestion
dotnet_style_predefined_type_for_member_access = true:suggestion

# Parentheses preferences
dotnet_style_parentheses_in_arithmetic_binary_operators = always_for_clarity:silent
dotnet_style_parentheses_in_relational_binary_operators = always_for_clarity:silent
dotnet_style_parentheses_in_other_binary_operators = always_for_clarity:silent
dotnet_style_parentheses_in_other_operators = never_if_unnecessary:silent

# Modifier preferences
dotnet_style_require_accessibility_modifiers = for_non_interface_members:suggestion
dotnet_style_readonly_field = true:warning

# Expression-level preferences
dotnet_style_object_initializer = true:suggestion
dotnet_style_collection_initializer = true:suggestion
dotnet_style_explicit_tuple_names = true:suggestion
dotnet_style_null_propagation = true:suggestion
dotnet_style_coalesce_expression = true:suggestion
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:suggestion
dotnet_style_prefer_inferred_tuple_names = true:suggestion
dotnet_style_prefer_inferred_anonymous_type_member_names = true:suggestion
dotnet_style_prefer_auto_properties = true:suggestion
dotnet_style_prefer_conditional_expression_over_assignment = true:silent
dotnet_style_prefer_conditional_expression_over_return = true:silent
dotnet_style_prefer_simplified_boolean_expressions = true:suggestion
dotnet_style_prefer_compound_assignment = true:suggestion
dotnet_style_prefer_simplified_interpolation = true:suggestion

# Namespace preferences
dotnet_style_namespace_match_folder = true:suggestion

# Parameter preferences
dotnet_code_quality_unused_parameters = all:warning

# Suppression preferences
dotnet_remove_unnecessary_suppression_exclusions = none

#### C# Coding Conventions ####

# var preferences
csharp_style_var_for_built_in_types = false:silent
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:silent

# Expression-bodied members
csharp_style_expression_bodied_methods = when_on_single_line:silent
csharp_style_expression_bodied_constructors = false:silent
csharp_style_expression_bodied_operators = when_on_single_line:silent
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_expression_bodied_indexers = true:suggestion
csharp_style_expression_bodied_accessors = true:suggestion
csharp_style_expression_bodied_lambdas = true:suggestion
csharp_style_expression_bodied_local_functions = when_on_single_line:silent

# Pattern matching preferences
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion
csharp_style_prefer_switch_expression = true:suggestion
csharp_style_prefer_pattern_matching = true:suggestion
csharp_style_prefer_not_pattern = true:suggestion

# Null-checking preferences
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion

# Code-block preferences
csharp_prefer_braces = true:suggestion
csharp_prefer_simple_using_statement = true:suggestion

# 'using' directive preferences
csharp_using_directive_placement = outside_namespace:warning

# Modifier preferences
csharp_prefer_static_local_function = true:suggestion
csharp_preferred_modifier_order = public,private,protected,internal,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,volatile,async:suggestion

# New line preferences
csharp_style_namespace_declarations = file_scoped:suggestion

#### C# Formatting Rules ####

# New line preferences
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_members_in_anonymous_types = true
csharp_new_line_between_query_expression_clauses = true

# Indentation preferences
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_indent_labels = one_less_than_current
csharp_indent_block_contents = true
csharp_indent_braces = false
csharp_indent_case_contents_when_block = true

# Space preferences
csharp_space_after_cast = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_between_parentheses = false
csharp_space_before_colon_in_inheritance_clause = true
csharp_space_after_colon_in_inheritance_clause = true
csharp_space_around_binary_operators = before_and_after
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_space_between_method_declaration_empty_parameter_list_parentheses = false
csharp_space_between_method_declaration_name_and_open_parenthesis = false
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_method_call_empty_parameter_list_parentheses = false
csharp_space_between_method_call_name_and_opening_parenthesis = false
csharp_space_after_comma = true
csharp_space_before_comma = false
csharp_space_after_dot = false
csharp_space_before_dot = false
csharp_space_after_semicolon_in_for_statement = true
csharp_space_before_semicolon_in_for_statement = false
csharp_space_around_declaration_statements = false
csharp_space_before_open_square_brackets = false
csharp_space_between_empty_square_brackets = false
csharp_space_between_square_brackets = false

# Wrapping preferences
csharp_preserve_single_line_statements = true
csharp_preserve_single_line_blocks = true

#### Naming Rules ####

# Interfaces should begin with I
dotnet_naming_rule.interface_should_be_begins_with_i.severity = warning
dotnet_naming_rule.interface_should_be_begins_with_i.symbols = interface
dotnet_naming_rule.interface_should_be_begins_with_i.style = begins_with_i
dotnet_naming_symbols.interface.applicable_kinds = interface
dotnet_naming_symbols.interface.applicable_accessibilities = public, internal, private, protected, protected_internal, private_protected
dotnet_naming_style.begins_with_i.required_prefix = I
dotnet_naming_style.begins_with_i.capitalization = pascal_case

# Async methods should end with Async
dotnet_naming_rule.async_methods_should_end_with_async.severity = suggestion
dotnet_naming_rule.async_methods_should_end_with_async.symbols = async_methods
dotnet_naming_rule.async_methods_should_end_with_async.style = end_with_async
dotnet_naming_symbols.async_methods.applicable_kinds = method
dotnet_naming_symbols.async_methods.required_modifiers = async
dotnet_naming_style.end_with_async.required_suffix = Async
dotnet_naming_style.end_with_async.capitalization = pascal_case

# Private fields should be _camelCase
dotnet_naming_rule.private_fields_should_be_camel_case.severity = suggestion
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_with_underscore
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.camel_case_with_underscore.required_prefix = _
dotnet_naming_style.camel_case_with_underscore.capitalization = camel_case

# Constants should be UPPER_CASE
dotnet_naming_rule.constants_should_be_upper_case.severity = suggestion
dotnet_naming_rule.constants_should_be_upper_case.symbols = constants
dotnet_naming_rule.constants_should_be_upper_case.style = constant_style
dotnet_naming_symbols.constants.applicable_kinds = field, local
dotnet_naming_symbols.constants.required_modifiers = const
dotnet_naming_style.constant_style.capitalization = all_upper
dotnet_naming_style.constant_style.word_separator = _

#### Analyzer Severity ####

# Microsoft.CodeAnalysis.NetAnalyzers
dotnet_diagnostic.CA1001.severity = warning      # Types that own disposable fields
dotnet_diagnostic.CA1031.severity = suggestion   # Do not catch general exceptions
dotnet_diagnostic.CA1032.severity = warning      # Implement standard exception constructors
dotnet_diagnostic.CA1054.severity = suggestion   # URI parameters should not be strings
dotnet_diagnostic.CA1062.severity = warning      # Validate arguments of public methods
dotnet_diagnostic.CA1303.severity = none         # Do not pass literals as localized
dotnet_diagnostic.CA1304.severity = suggestion   # Specify CultureInfo
dotnet_diagnostic.CA1305.severity = suggestion   # Specify IFormatProvider
dotnet_diagnostic.CA1307.severity = suggestion   # Specify StringComparison
dotnet_diagnostic.CA1308.severity = suggestion   # Normalize strings to uppercase
dotnet_diagnostic.CA1310.severity = warning      # Specify StringComparison for correctness
dotnet_diagnostic.CA1707.severity = warning      # Remove underscores from member names
dotnet_diagnostic.CA1711.severity = suggestion   # Identifiers should not have incorrect suffix
dotnet_diagnostic.CA1716.severity = suggestion   # Identifiers should not match keywords
dotnet_diagnostic.CA1720.severity = suggestion   # Identifier contains type name
dotnet_diagnostic.CA1724.severity = suggestion   # Type names should not match namespaces
dotnet_diagnostic.CA1805.severity = suggestion   # Do not initialize unnecessarily
dotnet_diagnostic.CA1812.severity = suggestion   # Avoid uninstantiated internal classes
dotnet_diagnostic.CA1822.severity = suggestion   # Mark members as static
dotnet_diagnostic.CA1848.severity = suggestion   # Use LoggerMessage delegates
dotnet_diagnostic.CA1852.severity = suggestion   # Seal internal types
dotnet_diagnostic.CA2000.severity = warning      # Dispose objects before losing scope
dotnet_diagnostic.CA2007.severity = none         # Consider calling ConfigureAwait
dotnet_diagnostic.CA2100.severity = warning      # Review SQL queries for vulnerabilities
dotnet_diagnostic.CA2201.severity = warning      # Do not raise reserved exception types
dotnet_diagnostic.CA2208.severity = warning      # Instantiate argument exceptions correctly
dotnet_diagnostic.CA2213.severity = warning      # Disposable fields should be disposed
dotnet_diagnostic.CA2214.severity = warning      # Do not call overridable methods in constructors
dotnet_diagnostic.CA2215.severity = warning      # Dispose methods should call base dispose
dotnet_diagnostic.CA2219.severity = warning      # Do not raise exceptions in finally
dotnet_diagnostic.CA2229.severity = warning      # Implement serialization constructors
dotnet_diagnostic.CA2231.severity = warning      # Overload operator equals on ValueType
dotnet_diagnostic.CA2241.severity = error        # Provide correct arguments to formatting
dotnet_diagnostic.CA2245.severity = error        # Do not assign a property to itself

# AsyncFixer
dotnet_diagnostic.AsyncFixer01.severity = warning  # Unnecessary async/await usage
dotnet_diagnostic.AsyncFixer02.severity = warning  # Long-running on threadpool
dotnet_diagnostic.AsyncFixer03.severity = warning  # Fire-and-forget async-void methods
dotnet_diagnostic.AsyncFixer04.severity = warning  # Fire-and-forget async call inside using
dotnet_diagnostic.AsyncFixer05.severity = suggestion # Downcasting from Task<T> to Task

# StyleCop
dotnet_diagnostic.SA0001.severity = none         # XML comment analysis disabled
dotnet_diagnostic.SA1101.severity = none         # Prefix local calls with this
dotnet_diagnostic.SA1200.severity = none         # Using directives must be inside namespace
dotnet_diagnostic.SA1309.severity = none         # Field names should not begin with underscore
dotnet_diagnostic.SA1402.severity = suggestion   # File may only contain a single type
dotnet_diagnostic.SA1413.severity = none         # Use trailing comma
dotnet_diagnostic.SA1503.severity = none         # Braces should not be omitted
dotnet_diagnostic.SA1600.severity = suggestion   # Elements should be documented
dotnet_diagnostic.SA1601.severity = suggestion   # Partial elements should be documented
dotnet_diagnostic.SA1602.severity = suggestion   # Enumeration items should be documented
dotnet_diagnostic.SA1633.severity = none         # File should have header
dotnet_diagnostic.SA1649.severity = suggestion   # File name should match first type name

# Security Code Scan
dotnet_diagnostic.SCS0001.severity = warning     # Potential command injection
dotnet_diagnostic.SCS0002.severity = warning     # Potential SQL injection (LINQ)
dotnet_diagnostic.SCS0003.severity = warning     # Potential XPath injection
dotnet_diagnostic.SCS0005.severity = warning     # Weak random number generator
dotnet_diagnostic.SCS0006.severity = warning     # Weak hashing function
dotnet_diagnostic.SCS0007.severity = warning     # XML External Entity (XXE)
dotnet_diagnostic.SCS0008.severity = warning     # Insecure cookie
dotnet_diagnostic.SCS0012.severity = warning     # Weak cipher algorithm
dotnet_diagnostic.SCS0013.severity = warning     # Weak cipher mode
dotnet_diagnostic.SCS0018.severity = warning     # Potential Path Traversal
dotnet_diagnostic.SCS0026.severity = warning     # Potential LDAP injection
dotnet_diagnostic.SCS0028.severity = warning     # Potential insecure deserialization
dotnet_diagnostic.SCS0029.severity = warning     # Potential XSS vulnerability

# Roslynator
dotnet_diagnostic.RCS1001.severity = suggestion  # Add braces
dotnet_diagnostic.RCS1003.severity = suggestion  # Add braces to if-else
dotnet_diagnostic.RCS1005.severity = suggestion  # Simplify nested using statement
dotnet_diagnostic.RCS1006.severity = suggestion  # Merge else clause with nested if
dotnet_diagnostic.RCS1010.severity = suggestion  # Use var instead of explicit type
dotnet_diagnostic.RCS1018.severity = suggestion  # Add accessibility modifiers
dotnet_diagnostic.RCS1019.severity = suggestion  # Order modifiers
dotnet_diagnostic.RCS1021.severity = suggestion  # Simplify lambda expression
dotnet_diagnostic.RCS1032.severity = suggestion  # Remove redundant parentheses
dotnet_diagnostic.RCS1036.severity = suggestion  # Remove redundant empty line
dotnet_diagnostic.RCS1037.severity = suggestion  # Remove trailing white-space
dotnet_diagnostic.RCS1038.severity = suggestion  # Remove empty statement
dotnet_diagnostic.RCS1039.severity = suggestion  # Remove argument list from attribute
dotnet_diagnostic.RCS1040.severity = suggestion  # Remove empty else clause
dotnet_diagnostic.RCS1049.severity = suggestion  # Simplify boolean comparison
dotnet_diagnostic.RCS1058.severity = warning     # Use compound assignment
dotnet_diagnostic.RCS1061.severity = suggestion  # Merge if statement with nested if
dotnet_diagnostic.RCS1066.severity = suggestion  # Remove empty finally clause
dotnet_diagnostic.RCS1068.severity = suggestion  # Simplify logical negation
dotnet_diagnostic.RCS1069.severity = suggestion  # Remove unnecessary case label
dotnet_diagnostic.RCS1073.severity = suggestion  # Convert if to return statement
dotnet_diagnostic.RCS1074.severity = suggestion  # Remove redundant constructor
dotnet_diagnostic.RCS1077.severity = warning     # Optimize LINQ method call
dotnet_diagnostic.RCS1080.severity = suggestion  # Use Count/Length property
dotnet_diagnostic.RCS1084.severity = suggestion  # Use coalesce expression instead of if
dotnet_diagnostic.RCS1085.severity = suggestion  # Use auto-implemented property
dotnet_diagnostic.RCS1089.severity = suggestion  # Use ++ or --
dotnet_diagnostic.RCS1090.severity = suggestion  # Call ConfigureAwait
dotnet_diagnostic.RCS1097.severity = suggestion  # Remove redundant ToString call
dotnet_diagnostic.RCS1118.severity = warning     # Mark local variable as const
dotnet_diagnostic.RCS1123.severity = suggestion  # Add parentheses according to operator precedence
dotnet_diagnostic.RCS1128.severity = suggestion  # Use coalesce expression
dotnet_diagnostic.RCS1129.severity = suggestion  # Remove redundant field initialization
dotnet_diagnostic.RCS1138.severity = warning     # Add summary to documentation comment
dotnet_diagnostic.RCS1139.severity = warning     # Add summary element to documentation comment
dotnet_diagnostic.RCS1140.severity = warning     # Add exception to documentation comment
dotnet_diagnostic.RCS1141.severity = suggestion  # Add 'param' element to documentation comment
dotnet_diagnostic.RCS1146.severity = warning     # Use conditional access
dotnet_diagnostic.RCS1151.severity = suggestion  # Remove redundant cast
dotnet_diagnostic.RCS1155.severity = warning     # Use StringComparison
dotnet_diagnostic.RCS1163.severity = warning     # Unused parameter
dotnet_diagnostic.RCS1168.severity = suggestion  # Parameter name differs from base name
dotnet_diagnostic.RCS1169.severity = warning     # Mark field as read-only
dotnet_diagnostic.RCS1170.severity = warning     # Use read-only auto-implemented property
dotnet_diagnostic.RCS1171.severity = suggestion  # Simplify lazy initialization
dotnet_diagnostic.RCS1173.severity = suggestion  # Use coalesce expression instead of if
dotnet_diagnostic.RCS1175.severity = warning     # Unused this parameter
dotnet_diagnostic.RCS1176.severity = suggestion  # Use 'var' instead of explicit type
dotnet_diagnostic.RCS1177.severity = suggestion  # Use 'var' instead of explicit type
dotnet_diagnostic.RCS1181.severity = warning     # Convert comment to documentation comment
dotnet_diagnostic.RCS1186.severity = suggestion  # Use Regex instance instead of static method
dotnet_diagnostic.RCS1187.severity = suggestion  # Use constant instead of field
dotnet_diagnostic.RCS1188.severity = suggestion  # Remove redundant auto-property initialization
dotnet_diagnostic.RCS1189.severity = suggestion  # Add or remove region name
dotnet_diagnostic.RCS1190.severity = suggestion  # Join string expressions
dotnet_diagnostic.RCS1192.severity = warning     # Unnecessary usage of verbatim string literal
dotnet_diagnostic.RCS1194.severity = warning     # Implement exception constructors
dotnet_diagnostic.RCS1195.severity = suggestion  # Use ^ operator
dotnet_diagnostic.RCS1196.severity = warning     # Call extension method as instance method
dotnet_diagnostic.RCS1197.severity = suggestion  # Optimize StringBuilder.Append/AppendLine call
dotnet_diagnostic.RCS1198.severity = none        # Avoid unnecessary boxing of value type
dotnet_diagnostic.RCS1199.severity = suggestion  # Unnecessary null check
dotnet_diagnostic.RCS1201.severity = suggestion  # Use method chaining
dotnet_diagnostic.RCS1202.severity = suggestion  # Avoid NullReferenceException
dotnet_diagnostic.RCS1205.severity = suggestion  # Order named arguments according to order of parameters
dotnet_diagnostic.RCS1206.severity = suggestion  # Use conditional access instead of conditional expression
dotnet_diagnostic.RCS1207.severity = suggestion  # Convert anonymous function to method group
dotnet_diagnostic.RCS1208.severity = suggestion  # Reduce 'if' nesting
dotnet_diagnostic.RCS1210.severity = warning     # Return Task.FromResult instead of returning null
dotnet_diagnostic.RCS1211.severity = suggestion  # Remove unnecessary else clause
dotnet_diagnostic.RCS1212.severity = suggestion  # Remove redundant assignment
dotnet_diagnostic.RCS1213.severity = warning     # Remove unused member declaration
dotnet_diagnostic.RCS1214.severity = warning     # Unnecessary interpolated string
dotnet_diagnostic.RCS1215.severity = warning     # Expression is always equal to true/false
dotnet_diagnostic.RCS1216.severity = warning     # Unnecessary unsafe context
dotnet_diagnostic.RCS1217.severity = warning     # Convert interpolated string to concatenation
dotnet_diagnostic.RCS1218.severity = suggestion  # Simplify code branching
dotnet_diagnostic.RCS1220.severity = warning     # Use pattern matching instead of combination of 'is' operator and cast
dotnet_diagnostic.RCS1221.severity = suggestion  # Use pattern matching instead of combination of 'as' operator and null check
dotnet_diagnostic.RCS1222.severity = suggestion  # Merge preprocessor directives
dotnet_diagnostic.RCS1224.severity = suggestion  # Make method an extension method
dotnet_diagnostic.RCS1225.severity = suggestion  # Make class sealed
dotnet_diagnostic.RCS1226.severity = suggestion  # Add 'param' element to documentation comment
dotnet_diagnostic.RCS1227.severity = suggestion  # Validate arguments correctly
dotnet_diagnostic.RCS1228.severity = suggestion  # Unused element in documentation comment
dotnet_diagnostic.RCS1229.severity = suggestion  # Use async/await when necessary
dotnet_diagnostic.RCS1230.severity = suggestion  # Unnecessary explicit use of enumerator
dotnet_diagnostic.RCS1231.severity = warning     # Make parameter ref read-only
dotnet_diagnostic.RCS1232.severity = suggestion  # Order elements in documentation comment
dotnet_diagnostic.RCS1233.severity = suggestion  # Use short-circuiting operator
dotnet_diagnostic.RCS1234.severity = suggestion  # Duplicate enum value
dotnet_diagnostic.RCS1235.severity = suggestion  # Optimize method call
dotnet_diagnostic.RCS1236.severity = warning     # Use exception filter
dotnet_diagnostic.RCS1238.severity = suggestion  # Avoid nested ?: operators
dotnet_diagnostic.RCS1239.severity = suggestion  # Use 'for' statement instead of 'while' statement
dotnet_diagnostic.RCS1240.severity = suggestion  # Operator is unnecessary
dotnet_diagnostic.RCS1241.severity = suggestion  # Implement non-generic counterpart
dotnet_diagnostic.RCS1242.severity = warning     # Do not pass non-read-only struct by read-only reference
dotnet_diagnostic.RCS1243.severity = suggestion  # Duplicate word in a comment
dotnet_diagnostic.RCS1244.severity = suggestion  # Simplify 'default' expression
dotnet_diagnostic.RCS1246.severity = suggestion  # Use element access
dotnet_diagnostic.RCS1247.severity = warning     # Fix documentation comment tag
dotnet_diagnostic.RCS1248.severity = suggestion  # Normalize null check
dotnet_diagnostic.RCS1249.severity = suggestion  # Unnecessary null-forgiving operator
dotnet_diagnostic.RCS1250.severity = suggestion  # Use implicit/explicit object creation

# JSON files
[*.json]
indent_size = 2

# XML files
[*.{xml,csproj,props,targets}]
indent_size = 2

# YAML files
[*.{yml,yaml}]
indent_size = 2

# Markdown files
[*.md]
trim_trailing_whitespace = false
"@

$editorconfigPath = Join-Path $ProjectPath ".editorconfig"
[System.IO.File]::WriteAllText($editorconfigPath, $editorconfig, [System.Text.UTF8Encoding]::new($false))
Write-Host "  Created: $editorconfigPath" -ForegroundColor Green

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  ROSLYN ANALYZER SETUP COMPLETE" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Copy Directory.Build.props to your solution root" -ForegroundColor White
Write-Host "  2. Copy .editorconfig to your solution root" -ForegroundColor White
Write-Host "  3. Restore packages: dotnet restore" -ForegroundColor White
Write-Host "  4. Build to see analyzer warnings: dotnet build" -ForegroundColor White
Write-Host ""
Write-Host "The analyzers will now run on every build and in VS/VS Code." -ForegroundColor Cyan
Write-Host ""
