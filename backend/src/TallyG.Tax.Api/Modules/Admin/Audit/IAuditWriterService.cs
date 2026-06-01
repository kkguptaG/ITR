using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Api.Modules.Admin.Audit;

/// <summary>
/// Scrutor-bindable surface over the domain <see cref="IAuditWriter"/> contract. The DI convention
/// (Program.cs) auto-registers any "<c>FooService : IFooService</c>" scoped via name matching, so
/// the writer is registered against <b>this</b> interface — inject <see cref="IAuditWriterService"/>
/// (which is an <see cref="IAuditWriter"/>) wherever an audit trail is needed. It adds no members;
/// it exists purely so the writer participates in the name-matching scan without a manual
/// registration or a Program.cs edit.
/// </summary>
public interface IAuditWriterService : IAuditWriter;
