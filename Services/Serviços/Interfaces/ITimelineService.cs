// ========================================
// ITimelineService.cs
// Localização: Business_Logic/Serviços/Interfaces/ITimelineService.cs
// ========================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SistemaProducao3D.Modelos.Timeline;

namespace Business_Logic.Serviços.Interfaces
{
    /// <summary>
    /// Interface para serviço de construção e análise de timeline de produção
    /// </summary>
    public interface ITimelineService
    {
        // ========================================
        // MÉTODOS BÁSICOS
        // ========================================

        /// <summary>
        /// Obtém timeline completa de um dia específico para uma máquina
        /// </summary>
        Task<List<BlocoTimeline>> ObterTimelineDiaAsync(int maquinaId, DateTime data);

        /// <summary>
        /// Obtém timeline enriquecida com eventos da API Ultimaker
        /// </summary>
        Task<List<BlocoTimeline>> ObterTimelineEnriquecidaAsync(
            int maquinaId,
            DateTime data,
            bool incluirEventos = true);

        /// <summary>
        /// Obtém timeline de múltiplos dias agrupados por data
        /// </summary>
        Task<Dictionary<DateTime, List<BlocoTimeline>>> ObterTimelinePeriodoAsync(
            int maquinaId,
            DateTime dataInicio,
            DateTime dataFim);

        /// <summary>
        /// Valida se a timeline possui 100% de cobertura (1440 minutos)
        /// </summary>
        

        // ========================================
        // MÉTODOS DE ANÁLISE EM CAMADAS
        // ========================================

        /// <summary>
        /// Obtém resumo consolidado de todas as impressoras em um mês
        /// CAMADA 1 - Visão Executiva
        /// </summary>
        Task<ResumoConsolidado> ObterResumoConsolidado(int ano, int mes);

        /// <summary>
        /// Obtém resumo mensal de uma impressora específica
        /// CAMADA 2 - Análise por Impressora
        /// </summary>
        Task<ResumoMensal> ObterResumoMensal(int maquinaId, int ano, int mes);

        /// <summary>
        /// Obtém resumo de um dia específico com timeline
        /// CAMADA 3 e 4 - Análise Diária e Timeline Horária
        /// </summary>
        Task<ResumoDiario> ObterResumoDiario(int maquinaId, DateTime data);
    }

    /// <summary>
    /// Resultado de validação de cobertura da timeline
    /// </summary>
    public class ResultadoValidacao
    {
        public bool Valido { get; set; }
        public int MinutosTotais { get; set; }
        public int MinutosEsperados => 1440;
        public int Diferenca => MinutosTotais - MinutosEsperados;
        public List<string> Problemas { get; set; } = new List<string>();
        public List<string> Avisos { get; set; } = new List<string>();
    }
}