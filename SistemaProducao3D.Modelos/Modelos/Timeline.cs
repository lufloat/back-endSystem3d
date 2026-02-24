using System;
using System.Collections.Generic;
using System.Linq;

namespace SistemaProducao3D.Modelos.Timeline
{
    // ========================================
    // ENUMS
    // ========================================

    public enum StatusMaquina
    {
        Producao = 1,      // 🟢 Imprimindo
        Pausa = 2,         // 🟡 Pausada durante job
        Ociosidade = 3,    // 🔴 Sem job
        EsperaOperador = 4,// 🟠 Aguardando operador
        Manutencao = 5     // 🔵 Em manutenção
    }

    public enum MotivoStatus
    {
        // PRODUÇÃO
        ProducaoNormal = 1,

        // PAUSAS
        TrocaMaterial = 10,
        TrocaHotend = 11,
        AjusteImpressao = 12,
        LimpezaMesa = 13,
        FalhaTemporaria = 14,
        JobAbortado = 15,        // ⭐ ADICIONADO
        JobFalhado = 16,         // ⭐ ADICIONADO

        // OCIOSIDADE
        FaltaJob = 20,
        AguardandoAprovacao = 21,
        FimExpediente = 22,

        // ESPERA OPERADOR
        EsperaInicioJob = 30,
        EsperaRetomada = 31,
        EsperaRemocaoPeca = 32,

        // MANUTENÇÃO
        ManutencaoPreventiva = 40,
        ManutencaoCorretiva = 41,
        Calibracao = 42,

        // OUTROS
        Desconhecido = 99
    }

    // ========================================
    // CAMADA 4 - TIMELINE HORÁRIA (Base da pirâmide)
    // ========================================

    /// <summary>
    /// Bloco da timeline - Representa 1 período de tempo
    /// Exemplo: 08:00-09:30 = Produção (Job XYZ)
    /// </summary>
    public class BlocoTimeline
    {
        public DateTime Inicio { get; set; }
        public DateTime Fim { get; set; }
        public int DuracaoMinutos { get; set; }

        public StatusMaquina Status { get; set; }
        public MotivoStatus Motivo { get; set; }
        public string Mensagem { get; set; } = string.Empty;

        // Informações do Job (se aplicável)
        public string? JobUuid { get; set; }
        public string? JobName { get; set; }
        public int? TypeId { get; set; } // ID do evento da API
    }

    // ========================================
    // CAMADA 3 - RESUMO DIÁRIO
    // ========================================

    /// <summary>
    /// Resumo de 1 dia - Consolida 24 blocos horários
    /// </summary>
    public class ResumoDiario
    {
        public DateTime Data { get; set; }
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;

        // Timeline do dia (0h-24h)
        public List<BlocoTimeline> Timeline { get; set; } = new();

        // Tempos em MINUTOS
        public decimal TempoProducao { get; set; }    // 720 min
        public decimal TempoPausas { get; set; }      // 360 min
        public decimal TempoOciosidade { get; set; }  // 360 min
        public decimal TempoEsperaOperador { get; set; }
        public decimal TempoManutencao { get; set; }

        // Taxas em PERCENTUAL (devem somar 100%)
        public decimal TaxaProducao => CalcularTaxa(TempoProducao);
        public decimal TaxaPausas => CalcularTaxa(TempoPausas);
        public decimal TaxaOciosidade => CalcularTaxa(TempoOciosidade);
        public decimal TaxaEsperaOperador => CalcularTaxa(TempoEsperaOperador);
        public decimal TaxaManutencao => CalcularTaxa(TempoManutencao);

        // Motivos consolidados do dia
        public List<MotivoConsolidado> Motivos { get; set; } = new();

        private decimal CalcularTaxa(decimal minutos)
        {
            return Math.Round((minutos / 1440m) * 100, 2); // 1440 = 24h
        }
    }

    /// <summary>
    /// Motivo consolidado - Agrupa blocos por status+motivo
    /// </summary>
    public class MotivoConsolidado
    {
        public StatusMaquina Status { get; set; }
        public MotivoStatus Motivo { get; set; }
        public string MotivoDescricao { get; set; } = string.Empty;
        public int TempoTotal { get; set; } // em minutos
        public int Ocorrencias { get; set; }
        public decimal Percentual { get; set; }
    }

    // ========================================
    // CAMADA 2 - RESUMO MENSAL POR IMPRESSORA
    // ========================================

    /// <summary>
    /// Resumo do mês de 1 impressora - Consolida 28-31 dias
    /// </summary>
    public class ResumoMensal
    {
        public int Ano { get; set; }
        public int Mes { get; set; }
        public string MesNome { get; set; } = string.Empty; // "Janeiro"
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;

