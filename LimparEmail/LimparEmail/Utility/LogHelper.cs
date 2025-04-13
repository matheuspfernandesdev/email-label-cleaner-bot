﻿using System.Text;

namespace LimparEmail.Utility;

public static class LogHelper
{
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "Output");

    public static void SalvarLog(string mensagem, string nomeArquivo)
    {
        CriarDiretorioSeNaoExistir();

        string caminhoArquivo = Path.Combine(OutputDir, nomeArquivo);

        if (!File.Exists(caminhoArquivo))
            File.Create(caminhoArquivo).Dispose();
        
        string linha = $"[{DateTime.Now:dd-MM-yyyy HH:mm:ss}] - {mensagem}{Environment.NewLine}";

        File.AppendAllText(caminhoArquivo, linha);
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

