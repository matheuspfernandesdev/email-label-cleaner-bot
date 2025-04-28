using System.Text;

namespace LimparEmail.Utility;

public static class LogHelper
{
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "Output");
    private static readonly object _lock = new object();

    public static void SalvarLog(string mensagem, string nomeArquivo)
    {
        //lock criado pois estava ocorrendo um erro de concorrencia no arquivo, como se estivesse utilizado em outro processo
        //o erro de fato era outro, porem deixei o lock aqui por via das dúvidas
        lock (_lock)
        {
            CriarDiretorioSeNaoExistir();
            string caminhoArquivo = Path.Combine(OutputDir, nomeArquivo);

            string linha = $"[{DateTime.Now:dd-MM-yyyy HH:mm:ss}] - {mensagem}{Environment.NewLine}";
            File.AppendAllText(caminhoArquivo, linha);
        }
    }

    public static void SalvarCsv(string conteudoLinhaCsv, string nomeArquivo)
    {
        CriarDiretorioSeNaoExistir();

        string caminhoArquivo = Path.Combine(OutputDir, nomeArquivo);

        if (!File.Exists(caminhoArquivo))
            File.Create(caminhoArquivo).Dispose();
        
        string linha = conteudoLinhaCsv + Environment.NewLine;

        File.AppendAllText(caminhoArquivo, linha, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void CriarDiretorioSeNaoExistir()
    {
        if (!Directory.Exists(OutputDir))
            Directory.CreateDirectory(OutputDir);
    }
}