        // Calendário do mês
        public List<ResumoDiario> Dias { get; set; } = new();

        // Tempos em HORAS
        public decimal TempoTotal { get; set; }      // 720h (30 dias)
        public decimal TempoProducao { get; set; }   // 273h (38%)
        public decimal TempoPausas { get; set; }     // 173h (24%)
        public decimal TempoOciosidade { get; set; } // 274h (38%)
        public decimal TempoEsperaOperador { get; set; }
        public decimal TempoManutencao { get; set; }

        // Taxas em PERCENTUAL (devem somar 100%)
        public decimal TaxaProducao => CalcularTaxa(TempoProducao, TempoTotal);
        public decimal TaxaPausas => CalcularTaxa(TempoPausas, TempoTotal);
        public decimal TaxaOciosidade => CalcularTaxa(TempoOciosidade, TempoTotal);
        public decimal TaxaEsperaOperador => CalcularTaxa(TempoEsperaOperador, TempoTotal);
        public decimal TaxaManutencao => CalcularTaxa(TempoManutencao, TempoTotal);

        // Motivos consolidados do mês
        public List<MotivoConsolidado> Motivos { get; set; } = new();

        // Métricas de Jobs
        public int JobsFinalizados { get; set; }
        public int JobsAbortados { get; set; }
        public decimal TaxaSucesso => JobsFinalizados + JobsAbortados > 0
            ? Math.Round((decimal)JobsFinalizados / (JobsFinalizados + JobsAbortados) * 100, 1)
            : 0;

        private decimal CalcularTaxa(decimal valor, decimal total)
        {
            return total > 0 ? Math.Round((valor / total) * 100, 2) : 0;
        }
    }

    // ========================================
    // CAMADA 1 - RESUMO CONSOLIDADO (Topo da pirâmide)
    // ========================================

    /// <summary>
    /// Visão executiva do mês - Consolida TODAS as impressoras
    /// </summary>
    public class ResumoConsolidado
    {
        public int Ano { get; set; }
        public int Mes { get; set; }
        public string Periodo { get; set; } = string.Empty; // "JAN/26"

        // Resumos individuais
        public List<ResumoMensal> Impressoras { get; set; } = new();

        // Totais em HORAS
        public decimal TempoTotalDisponivel { get; set; } // 4320h (6 máquinas x 30 dias)
        public decimal TempoProducaoTotal { get; set; }   // 1737h
        public decimal TempoPausasTotal { get; set; }     // 1120h
        public decimal TempoOciosidadeTotal { get; set; } // 1715h
        public decimal TempoEsperaOperadorTotal { get; set; }
        public decimal TempoManutencaoTotal { get; set; }

        // Taxas CONSOLIDADAS (devem somar 100%)
        public decimal TaxaProducao { get; set; }      // 38%
        public decimal TaxaPausas { get; set; }        // 24%
        public decimal TaxaOciosidade { get; set; }    // 38%
        public decimal TaxaEsperaOperador { get; set; }
        public decimal TaxaManutencao { get; set; }

        // Métricas principais
        public decimal Utilizacao { get; set; }      // = TaxaProducao
        public decimal HorasProdutivas { get; set; } // 1737h
        public decimal TaxaSucesso { get; set; }     // 55%

        // Top motivos (ranking estratégico)
        public List<MotivoConsolidado> TopMotivosPausas { get; set; } = new();
        public List<MotivoConsolidado> TopMotivosOciosidade { get; set; } = new();
        public List<MotivoConsolidado> TopMotivosEsperaOperador { get; set; } = new();

        public int TotalImpressoras => Impressoras.Count;
    }

    // ========================================
    // HELPER - MAPEADOR DE MOTIVOS
    // ========================================

