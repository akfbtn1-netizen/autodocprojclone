# Azure Durable Functions Alternative

Use this approach if you prefer serverless over MassTransit.

## Human Approval Orchestration

```csharp
[FunctionName("DocumentApprovalOrchestrator")]
public static async Task<ApprovalResult> RunOrchestrator(
    [OrchestrationTrigger] IDurableOrchestrationContext context,
    ILogger log)
{
    var request = context.GetInput<ApprovalRequest>();
    
    // Step 1: Generate draft document
    var draftPath = await context.CallActivityAsync<string>(
        "GenerateDraft",
        new DraftRequest
        {
            DocumentId = request.DocumentId,
            PhysicalName = request.PhysicalName,
            DatabaseName = request.DatabaseName
        });
    
    // Step 2: Determine approval tier
    var tier = await context.CallActivityAsync<ApprovalTierResult>(
        "DetermineApprovalTier",
        new TierRequest
        {
            DocumentId = request.DocumentId,
            DraftPath = draftPath
        });
    
    // Step 3: Notify approvers
    await context.CallActivityAsync(
        "NotifyApprovers",
        new NotificationRequest
        {
            DocumentId = request.DocumentId,
            Approvers = tier.Approvers,
            DraftPath = draftPath,
            ApprovalUrl = $"{baseUrl}/approve/{context.InstanceId}",
            RejectUrl = $"{baseUrl}/reject/{context.InstanceId}",
            Deadline = context.CurrentUtcDateTime.AddHours(tier.SlaHours)
        });
    
    // Step 4: Wait for human approval (with timeout)
    var deadline = context.CurrentUtcDateTime.AddHours(tier.SlaHours);
    
    using var cts = new CancellationTokenSource();
    var approvalTask = context.WaitForExternalEvent<ApprovalResponse>("ApprovalEvent");
    var timeoutTask = context.CreateTimer(deadline, cts.Token);
    
    var winner = await Task.WhenAny(approvalTask, timeoutTask);
    
    if (winner == timeoutTask)
    {
        // Timeout - escalate
        await context.CallActivityAsync(
            "EscalateApproval",
            new EscalationRequest
            {
                DocumentId = request.DocumentId,
                OriginalApprovers = tier.Approvers,
                EscalateTo = tier.EscalationPath
            });
        
        // Wait again with extended deadline
        var extendedDeadline = context.CurrentUtcDateTime.AddHours(24);
        var escalatedApprovalTask = context.WaitForExternalEvent<ApprovalResponse>("ApprovalEvent");
        var escalatedTimeoutTask = context.CreateTimer(extendedDeadline, CancellationToken.None);
        
        winner = await Task.WhenAny(escalatedApprovalTask, escalatedTimeoutTask);
        
        if (winner == escalatedTimeoutTask)
        {
            return new ApprovalResult
            {
                Status = "TimedOut",
                DocumentId = request.DocumentId
            };
        }
        
        approvalTask = escalatedApprovalTask;
    }
    else
    {
        cts.Cancel(); // Cancel timer
    }
    
    var approval = approvalTask.Result;
    
    if (!approval.IsApproved)
    {
        // Rejection
        await context.CallActivityAsync(
            "NotifyRejection",
            new RejectionNotification
            {
                DocumentId = request.DocumentId,
                RejectedBy = approval.ApprovedBy,
                Reason = approval.Comments
            });
        
        return new ApprovalResult
        {
            Status = "Rejected",
            DocumentId = request.DocumentId,
            Reason = approval.Comments
        };
    }
    
    // Step 5: Generate final document
    var finalPath = await context.CallActivityAsync<string>(
        "GenerateFinalDocument",
        new FinalDocRequest
        {
            DocumentId = request.DocumentId,
            DraftPath = draftPath,
            ApprovedBy = approval.ApprovedBy,
            ApprovedAt = context.CurrentUtcDateTime
        });
    
    // Step 6: File to SharePoint
    var sharePointUrl = await context.CallActivityAsync<string>(
        "FileToSharePoint",
        new SharePointRequest
        {
            DocumentId = request.DocumentId,
            FinalPath = finalPath,
            PhysicalName = request.PhysicalName
        });
    
    // Step 7: Update Master Index
    await context.CallActivityAsync(
        "UpdateMasterIndex",
        new MasterIndexUpdate
        {
            DocumentId = request.DocumentId,
            SharePointUrl = sharePointUrl,
            ApprovedBy = approval.ApprovedBy
        });
    
    return new ApprovalResult
    {
        Status = "Approved",
        DocumentId = request.DocumentId,
        SharePointUrl = sharePointUrl,
        ApprovedBy = approval.ApprovedBy
    };
}
```

