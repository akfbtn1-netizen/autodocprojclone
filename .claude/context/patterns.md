# Code Patterns Reference

## MediatR Command Handler

```csharp
public class GenerateDocumentCommandHandler
    : IRequestHandler<GenerateDocumentCommand, DocumentDto>
{
    private readonly IDocumentRepository _repository;
    private readonly IDocGeneratorService _generator;
    private readonly ILogger<GenerateDocumentCommandHandler> _logger;

    public GenerateDocumentCommandHandler(
        IDocumentRepository repository,
        IDocGeneratorService generator,
        ILogger<GenerateDocumentCommandHandler> logger)
    {
        _repository = repository;
        _generator = generator;
        _logger = logger;
    }

    public async Task<DocumentDto> Handle(
        GenerateDocumentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await _generator.GenerateAsync(
                request, cancellationToken);

            await _repository.AddAsync(document, cancellationToken);

            _logger.LogInformation(
                "Document {DocId} generated successfully",
                document.Id);

            return document.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate document for request {Request}",
                request);
            throw;
        }
    }
}
```

## MediatR Query Handler

```csharp
public class GetDocumentByIdQueryHandler
    : IRequestHandler<GetDocumentByIdQuery, DocumentDto?>
{
    private readonly IDocumentRepository _repository;

    public GetDocumentByIdQueryHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<DocumentDto?> Handle(
        GetDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(
            request.Id, cancellationToken);

        return document?.ToDto();
    }
}
```

## FluentValidation Validator

```csharp
public class GenerateDocumentCommandValidator
    : AbstractValidator<GenerateDocumentCommand>
{
    public GenerateDocumentCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.SourceType)
            .IsInEnum();

        RuleFor(x => x.Metadata)
            .NotNull()
            .Must(m => m.Count > 0)
            .WithMessage("At least one metadata entry required");
    }
}
```

## Governance-Compliant Data Access

```csharp
public async Task<IEnumerable<SchemaInfo>> GetSchemaAsync(
    string schemaName,
    CancellationToken cancellationToken)
{
    var query = new GovernanceQueryRequest
    {
        AgentId = "schema-detector-agent",
        SqlQuery = @"
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema",
        Parameters = new { Schema = schemaName },
        RequestedTables = new[] { "INFORMATION_SCHEMA.COLUMNS" },
        ClearanceLevel = AgentClearanceLevel.Standard
    };

    var result = await _governanceProxy.ExecuteSecureQueryAsync<SchemaInfo>(
        query, cancellationToken);

    return result.Data;
}
```

## React Component with SignalR

```typescript
export const WorkflowVisualization: React.FC = () => {
  const { connection, isConnected } = useSignalR();
  const updateWorkflow = useWorkflowStore(state => state.updateWorkflow);

  useEffect(() => {
    if (!connection) return;

    connection.on('WorkflowUpdated', (data: WorkflowUpdate) => {
      updateWorkflow(data);
    });

    return () => {
      connection.off('WorkflowUpdated');
    };
  }, [connection]);

  if (!isConnected) {
    return <ConnectionStatus status="connecting" />;
  }

  return (
    <div className="workflow-container">
      {/* Component render logic */}
    </div>
  );
};
```

## Zustand Store

```typescript
interface WorkflowState {
  workflows: Workflow[];
  activeWorkflow: Workflow | null;
  isLoading: boolean;
  error: string | null;

  // Actions
  setWorkflows: (workflows: Workflow[]) => void;
  updateWorkflow: (update: WorkflowUpdate) => void;
  setActiveWorkflow: (id: string) => void;
  setError: (error: string | null) => void;
}

export const useWorkflowStore = create<WorkflowState>((set, get) => ({
  workflows: [],
  activeWorkflow: null,
  isLoading: false,
  error: null,

  setWorkflows: (workflows) => set({ workflows, isLoading: false }),

  updateWorkflow: (update) => set((state) => ({
    workflows: state.workflows.map(w =>
      w.id === update.id ? { ...w, ...update } : w
    ),
  })),

  setActiveWorkflow: (id) => set((state) => ({
    activeWorkflow: state.workflows.find(w => w.id === id) || null,
  })),

  setError: (error) => set({ error, isLoading: false }),
}));
```

## React Query API Hook

```typescript
export const useDocuments = (filter?: DocumentFilter) => {
  return useQuery({
    queryKey: ['documents', filter],
    queryFn: () => documentApi.getDocuments(filter),
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
};

export const useGenerateDocument = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: documentApi.generateDocument,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['documents'] });
    },
  });
};
```

## API Controller

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetDocumentByIdQuery(id),
            cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.ToCommand();
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }
}
```

## Agent Base Class

```csharp
public abstract class AgentBase : IAgent
{
    protected readonly ILogger _logger;
    protected readonly IMediator _mediator;
    protected readonly IDataGovernanceProxy _governance;

    public abstract string AgentId { get; }
    public abstract AgentClearanceLevel ClearanceLevel { get; }

    protected AgentBase(
        ILogger logger,
        IMediator mediator,
        IDataGovernanceProxy governance)
    {
        _logger = logger;
        _mediator = mediator;
        _governance = governance;
    }

    public async Task<AgentResult> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(
            "Agent {AgentId} executing {RequestType}",
            AgentId,
            request.GetType().Name);

        try
        {
            return await ExecuteCoreAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent execution failed");
            return AgentResult.Failure(ex.Message);
        }
    }

    protected abstract Task<AgentResult> ExecuteCoreAsync(
        AgentRequest request,
        CancellationToken cancellationToken);
}
```
