# LLM Documentation Generation Patterns

> **Last Updated**: December 2025  
> **Research Sources**: Databricks, Snowflake Cortex, Stanford HAI, arXiv papers

## Overview

This document covers research-backed patterns for using Large Language Models to automate database documentation generation, including fine-tuning approaches, bulk generation strategies, and quality optimization techniques.

## Industry Benchmarks

### Databricks AI-Generated Documentation

**Key Findings** (November 2023 - December 2025):
- **80%+ of metadata updates** on Databricks are now AI-assisted
- Fine-tuned 7B model matches expensive SaaS LLM quality
- **10x cost reduction** vs. SaaS API calls
- **2 engineers, 1 month, <$1000** to develop bespoke model

**Training Approach:**
1. Generated ~3,600 synthetic training examples
2. Used NAICS codes for domain diversity
3. No customer data used in training
4. 15-minute fine-tuning time on A10 GPU

**Quality Improvements (DatabricksIQ v2):**
- Switched to Mixtral 8x7B for synthetic data generation
- Added heuristic filters for training data cleaning
- Updated base model selection
- Automated LLM-based evaluation
- **2x preference rate improvement** over previous model

### Stanford HAI 2025 AI Index Key Metrics

**Investment:**
- Generative AI attracted **$33.9 billion** globally (18.7% increase from 2023)
- Total corporate AI investment: **$252.3 billion** (44.5% growth)
- 78% of organizations reported AI use (up from 55%)

**Productivity Impact:**
- Research confirms AI boosts productivity in most cases
- Helps narrow skill gaps between low- and high-skilled workers
- Inference costs dropped **280-fold** (Nov 2022 - Oct 2024)
- Open-weight models closing gap: 8% → 1.7% performance difference

## Bulk Column Documentation Patterns

### Snowflake Cortex Pattern

```python
# Snowflake stored procedure for bulk documentation
CREATE OR REPLACE PROCEDURE public.create_col_desc_py(
    scope STRING,           -- "database", "schema", "table", "column"
    name STRING,            -- Fully qualified name
    model STRING,           -- LLM model (e.g., "llama3.1-70b")
    overwrite BOOLEAN       -- TRUE to replace existing
)
RETURNS VARCHAR
LANGUAGE PYTHON
RUNTIME_VERSION = '3.9'
HANDLER = 'create_col_desc_py'
PACKAGES = ('snowflake-snowpark-python', 'simplejson')
AS $$
def create_col_desc_py(session, scope, name, model, overwrite):
    # Traverse specified scope
    # Build intelligent prompts from schema context
    # Generate descriptions using Cortex LLM
    # Apply as COMMENT on columns
    pass
$$;
```

**Key Benefits:**
- All AI calls happen inside data platform (no data leaves account)
- Security and governance maintained
- Automatic metadata updates on schedule
- Engineers freed from boilerplate tasks

### Dual-Process Description Generation

Based on 2025 arXiv research on automatic database descriptions for Text-to-SQL:

**Approach:**
1. **Coarse-to-Fine**: Database → Table → Column context
2. **Fine-to-Coarse**: Column details → Table understanding

```python
def dual_process_documentation(table_info, column_info):
    """
    Step 1: Coarse-to-Fine
    - Start with database-level context
    - Narrow to table purpose
    - Finally describe individual columns
    """
    database_context = get_database_summary()
    table_context = analyze_table_structure(table_info)
    
    """
    Step 2: Fine-to-Coarse
    - Analyze individual column semantics
    - Use column relationships to understand table
    - Validate descriptions against table context
    """
    column_semantics = infer_column_meaning(column_info, table_context)
    table_description = synthesize_from_columns(column_semantics)
    
    return {
        "table_description": table_description,
        "column_descriptions": column_semantics
    }
```

**Research Results:**
- 0.93% improvement in SQL generation accuracy
- Descriptions under 20 words perform best for downstream tasks
- Overly long descriptions hinder Text-to-SQL effectiveness

## Tiered Documentation Strategy

### Complexity-Based Tiers

Research shows **70% time savings** through right-sizing documentation effort:

| Tier | Criteria | Token Budget | Documentation Depth |
|------|----------|--------------|---------------------|
| **Critical** | >100 dependencies, core business | 2000 | Full spec + examples |
| **Standard** | 10-100 dependencies | 1000 | Parameters + usage |
| **Basic** | <10 dependencies | 200 | Auto-generated only |

### Implementation

