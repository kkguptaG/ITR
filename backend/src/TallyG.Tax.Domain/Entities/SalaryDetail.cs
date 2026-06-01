using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Domain.Entities;

/// <summary>Salary head detail, mapping to Form 16 Part B (Ch.2 §2.5).</summary>
public class SalaryDetail : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid TaxReturnId { get; set; }

    public string Employer { get; set; } = string.Empty;
    public string? Tan { get; set; }

    public decimal Gross { get; set; }
    public decimal Hra { get; set; }
    public decimal Perquisites { get; set; }
    public decimal ProfitsInLieu { get; set; }
    public decimal ExemptAllowances { get; set; }
    public decimal HraExemption { get; set; }
    public decimal StdDeduction { get; set; }
    public decimal ProfessionalTax { get; set; }

    public Guid? Form16DocumentId { get; set; }

    public TaxReturn? TaxReturn { get; set; }
}
