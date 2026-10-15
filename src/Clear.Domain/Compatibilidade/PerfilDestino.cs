namespace Clear.Domain.Compatibilidade;

/// <summary>Perfis mutuamente exclusivos; nenhum deles realiza migração.</summary>
public enum AmbienteDestino
{
    AzureFilesClassicoSmb,
    SharePointOneDriveEmpresarial
}

public enum SubtipoSharePoint
{
    SharePointOnline,
    OneDriveForBusiness
}

/// <summary>
/// Um único destino por execução. No primeiro catálogo, Azure Files significa
/// Microsoft.Storage + SMB (cliente Windows); NFS e OneDrive pessoal não se aplicam.
/// </summary>
public sealed record PerfilDestino
{
    private PerfilDestino(AmbienteDestino ambiente, SubtipoSharePoint? subtipo)
    {
        Ambiente = ambiente;
        Subtipo = subtipo;
    }

    public AmbienteDestino Ambiente { get; }
    public SubtipoSharePoint? Subtipo { get; }

    public static PerfilDestino AzureFilesSmb() =>
        new(AmbienteDestino.AzureFilesClassicoSmb, null);

    public static PerfilDestino SharePointEmpresarial(SubtipoSharePoint subtipo)
    {
        if (!Enum.IsDefined(subtipo))
            throw new ArgumentOutOfRangeException(nameof(subtipo));

        return new PerfilDestino(AmbienteDestino.SharePointOneDriveEmpresarial, subtipo);
    }
}
