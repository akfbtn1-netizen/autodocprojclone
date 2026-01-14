// INTEGRATION: Update Program.cs with all services

using Enterprise.Documentation.Api.Hubs;
using Enterprise.Documentation.Core.Application.Services.Workflow;
using Enterprise.Documentation.Core.Application.Services.CodeQuality;
using Enterprise.Documentation.Core.Application.Services.Notifications;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;

// Add HttpClient for Teams notifications
builder.Services.AddHttpClient();

// Add SignalR
builder.Services.AddSignalR();

// Add Workflow services
builder.Services.AddScoped<IWorkflowEventService, WorkflowEventService>();

// Add Code Quality service
builder.Services.AddScoped<IEnterpriseCodeQualityAuditService, EnterpriseCodeQualityAuditService>();

// Add Teams Notification service
builder.Services.AddScoped<ITeamsNotificationService, TeamsNotificationService>();

// Add MasterIndex service
builder.Services.AddScoped<IComprehensiveMasterIndexService, ComprehensiveMasterIndexService>();

// Add StoredProcedure Documentation service
builder.Services.AddScoped<IStoredProcedureDocumentationService, StoredProcedureDocumentationService>();

// Add StoredProcedure Documentation service
builder.Services.AddScoped<IStoredProcedureDocumentationService, StoredProcedureDocumentationService>();

// CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AllowFrontend");
app.MapHub<WorkflowHub>("/hubs/workflow");

app.Run();
