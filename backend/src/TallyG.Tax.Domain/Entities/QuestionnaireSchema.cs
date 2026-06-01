using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.Entities;

/// <summary>
/// Schema-driven dynamic questionnaire (Ch.3 §3.3), versioned by AY. A DAG of question
/// nodes + branching rules stored as JSON; the runtime is a dumb interpreter.
/// </summary>
public class QuestionnaireSchema : BaseEntity
{
    public Guid AssessmentYearId { get; set; }

    public string Version { get; set; } = string.Empty;

    /// <summary>The node/rule DAG (jsonb on Postgres, text on Sqlite).</summary>
    public string SchemaJson { get; set; } = "{}";

    public SchemaStatus Status { get; set; } = SchemaStatus.Draft;

    public AssessmentYear? AssessmentYear { get; set; }
}
