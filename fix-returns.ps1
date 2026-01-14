# Fix SA1615 (missing return documentation) and SA1618 (missing type parameter docs)

$files = @(
    "src\Shared\Contracts\Interfaces\IRepository.cs",
    "src\Shared\Contracts\Interfaces\IUnitOfWork.cs", 
    "src\Shared\Contracts\Interfaces\IMessageBus.cs",
    "src\Shared\Contracts\Interfaces\IEventHandler.cs",
    "src\Shared\Contracts\DTOs\BaseDto.cs"
)

foreach ($file in $files) {
    $fullPath = "C:\Projects\EnterpriseDocumentationPlatform.V2\$file"
    if (Test-Path $fullPath) {
        Write-Host "Fixing return docs and type params in $file..."
        
        $content = Get-Content $fullPath -Raw
        
        # Add missing return documentation patterns
        $content = $content -replace '(\s*/// <param[^>]*>[^<]*</param>)\s*(\s*Task<[^>]+>)', '$1`r`n    /// <returns>A task that represents the asynchronous operation.</returns>`r`n$2'
        $content = $content -replace '(\s*/// <param[^>]*>[^<]*</param>)\s*(\s*bool\s)', '$1`r`n    /// <returns>True if the operation succeeds; otherwise, false.</returns>`r`n$2'
        $content = $content -replace '(\s*/// <param[^>]*>[^<]*</param>)\s*(\s*int\s)', '$1`r`n    /// <returns>The number of items.</returns>`r`n$2'
        
        # Add missing type parameter documentation
        $content = $content -replace '(\s*/// <summary>[^<]*</summary>)\s*(\s*[^<]*<T>[^{]*where T)', '$1`r`n    /// <typeparam name="T">The entity type.</typeparam>`r`n$2'
        $content = $content -replace '(\s*/// <summary>[^<]*</summary>)\s*(\s*[^<]*<TEvent>[^{]*where TEvent)', '$1`r`n    /// <typeparam name="TEvent">The event type to handle.</typeparam>`r`n$2'
        
        Set-Content $fullPath -Value $content -NoNewline
    }
}

Write-Host "Return documentation fixes completed!"