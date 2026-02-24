// ============================================================
// DashboardController.cs — VERSÃO FINAL
// CORREÇÃO DEFINITIVA DAS PAUSAS:
//
// PROBLEMA RAIZ (matemático):
//   M04 JAN/2026: 0.2h pausa / 744h total × 100 = 0.027%
//   Math.Round(0.027, 1) = 0.0  ← SEMPRE ZERO com 1 casa decimal
//
// SOLUÇÃO: usar 2 casas decimais (round=2) em TODOS os campos taxaPausas
//   Math.Round(0.027, 2) = 0.03  ← VISÍVEL
//
// Os outros campos (produção, ociosidade) ficam com 1 casa pois
// seus valores são grandes o suficiente (30%, 68%) para não desaparecer.
// ============================================================

using Business_Logic.Serviços.Interfaces;
using Business_Logic.Repositories.Interfaces;
using Business_Logic.Serviços.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Business_Logic.Repositories;
using SistemaProducao3D.Integration.Ultimaker;
using SistemaProducao3D.Modelos.Timeline;
using Business_Logic.Serviços;

namespace SistemaProducao3D.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardSKUService _dashboardService;
        private readonly IVisaoGeralService _visaoGeralService;
        private readonly ICardService _cardService;
        private readonly ISyncService _syncService;
        private readonly IProducaoRepository _producaoRepository;
        private readonly IMaterialRepository _materialRepository;
        private readonly IUltimakerClient _ultimakerClient;
        private readonly ITimelineService _timelineService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IDashboardSKUService dashboardService,
            IVisaoGeralService visaoGeralService,
            ICardService cardService,
            ISyncService syncService,
            IProducaoRepository producaoRepository,
            IMaterialRepository materialRepository,
            IUltimakerClient ultimakerClient,
            ITimelineService timelineService,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _visaoGeralService = visaoGeralService;
            _cardService = cardService;
            _syncService = syncService;
            _producaoRepository = producaoRepository;
            _materialRepository = materialRepository;
            _ultimakerClient = ultimakerClient;
            _timelineService = timelineService;
            _logger = logger;
        }

        // ============================================================
        // KPIs e SKUs
        // ============================================================

        [HttpGet("kpis")]
        public async Task<IActionResult> ObterKPIs(
            [FromQuery] int? ano = null,
            [FromQuery] int mesInicio = 1,
            [FromQuery] int? mesFim = null)
        {
            try
            {
                var kpis = await _dashboardService.ObterMetricasKPI(ano, mesInicio, mesFim);
                return Ok(kpis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter KPIs");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("evolucao-skus")]
        public async Task<IActionResult> ObterEvolucaoSKUs(
            [FromQuery] int? anoInicio = null,
            [FromQuery] int mesInicio = 1,
            [FromQuery] int? anoFim = null,
            [FromQuery] int mesFim = 12)
        {
            try
            {
                var evolucao = await _dashboardService.ObterEvolucaoSKUs(anoInicio, mesInicio, anoFim, mesFim);
                return Ok(evolucao);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter evolução SKUs");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================================
        // VISAO GERAL
        // ============================================================

        [HttpGet("visao-geral/producao")]
        public async Task<IActionResult> ObterProducaoMensal([FromQuery] int ano = 2025, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterProducaoMensal(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/prototipos")]
        public async Task<IActionResult> ObterPrototiposMensal([FromQuery] int ano = 2025, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterPrototipoMensal(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/erros")]
        public async Task<IActionResult> ObterErrosMensal([FromQuery] int ano = 2025, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterErrosMensais(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/peso")]
        public async Task<IActionResult> ObterPesoMensal([FromQuery] int ano = 2025, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterPesoMensal(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/failed")]
        public async Task<IActionResult> ObterFailedMensal([FromQuery] int ano = 2025, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterFailedMensais(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/aborted")]
        public async Task<IActionResult> ObterAbortedMensal([FromQuery] int ano = 2025, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterAbortedMensais(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // ============================================================
        // VISAO GERAL - POR IMPRESSORA (ANUAL)
        // ============================================================

        [HttpGet("visao-geral/producao/impressora/anual")]
        public async Task<IActionResult> ObterProducaoPorImpressoraAnual([FromQuery] int ano = 2026, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterProducaoPorImpressoraAnual(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/prototipos/impressora/anual")]
        public async Task<IActionResult> ObterPrototiposPorImpressoraAnual([FromQuery] int ano = 2026, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterPrototiposPorImpressoraAnual(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/erros/impressora/anual")]
        public async Task<IActionResult> ObterErrosPorImpressoraAnual([FromQuery] int ano = 2026, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterErrosPorImpressoraAnual(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/peso/impressora/anual")]
        public async Task<IActionResult> ObterPesoPorImpressoraAnual([FromQuery] int ano = 2026, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterPesoPorImpressoraAnual(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/failed/impressora/anual")]
        public async Task<IActionResult> ObterFailedPorImpressoraAnual([FromQuery] int ano = 2026, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterFailedPorImpressoraAnual(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("visao-geral/aborted/impressora/anual")]
        public async Task<IActionResult> ObterAbortedPorImpressoraAnual([FromQuery] int ano = 2026, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _visaoGeralService.ObterAbortedPorImpressoraAnual(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // ============================================================
        // CARDS
        // ============================================================

        [HttpGet("cards/kg")]
        public async Task<IActionResult> ObterCardsKg([FromQuery] int ano = 2025, [FromQuery] int mesInicio = 1, [FromQuery] int mesFim = 12)
        {
            try { return Ok(await _cardService.ObterCardsKg(ano, mesInicio, mesFim)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("cards/capacidade/impressora")]
        public async Task<IActionResult> ObterCapacidadePorImpressora([FromQuery] int ano = 2026, [FromQuery] int mes = 1)
        {
            try { return Ok(await _cardService.ObterCapacidadePorImpressora(ano, mes)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("cards/kg/impressora")]
        public async Task<IActionResult> ObterKgPorImpressora([FromQuery] int ano = 2026, [FromQuery] int mes = 1)
        {
            try { return Ok(await _cardService.ObterKgPorImpressora(ano, mes)); }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // ============================================================
        // TIMELINE - CONSOLIDADO MENSAL
        // ============================================================

        [HttpGet("timeline/mes-consolidado")]
        public async Task<IActionResult> ObterResumoConsolidado([FromQuery] int ano, [FromQuery] int mes)
        {
            try
            {
                var resumo = await _timelineService.ObterResumoConsolidado(ano, mes);

                var response = new
                {
                    periodo = new { ano, mes, mesNome = new DateTime(ano, mes, 1).ToString("MMMM") },
                    metricas = new
                    {
                        utilizacao = Math.Round(resumo.Utilizacao, 1),
                        horasProdutivas = Math.Round(resumo.HorasProdutivas, 1), // ✅ 1 decimal, não Math.round
                        taxaSucesso = Math.Round(resumo.TaxaSucesso, 1)
                    },
                    distribuicao = new
                    {
                        producao = new
                        {
                            taxa = Math.Round(resumo.TaxaProducao, 1),
                            horas = Math.Round(resumo.TempoProducaoTotal, 1)      // ✅ 1 decimal
                        },
                        // ✅ CORREÇÃO DEFINITIVA: pausas com 2 casas decimais
                        // 0.027% com round=1 → 0.0 (invisível)
                        // 0.027% com round=2 → 0.03 (visível)
                        pausas = new
                        {
                            taxa = Math.Round(resumo.TaxaPausas, 2),             // ✅ 2 casas
                            horas = Math.Round(resumo.TempoPausasTotal, 2)         // ✅ 2 casas
                        },
                        ociosidade = new
                        {
                            taxa = Math.Round(resumo.TaxaOciosidade, 1),
                            horas = Math.Round(resumo.TempoOciosidadeTotal, 1)
                        },
                        esperaOperador = new
                        {
                            taxa = Math.Round(resumo.TaxaEsperaOperador, 1),
                            horas = Math.Round(resumo.TempoEsperaOperadorTotal, 1)
                        },
                        manutencao = new
                        {
                            taxa = Math.Round(resumo.TaxaManutencao, 1),
                            horas = Math.Round(resumo.TempoManutencaoTotal, 1)
                        },
                        totalTaxas = Math.Round(
                            resumo.TaxaProducao + resumo.TaxaPausas + resumo.TaxaOciosidade +
                            resumo.TaxaEsperaOperador + resumo.TaxaManutencao, 2)
                    },
                    motivos = new
                    {
                        pausas = resumo.TopMotivosPausas.Select(m => new
                        {
                            motivo = m.MotivoDescricao,
                            taxa = Math.Round(m.Percentual, 2),            // ✅ 2 casas
                            horas = Math.Round((decimal)m.TempoTotal / 60, 2), // ✅ 2 casas
                            ocorrencias = m.Ocorrencias
                        }),
                        ociosidade = resumo.TopMotivosOciosidade.Select(m => new
                        {
                            motivo = m.MotivoDescricao,
                            taxa = Math.Round(m.Percentual, 1),
                            horas = Math.Round((decimal)m.TempoTotal / 60, 1),
                            ocorrencias = m.Ocorrencias
                        }),
                        esperaOperador = resumo.TopMotivosEsperaOperador.Select(m => new
                        {
                            motivo = m.MotivoDescricao,
                            taxa = Math.Round(m.Percentual, 1),
                            horas = Math.Round((decimal)m.TempoTotal / 60, 1),
                            ocorrencias = m.Ocorrencias
                        })
                    },
                    impressoras = resumo.Impressoras.Select(imp => new
                    {
                        impressoraId = imp.MachineId,
                        impressoraNome = imp.MachineName,
                        taxaProducao = Math.Round((imp.TempoProducao / imp.TempoTotal) * 100, 1),
                        // ✅ 2 casas decimais para pausas de cada impressora
                        taxaPausas = Math.Round((imp.TempoPausas / imp.TempoTotal) * 100, 2),
                        taxaOciosidade = Math.Round((imp.TempoOciosidade / imp.TempoTotal) * 100, 1),
                        taxaEsperaOperador = Math.Round((imp.TempoEsperaOperador / imp.TempoTotal) * 100, 1),
                        taxaManutencao = Math.Round((imp.TempoManutencao / imp.TempoTotal) * 100, 1),
                        horasProducao = Math.Round(imp.TempoProducao, 1),
                        // ✅ 2 casas para horas de pausa (0.20h em vez de 0h)
                        horasPausas = Math.Round(imp.TempoPausas, 2),
                        horasOciosidade = Math.Round(imp.TempoOciosidade, 1),
                        jobsFinalizados = imp.JobsFinalizados,
                        jobsAbortados = imp.JobsAbortados,
                        taxaSucesso = Math.Round(imp.TaxaSucesso, 1)
                    }),
                    totalImpressoras = resumo.Impressoras.Count
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter resumo consolidado para {Ano}/{Mes}", ano, mes);
                return StatusCode(500, new { message = "Erro ao processar requisição", error = ex.Message });
            }
        }

        // ============================================================
        // TIMELINE - MÊS POR IMPRESSORA
        // ============================================================

        [HttpGet("timeline/mes-impressora/{machineId}")]
        public async Task<IActionResult> ObterResumoMensalImpressora(
            [FromRoute] int machineId,
            [FromQuery] int ano,
            [FromQuery] int mes)
        {
            try
            {
                var resumo = await _timelineService.ObterResumoMensal(machineId, ano, mes);

                var tTotal = resumo.TempoTotal > 0 ? resumo.TempoTotal : 1m;

                var response = new
                {
                    impressora = new { id = resumo.MachineId, nome = resumo.MachineName },
                    periodo = new { ano = resumo.Ano, mes = resumo.Mes, mesNome = resumo.MesNome },
                    metricas = new
                    {
                        taxaProducao = Math.Round((resumo.TempoProducao / tTotal) * 100, 1),
                        producao = Math.Round(resumo.TempoProducao, 1),
                        // ✅ 2 casas decimais
                        taxaPausas = Math.Round((resumo.TempoPausas / tTotal) * 100, 2),
                        pausas = Math.Round(resumo.TempoPausas, 2),
                        taxaOciosidade = Math.Round((resumo.TempoOciosidade / tTotal) * 100, 1),
                        ociosidade = Math.Round(resumo.TempoOciosidade, 1),
                        taxaEsperaOperador = Math.Round((resumo.TempoEsperaOperador / tTotal) * 100, 1),
                        esperaOperador = Math.Round(resumo.TempoEsperaOperador, 1),
                        taxaManutencao = Math.Round((resumo.TempoManutencao / tTotal) * 100, 1),
                        manutencao = Math.Round(resumo.TempoManutencao, 1),
                        totalTaxas = Math.Round(
                            (resumo.TempoProducao / tTotal) * 100 +
                            (resumo.TempoPausas / tTotal) * 100 +
                            (resumo.TempoOciosidade / tTotal) * 100 +
                            (resumo.TempoEsperaOperador / tTotal) * 100 +
                            (resumo.TempoManutencao / tTotal) * 100, 2),
                        jobsFinalizados = resumo.JobsFinalizados,
                        jobsAbortados = resumo.JobsAbortados,
                        taxaSucesso = Math.Round(resumo.TaxaSucesso, 1)
                    },
                    motivos = resumo.Motivos
                        .OrderByDescending(m => m.TempoTotal)
                        .Select(m => new
                        {
                            status = m.Status.ToString(),
                            motivo = m.MotivoDescricao,
                            // ✅ pausas com 2 casas nos motivos também
                            horas = m.Status == StatusMaquina.Pausa
                                ? Math.Round((decimal)m.TempoTotal / 60, 2)
                                : Math.Round((decimal)m.TempoTotal / 60, 1),
                            taxa = m.Status == StatusMaquina.Pausa
                                ? Math.Round(m.Percentual, 2)
                                : Math.Round(m.Percentual, 1),
                            ocorrencias = m.Ocorrencias
                        }),
                    calendario = resumo.Dias.Select(dia => new
                    {
                        dia = dia.Data.Day,
                        data = dia.Data.ToString("yyyy-MM-dd"),
                        taxaProducao = Math.Round((dia.TempoProducao / 1440m) * 100, 1),
                        horasProducao = Math.Round(dia.TempoProducao / 60m, 1),
                        horasPausas = Math.Round(dia.TempoPausas / 60m, 2), // ✅ 2 casas
                        horasOciosidade = Math.Round(dia.TempoOciosidade / 60m, 1),
                        horasEsperaOperador = Math.Round(dia.TempoEsperaOperador / 60m, 1),
                        horasManutencao = Math.Round(dia.TempoManutencao / 60m, 1)
                    })
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter resumo mensal da impressora {MachineId}", machineId);
                return StatusCode(500, new { message = "Erro ao processar requisição", error = ex.Message });
            }
        }

        // ============================================================
        // TIMELINE - DIA
        // ============================================================

        [HttpGet("timeline/dia/{machineId}")]
        public async Task<IActionResult> ObterResumoDiario(
            [FromRoute] int machineId,
            [FromQuery] DateTime data)
        {
            try
            {
                var resumo = await _timelineService.ObterResumoDiario(machineId, data);

                var totalMin = resumo.TempoProducao + resumo.TempoPausas + resumo.TempoOciosidade +
                               resumo.TempoEsperaOperador + resumo.TempoManutencao;
                var tTotal = totalMin > 0 ? totalMin : 1m;

                var response = new
                {
                    impressora = new { id = resumo.MachineId, nome = resumo.MachineName },
                    data = resumo.Data.ToString("yyyy-MM-dd"),
                    diaSemana = resumo.Data.ToString("dddd"),
                    resumo = new
                    {
                        taxaProducao = Math.Round((resumo.TempoProducao / tTotal) * 100, 1),
                        horasProducao = Math.Round(resumo.TempoProducao / 60m, 1),
                        // ✅ pausas com 2 casas no dia também
                        taxaPausas = Math.Round((resumo.TempoPausas / tTotal) * 100, 2),
                        horasPausas = Math.Round(resumo.TempoPausas / 60m, 2),
                        taxaOciosidade = Math.Round((resumo.TempoOciosidade / tTotal) * 100, 1),
                        horasOciosidade = Math.Round(resumo.TempoOciosidade / 60m, 1),
                        taxaEsperaOperador = Math.Round((resumo.TempoEsperaOperador / tTotal) * 100, 1),
                        horasEsperaOperador = Math.Round(resumo.TempoEsperaOperador / 60m, 1),
                        taxaManutencao = Math.Round((resumo.TempoManutencao / tTotal) * 100, 1),
                        horasManutencao = Math.Round(resumo.TempoManutencao / 60m, 1),
                        totalTaxas = Math.Round(
                            (resumo.TempoProducao / tTotal) * 100 +
                            (resumo.TempoPausas / tTotal) * 100 +
                            (resumo.TempoOciosidade / tTotal) * 100 +
                            (resumo.TempoEsperaOperador / tTotal) * 100 +
                            (resumo.TempoManutencao / tTotal) * 100, 2)
                    },
                    motivosDia = new
                    {
                        pausas = resumo.Motivos
                            .Where(m => m.Status == StatusMaquina.Pausa)
                            .Select(m => new
                            {
                                motivo = m.MotivoDescricao,
                                horas = Math.Round((decimal)m.TempoTotal / 60, 2), // ✅
                                taxa = Math.Round(m.Percentual, 2),               // ✅
                                ocorrencias = m.Ocorrencias
                            }).Take(5),
                        ociosidade = resumo.Motivos
                            .Where(m => m.Status == StatusMaquina.Ociosidade)
                            .Select(m => new
                            {
                                motivo = m.MotivoDescricao,
                                horas = Math.Round((decimal)m.TempoTotal / 60, 1),
                                taxa = Math.Round(m.Percentual, 1),
                                ocorrencias = m.Ocorrencias
                            }).Take(5),
                        esperaOperador = resumo.Motivos
                            .Where(m => m.Status == StatusMaquina.EsperaOperador)
                            .Select(m => new
                            {
                                motivo = m.MotivoDescricao,
                                horas = Math.Round((decimal)m.TempoTotal / 60, 1),
                                taxa = Math.Round(m.Percentual, 1),
                                ocorrencias = m.Ocorrencias
                            }).Take(5)
                    },
                    blocos = resumo.Timeline.Select(bloco => new
                    {
                        inicio = bloco.Inicio.ToString("HH:mm"),
                        fim = bloco.Fim.ToString("HH:mm"),
                        // ✅ duracao em horas com 2 casas para pausas curtas
                        duracao = Math.Round((decimal)bloco.DuracaoMinutos / 60, 2),
                        status = bloco.Status.ToString(),
                        statusCor = ObterCorStatus(bloco.Status),
                        motivo = bloco.Mensagem,
                        mensagem = bloco.Mensagem,
                        jobId = bloco.JobUuid,
                        jobNome = bloco.JobName
                    }).OrderBy(b => b.inicio),
                    temTimeline = resumo.Timeline.Any()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter resumo diário da impressora {MachineId}", machineId);
                return StatusCode(500, new { message = "Erro ao processar requisição", error = ex.Message });
            }
        }

        // ============================================================
        // TIMELINE HORÁRIA
        // ============================================================

        [HttpGet("timeline/horaria/{machineId}")]
        public async Task<IActionResult> ObterTimelineHoraria(int machineId, [FromQuery] DateTime data)
        {
            try
            {
                var resumo = await _timelineService.ObterResumoDiario(machineId, data);
                var tTotal = resumo.TempoProducao + resumo.TempoPausas + resumo.TempoOciosidade +
                             resumo.TempoEsperaOperador + resumo.TempoManutencao;
                var den = tTotal > 0 ? tTotal : 1m;

                return Ok(new
                {
                    impressora = new { id = resumo.MachineId, nome = resumo.MachineName },
                    data = resumo.Data.ToString("yyyy-MM-dd"),
                    diaSemana = resumo.Data.ToString("dddd"),
                    timeline = resumo.Timeline.Select(b => new
                    {
                        inicio = b.Inicio.ToString("HH:mm"),
                        fim = b.Fim.ToString("HH:mm"),
                        duracaoMinutos = Math.Round((decimal)b.DuracaoMinutos, 1),
                        duracaoHoras = Math.Round((decimal)b.DuracaoMinutos / 60, 2), // ✅
                        status = b.Status.ToString(),
                        statusCor = ObterCorStatus(b.Status),
                        motivo = b.Motivo.ToString(),
                        motivoDescricao = b.Mensagem,
                        jobUuid = b.JobUuid,
                        jobName = b.JobName
                    }).OrderBy(b => b.inicio),
                    resumoDia = new
                    {
                        producao = new { horas = Math.Round(resumo.TempoProducao / 60, 1), taxa = Math.Round((resumo.TempoProducao / den) * 100, 1) },
                        pausas = new { horas = Math.Round(resumo.TempoPausas / 60, 2), taxa = Math.Round((resumo.TempoPausas / den) * 100, 2) }, // ✅
                        ociosidade = new { horas = Math.Round(resumo.TempoOciosidade / 60, 1), taxa = Math.Round((resumo.TempoOciosidade / den) * 100, 1) },
                        esperaOperador = new { horas = Math.Round(resumo.TempoEsperaOperador / 60, 1), taxa = Math.Round((resumo.TempoEsperaOperador / den) * 100, 1) },
                        manutencao = new { horas = Math.Round(resumo.TempoManutencao / 60, 1), taxa = Math.Round((resumo.TempoManutencao / den) * 100, 1) }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na timeline horária de {MachineId}", machineId);
                return StatusCode(500, new { erro = ex.Message });
            }
        }

        // ============================================================
        // ANÁLISE - TODAS IMPRESSORAS
        // ============================================================

        [HttpGet("analise/todas-impressoras")]
        public async Task<IActionResult> AnalisarTodasImpressoras(
            [FromQuery] DateTime dataInicio,
            [FromQuery] DateTime dataFim)
        {
            try
            {
                var printers = await _ultimakerClient.GetPrintersAsync();
                var analises = new List<object>();

                foreach (var printer in printers.Where(p => p.IsActive))
                {
                    try
                    {
                        var resumo = await _timelineService.ObterResumoMensal(
                            printer.Id, dataInicio.Year, dataInicio.Month);

                        var tempoTotal = resumo.TempoTotal > 0 ? resumo.TempoTotal : 1m;

                        analises.Add(new
                        {
                            impressoraId = resumo.MachineId,
                            impressoraNome = resumo.MachineName,
                            taxaProducao = Math.Round((resumo.TempoProducao / tempoTotal) * 100, 1),
                            taxaOciosidade = Math.Round((resumo.TempoOciosidade / tempoTotal) * 100, 1),
                            // ✅ CORREÇÃO DEFINITIVA: 2 casas decimais
                            // 0.027% com round=1 → 0.0 | com round=2 → 0.03 ← VISÍVEL
                            taxaPausas = Math.Round((resumo.TempoPausas / tempoTotal) * 100, 2),
                            horasProducao = Math.Round(resumo.TempoProducao, 1),
                            horasOciosidade = Math.Round(resumo.TempoOciosidade, 1),
                            horasPausas = Math.Round(resumo.TempoPausas, 2), // ✅
                            jobsFinalizados = resumo.JobsFinalizados,
                            jobsAbortados = resumo.JobsAbortados,
                            taxaEsperaOperador = Math.Round((resumo.TempoEsperaOperador / tempoTotal) * 100, 1),
                            taxaManutencao = Math.Round((resumo.TempoManutencao / tempoTotal) * 100, 1),
                            horasEsperaOperador = Math.Round(resumo.TempoEsperaOperador, 1),
                            horasManutencao = Math.Round(resumo.TempoManutencao, 1),
                            taxaSucesso = Math.Round(resumo.TaxaSucesso, 1)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erro ao analisar impressora {PrinterName}", printer.Name);
                    }
                }

                var totais = analises.Any()
                    ? new
                    {
                        taxaProducaoMedia = Math.Round(analises.Average(a => (decimal)((dynamic)a).taxaProducao), 1),
                        taxaOciosidadeMedia = Math.Round(analises.Average(a => (decimal)((dynamic)a).taxaOciosidade), 1),
                        // ✅ 2 casas na média também
                        taxaPausasMedia = Math.Round(analises.Average(a => (decimal)((dynamic)a).taxaPausas), 2)
                    }
                    : new { taxaProducaoMedia = 0m, taxaOciosidadeMedia = 0m, taxaPausasMedia = 0m };

                return Ok(new
                {
                    periodo = new
                    {
                        inicio = dataInicio,
                        fim = dataFim,
                        ano = dataInicio.Year,
                        mes = dataInicio.Month
                    },
                    totalImpressoras = analises.Count,
                    medias = totais,
                    impressoras = analises.OrderByDescending(a => ((dynamic)a).taxaProducao)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar todas impressoras");
                return StatusCode(500, new { erro = ex.Message });
            }
        }

        // ============================================================
        // ANÁLISE - IMPRESSORA INDIVIDUAL
        // ============================================================

        [HttpGet("analise/impressora/{machineId}")]
        public async Task<IActionResult> AnalisarImpressora(
            int machineId,
            [FromQuery] DateTime dataInicio,
            [FromQuery] DateTime dataFim)
        {
            try
            {
                var resumo = await _timelineService.ObterResumoMensal(
                    machineId, dataInicio.Year, dataInicio.Month);

                var tempoTotal = resumo.TempoTotal > 0 ? resumo.TempoTotal : 1m;

                return Ok(new
                {
                    impressora = new { id = resumo.MachineId, nome = resumo.MachineName },
                    periodo = new { inicio = dataInicio, fim = dataFim, totalHoras = Math.Round(resumo.TempoTotal, 1) },
                    jobs = new { finalizados = resumo.JobsFinalizados, abortados = resumo.JobsAbortados, taxaSucesso = Math.Round(resumo.TaxaSucesso, 1) },
                    tempos = new
                    {
                        producao = new { horas = Math.Round(resumo.TempoProducao, 1), taxa = Math.Round((resumo.TempoProducao / tempoTotal) * 100, 1) },
                        pausas = new { horas = Math.Round(resumo.TempoPausas, 2), taxa = Math.Round((resumo.TempoPausas / tempoTotal) * 100, 2) }, // ✅
                        ociosidade = new { horas = Math.Round(resumo.TempoOciosidade, 1), taxa = Math.Round((resumo.TempoOciosidade / tempoTotal) * 100, 1) },
                        esperaOperador = new { horas = Math.Round(resumo.TempoEsperaOperador, 1), taxa = Math.Round((resumo.TempoEsperaOperador / tempoTotal) * 100, 1) },
                        manutencao = new { horas = Math.Round(resumo.TempoManutencao, 1), taxa = Math.Round((resumo.TempoManutencao / tempoTotal) * 100, 1) }
                    },
                    motivos = resumo.Motivos
                        .OrderByDescending(m => m.TempoTotal)
                        .Select(m => new
                        {
                            status = m.Status.ToString(),
                            motivo = m.MotivoDescricao,
                            horas = m.Status == StatusMaquina.Pausa
                                ? Math.Round((decimal)m.TempoTotal / 60, 2)
                                : Math.Round((decimal)m.TempoTotal / 60, 1),
                            taxa = m.Status == StatusMaquina.Pausa
                                ? Math.Round(m.Percentual, 2)
                                : Math.Round(m.Percentual, 1),
                            ocorrencias = m.Ocorrencias
                        })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar impressora {MachineId}", machineId);
                return StatusCode(500, new { erro = ex.Message });
            }
        }

        // ============================================================
        // EVENTOS
        // ============================================================

        [HttpGet("eventos/impressora/{machineId}")]
        public async Task<IActionResult> ObterEventosImpressora(int machineId, [FromQuery] DateTime dataInicio, [FromQuery] DateTime dataFim)
        {
            try
            {
                var eventos = await _ultimakerClient.GetEventsAsync(machineId, dataInicio, dataFim);
                return Ok(new
                {
                    impressoraId = machineId,
                    periodo = new { inicio = dataInicio, fim = dataFim },
                    totalEventos = eventos.Count,
                    eventos = eventos.Select(e => new
                    {
                        timestamp = e.Time,
                        mensagem = e.Message,
                        tipo = e.EventCategory,
                        typeId = e.TypeId,
                        jobUuid = e.GetJobUuid(),
                        parametros = e.Parameters
                    }).OrderBy(e => e.timestamp)
                });
            }
            catch (Exception ex) { return StatusCode(500, new { erro = ex.Message }); }
        }

        [HttpGet("eventos/job/{jobUuid}")]
        public async Task<IActionResult> ObterEventosJob(string jobUuid, [FromQuery] int machineId)
        {
            try
            {
                var eventos = await _ultimakerClient.GetEventsByJobUuidAsync(machineId, jobUuid);
                return Ok(new
                {
                    jobUuid,
                    impressoraId = machineId,
                    totalEventos = eventos.Count,
                    eventos = eventos.Select(e => new
                    {
                        timestamp = e.Time,
                        mensagem = e.Message,
                        categoria = e.EventCategory,
                        parametros = e.Parameters
                    }).OrderBy(e => e.timestamp)
                });
            }
            catch (Exception ex) { return StatusCode(500, new { erro = ex.Message }); }
        }

        // ============================================================
        // TESTES
        // ============================================================

        [HttpPost("test/sincronizar-mes")]
        public async Task<IActionResult> SincronizarMes([FromQuery] int ano = 2026, [FromQuery] int mes = 1)
        {
            try
            {
                await _syncService.SincronizarMesAsync(ano, mes);
                return Ok(new { sucesso = true, mensagem = $"Sincronização de {mes:D2}/{ano} concluída" });
            }
            catch (Exception ex) { return StatusCode(500, new { sucesso = false, erro = ex.Message }); }
        }

        [HttpGet("test/job/{uuid}")]
        public async Task<IActionResult> VerJob(string uuid)
        {
            try
            {
                var job = await _producaoRepository.ObterPorUuid(uuid);
                if (job == null) return NotFound(new { mensagem = "Job não encontrado" });

                var m0 = job.Material0Guid.HasValue ? await _materialRepository.ObterPorGuid(job.Material0Guid.Value) : null;
                var m1 = job.Material1Guid.HasValue ? await _materialRepository.ObterPorGuid(job.Material1Guid.Value) : null;

                return Ok(new
                {
                    job.JobName,
                    job.Status,
                    job.DatetimeStarted,
                    job.DatetimeFinished,
                    material0 = new { guid = job.Material0Guid, nome = m0?.Nome, densidade = m0?.Densidade, volume_mm3 = job.Material0Amount, peso_g = job.Material0WeightG, peso_kg = Math.Round(job.Material0WeightG / 1000m, 3) },
                    material1 = new { guid = job.Material1Guid, nome = m1?.Nome, densidade = m1?.Densidade, volume_mm3 = job.Material1Amount, peso_g = job.Material1WeightG, peso_kg = Math.Round(job.Material1WeightG / 1000m, 3) },
                    total = new { peso_g = job.MaterialTotal, peso_kg = Math.Round(job.MaterialTotalKg, 3) }
                });
            }
            catch (Exception ex) { return StatusCode(500, new { erro = ex.Message }); }
        }

        [HttpGet("test/materiais")]
        public async Task<IActionResult> ListarMateriais()
        {
            try
            {
                var materiais = await _materialRepository.ListarTodos();
                return Ok(materiais.Select(m => new
                {
                    guid = m.UltimakerMaterialGuid,
                    m.Nome,
                    densidade_g_cm3 = m.Densidade,
                    m.Fabricante,
                    criado_em = m.CreatedAt
                }).OrderBy(m => m.Nome));
            }
            catch (Exception ex) { return StatusCode(500, new { erro = ex.Message }); }
        }

        // ============================================================
        // MATERIAIS
        // ============================================================

        [HttpPost("materiais/atualizar-densidades")]
        public async Task<IActionResult> AtualizarDensidadesMateriais()
        {
            try
            {
                if (_materialRepository is MaterialRepository repo)
                {
                    var qtd = await repo.AtualizarDensidadesExistentes();
                    return Ok(new { sucesso = true, mensagem = "Densidades atualizadas", materiaisAtualizados = qtd });
                }
                return BadRequest(new { sucesso = false, mensagem = "MaterialRepository não disponível" });
            }
            catch (Exception ex) { return StatusCode(500, new { sucesso = false, erro = ex.Message }); }
        }

        [HttpGet("materiais/verificar-densidades")]
        public async Task<IActionResult> VerificarDensidades()
        {
            try
            {
                var materiais = await _materialRepository.ListarTodos();
                var padrao = materiais.Where(m => m.Densidade == 1.24m).ToList();
                var diferente = materiais.Where(m => m.Densidade != 1.24m).ToList();
                return Ok(new
                {
                    resumo = new
                    {
                        totalMateriais = materiais.Count,
                        comDensidadePadrao = padrao.Count,
                        comDensidadeReal = diferente.Count,
                        percentualPadrao = materiais.Count > 0 ? Math.Round((decimal)padrao.Count / materiais.Count * 100, 1) : 0
                    },
                    materiaisComDensidadePadrao = padrao.Select(m => new { guid = m.UltimakerMaterialGuid, m.Nome, m.Fabricante, m.Densidade, criadoEm = m.CreatedAt }).OrderBy(m => m.Nome),
                    materiaisComDensidadeReal = diferente.Select(m => new { guid = m.UltimakerMaterialGuid, m.Nome, m.Fabricante, m.Densidade, criadoEm = m.CreatedAt }).OrderBy(m => m.Nome)
                });
            }
            catch (Exception ex) { return StatusCode(500, new { erro = ex.Message }); }
        }

        // ============================================================
        // DEBUG
        // ============================================================

        [HttpGet("debug/impressoras")]
        public async Task<IActionResult> DebugImpressoras()
        {
            try
            {
                var printers = await _ultimakerClient.GetPrintersAsync();
                return Ok(new
                {
                    totalImpressoras = printers.Count,
                    impressoras = printers.Select(p => new { id = p.Id, nome = p.Name, ativa = p.IsActive, hostname = p.BaseUrl })
                });
            }
            catch (Exception ex) { return StatusCode(500, new { erro = ex.Message }); }
        }

        [HttpGet("debug/analise/{machineId}")]
        public async Task<IActionResult> DebugAnaliseImpressora(int machineId, [FromQuery] DateTime dataInicio, [FromQuery] DateTime dataFim)
        {
            try
            {
                var analise = await _ultimakerClient.AnalyzeEventsAsync(machineId, dataInicio, dataFim);
                return Ok(new
                {
                    debug = true,
                    machineId,
                    periodo = new { dataInicio, dataFim },
                    resultado = new
                    {
                        impressoraId = analise.MachineId,
                        impressoraNome = analise.MachineName,
                        taxaProducao = Math.Round(analise.TaxaProducao, 1),
                        taxaOciosidade = Math.Round(analise.TaxaOciosidade, 1),
                        taxaPausas = Math.Round(analise.TaxaPausas, 2),  // ✅
                        horasProducao = Math.Round(analise.TempoProducao / 60, 1),
                        horasOciosidade = Math.Round(analise.TempoOciosidade / 60, 1),
                        horasPausas = Math.Round(analise.TempoPausas / 60, 2), // ✅
                        jobsFinalizados = analise.JobsFinalizados,
                        jobsAbortados = analise.JobsAbortados
                    }
                });
            }
            catch (Exception ex) { return StatusCode(500, new { erro = ex.Message, detalhes = ex.InnerException?.Message, stackTrace = ex.StackTrace }); }
        }

        // ============================================================
        // HELPER
        // ============================================================

        private string ObterCorStatus(StatusMaquina status) => status switch
        {
            StatusMaquina.Producao => "green",
            StatusMaquina.Pausa => "yellow",
            StatusMaquina.Ociosidade => "red",
            StatusMaquina.EsperaOperador => "orange",
            StatusMaquina.Manutencao => "blue",
            _ => "gray"
        };
    }
}