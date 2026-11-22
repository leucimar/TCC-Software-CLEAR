using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;

namespace Clear.Infrastructure.Quarentena;

/// <summary>
/// Preserva DACL, proprietário e grupo. Não configura compartilhamentos SMB,
/// não resolve nomes no AD e não concede privilégios ao processo.
/// As referências aos usuários/grupos permanecem pelos mesmos SIDs.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class PreservacaoPermissoesQuarentena
{
    private const AccessControlSections Secoes = AccessControlSections.Access |
        AccessControlSections.Owner | AccessControlSections.Group;
    private readonly List<(string Origem, string Destino, string Esperado)> _pastas = [];
    private readonly string _arquivoOrigem;
    private readonly string _arquivoDestino;
    private readonly string _segurancaArquivo;

    private PreservacaoPermissoesQuarentena(string origem, string destino)
    {
        _arquivoOrigem = origem;
        _arquivoDestino = destino;
        _segurancaArquivo = LerArquivo(origem);
    }

    public string ImpressaoArquivo => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(_segurancaArquivo)));

    /// <summary>
    /// Cria cada pasta com o descritor já no ato de criação: não cria uma pasta
    /// aberta para só depois restringi-la. Nunca altera ACL de pasta existente.
    /// </summary>
    public static PreservacaoPermissoesQuarentena Preparar(PlanoQuarentenaM7 plano)
    {
        var origem = Path.GetFullPath(plano.RaizAnalisada);
        var destino = Path.Combine(Path.GetFullPath(plano.RaizQuarentena),
            Path.GetFileName(Path.TrimEndingDirectorySeparator(origem)));
        var resultado = new PreservacaoPermissoesQuarentena(
            plano.CaminhoOriginal, plano.CaminhoDestino);

        // A raiz selecionada pelo operador pode ter outra política. A árvore
        // espelhada abaixo dela é protegida com a política efetiva da origem.
        var segurancaRaiz = LerPasta(origem);
        CriarAncestraisAusentes(Path.GetFullPath(plano.RaizQuarentena), segurancaRaiz);
        resultado.EspelharPasta(origem, destino);
        var relativo = Path.GetRelativePath(origem, Path.GetDirectoryName(plano.CaminhoOriginal)!);
        if (relativo != ".")
        {
            if (Path.IsPathRooted(relativo) || relativo == ".." ||
                relativo.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new IOException("Pasta original fora da raiz analisada.");
            foreach (var parte in relativo.Split(Path.DirectorySeparatorChar))
            {
                origem = Path.Combine(origem, parte);
                destino = Path.Combine(destino, parte);
                resultado.EspelharPasta(origem, destino);
            }
        }
        resultado.ValidarAntesDaMovimentacao();
        return resultado;
    }

    // A restauração não recria nem modifica a segurança das pastas originais.
    public static PreservacaoPermissoesQuarentena PrepararRestauracao(string origem, string destino)
    {
        VerificarSemLinks(origem);
        VerificarSemLinks(Path.GetDirectoryName(destino)!);
        return new PreservacaoPermissoesQuarentena(origem, destino);
    }

    public void ValidarAntesDaMovimentacao()
    {
        ValidarPastas();
        if (LerArquivo(_arquivoOrigem) != _segurancaArquivo)
            throw new IOException("As permissões do arquivo mudaram durante a operação; envio bloqueado.");
    }

    public void ValidarDepoisDaMovimentacao()
    {
        ValidarPastas();
        if (LerArquivo(_arquivoDestino) != _segurancaArquivo)
            throw new IOException("Não foi possível confirmar a preservação de DACL/proprietário/grupo. " +
                "A movimentação não será marcada como concluída; requer revisão da auditoria.");
    }

    private void EspelharPasta(string origem, string destino)
    {
        var seguranca = LerPasta(origem);
        var esperado = Normalizar(seguranca);
        CriarOuValidar(destino, seguranca);
        _pastas.Add((origem, destino, esperado));
    }

    private void ValidarPastas()
    {
        foreach (var pasta in _pastas)
            if (Normalizar(LerPasta(pasta.Origem)) != pasta.Esperado ||
                Normalizar(LerPasta(pasta.Destino)) != pasta.Esperado)
                throw new IOException("Permissões da pasta mudaram ou divergem da origem. Operação bloqueada: " + pasta.Destino);
    }

    private static void CriarAncestraisAusentes(string caminho, DirectorySecurity seguranca)
    {
        VerificarSemLinks(caminho);
        if (Directory.Exists(caminho)) return;
        var pai = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(caminho));
        if (string.IsNullOrWhiteSpace(pai)) throw new IOException("Raiz de quarentena inexistente.");
        CriarAncestraisAusentes(pai, seguranca);
        CriarOuValidar(caminho, seguranca);
    }

    private static void CriarOuValidar(string caminho, DirectorySecurity seguranca)
    {
        VerificarSemLinks(caminho);
        var esperado = Normalizar(seguranca);
        if (!Directory.Exists(caminho))
        {
            var copia = new DirectorySecurity();
            copia.SetSecurityDescriptorSddlForm(esperado, Secoes);
            // A criação inclui a DACL protegida, proprietário e grupo.
            // Falta de WRITE_DAC/WRITE_OWNER/privilégios interrompe a operação.
            new DirectoryInfo(caminho).Create(copia);
        }
        var atual = LerPasta(caminho);
        if (!atual.AreAccessRulesProtected || Normalizar(atual) != esperado)
            throw new IOException("A pasta de quarentena tem permissões diferentes ou herança aberta. " +
                "Nenhuma permissão existente foi alterada; escolha uma quarentena vazia/compatível: " + caminho);
    }

    private static DirectorySecurity LerPasta(string caminho)
    {
        VerificarSemLinks(caminho);
        if (!Directory.Exists(caminho)) throw new DirectoryNotFoundException(caminho);
        return new DirectoryInfo(caminho).GetAccessControl(Secoes);
    }

    private static string LerArquivo(string caminho)
    {
        VerificarSemLinks(caminho);
        if (!File.Exists(caminho)) throw new FileNotFoundException("Arquivo indisponível.", caminho);
        return Normalizar(new FileInfo(caminho).GetAccessControl(Secoes));
    }

    private static string Normalizar(FileSystemSecurity seguranca)
    {
        // Congela as permissões efetivas da origem na cópia, convertendo regras
        // herdadas em explícitas. Não chama SetAccessControl na origem.
        var copia = seguranca is DirectorySecurity ? (FileSystemSecurity)new DirectorySecurity() : new FileSecurity();
        copia.SetSecurityDescriptorBinaryForm(seguranca.GetSecurityDescriptorBinaryForm(), Secoes);
        var raw = new RawSecurityDescriptor(copia.GetSecurityDescriptorBinaryForm(), 0);
        if (raw.DiscretionaryAcl is null)
            throw new IOException("DACL ausente/nula: não presumir uma política de acesso segura.");
        copia.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
        var protegido = new RawSecurityDescriptor(copia.GetSecurityDescriptorBinaryForm(), 0);
        // Flags de auto-herança podem variar após CreateDirectory. Comparamos
        // os SIDs, máscaras, flags das ACEs e proteção, não esse carimbo do SO.
        var canonico = new RawSecurityDescriptor(ControlFlags.DiscretionaryAclPresent |
            ControlFlags.DiscretionaryAclProtected | ControlFlags.SelfRelative,
            protegido.Owner, protegido.Group, null, protegido.DiscretionaryAcl);
        return canonico.GetSddlForm(Secoes);
    }

    private static void VerificarSemLinks(string caminho)
    {
        var completo = Path.GetFullPath(caminho);
        if (File.Exists(completo) && (File.GetAttributes(completo) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Permissões: arquivo link/reparse point não é permitido.");
        for (DirectoryInfo? item = new(Directory.Exists(completo) ? completo : Path.GetDirectoryName(completo)!);
             item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Permissões: diretório link/junction não é permitido.");
    }
}
