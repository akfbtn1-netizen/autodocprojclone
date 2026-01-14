# Simple PowerShell HTTP Server for Dashboard
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:8080/")
$listener.Start()

Write-Host "🚀 Dashboard Server Started!" -ForegroundColor Green
Write-Host "📊 Access Dashboard: http://localhost:8080/" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop..." -ForegroundColor Yellow

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response
        
        Write-Host "📡 Request: $($request.HttpMethod) $($request.Url.LocalPath)" -ForegroundColor Gray
        
        if ($request.Url.LocalPath -eq "/" -or $request.Url.LocalPath -eq "/dashboard.html") {
            # Serve dashboard.html
            $dashboardPath = "dashboard.html"
            if (Test-Path $dashboardPath) {
                $content = Get-Content $dashboardPath -Raw
                $buffer = [System.Text.Encoding]::UTF8.GetBytes($content)
                $response.ContentType = "text/html; charset=UTF-8"
                $response.ContentLength64 = $buffer.Length
                $response.OutputStream.Write($buffer, 0, $buffer.Length)
            } else {
                $errorMsg = "Dashboard file not found"
                $buffer = [System.Text.Encoding]::UTF8.GetBytes($errorMsg)
                $response.StatusCode = 404
                $response.ContentLength64 = $buffer.Length
                $response.OutputStream.Write($buffer, 0, $buffer.Length)
            }
        } else {
            # 404 for other paths
            $errorMsg = "Not Found"
            $buffer = [System.Text.Encoding]::UTF8.GetBytes($errorMsg)
            $response.StatusCode = 404
            $response.ContentLength64 = $buffer.Length
            $response.OutputStream.Write($buffer, 0, $buffer.Length)
        }
        
        $response.OutputStream.Close()
    }
} finally {
    $listener.Stop()
    Write-Host "Server stopped." -ForegroundColor Yellow
}