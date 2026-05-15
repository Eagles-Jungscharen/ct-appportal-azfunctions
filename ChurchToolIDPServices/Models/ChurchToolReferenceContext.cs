namespace EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Models;
public interface IChurchToolReferenceContext
{
    string ChurchToolIDPReferenceId { get; }
}

public class FunctionChurchToolReferenceContext : IChurchToolReferenceContext
{
    private static readonly AsyncLocal<string?> _churchToolIDPReferenceId = new();

    public string ChurchToolIDPReferenceId =>
        _churchToolIDPReferenceId.Value ?? throw new InvalidOperationException("ChurchToolIDPReferenceId not set");

    public static void Set(string churchToolIDPReferenceId) => _churchToolIDPReferenceId.Value = churchToolIDPReferenceId;
}
