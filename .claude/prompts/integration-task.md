# API Integration Task

## Expert Mode: API Integration Specialist
Reference: api-integration-specialist skill

## Task Description
[DESCRIBE YOUR API INTEGRATION TASK HERE]

## Apply These Patterns

### REST API Client Pattern
- Typed request/response models
- Centralized error handling
- Retry logic with exponential backoff
- Rate limiting
- Proper timeout configuration

### Authentication
- OAuth 2.0 flows (authorization code, client credentials)
- API key management (environment variables)
- JWT token handling and refresh
- Webhook signature verification

### Error Handling
```csharp
public class ApiError : Exception
{
    public int StatusCode { get; }
    public string ResponseBody { get; }
    
    public bool IsRetryable => StatusCode >= 500 || StatusCode == 429;
}
```

### Retry Logic (Polly)
```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<ApiError>(e => e.IsRetryable)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, attempt, context) =>
        {
            _logger.LogWarning("Retry {Attempt} after {Delay}s", attempt, timeSpan.TotalSeconds);
        });
```

### Circuit Breaker
```csharp
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromMinutes(1));
```

### Rate Limiting
```csharp
public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _requestsPerSecond;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _ = Task.Delay(1000 / _requestsPerSecond)
                .ContinueWith(_ => _semaphore.Release());
        }
    }
}
```

### Webhook Handling
```csharp
public bool VerifyWebhookSignature(string payload, string signature, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    var expectedSignature = Convert.ToBase64String(hash);
    
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(signature),
        Encoding.UTF8.GetBytes(expectedSignature));
}
```

## API Client Structure
```csharp
public interface IExternalApiClient
{
    Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken ct = default);
    Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken ct = default);
}

public class ExternalApiClient : IExternalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalApiClient> _logger;
    private readonly IAsyncPolicy _retryPolicy;
    
    // Implementation with error handling, retries, logging
}
```

## Success Criteria
- [ ] Proper authentication implemented
- [ ] Retry logic with exponential backoff
- [ ] Circuit breaker for failing services
- [ ] Rate limiting to avoid throttling
- [ ] Comprehensive error handling
- [ ] Logging for debugging
- [ ] Timeout configuration
- [ ] Webhook signature verification (if applicable)
- [ ] Integration tests with mocked responses

## Security Checklist
- [ ] API keys in environment variables (not code)
- [ ] HTTPS only for production
- [ ] Webhook signatures verified
- [ ] Sensitive data not logged
- [ ] Proper CORS configuration
- [ ] Input validation on all requests

## Common Integration Scenarios

### Stripe Payment
```csharp
var options = new PaymentIntentCreateOptions
{
    Amount = 2000,
    Currency = "usd",
    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
    {
        Enabled = true,
    },
};
var service = new PaymentIntentService();
var paymentIntent = await service.CreateAsync(options);
```

### SendGrid Email
```csharp
var client = new SendGridClient(apiKey);
var msg = new SendGridMessage()
{
    From = new EmailAddress("from@example.com"),
    Subject = "Subject",
    HtmlContent = "<p>Content</p>"
};
msg.AddTo(new EmailAddress("to@example.com"));
await client.SendEmailAsync(msg);
```

## Next Steps After Integration
1. Write integration tests with mocked API
2. Test error scenarios (timeout, 500 errors, rate limits)
3. Verify retry logic works
4. Add monitoring/alerting for API failures
5. Document API endpoints used
6. Update environment variables documentation
