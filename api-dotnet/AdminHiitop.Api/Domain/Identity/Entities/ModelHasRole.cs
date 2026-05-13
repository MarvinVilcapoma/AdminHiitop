namespace AdminHiitop.Api.Domain.Identity.Entities;

public sealed class ModelHasRole
{
    public const string UserModelType = "User";

    public int RoleId { get; set; }
    public string ModelType { get; set; } = UserModelType;
    public int ModelId { get; set; }

    public Role Role { get; set; } = null!;
}