## Activity Functions

```csharp
[FunctionName("GenerateDraft")]
public static async Task<string> GenerateDraft(
    [ActivityTrigger] DraftRequest request,
    [Blob("drafts", FileAccess.Write)] BlobContainerClient blobContainer,
    ILogger log)
{
    // Generate draft using DocGenerator logic
    var draftContent = await _docGenerator.GenerateAsync(
        request.PhysicalName,
        request.DatabaseName);
    
    var blobPath = $"{request.DocumentId}/draft.docx";
    var blobClient = blobContainer.GetBlobClient(blobPath);
    await blobClient.UploadAsync(new MemoryStream(draftContent));
    
    return blobPath;
}

[FunctionName("DetermineApprovalTier")]
public static async Task<ApprovalTierResult> DetermineApprovalTier(
    [ActivityTrigger] TierRequest request,
    ILogger log)
{
    // Check for PII, data classification, database criticality
    var metadata = await _metadataService.GetDocumentMetadataAsync(request.DocumentId);
    
    var tier = metadata switch
    {
        { ContainsPII: true } => 3,
        { DatabaseCriticality: "Critical" } => 4,
        { ObjectType: "Table" or "View" } => 2,
        _ => 1
    };
    
    return new ApprovalTierResult
    {
        Tier = tier,
        Approvers = GetApproversForTier(tier),
        SlaHours = GetSlaHours(tier),
        EscalationPath = GetEscalationPath(tier)
    };
}

[FunctionName("NotifyApprovers")]
public static async Task NotifyApprovers(
    [ActivityTrigger] NotificationRequest request,
    [SendGrid(ApiKey = "SendGridApiKey")] IAsyncCollector<SendGridMessage> messages,
    ILogger log)
{
    foreach (var approver in request.Approvers)
    {
        var email = await _userService.GetEmailForRoleAsync(approver);
        
        var message = new SendGridMessage
        {
            Subject = $"Approval Required: {request.DocumentId}",
            HtmlContent = $@"
                <p>A document requires your approval.</p>
                <p><a href='{request.ApprovalUrl}'>Approve</a> | 
                   <a href='{request.RejectUrl}'>Reject</a></p>
                <p>Deadline: {request.Deadline:g}</p>"
        };
        message.AddTo(email);
        
        await messages.AddAsync(message);
    }
}

[FunctionName("FileToSharePoint")]
public static async Task<string> FileToSharePoint(
    [ActivityTrigger] SharePointRequest request,
    ILogger log)
{
    var graphClient = GetGraphClient();
    
    var driveItem = await graphClient.Sites[siteId]
        .Drive.Root
        .ItemWithPath($"Documentation/{request.PhysicalName}.docx")
        .Content
        .Request()
        .PutAsync<DriveItem>(await GetDocumentStream(request.FinalPath));
    
    return driveItem.WebUrl;
}
```

## HTTP Triggers for Human Approval

```csharp
[FunctionName("ApproveDocument")]
public static async Task<IActionResult> ApproveDocument(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "approve/{instanceId}")] 
    HttpRequest req,
    [DurableClient] IDurableOrchestrationClient client,
    string instanceId,
    ILogger log)
{
    var approval = await req.ReadFromJsonAsync<ApprovalRequest>();
    
    // Validate user has permission to approve
    var userId = req.Headers["X-User-Id"].FirstOrDefault();
    if (!await _authService.CanApproveAsync(instanceId, userId))
    {
        return new UnauthorizedResult();
    }
    
    // Raise the approval event
    await client.RaiseEventAsync(instanceId, "ApprovalEvent", new ApprovalResponse
    {
        IsApproved = true,
        ApprovedBy = userId,
        Comments = approval.Comments,
        ApprovedAt = DateTime.UtcNow
    });
    
    return new OkObjectResult(new { message = "Approval recorded" });
}

[FunctionName("RejectDocument")]
public static async Task<IActionResult> RejectDocument(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "reject/{instanceId}")] 
    HttpRequest req,
    [DurableClient] IDurableOrchestrationClient client,
    string instanceId,
    ILogger log)
{
    var rejection = await req.ReadFromJsonAsync<RejectionRequest>();
    
    var userId = req.Headers["X-User-Id"].FirstOrDefault();
    if (!await _authService.CanApproveAsync(instanceId, userId))
    {
        return new UnauthorizedResult();
    }
    
    await client.RaiseEventAsync(instanceId, "ApprovalEvent", new ApprovalResponse
    {
        IsApproved = false,
        ApprovedBy = userId,
        Comments = rejection.Reason,
        ApprovedAt = DateTime.UtcNow
    });
    
    return new OkObjectResult(new { message = "Rejection recorded" });
}

[FunctionName("GetApprovalStatus")]
public static async Task<IActionResult> GetApprovalStatus(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{instanceId}")] 
    HttpRequest req,
    [DurableClient] IDurableOrchestrationClient client,
    string instanceId,
    ILogger log)
{
    var status = await client.GetStatusAsync(instanceId);
    
    if (status == null)
    {
        return new NotFoundResult();
    }
    
    return new OkObjectResult(new
    {
        status.InstanceId,
        status.RuntimeStatus,
        status.CreatedTime,
        status.LastUpdatedTime,
        status.Output
    });
}
```

