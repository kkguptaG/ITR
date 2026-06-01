namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Maps the common return model + computation to the ITD-format ITR JSON for the selected form.
/// A form is a PROJECTION of the shared capture model — adding a form = adding a mapper here, not
/// rebuilding capture or computation. Throws AppException("ITRJSON.FORM_UNSUPPORTED", 422) for forms
/// whose mapper is not yet implemented (capture is ready; the schema mapper is on the roadmap).
/// </summary>
public interface IItrJsonGenerationService
{
    GeneratedItrJson Generate(ItrFilingContext ctx);
}