```python
def classify_and_document(conn, object_name):
    # Get dependency count
    deps = get_dependency_count(conn, object_name)
    code_length = get_code_length(conn, object_name)
    
    if deps > 100 or code_length > 10000:
        tier = "CRITICAL"
        prompt = CRITICAL_PROMPT_TEMPLATE
        max_tokens = 2000
    elif deps > 10 or code_length > 2000:
        tier = "STANDARD"
        prompt = STANDARD_PROMPT_TEMPLATE
        max_tokens = 1000
    else:
        tier = "BASIC"
        prompt = BASIC_PROMPT_TEMPLATE
        max_tokens = 200
    
    documentation = generate_with_llm(prompt, max_tokens)
    return tier, documentation

# Prompt Templates
CRITICAL_PROMPT_TEMPLATE = """
Generate comprehensive technical documentation for this database object.

Include:
1. **Purpose**: Business function and use cases
2. **Parameters**: Each parameter with type, constraints, defaults
3. **Business Logic**: Step-by-step process flow
4. **Dependencies**: Upstream/downstream objects
5. **Performance**: Expected execution characteristics
6. **Examples**: 2-3 usage examples with sample data
7. **Error Handling**: Common errors and resolution
8. **Change History**: Version notes if available

Object Definition:
{object_definition}

Related Objects:
{dependencies}
"""

STANDARD_PROMPT_TEMPLATE = """
Generate documentation for this database object.

Include:
1. **Purpose**: Brief description of function
2. **Parameters**: Name, type, description for each
3. **Returns**: Output description
4. **Example**: One usage example

Object Definition:
{object_definition}
"""

BASIC_PROMPT_TEMPLATE = """
Generate a one-paragraph description (max 200 characters) for:
{object_name} ({object_type})

Columns/Parameters: {column_list}
"""
```

## Fine-Tuning Strategies

### Databricks Bespoke Model Approach

**Why Fine-Tune?**
1. **Quality Control**: Full control over output quality
2. **Cost Reduction**: 10x cheaper than SaaS APIs
3. **Performance**: Smaller models = faster inference
4. **Consistency**: No external model updates affecting output

**Training Data Generation:**

```python
from datasets import Dataset

def generate_training_data():
    """Generate synthetic training examples"""
    
    # Source 1: NAICS codes for industry diversity
    naics_codes = load_naics_taxonomy()
    
    # Source 2: Internal use case patterns
    use_cases = load_internal_patterns()
    
    examples = []
    
    for industry in naics_codes:
        for use_case in use_cases:
            # Generate CREATE TABLE statement
            create_stmt = generate_create_statement(industry, use_case)
            
            # Generate documentation with teacher model
            docs = generate_docs_with_teacher(create_stmt)
            
            examples.append({
                "input": f"Document this table:\n{create_stmt}",
                "output": docs
            })
    
    return Dataset.from_list(examples)

# Generate ~3600 examples (proven sufficient by Databricks)
training_data = generate_training_data()
```

**Model Selection Criteria:**

| Model | License | Quality | Speed | Cost |
|-------|---------|---------|-------|------|
| MPT-7B | Commercial OK | Good | Fast | Low |
| Llama2-7B | Commercial OK | Good | Fast | Low |
| Mistral-7B | Apache 2.0 | Better | Fast | Low |
| Mixtral-8x7B | Apache 2.0 | Best | Medium | Medium |

**Fine-Tuning Configuration:**

```python
from transformers import TrainingArguments

training_args = TrainingArguments(
    output_dir="./sql-doc-model",
    num_train_epochs=3,
    per_device_train_batch_size=4,
    gradient_accumulation_steps=4,
    learning_rate=2e-5,
    warmup_steps=100,
    logging_steps=10,
    save_steps=500,
    fp16=True,  # Mixed precision for speed
    evaluation_strategy="steps",
    eval_steps=100
)

# Training time: ~15 minutes on A10 GPU
# Cost per run: ~$5-10
```

### Evaluation Framework

**Double-Blind Human Evaluation:**

```python
def evaluate_model_quality(model_a, model_b, test_tables):
    """
    Databricks evaluation approach:
    - 4 human evaluators
    - 62 evaluation examples (minimum viable)
    - Randomized presentation order
    - Allow ties
    """
    
    results = []
    
    for table in test_tables:
        # Generate outputs from both models
        output_a = model_a.generate(table)
        output_b = model_b.generate(table)
        
        # Randomize order
        if random.random() > 0.5:
            options = [output_a, output_b]
            mapping = ['A', 'B']
        else:
            options = [output_b, output_a]
            mapping = ['B', 'A']
        
        results.append({
            "table": table,
            "option_1": options[0],
            "option_2": options[1],
            "mapping": mapping
        })
    
    return results

# For automated evaluation at scale
def llm_evaluator(output_a, output_b, criteria):
    """Use LLM as evaluator with clear criteria"""
    
    prompt = f"""
    Compare these two table descriptions:
    
    Description A:
    {output_a}
    
    Description B:
    {output_b}
    
    Evaluation Criteria:
    1. Accuracy: Does it correctly describe the table?
    2. Completeness: Are all columns documented?
    3. Clarity: Is it easy to understand?
    4. Conciseness: Is it appropriately brief?
    
    Choose: A is better / B is better / Tie
    Explain your reasoning.
    """
    
    return llm.generate(prompt)
```

## Prompt Engineering for Documentation

### Effective Prompts for SQL Objects

**Stored Procedure Documentation:**

