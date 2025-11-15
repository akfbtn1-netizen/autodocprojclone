namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Base class for template data
/// </summary>
public abstract class TemplateData
{
    public string Author { get; set; } = string.Empty;
    public string Ticket { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
}

/// <summary>
/// Data model for stored procedure templates
/// </summary>
public class StoredProcedureTemplateData : TemplateData
{
    public string Schema { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> ExecutionLogic { get; set; } = new();
    public DependenciesData Dependencies { get; set; } = new();
}

/// <summary>
/// Data model for business request templates
/// </summary>
public class BusinessRequestTemplateData : TemplateData
{
    public string DateEntered { get; set; } = string.Empty;
    public string NewTableCreated { get; set; } = string.Empty;
    public string BusinessPurpose { get; set; } = string.Empty;
    public string SourceTables { get; set; } = string.Empty;
}

/// <summary>
/// Data model for defect fix templates
/// </summary>
public class DefectFixTemplateData : TemplateData
{
    public string DateEntered { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string TablesAffected { get; set; } = string.Empty;
    public string DefectDescription { get; set; } = string.Empty;
    public string TablePurpose { get; set; } = string.Empty;
}

/// <summary>
/// Dependencies data for stored procedures
/// </summary>
public class DependenciesData
{
    public List<string> SourceTables { get; set; } = new();
    public List<string> TargetTables { get; set; } = new();
}