    public static class MapeadorMotivos
    {
        /// <summary>
        /// Determina o motivo baseado em eventos da API e banco de dados
        /// </summary>
        public static MotivoStatus DeterminarMotivo(
            StatusMaquina status,
            int? typeId,
            string mensagem,
            TimeSpan? duracao)
        {
            // PRODUÇÃO
            if (status == StatusMaquina.Producao)
            {
                return MotivoStatus.ProducaoNormal;
            }

            // PAUSAS - Baseado em type_id da API
            if (status == StatusMaquina.Pausa)
            {
                // Troca de material (type_id = 65537)
                if (typeId == 65537)
                    return MotivoStatus.TrocaMaterial;

                // Troca de hotend (type_id = 65536)
                if (typeId == 65536)
                    return MotivoStatus.TrocaHotend;

                // Pausa curta (< 10 min)
                if (duracao.HasValue && duracao.Value.TotalMinutes < 10)
                    return MotivoStatus.AjusteImpressao;

                // Pausa média (10-30 min)
                if (duracao.HasValue && duracao.Value.TotalMinutes < 30)
                    return MotivoStatus.LimpezaMesa;

                return MotivoStatus.FalhaTemporaria;
            }

            // OCIOSIDADE
            if (status == StatusMaquina.Ociosidade)
            {
                // Verificar se é período noturno (23h-6h)
                if (mensagem.Contains("noite", StringComparison.OrdinalIgnoreCase) ||
                    mensagem.Contains("expediente", StringComparison.OrdinalIgnoreCase))
                {
                    return MotivoStatus.FimExpediente;
                }

                // Ociosidade longa (> 2h) = provavelmente falta de job
                if (duracao.HasValue && duracao.Value.TotalHours > 2)
                    return MotivoStatus.FaltaJob;

                return MotivoStatus.AguardandoAprovacao;
            }

            // ESPERA OPERADOR
            if (status == StatusMaquina.EsperaOperador)
            {
                if (mensagem.Contains("início", StringComparison.OrdinalIgnoreCase))
                    return MotivoStatus.EsperaInicioJob;

                if (mensagem.Contains("retomada", StringComparison.OrdinalIgnoreCase))
                    return MotivoStatus.EsperaRetomada;

                return MotivoStatus.EsperaRemocaoPeca;
            }

            // MANUTENÇÃO
            if (status == StatusMaquina.Manutencao)
            {
                if (mensagem.Contains("preventiva", StringComparison.OrdinalIgnoreCase))
                    return MotivoStatus.ManutencaoPreventiva;

                if (mensagem.Contains("calibr", StringComparison.OrdinalIgnoreCase))
                    return MotivoStatus.Calibracao;

                return MotivoStatus.ManutencaoCorretiva;
            }

            return MotivoStatus.Desconhecido;
        }

        /// <summary>
        /// Retorna descrição amigável do motivo
        /// </summary>
        public static string ObterDescricaoMotivo(MotivoStatus motivo)
        {
            return motivo switch
            {
                MotivoStatus.ProducaoNormal => "Produção contínua",
                MotivoStatus.TrocaMaterial => "Troca de material",
                MotivoStatus.TrocaHotend => "Troca de hotend",
                MotivoStatus.AjusteImpressao => "Ajustes durante impressão",
                MotivoStatus.LimpezaMesa => "Limpeza da mesa",
                MotivoStatus.FalhaTemporaria => "Falha temporária",
                MotivoStatus.JobAbortado => "Job abortado/cancelado",      // ⭐ ADICIONADO
                MotivoStatus.JobFalhado => "Job falhou ao iniciar",        // ⭐ ADICIONADO
                MotivoStatus.FaltaJob => "Aguardando novo job",
                MotivoStatus.AguardandoAprovacao => "Aguardando aprovação",
                MotivoStatus.FimExpediente => "Fim do expediente",
                MotivoStatus.EsperaInicioJob => "Aguardando início do job",
                MotivoStatus.EsperaRetomada => "Aguardando retomada do operador",
                MotivoStatus.EsperaRemocaoPeca => "Aguardando remoção de peça",
                MotivoStatus.ManutencaoPreventiva => "Manutenção preventiva",
                MotivoStatus.ManutencaoCorretiva => "Manutenção corretiva",
                MotivoStatus.Calibracao => "Calibração",
                _ => "Não identificado"
            };
        }

        /// <summary>
        /// Retorna cor para visualização
        /// </summary>
        public static string ObterCor(StatusMaquina status)
        {
            return status switch
            {
                StatusMaquina.Producao => "#22c55e",        // Verde
                StatusMaquina.Pausa => "#eab308",           // Amarelo
                StatusMaquina.Ociosidade => "#ef4444",      // Vermelho
                StatusMaquina.EsperaOperador => "#f97316",  // Laranja
                StatusMaquina.Manutencao => "#3b82f6",      // Azul
                _ => "#9ca3af"                              // Cinza
            };
        }

        /// <summary>
        /// Retorna ícone para visualização
        /// </summary>
        public static string ObterIcone(StatusMaquina status)
        {
            return status switch
            {
                StatusMaquina.Producao => "Play",
                StatusMaquina.Pausa => "Pause",
                StatusMaquina.Ociosidade => "Circle",
                StatusMaquina.EsperaOperador => "User",
                StatusMaquina.Manutencao => "Wrench",
                _ => "HelpCircle"
            };
        }
    }
}