```python
STORED_PROCEDURE_PROMPT = """
You are a technical documentation specialist. Generate documentation for this SQL Server stored procedure.

## Procedure Code:
```sql
{procedure_code}
```

## Required Documentation Sections:

### Summary
[One sentence describing the procedure's purpose]

### Parameters
| Name | Type | Direction | Description |
|------|------|-----------|-------------|
[For each parameter]

### Business Logic
[Step-by-step explanation of what the procedure does]

### Dependencies
- Tables accessed: [list]
- Other procedures called: [list]
- Functions used: [list]

### Example Usage
```sql
[Realistic example with sample parameter values]
```

### Error Handling
[Describe error conditions and how they're handled]

### Performance Notes
[Any performance considerations]

Keep descriptions factual and derived from the code. Do not invent functionality not present.
"""
```

**Column Description Generation:**

```python
COLUMN_DESCRIPTION_PROMPT = """
Generate brief, accurate descriptions for each column in this table.

Table: {schema}.{table_name}
Purpose: {table_description}

Columns:
{column_list_with_types}

Rules:
- Max 100 characters per description
- Be specific about business meaning
- Include valid values if determinable from name
- Don't repeat the column name in the description

Output Format (JSON):
{
  "column_name": "description",
  ...
}
"""
```

### Token Optimization

**JavaScript/docx vs Markdown approach:**

Research finding: JavaScript-based document generation using the docx library consumes **63% fewer tokens** than markdown conversion while producing superior Word document formatting.

```python
# Token-efficient approach
def optimize_prompt_tokens(object_definition):
    """Reduce prompt tokens while maintaining context"""
    
    # Remove comments (usually redundant)
    clean_code = remove_sql_comments(object_definition)
    
    # Normalize whitespace
    clean_code = normalize_whitespace(clean_code)
    
    # Abbreviate common keywords (expand in post-processing)
    abbreviations = {
        'NVARCHAR': 'NVC',
        'VARCHAR': 'VC',
        'DATETIME': 'DT',
        'PRIMARY KEY': 'PK',
        'FOREIGN KEY': 'FK'
    }
    
    for full, abbrev in abbreviations.items():
        clean_code = clean_code.replace(full, abbrev)
    
    return clean_code

# Databricks finding: Shorter prompts = same quality, lower cost
# Fine-tuned models need less instruction = 50%+ token reduction
```

## Quality Assurance Patterns

### Validation Pipeline

```python
def validate_generated_documentation(doc, object_info):
    """Validate AI-generated documentation quality"""
    
    issues = []
    
    # Check completeness
    if not doc.get('summary'):
        issues.append("Missing summary")
    
    # Check parameter coverage
    expected_params = extract_parameters(object_info['definition'])
    documented_params = doc.get('parameters', [])
    
    missing_params = set(expected_params) - set(documented_params)
    if missing_params:
        issues.append(f"Undocumented parameters: {missing_params}")
    
    # Check for hallucinations
    mentioned_tables = extract_table_references(doc['business_logic'])
    actual_tables = extract_table_references(object_info['definition'])
    
    hallucinated = mentioned_tables - actual_tables
    if hallucinated:
        issues.append(f"Hallucinated tables: {hallucinated}")
    
    # Check description length
    if len(doc.get('summary', '')) > 500:
        issues.append("Summary too long (>500 chars)")
    
    return {
        "valid": len(issues) == 0,
        "issues": issues,
        "confidence": calculate_confidence(doc, issues)
    }
```

### Human-in-the-Loop Workflow

```python
class DocumentationWorkflow:
    """Workflow with human review for critical objects"""
    
    def __init__(self, auto_approve_threshold=0.9):
        self.threshold = auto_approve_threshold
    
    def process(self, object_name, tier):
        # Generate documentation
        doc = generate_documentation(object_name)
        
        # Validate
        validation = validate_generated_documentation(doc)
        
        if tier == "CRITICAL":
            # Always require human review
            return self.queue_for_review(doc, validation)
        
        elif validation['confidence'] >= self.threshold:
            # Auto-approve high-confidence
            return self.auto_approve(doc)
        
        else:
            # Queue medium-confidence for review
            return self.queue_for_review(doc, validation)
    
    def queue_for_review(self, doc, validation):
        """Send to human reviewer"""
        return {
            "status": "pending_review",
            "documentation": doc,
            "validation": validation,
            "reviewer_queue": "data-stewards"
        }
```

## References

### Research Papers
- "Creating a Bespoke LLM for AI-Generated Documentation" - Databricks Blog, 2023
- "How We Improved DatabricksIQ LLM Quality" - Databricks Blog, 2024
- "Automatic Database Description Generation for Text-to-SQL" - arXiv, 2025
- "Synthetic SQL Column Descriptions Impact on Text-to-SQL" - arXiv, 2024

### Industry Reports
- Stanford HAI 2025 AI Index Report
- Gartner Hype Cycle for Data & Analytics Governance 2025

### Tools
- Snowflake Cortex LLM
- Databricks Unity Catalog
- DBInsights.ai
- Secoda AI
