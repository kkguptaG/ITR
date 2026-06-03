namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Maps the common return model + computation to the ITD-format ITR JSON for the selected form.
/// A form is a PROJECTION of the shared capture model — adding a form = adding a mapper here, not
/// rebuilding capture or computation. All four individual forms (ITR-1/2/3/4) are implemented.
/// Throws AppException("ITRJSON.FORM_UNSUPPORTED", 422) for any other form (ITR-5/6/7).
/// </summary>
public interface IItrJsonGenerationService
{
    GeneratedItrJson Generate(ItrFilingContext ctx);
}
