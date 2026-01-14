# Fix all interface files with copyright, periods, and file endings

$interfaceFiles = @(
    "src\Shared\Contracts\Interfaces\IUnitOfWork.cs",
    "src\Shared\Contracts\Interfaces\IMessageBus.cs", 
    "src\Shared\Contracts\Interfaces\IEventHandler.cs",
    "src\Shared\Contracts\Interfaces\IBaseEvent.cs",
    "src\Shared\Contracts\Interfaces\IAgentContract.cs",
    "src\Shared\Contracts\Interfaces\IAgentConfiguration.cs"
)

foreach ($file in $interfaceFiles) {
    $fullPath = "C:\Projects\EnterpriseDocumentationPlatform.V2\$file"
    Write-Host "Fixing $file..."
    
    $content = Get-Content $fullPath -Raw
    
    # Fix copyright header
    $content = $content -replace '// <copyright file="([^"]+)" company="Enterprise Documentation Platform">\r?\n// Copyright \(c\) Enterprise Documentation Platform\. All rights reserved\.\r?\n// </copyright>', '// <copyright file="$1" company="Enterprise Documentation Platform">`r`n// Copyright (c) Enterprise Documentation Platform. All rights reserved.`r`n// This software is proprietary and confidential.`r`n// </copyright>'
    
    # Fix namespace if needed
    $content = $content -replace 'namespace Shared\.Contracts\.Interfaces;', 'namespace Enterprise.Documentation.Shared.Contracts.Interfaces;'
    
    # Fix missing periods in documentation
    $content = $content -replace '    /// <param name="([^"]+)">([^<]+(?<!\.))(?=</param>)', '    /// <param name="$1">$2.</param>'
    $content = $content -replace '    /// <returns>([^<]+(?<!\.))(?=</returns>)', '    /// <returns>$1.</returns>'
    $content = $content -replace '    /// <summary>([^<]+(?<!\.))(?=</summary>)', '    /// <summary>$1.</summary>'
    
    # Remove trailing whitespace
    $content = $content -replace '\s+$', ''
    
    # Ensure single newline at end
    $content = $content.TrimEnd() + "`n"
    
    Set-Content $fullPath -Value $content -NoNewline
}

Write-Host "All interface files fixed!"