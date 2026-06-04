using TallyG.Tax.Domain.Common;

namespace TallyG.Tax.Api.Modules.Returns;

/// <summary>
/// Application service for the Returns/Filing module (docs 04 §4.2). Owns the TaxReturn
/// aggregate and its child income heads/deductions, the pre-file validation, and the
/// pay → file → snapshot saga. Every operation is scoped to the current user + tenant
/// (cross-tenant/owner access surfaces as 404). Auto-registered scoped by Scrutor.
/// </summary>
public interface IReturnService
{
    // --- return header CRUD ---
    Task<ReturnDetailDto> CreateAsync(CreateReturnRequest request, CancellationToken ct = default);
    Task<PagedResult<ReturnSummaryDto>> ListAsync(string? ay, string? status, string? itrType, int page, int pageSize, CancellationToken ct = default);
    Task<ReturnDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ReturnDetailDto> UpdateAsync(Guid id, UpdateReturnRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // --- income sources (generic head rows) ---
    Task<IReadOnlyList<IncomeSourceDto>> ListIncomeSourcesAsync(Guid id, CancellationToken ct = default);
    Task<IncomeSourceDto> AddIncomeSourceAsync(Guid id, UpsertIncomeSourceRequest request, CancellationToken ct = default);
    Task<IncomeSourceDto> UpdateIncomeSourceAsync(Guid id, Guid sourceId, UpsertIncomeSourceRequest request, CancellationToken ct = default);
    Task DeleteIncomeSourceAsync(Guid id, Guid sourceId, CancellationToken ct = default);

    // --- salary ---
    Task<IReadOnlyList<SalaryDetailDto>> ListSalariesAsync(Guid id, CancellationToken ct = default);
    Task<SalaryDetailDto> AddSalaryAsync(Guid id, UpsertSalaryRequest request, CancellationToken ct = default);
    Task<SalaryDetailDto> UpdateSalaryAsync(Guid id, Guid salaryId, UpsertSalaryRequest request, CancellationToken ct = default);
    Task DeleteSalaryAsync(Guid id, Guid salaryId, CancellationToken ct = default);

    // --- house property ---
    Task<IReadOnlyList<HousePropertyDto>> ListHousePropertiesAsync(Guid id, CancellationToken ct = default);
    Task<HousePropertyDto> AddHousePropertyAsync(Guid id, UpsertHousePropertyRequest request, CancellationToken ct = default);
    Task<HousePropertyDto> UpdateHousePropertyAsync(Guid id, Guid propertyId, UpsertHousePropertyRequest request, CancellationToken ct = default);
    Task DeleteHousePropertyAsync(Guid id, Guid propertyId, CancellationToken ct = default);

    // --- capital gains ---
    Task<IReadOnlyList<CapitalGainDto>> ListCapitalGainsAsync(Guid id, CancellationToken ct = default);
    Task<CapitalGainDto> AddCapitalGainAsync(Guid id, UpsertCapitalGainRequest request, CancellationToken ct = default);
    Task<CapitalGainDto> UpdateCapitalGainAsync(Guid id, Guid gainId, UpsertCapitalGainRequest request, CancellationToken ct = default);
    Task DeleteCapitalGainAsync(Guid id, Guid gainId, CancellationToken ct = default);

    /// <summary>Preview (Commit=false) or commit (Commit=true) a bulk CSV import of capital-gain rows.</summary>
    Task<CapitalGainImportResult> ImportCapitalGainsAsync(Guid id, CapitalGainImportRequest request, CancellationToken ct = default);

    // --- immovable-property buyers (s.194-IA) attached to a capital gain ---
    Task<IReadOnlyList<CapitalGainBuyerDto>> ListCapitalGainBuyersAsync(Guid id, Guid gainId, CancellationToken ct = default);
    Task<CapitalGainBuyerDto> AddCapitalGainBuyerAsync(Guid id, Guid gainId, UpsertCapitalGainBuyerRequest request, CancellationToken ct = default);
    Task DeleteCapitalGainBuyerAsync(Guid id, Guid gainId, Guid buyerId, CancellationToken ct = default);

    // --- business income ---
    Task<IReadOnlyList<BusinessIncomeDto>> ListBusinessIncomesAsync(Guid id, CancellationToken ct = default);
    Task<BusinessIncomeDto> AddBusinessIncomeAsync(Guid id, UpsertBusinessIncomeRequest request, CancellationToken ct = default);
    Task<BusinessIncomeDto> UpdateBusinessIncomeAsync(Guid id, Guid businessId, UpsertBusinessIncomeRequest request, CancellationToken ct = default);
    Task DeleteBusinessIncomeAsync(Guid id, Guid businessId, CancellationToken ct = default);

    // --- deductions ---
    Task<IReadOnlyList<DeductionDto>> ListDeductionsAsync(Guid id, CancellationToken ct = default);
    Task<DeductionDto> AddDeductionAsync(Guid id, UpsertDeductionRequest request, CancellationToken ct = default);
    Task<DeductionDto> UpdateDeductionAsync(Guid id, Guid deductionId, UpsertDeductionRequest request, CancellationToken ct = default);
    Task DeleteDeductionAsync(Guid id, Guid deductionId, CancellationToken ct = default);

    // --- lifecycle actions ---
    Task<ValidateReturnResponse> ValidateAsync(Guid id, CancellationToken ct = default);
    Task<SubmitReturnResponse> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<ReturnStatusDto> GetStatusAsync(Guid id, CancellationToken ct = default);
    Task<ItrSelectionVerdict> SuggestTypeAsync(Guid id, CancellationToken ct = default);
}
