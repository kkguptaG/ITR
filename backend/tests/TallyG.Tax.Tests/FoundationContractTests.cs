using FluentAssertions;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Domain.TaxEngine;
using Xunit;

namespace TallyG.Tax.Tests;

/// <summary>
/// Smoke tests over the shared kernel + tax-engine contract the foundation owns.
/// The real golden-master tax-engine vectors are added by the Tax feature agent once
/// <see cref="TaxCalculator"/> is implemented.
/// </summary>
public class FoundationContractTests
{
    [Fact]
    public void AppException_carries_code_and_status()
    {
        var ex = new AppException("TAX.RULESET_MISSING", "No active rule set", 422);

        ex.Code.Should().Be("TAX.RULESET_MISSING");
        ex.HttpStatus.Should().Be(422);
        ex.Message.Should().Be("No active rule set");
    }

    [Fact]
    public void AppException_factories_set_expected_status()
    {
        AppException.NotFound("x").HttpStatus.Should().Be(404);
        AppException.Forbidden("x").HttpStatus.Should().Be(403);
        AppException.Conflict("x").HttpStatus.Should().Be(409);
        AppException.Validation("x").HttpStatus.Should().Be(422);
    }

    [Fact]
    public void PagedResult_computes_total_pages()
    {
        var page = new PagedResult<int>(new[] { 1, 2, 3 }, page: 1, pageSize: 10, total: 23);

        page.Items.Should().HaveCount(3);
        page.TotalPages.Should().Be(3);
    }

    [Fact]
    public void Result_success_and_failure_behave()
    {
        Result.Success().Succeeded.Should().BeTrue();

        var fail = Result.Failure("PAYMENT.SIGNATURE_INVALID", "bad signature");
        fail.Failed.Should().BeTrue();
        fail.Code.Should().Be("PAYMENT.SIGNATURE_INVALID");

        var ok = Result.Success(42);
        ok.Succeeded.Should().BeTrue();
        ok.Value.Should().Be(42);
    }

    [Fact]
    public void TaxCalculator_is_implemented_and_rejects_an_empty_ruleset()
    {
        // The Tax feature agent has replaced the foundation stub with the real engine. An empty
        // rule-set document is invalid input and must fail loudly (rather than silently computing
        // zero tax), so this guards that the contract is wired AND that the engine validates its law.
        ITaxCalculator engine = new TaxCalculator();
        var input = new TaxComputationInput
        {
            AssessmentYearCode = "AY2025-26",
            RuleSetVersion = "1.0.0",
            RulesJson = "{}"
        };

        var compute = () => engine.Compute(input, Regime.New);
        var compare = () => engine.Compare(input);

        compute.Should().Throw<AppException>().Which.Code.Should().Be("TAX.RULESET_INVALID");
        compare.Should().Throw<AppException>().Which.Code.Should().Be("TAX.RULESET_INVALID");
    }

    [Fact]
    public void Itr_and_status_enums_match_contract_names()
    {
        // Guards against accidental renames that would break parallel feature code.
        Enum.GetNames<ItrType>().Should().BeEquivalentTo("ITR1", "ITR2", "ITR3", "ITR4");
        Enum.IsDefined(ReturnStatus.ComputedReady).Should().BeTrue();
        Enum.IsDefined(Regime.Old).Should().BeTrue();
        Enum.IsDefined(Regime.New).Should().BeTrue();
    }
}
