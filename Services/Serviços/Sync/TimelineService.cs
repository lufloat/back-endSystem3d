// ========================================
// TimelineService.cs - VERSÃO FINAL CORRIGIDA
// Localização: Business_Logic/Serviços/TimelineService.cs
// ========================================
// ✅ CORREÇÃO 1: filtro cross-day (InicioPausa.Date OU FimPausa.Date)
// ✅ CORREÇÃO 2: janela expandida +1 dia para jobs cross-midnight
// ✅ CORREÇÃO 3: taxas calculadas como média das impressoras (não pool total)
// ========================================

using Business_Logic.Repositories.Interfaces;
using Business_Logic.Serviços.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SistemaProducao3D.Integration.Ultimaker;
using SistemaProducao3D.Modelos.Modelos;
using SistemaProducao3D.Modelos.Timeline;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Business_Logic.Serviços
{
    public class TimelineService : ITimelineService
    {
        private readonly IProducaoRepository _producaoRepository;
        private readonly IUltimakerClient _ultimakerClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TimelineService> _logger;

        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(4);
        private static readonly TimeSpan TIMEOUT_PER_PRINTER = TimeSpan.FromSeconds(45);
        private const int TEMPO_ESPERA_PADRAO_MIN = 5;

        public TimelineService(
            IProducaoRepository producaoRepository,
            IUltimakerClient ultimakerClient,
            IMemoryCache cache,
            ILogger<TimelineService> logger)
        {
            _producaoRepository = producaoRepository;
            _ultimakerClient = ultimakerClient;
            _cache = cache;
            _logger = logger;
        }

        // NOTA IMPORTANTE SOBRE TAXAS DE PAUSA:
        // As pausas detectadas via eventos são genuinamente curtas (segundos a minutos).
        // Ex: job 96a37aa0 pausou por 12 SEGUNDOS. Numa janela de 744h mensais,
        // isso resulta em taxas de 0.01~0.05% — valores reais, não bugs.
        // Usamos 2 casas decimais para não perder esses valores no arredondamento.

        // ========================================
        // RESUMO CONSOLIDADO
        // ========================================

        public async Task<ResumoConsolidado> ObterResumoConsolidado(int ano, int mes)
        {
            var swTotal = Stopwatch.StartNew();
            var cacheKey = $"consolidado_{ano}_{mes}";

            if (_cache.TryGetValue(cacheKey, out ResumoConsolidado cached))
            {
                _logger.LogInformation("⚡ CACHE HIT: {CacheKey} ({Ms}ms)", cacheKey, swTotal.ElapsedMilliseconds);
                return cached;
            }

            _logger.LogInformation("📊 [CONSOLIDADO] Gerando: {Mes:D2}/{Ano}", mes, ano);

            var printers = await _ultimakerClient.GetPrintersAsync();
            var printersAtivas = printers.Where(p => p.IsActive).ToList();

            _logger.LogInformation("   🖨️  {Count} impressoras ativas", printersAtivas.Count);

            var inicioMes = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var fimMes = inicioMes.AddMonths(1).AddSeconds(-1);

            var swJobs = Stopwatch.StartNew();
            var todosJobsMes = await _producaoRepository.ObterPorIntervalo(inicioMes, fimMes);
            swJobs.Stop();
            _logger.LogInformation("   📦 {Count} jobs carregados em {Ms}ms", todosJobsMes.Count, swJobs.ElapsedMilliseconds);

            var swEventos = Stopwatch.StartNew();
            var eventosPausaMes = await BuscarEventosPausaMesAsync(printersAtivas, inicioMes, fimMes);
            swEventos.Stop();
            _logger.LogInformation("   ⏸️  {Count} eventos de pausa carregados em {Ms}ms", eventosPausaMes.Count, swEventos.ElapsedMilliseconds);

            var jobsPorImpressora = todosJobsMes.GroupBy(j => j.MachineId).ToDictionary(g => g.Key, g => g.ToList());
            var eventosPorImpressora = eventosPausaMes.GroupBy(e => e.MachineId).ToDictionary(g => g.Key, g => g.ToList());

            var cultureInfo = new CultureInfo("pt-BR");
            var resumo = new ResumoConsolidado
            {
                Ano = ano,
                Mes = mes,
                Periodo = $"{cultureInfo.DateTimeFormat.GetAbbreviatedMonthName(mes).ToUpper()}/{ano.ToString().Substring(2)}"
            };

            var resumosImpressoras = new ConcurrentBag<ResumoMensal>();
            var todosMotivos = new ConcurrentBag<MotivoConsolidado>();

            var tasks = printersAtivas.Select(async printer =>
            {
                var swPrinter = Stopwatch.StartNew();
                try
                {
                    using var cts = new CancellationTokenSource(TIMEOUT_PER_PRINTER);

                    var jobsImpressora = jobsPorImpressora.ContainsKey(printer.Id)
                        ? jobsPorImpressora[printer.Id]
                        : new List<MesaProducao>();

                    var eventosImpressora = eventosPorImpressora.ContainsKey(printer.Id)
                        ? eventosPorImpressora[printer.Id]
                        : new List<EventoPausa>();

                    var resumoImpressora = ProcessarResumoMensalComPausas(
                        printer.Id, printer.Name, ano, mes,
                        jobsImpressora, eventosImpressora
                    );

                    resumosImpressoras.Add(resumoImpressora);

                    foreach (var motivo in resumoImpressora.Motivos)
                        todosMotivos.Add(motivo);

                    swPrinter.Stop();

                    var tTotal = resumoImpressora.TempoTotal > 0 ? resumoImpressora.TempoTotal : 1;
                    _logger.LogInformation("      ✅ {PrinterName}: {Ms}ms (Prod: {Prod}% | Pausas: {Pausas}%)",
                        printer.Name, swPrinter.ElapsedMilliseconds,
                        Math.Round((resumoImpressora.TempoProducao / tTotal) * 100, 1),
                        Math.Round((resumoImpressora.TempoPausas / tTotal) * 100, 1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "      ❌ Erro em {PrinterName}", printer.Name);
                }
            });

            await Task.WhenAll(tasks);

            resumo.Impressoras = resumosImpressoras.OrderBy(i => i.MachineName).ToList();

            // ── Totais absolutos (em horas) ──────────────────────────────────
            decimal tempoTotalProducao = resumo.Impressoras.Sum(i => i.TempoProducao);
            decimal tempoTotalPausas = resumo.Impressoras.Sum(i => i.TempoPausas);
            decimal tempoTotalOciosidade = resumo.Impressoras.Sum(i => i.TempoOciosidade);
            decimal tempoTotalEsperaOperador = resumo.Impressoras.Sum(i => i.TempoEsperaOperador);
            decimal tempoTotalManutencao = resumo.Impressoras.Sum(i => i.TempoManutencao);

            var tempoTotal = tempoTotalProducao + tempoTotalPausas + tempoTotalOciosidade +
                             tempoTotalEsperaOperador + tempoTotalManutencao;

            resumo.TempoTotalDisponivel = tempoTotal;
            resumo.TempoProducaoTotal = tempoTotalProducao;
            resumo.TempoPausasTotal = tempoTotalPausas;
            resumo.TempoOciosidadeTotal = tempoTotalOciosidade;
            resumo.TempoEsperaOperadorTotal = tempoTotalEsperaOperador;
            resumo.TempoManutencaoTotal = tempoTotalManutencao;

            // ── ✅ CORREÇÃO 3: taxas como MÉDIA das impressoras individuais ──
            // Calcular sobre pool total faz pausas ficarem 0.0% porque
            // 0.3h / 4464h_total = 0.007% → arredonda para 0.0%
            var impressorasComDados = resumo.Impressoras.Where(i => i.TempoTotal > 0).ToList();
            var n = impressorasComDados.Count > 0 ? (decimal)impressorasComDados.Count : 1;

            if (impressorasComDados.Any())
            {
                resumo.TaxaProducao = Math.Round(impressorasComDados.Sum(i => (i.TempoProducao / i.TempoTotal) * 100) / n, 2);
                resumo.TaxaPausas = Math.Round(impressorasComDados.Sum(i => (i.TempoPausas / i.TempoTotal) * 100) / n, 2);
                resumo.TaxaOciosidade = Math.Round(impressorasComDados.Sum(i => (i.TempoOciosidade / i.TempoTotal) * 100) / n, 2);
                resumo.TaxaEsperaOperador = Math.Round(impressorasComDados.Sum(i => (i.TempoEsperaOperador / i.TempoTotal) * 100) / n, 2);
                resumo.TaxaManutencao = Math.Round(impressorasComDados.Sum(i => (i.TempoManutencao / i.TempoTotal) * 100) / n, 2);

                // Garante que soma = 100%
                var somaAtual = resumo.TaxaProducao + resumo.TaxaPausas + resumo.TaxaOciosidade +
                                resumo.TaxaEsperaOperador + resumo.TaxaManutencao;
                if (Math.Abs(somaAtual - 100) > 0.01m)
                    resumo.TaxaProducao += (100 - somaAtual);
            }

            resumo.HorasProdutivas = tempoTotalProducao;
            resumo.Utilizacao = resumo.TaxaProducao;

            int totalJobsFinalizados = resumo.Impressoras.Sum(i => i.JobsFinalizados);
            int totalJobsAbortados = resumo.Impressoras.Sum(i => i.JobsAbortados);

            // resumo.TaxaSucesso = totalJobs > 0
            //     ? Math.Round((decimal)totalJobsFinalizados / totalJobs * 100, 1)
            //     : 0;

            var todosMotivosLista = todosMotivos.ToList();

            resumo.TopMotivosPausas = ConsolidarMotivos(todosMotivosLista, StatusMaquina.Pausa, tempoTotalPausas * 60);
            resumo.TopMotivosOciosidade = ConsolidarMotivos(todosMotivosLista, StatusMaquina.Ociosidade, tempoTotalOciosidade * 60);
            resumo.TopMotivosEsperaOperador = ConsolidarMotivos(todosMotivosLista, StatusMaquina.EsperaOperador, tempoTotalEsperaOperador * 60);

            swTotal.Stop();
            _logger.LogInformation("   ✅ Consolidado: {Count} impressoras em {Ms}ms | Prod: {Prod}% | Pausas: {Pausas}% | Ocio: {Ocio}%",
                resumo.Impressoras.Count, swTotal.ElapsedMilliseconds,
                resumo.TaxaProducao, resumo.TaxaPausas, resumo.TaxaOciosidade);

            _cache.Set(cacheKey, resumo, CACHE_DURATION);
            return resumo;
        }

        // ========================================
        // BUSCAR EVENTOS DE PAUSA DA API
        // ✅ CORREÇÃO 2: janela +1 dia para capturar jobs cross-midnight
        // ========================================

        private async Task<List<EventoPausa>> BuscarEventosPausaMesAsync(
            List<UltimakerPrinterConfig> printers,
            DateTime inicioMes,
            DateTime fimMes)
        {
            var todosEventos = new ConcurrentBag<EventoPausa>();

            var tasks = printers.Select(async printer =>
            {
                try
                {
                    // ✅ Expande janela ±1 dia para capturar jobs cross-day/cross-midnight
                    var inicioJanela = inicioMes.AddDays(-1);
                    var fimJanela = fimMes.AddDays(1);

                    var eventos = await _ultimakerClient.GetEventsAsync(printer.Id, inicioJanela, fimJanela);

                    var eventosPausa = eventos.Where(e =>
                        e.TypeId == 131073 ||  // Print paused
                        e.TypeId == 131074 ||  // Print resumed
                        e.TypeId == 131075     // Print aborted
                    ).OrderBy(e => e.Time).ToList();

                    for (int i = 0; i < eventosPausa.Count; i++)
                    {
                        var evento = eventosPausa[i];
                        if (evento.TypeId != 131073) continue; // só processa "paused"

                        var jobUuid = evento.GetJobUuid();

                        var eventoFim = eventosPausa
                            .Skip(i + 1)
                            .FirstOrDefault(e =>
                                (e.TypeId == 131074 || e.TypeId == 131075) &&
                                e.Time > evento.Time &&
                                e.GetJobUuid() == jobUuid);

                        if (eventoFim == null) continue;

                        var duracao = Math.Round((decimal)(eventoFim.Time - evento.Time).TotalMinutes, 2);

                        // Pausas reais raramente excedem 8h
                        if (duracao <= 0 || duracao > 2400) continue;  // duracao is decimal, 12s = 0.2 → passes through, ceiling to 1min

                        // A janela expandida é só para busca;
                        // só inclui pausas que pertencem ao período do mês alvo
                        if (eventoFim.Time < inicioMes || evento.Time > fimMes) continue;

                        todosEventos.Add(new EventoPausa
                        {
                            MachineId = printer.Id,
                            JobUuid = jobUuid,
                            InicioPausa = evento.Time,
                            FimPausa = eventoFim.Time,
                            DuracaoMinutos = duracao,
                            Motivo = eventoFim.TypeId == 131074
                                ? "Impressão pausada manualmente"
                                : "Pausa até abort do job",
                            TipoFim = eventoFim.TypeId == 131074 ? "resumed" : "aborted"
                        });
                    }

                    _logger.LogDebug("      Impressora {PrinterId}: {Count} pausas detectadas",
                        printer.Id,
                        todosEventos.Count(e => e.MachineId == printer.Id));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao buscar eventos de pausa para {PrinterId}", printer.Id);
                }
            });

            await Task.WhenAll(tasks);
            return todosEventos.OrderBy(e => e.InicioPausa).ToList();
        }

        // ========================================
        // PROCESSAR MÊS COM PAUSAS
        // ✅ CORREÇÃO 1: filtro considera InicioPausa.Date OU FimPausa.Date
        // ========================================

        private ResumoMensal ProcessarResumoMensalComPausas(
            int machineId,
            string machineName,
            int ano,
            int mes,
            List<MesaProducao> todosJobsMes,
            List<EventoPausa> eventosPausaMes)
        {
            var cultureInfo = new CultureInfo("pt-BR");
            var totalDias = DateTime.DaysInMonth(ano, mes);

            var resumo = new ResumoMensal
            {
                MachineId = machineId,
                MachineName = machineName,
                Ano = ano,
                Mes = mes,
                MesNome = cultureInfo.DateTimeFormat.GetMonthName(mes)
            };

            var resumosDias = new List<ResumoDiario>();
            var todosMotivos = new Dictionary<string, MotivoConsolidado>();

            for (int dia = 1; dia <= totalDias; dia++)
            {
                var data = new DateTime(ano, mes, dia);

                var jobsDia = todosJobsMes
                    .Where(j => j.DatetimeStarted.Date == data.Date)
                    .OrderBy(j => j.DatetimeStarted)
                    .ToList();

                // ✅ CORREÇÃO 1: inclui pausa se INÍCIO ou FIM cai no dia
                // Antes: e.InicioPausa.Date == data.Date
                // Perdia pausas de jobs que cruzam a meia-noite
                var eventosPausaDia = eventosPausaMes
                    .Where(e => e.InicioPausa.Date == data.Date || e.FimPausa.Date == data.Date)
                    .ToList();

                var timeline = ConstruirTimelineComPausas(jobsDia, eventosPausaDia, data);

                var producaoDia = timeline.Where(b => b.Status == StatusMaquina.Producao).Sum(b => b.DuracaoMinutos);
                var pausasDia = timeline.Where(b => b.Status == StatusMaquina.Pausa).Sum(b => b.DuracaoMinutos);
                var ociosidadeDia = timeline.Where(b => b.Status == StatusMaquina.Ociosidade).Sum(b => b.DuracaoMinutos);
                var esperaDia = timeline.Where(b => b.Status == StatusMaquina.EsperaOperador).Sum(b => b.DuracaoMinutos);
                var manutencaoDia = timeline.Where(b => b.Status == StatusMaquina.Manutencao).Sum(b => b.DuracaoMinutos);

                foreach (var bloco in timeline)
                {
                    var chave = $"{bloco.Status}_{bloco.Motivo}";
                    if (!todosMotivos.ContainsKey(chave))
                    {
                        todosMotivos[chave] = new MotivoConsolidado
                        {
                            Status = bloco.Status,
                            Motivo = bloco.Motivo,
                            MotivoDescricao = bloco.Mensagem,
                            TempoTotal = 0,
                            Ocorrencias = 0
                        };
                    }
                    todosMotivos[chave].TempoTotal += bloco.DuracaoMinutos;
                    todosMotivos[chave].Ocorrencias++;
                }

                resumosDias.Add(new ResumoDiario
                {
                    Data = data,
                    MachineId = machineId,
                    MachineName = machineName,
                    TempoProducao = producaoDia,
                    TempoPausas = pausasDia,
                    TempoOciosidade = ociosidadeDia,
                    TempoEsperaOperador = esperaDia,
                    TempoManutencao = manutencaoDia
                });
            }

            resumo.Dias = resumosDias;

            // Totais em MINUTOS
            decimal tempoTotalProducao = resumo.Dias.Sum(d => d.TempoProducao);
            decimal tempoTotalPausas = resumo.Dias.Sum(d => d.TempoPausas);
            decimal tempoTotalOciosidade = resumo.Dias.Sum(d => d.TempoOciosidade);
            decimal tempoTotalEsperaOperador = resumo.Dias.Sum(d => d.TempoEsperaOperador);
            decimal tempoTotalManutencao = resumo.Dias.Sum(d => d.TempoManutencao);

            // Converte minutos → horas para propriedades do resumo
            resumo.TempoProducao = Math.Round(tempoTotalProducao / 60, 1);
            resumo.TempoPausas = Math.Round(tempoTotalPausas / 60, 2);
            resumo.TempoOciosidade = Math.Round(tempoTotalOciosidade / 60, 1);
            resumo.TempoEsperaOperador = Math.Round(tempoTotalEsperaOperador / 60, 1);
            resumo.TempoManutencao = Math.Round(tempoTotalManutencao / 60, 1);
            resumo.TempoTotal = resumo.TempoProducao + resumo.TempoPausas +
                                         resumo.TempoOciosidade + resumo.TempoEsperaOperador +
                                         resumo.TempoManutencao;

            // Calcula percentual de cada motivo em relação ao total do seu status
            foreach (var motivo in todosMotivos.Values)
            {
                var tempoTotalStatus = motivo.Status switch
                {
                    StatusMaquina.Producao => tempoTotalProducao,
                    StatusMaquina.Pausa => tempoTotalPausas,
                    StatusMaquina.Ociosidade => tempoTotalOciosidade,
                    StatusMaquina.EsperaOperador => tempoTotalEsperaOperador,
                    StatusMaquina.Manutencao => tempoTotalManutencao,
                    _ => 1m
                };

                motivo.Percentual = tempoTotalStatus > 0
                    ? Math.Round((decimal)motivo.TempoTotal / tempoTotalStatus * 100, 1)
                    : 0;
            }

            resumo.Motivos = todosMotivos.Values.OrderByDescending(m => m.TempoTotal).ToList();
            resumo.JobsFinalizados = todosJobsMes.Count(j => j.IsSucess);
            resumo.JobsAbortados = todosJobsMes.Count(j => !j.IsSucess);

            // resumo.TaxaSucesso = totalJobs > 0
            //     ? Math.Round((decimal)resumo.JobsFinalizados / totalJobs * 100, 1)
            //     : 0;

            return resumo;
        }

        // ========================================
        // CONSTRUIR TIMELINE COM PAUSAS
        // ========================================

        private List<BlocoTimeline> ConstruirTimelineComPausas(
            List<MesaProducao> jobs,
            List<EventoPausa> eventosPausa,
            DateTime data)
        {
            var blocos = new List<BlocoTimeline>();
            var inicioDia = new DateTime(data.Year, data.Month, data.Day, 0, 0, 0, DateTimeKind.Utc);
            var fimDia = inicioDia.AddDays(1);

            if (!jobs.Any())
            {
                blocos.Add(new BlocoTimeline
                {
                    Inicio = inicioDia,
                    Fim = fimDia,
                    DuracaoMinutos = 1440,
                    Status = StatusMaquina.Ociosidade,
                    Motivo = MotivoStatus.FaltaJob,
                    Mensagem = "Sem jobs programados"
                });
                return blocos;
            }

            // Ociosidade antes do primeiro job
            if (jobs.First().DatetimeStarted > inicioDia)
            {
                var dur = (int)(jobs.First().DatetimeStarted - inicioDia).TotalMinutes;
                if (dur > 0)
                {
                    blocos.Add(new BlocoTimeline
                    {
                        Inicio = inicioDia,
                        Fim = jobs.First().DatetimeStarted,
                        DuracaoMinutos = dur,
                        Status = StatusMaquina.Ociosidade,
                        Motivo = MotivoStatus.FimExpediente,
                        Mensagem = "Período sem expediente"
                    });
                }
            }

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];

                if (!job.DatetimeFinished.HasValue)
                {
                    blocos.Add(new BlocoTimeline
                    {
                        Inicio = job.DatetimeStarted,
                        Fim = job.DatetimeStarted.AddMinutes(1),
                        DuracaoMinutos = 1,
                        Status = StatusMaquina.Pausa,
                        Motivo = MotivoStatus.FalhaTemporaria,
                        Mensagem = "Pausado e cancelado",
                        JobUuid = job.UltimakerJobUuid,
                        JobName = job.JobName
                    });
                    continue;
                }

                var pausasDoJob = eventosPausa
                    .Where(p => p.JobUuid == job.UltimakerJobUuid)
                    .OrderBy(p => p.InicioPausa)
                    .ToList();

                var momentoAtual = job.DatetimeStarted;

                foreach (var pausa in pausasDoJob)
                {
                    // Produção ANTES da pausa
                    if (pausa.InicioPausa > momentoAtual)
                    {
                        var durProd = (int)(pausa.InicioPausa - momentoAtual).TotalMinutes;
                        if (durProd > 0)
                        {
                            blocos.Add(new BlocoTimeline
                            {
                                Inicio = momentoAtual,
                                Fim = pausa.InicioPausa,
                                DuracaoMinutos = durProd,
                                Status = StatusMaquina.Producao,
                                Motivo = MotivoStatus.ProducaoNormal,
                                Mensagem = "Impressão em andamento",
                                JobUuid = job.UltimakerJobUuid,
                                JobName = job.JobName
                            });
                        }
                    }

                    // PAUSA
                    blocos.Add(new BlocoTimeline
                    {
                        Inicio = pausa.InicioPausa,
                        Fim = pausa.FimPausa,
                        DuracaoMinutos = (int)Math.Ceiling(pausa.DuracaoMinutos),
                        Status = StatusMaquina.Pausa,
                        Motivo = pausa.TipoFim == "resumed"
                            ? MotivoStatus.AjusteImpressao
                            : MotivoStatus.JobAbortado,
                        Mensagem = pausa.Motivo,
                        JobUuid = job.UltimakerJobUuid,
                        JobName = job.JobName
                    });

                    momentoAtual = pausa.FimPausa;
                }

                // Produção APÓS última pausa (ou produção total se não houve pausas)
                if (momentoAtual < job.DatetimeFinished.Value)
                {
                    var durProd = (int)(job.DatetimeFinished.Value - momentoAtual).TotalMinutes;
                    if (durProd > 0 && durProd < 2880)
                    {
                        blocos.Add(new BlocoTimeline
                        {
                            Inicio = momentoAtual,
                            Fim = job.DatetimeFinished.Value,
                            DuracaoMinutos = durProd,
                            Status = StatusMaquina.Producao,
                            Motivo = MotivoStatus.ProducaoNormal,
                            Mensagem = job.IsSucess ? "Produção concluída" : "Produção finalizada",
                            JobUuid = job.UltimakerJobUuid,
                            JobName = job.JobName
                        });
                    }
                }

                // Espera operador após job
                DateTime fimEspera = job.DatetimeFinished.Value.AddMinutes(TEMPO_ESPERA_PADRAO_MIN);

                if (i + 1 < jobs.Count && jobs[i + 1].DatetimeStarted < fimEspera)
                    fimEspera = jobs[i + 1].DatetimeStarted;
                else if (fimEspera > fimDia)
                    fimEspera = fimDia;

                var durEspera = (int)(fimEspera - job.DatetimeFinished.Value).TotalMinutes;
                if (durEspera > 1)
                {
                    blocos.Add(new BlocoTimeline
                    {
                        Inicio = job.DatetimeFinished.Value,
                        Fim = fimEspera,
                        DuracaoMinutos = durEspera,
                        Status = StatusMaquina.EsperaOperador,
                        Motivo = MotivoStatus.EsperaRemocaoPeca,
                        Mensagem = "Aguardando remoção"
                    });
                }

                // Ociosidade entre jobs
                if (i + 1 < jobs.Count)
                {
                    var proximoJob = jobs[i + 1];
                    var durOciosidade = (int)(proximoJob.DatetimeStarted - fimEspera).TotalMinutes;
                    if (durOciosidade > 1)
                    {
                        blocos.Add(new BlocoTimeline
                        {
                            Inicio = fimEspera,
                            Fim = proximoJob.DatetimeStarted,
                            DuracaoMinutos = durOciosidade,
                            Status = StatusMaquina.Ociosidade,
                            Motivo = durOciosidade >= 120
                                ? MotivoStatus.FaltaJob
                                : MotivoStatus.AguardandoAprovacao,
                            Mensagem = durOciosidade >= 120 ? "Aguardando job" : "Intervalo"
                        });
                    }
                }
            }

            // Ociosidade após último job até fim do dia
            if (blocos.Any())
            {
                var ultimoBloco = blocos.OrderBy(b => b.Fim).Last();
                if (ultimoBloco.Fim < fimDia)
                {
                    var durRest = (int)(fimDia - ultimoBloco.Fim).TotalMinutes;
                    if (durRest > 0)
                    {
                        blocos.Add(new BlocoTimeline
                        {
                            Inicio = ultimoBloco.Fim,
                            Fim = fimDia,
                            DuracaoMinutos = durRest,
                            Status = StatusMaquina.Ociosidade,
                            Motivo = MotivoStatus.FimExpediente,
                            Mensagem = "Fim do expediente"
                        });
                    }
                }
            }

            // Garante exatamente 1440 minutos no dia
            var totalMinutos = blocos.Sum(b => b.DuracaoMinutos);
            if (totalMinutos != 1440 && blocos.Any())
            {
                var diferenca = 1440 - totalMinutos;
                var ultimo = blocos.OrderBy(b => b.Fim).Last();
                ultimo.DuracaoMinutos += diferenca;
                ultimo.Fim = ultimo.Fim.AddMinutes(diferenca);
            }

            return blocos.OrderBy(b => b.Inicio).ToList();
        }

        // ========================================
        // CLASSE AUXILIAR - EVENTO DE PAUSA
        // ========================================

        private class EventoPausa
        {
            public int MachineId { get; set; }
            public string JobUuid { get; set; }
            public DateTime InicioPausa { get; set; }
            public DateTime FimPausa { get; set; }
            public decimal DuracaoMinutos { get; set; }
            public string Motivo { get; set; }
            public string TipoFim { get; set; } // "resumed" ou "aborted"
        }

        // ========================================
        // CONSOLIDAR MOTIVOS (TOP 5 por status)
        // ========================================

        private List<MotivoConsolidado> ConsolidarMotivos(
            List<MotivoConsolidado> todosMotivos,
            StatusMaquina status,
            decimal tempoTotalStatusMinutos)
        {
            return todosMotivos
                .Where(m => m.Status == status)
                .GroupBy(m => new { m.Status, m.Motivo, m.MotivoDescricao })
                .Select(g => new MotivoConsolidado
                {
                    Status = g.Key.Status,
                    Motivo = g.Key.Motivo,
                    MotivoDescricao = g.Key.MotivoDescricao,
                    TempoTotal = g.Sum(m => m.TempoTotal),
                    Ocorrencias = g.Sum(m => m.Ocorrencias),
                    Percentual = tempoTotalStatusMinutos > 0
                        ? Math.Round((decimal)g.Sum(m => m.TempoTotal) / tempoTotalStatusMinutos * 100, 1)
                        : 0
                })
                .OrderByDescending(m => m.TempoTotal)
                .Take(5)
                .ToList();
        }

        // ========================================
        // RESUMO MENSAL POR IMPRESSORA
        // ========================================

        public async Task<ResumoMensal> ObterResumoMensal(int maquinaId, int ano, int mes)
        {
            var cacheKey = $"mensal_{maquinaId}_{ano}_{mes}";
            if (_cache.TryGetValue(cacheKey, out ResumoMensal cached))
                return cached;

            var printer = (await _ultimakerClient.GetPrintersAsync())
                .FirstOrDefault(p => p.Id == maquinaId);

            var inicioMes = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var fimMes = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes), 23, 59, 59, DateTimeKind.Utc);

            var todosJobsMes = await _producaoRepository.ObterJobsPorMaquinaEPeriodo(maquinaId, inicioMes, fimMes);
            var eventosPausaMes = await BuscarEventosPausaMesAsync(
                new List<UltimakerPrinterConfig> { printer }, inicioMes, fimMes);

            var resumo = ProcessarResumoMensalComPausas(
                maquinaId,
                printer?.Name ?? $"M{maquinaId}",
                ano, mes,
                todosJobsMes,
                eventosPausaMes.Where(e => e.MachineId == maquinaId).ToList()
            );

            _cache.Set(cacheKey, resumo, CACHE_DURATION);
            return resumo;
        }

        // ========================================
        // RESUMO DIÁRIO
        // ========================================

        public async Task<ResumoDiario> ObterResumoDiario(int maquinaId, DateTime data)
        {
            var printer = (await _ultimakerClient.GetPrintersAsync())
                .FirstOrDefault(p => p.Id == maquinaId);

            var jobs = await _producaoRepository.ObterJobsPorMaquinaEData(maquinaId, data);
            var inicioDia = new DateTime(data.Year, data.Month, data.Day, 0, 0, 0, DateTimeKind.Utc);
            var fimDia = inicioDia.AddDays(1);

            var eventosPausa = await BuscarEventosPausaMesAsync(
                new List<UltimakerPrinterConfig> { printer }, inicioDia, fimDia);

            var timeline = ConstruirTimelineComPausas(
                jobs,
                eventosPausa.Where(e => e.MachineId == maquinaId).ToList(),
                data
            );

            var resumo = new ResumoDiario
            {
                MachineId = maquinaId,
                MachineName = printer?.Name ?? $"M{maquinaId}",
                Data = data,
                Timeline = timeline,
                TempoProducao = timeline.Where(b => b.Status == StatusMaquina.Producao).Sum(b => b.DuracaoMinutos),
                TempoPausas = timeline.Where(b => b.Status == StatusMaquina.Pausa).Sum(b => b.DuracaoMinutos),
                TempoOciosidade = timeline.Where(b => b.Status == StatusMaquina.Ociosidade).Sum(b => b.DuracaoMinutos),
                TempoEsperaOperador = timeline.Where(b => b.Status == StatusMaquina.EsperaOperador).Sum(b => b.DuracaoMinutos),
                TempoManutencao = timeline.Where(b => b.Status == StatusMaquina.Manutencao).Sum(b => b.DuracaoMinutos)
            };

            var motivos = timeline
                .GroupBy(b => new { b.Status, b.Motivo, b.Mensagem })
                .Select(g =>
                {
                    var tempoBloco = g.Sum(b => b.DuracaoMinutos);
                    var tempoBase = g.Key.Status switch
                    {
                        StatusMaquina.Producao => resumo.TempoProducao,
                        StatusMaquina.Pausa => resumo.TempoPausas,
                        StatusMaquina.Ociosidade => resumo.TempoOciosidade,
                        StatusMaquina.EsperaOperador => resumo.TempoEsperaOperador,
                        StatusMaquina.Manutencao => resumo.TempoManutencao,
                        _ => 1
                    };

                    return new MotivoConsolidado
                    {
                        Status = g.Key.Status,
                        Motivo = g.Key.Motivo,
                        MotivoDescricao = g.Key.Mensagem,
                        TempoTotal = tempoBloco,
                        Ocorrencias = g.Count(),
                        Percentual = tempoBase > 0
                            ? Math.Round((decimal)tempoBloco / tempoBase * 100, 1)
                            : 0
                    };
                })
                .OrderByDescending(m => m.TempoTotal)
                .ToList();

            resumo.Motivos = motivos;
            return resumo;
        }

        // ========================================
        // TIMELINE POR DIA (compatibilidade)
        // ========================================

        public async Task<List<BlocoTimeline>> ObterTimelineDiaAsync(int maquinaId, DateTime data)
        {
            var jobs = await _producaoRepository.ObterJobsPorMaquinaEData(maquinaId, data);
            var printer = (await _ultimakerClient.GetPrintersAsync()).FirstOrDefault(p => p.Id == maquinaId);

            var inicioDia = new DateTime(data.Year, data.Month, data.Day, 0, 0, 0, DateTimeKind.Utc);
            var fimDia = inicioDia.AddDays(1);

            var eventosPausa = await BuscarEventosPausaMesAsync(
                new List<UltimakerPrinterConfig> { printer }, inicioDia, fimDia);

            return ConstruirTimelineComPausas(
                jobs,
                eventosPausa.Where(e => e.MachineId == maquinaId).ToList(),
                data
            );
        }

        public async Task<List<BlocoTimeline>> ObterTimelineEnriquecidaAsync(
            int maquinaId,
            DateTime data,
            bool incluirEventos = true)
        {
            return await ObterTimelineDiaAsync(maquinaId, data);
        }

        public async Task<Dictionary<DateTime, List<BlocoTimeline>>> ObterTimelinePeriodoAsync(
            int maquinaId,
            DateTime dataInicio,
            DateTime dataFim)
        {
            var resultado = new Dictionary<DateTime, List<BlocoTimeline>>();
            var dataAtual = dataInicio.Date;

            while (dataAtual <= dataFim.Date)
            {
                var timeline = await ObterTimelineDiaAsync(maquinaId, dataAtual);
                resultado[dataAtual] = timeline;
                dataAtual = dataAtual.AddDays(1);
            }

            return resultado;
        }
    }
}