// ========================================
// UltimakerEventExtensions.cs
// Localização: SistemaProducao3D.Integration/Ultimaker/UltimakerEventExtensions.cs
// ========================================
// Extensão para facilitar extração de JobUuid dos eventos
// ========================================

using SistemaProducao3D.Integration.Ultimaker;
using System.Text.RegularExpressions;

namespace SistemaProducao3D.Integration.Ultimaker
{
    public static class UltimakerEventExtensions
    {
        /// <summary>
        /// Extrai o UUID do job do campo Message do evento
        /// </summary>
        public static string GetJobUuid(this UltimakerEvent evento)
        {
            if (string.IsNullOrEmpty(evento.Message))
                return string.Empty;

            // Exemplos de mensagens:
            // "Print paused (Job: abc123-def456...)"
            // "Print resumed (Job: abc123-def456...)"
            // "Print aborted (Job: abc123-def456...)"

            // Regex para extrair UUID (formato: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
            var match = Regex.Match(evento.Message, @"Job:\s*([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Se não encontrar no formato esperado, tentar encontrar qualquer UUID na mensagem
            match = Regex.Match(evento.Message, @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return string.Empty;
        }
    }
}