## Starter Function (Triggered by Schema Change)

```csharp
[FunctionName("StartApprovalWorkflow")]
public static async Task StartApprovalWorkflow(
    [ServiceBusTrigger("schema-changes", Connection = "ServiceBusConnection")] 
    SchemaChangeMessage message,
    [DurableClient] IDurableOrchestrationClient client,
    ILogger log)
{
    var instanceId = message.DocumentId.ToString();
    
    // Check if already running
    var existing = await client.GetStatusAsync(instanceId);
    if (existing != null && 
        existing.RuntimeStatus is OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending)
    {
        log.LogWarning("Workflow already running for {DocumentId}", message.DocumentId);
        return;
    }
    
    await client.StartNewAsync("DocumentApprovalOrchestrator", instanceId, new ApprovalRequest
    {
        DocumentId = message.DocumentId,
        PhysicalName = message.PhysicalName,
        DatabaseName = message.DatabaseName,
        ChangeType = message.ChangeType
    });
    
    log.LogInformation("Started approval workflow {InstanceId} for {PhysicalName}",
        instanceId, message.PhysicalName);
}
```

## Models

```csharp
public class ApprovalRequest
{
    public Guid DocumentId { get; set; }
    public string PhysicalName { get; set; }
    public string DatabaseName { get; set; }
    public string ChangeType { get; set; }
}

public class ApprovalResponse
{
    public bool IsApproved { get; set; }
    public string ApprovedBy { get; set; }
    public string Comments { get; set; }
    public DateTime ApprovedAt { get; set; }
}

public class ApprovalResult
{
    public string Status { get; set; }
    public Guid DocumentId { get; set; }
    public string SharePointUrl { get; set; }
    public string ApprovedBy { get; set; }
    public string Reason { get; set; }
}

public class ApprovalTierResult
{
    public int Tier { get; set; }
    public List<string> Approvers { get; set; }
    public int SlaHours { get; set; }
    public List<string> EscalationPath { get; set; }
}
```

## host.json Configuration

```json
{
  "version": "2.0",
  "extensions": {
    "durableTask": {
      "storageProvider": {
        "type": "azureStorage",
        "connectionStringName": "AzureWebJobsStorage"
      },
      "maxConcurrentActivityFunctions": 10,
      "maxConcurrentOrchestratorFunctions": 5
    },
    "serviceBus": {
      "prefetchCount": 10,
      "messageHandlerOptions": {
        "maxConcurrentCalls": 16
      }
    }
  }
}
```

## Comparison: Durable Functions vs MassTransit

| Aspect | Durable Functions | MassTransit |
|--------|-------------------|-------------|
| Hosting | Serverless (Azure) | Self-hosted (.NET) |
| Cost Model | Consumption-based | Fixed infrastructure |
| State Storage | Azure Storage | SQL Server / EF Core |
| Learning Curve | Lower | Medium |
| Testing | Azure Functions tools | Standard .NET testing |
| Debugging | Azure portal | Local debugging |
| Flexibility | Limited | High |
| Best For | Simple workflows | Complex sagas |

## When to Use Durable Functions

- Prefer serverless / pay-per-execution
- Simple linear workflows
- Already invested in Azure Functions
- Team unfamiliar with MassTransit

## When to Use MassTransit

- Complex state machines with many transitions
- Need fine-grained control over retry policies
- Already using Service Bus for other messaging
- Prefer code-first state definitions
