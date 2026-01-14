namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Represents metadata information about a database column.
/// </summary>
public class ColumnInfo
{
    /// <summary>
    /// Column name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Data type of the column.
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Maximum length for character data types.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Precision for numeric data types.
    /// </summary>
    public int? Precision { get; set; }

    /// <summary>
    /// Scale for decimal data types.
    /// </summary>
    public int? Scale { get; set; }

    /// <summary>
    /// Whether the column allows null values.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Whether the column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Whether the column is a foreign key.
    /// </summary>
    public bool IsForeignKey { get; set; }

    /// <summary>
    /// Whether the column has an identity/auto-increment specification.
    /// </summary>
    public bool IsIdentity { get; set; }

    /// <summary>
    /// Default value for the column.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Column description/comments.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Ordinal position of the column in the table.
    /// </summary>
    public int OrdinalPosition { get; set; }
}