using SistemaProducao3D.Data.Context;
using SistemaProducao3D.Integration.Ultimaker;
using SistemaProducao3D.Modelos.Modelos;
using Business_Logic.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using Business_Logic.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Business_Logic.Serviços.Sync
{
    public class SyncService : ISyncService
    {
        private readonly DatabaseContext _context;
        private readonly IUltimakerClient _ultimakerClient;
        private readonly IMaterialRepository _materialRepository;

        public SyncService(
            DatabaseContext context,
            IUltimakerClient ultimakerClient,
            IMaterialRepository materialRepository)
        {
            _context = context;
            _ultimakerClient = ultimakerClient;
            _materialRepository = materialRepository;
        }

        public async Task SincronizarMesAsync(int ano, int mes)
        {
            var inicio = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var fim = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes), 23, 59, 59, DateTimeKind.Utc);
            await SincronizarPorPeriodoAsync(inicio, fim);
        }

        public async Task SincronizarPeriodoAsync(int anoInicio, int mesInicio, int anoFim, int mesFim)
        {
            var inicio = new DateTime(anoInicio, mesInicio, 1, 0, 0, 0, DateTimeKind.Utc);
            var fim = new DateTime(anoFim, mesFim, DateTime.DaysInMonth(anoFim, mesFim), 23, 59, 59, DateTimeKind.Utc);
            await SincronizarPorPeriodoAsync(inicio, fim);
        }

        public async Task SincronizarJobAsync(string uuid)
        {
            var job = await _ultimakerClient.GetJobByUuidAsync(uuid);
            if (job == null) throw new Exception("Job não encontrado");

            var existe = _context.MesasProducao.Any(x => x.UltimakerJobUuid == job.Uuid);
            if (existe) return;

            var mesa = await CriarMesaAsync(job, 0);
            _context.MesasProducao.Add(mesa);
            await _context.SaveChangesAsync();
        }

        private async Task SincronizarPorPeriodoAsync(DateTime inicio, DateTime fim)
        {
            var printers = await _ultimakerClient.GetPrintersAsync();

            Console.WriteLine($"\n🔄 Sincronizando {inicio:yyyy-MM-dd} até {fim:yyyy-MM-dd}");
            Console.WriteLine($"📌 {printers.Count} impressoras\n");

            foreach (var printer in printers)
            {
                Console.WriteLine($"🖨️  {printer.Name} (ID: {printer.Id})");

                // ── Sincronizar JOBS ──────────────────────────────────────
                var jobs = await _ultimakerClient.GetJobsAsync(printer.Id, inicio, fim);
                int novos = 0, atualizados = 0, ignorados = 0;

                foreach (var job in jobs)
                {
                    var mesaExistente = _context.MesasProducao
                        .FirstOrDefault(x => x.UltimakerJobUuid == job.Uuid);

                    if (mesaExistente == null)
                    {
                        var novaMesa = await CriarMesaAsync(job, printer.Id);
                        _context.MesasProducao.Add(novaMesa);
                        novos++;
                    }
                    else
                    {
                        var statusAtual = NormalizarStatus(job.Result);
                        var jobFinalizado = JobFinalizado(statusAtual);

                        if (mesaExistente.Status != statusAtual || jobFinalizado)
                        {
                            await AtualizarMesaAsync(mesaExistente, job, printer.Id);
                            atualizados++;
                        }
                        else
                        {
                            ignorados++;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"   ✅ Jobs — Novos: {novos} | Atualizados: {atualizados} | Ignorados: {ignorados}");

                // ── Sincronizar EVENTOS ───────────────────────────────────
                try
                {
                    // Expande janela ±1 dia para capturar eventos cross-midnight
                    var inicioEventos = inicio.AddDays(-1);
                    var fimEventos = fim.AddDays(1);

                    var eventos = await _ultimakerClient.GetEventsAsync(printer.Id, inicioEventos, fimEventos);

                    // Filtra apenas eventos relevantes para timeline
                    var eventosFiltrados = eventos.Where(e =>
                        e.TypeId == 131072 || // started
                        e.TypeId == 131073 || // paused
                        e.TypeId == 131074 || // resumed
                        e.TypeId == 131075 || // aborted
                        e.TypeId == 131076 || // finished
                        e.TypeId == 131077    // cleared
                    ).ToList();

                    int eventosNovos = 0;

                    foreach (var evento in eventosFiltrados)
                    {
                        var jobUuid = evento.GetJobUuid();
                        if (string.IsNullOrEmpty(jobUuid)) continue;

                        var timeUtc = DateTime.SpecifyKind(evento.Time, DateTimeKind.Utc);

                        // Evita duplicatas usando o índice único
                        var existe = await _context.EventosImpressora.AnyAsync(e =>
                            e.MachineId == printer.Id &&
                            e.JobUuid == jobUuid &&
                            e.TypeId == evento.TypeId &&
                            e.Time == timeUtc);

                        if (!existe)
                        {
                            _context.EventosImpressora.Add(new EventoImpressora
                            {
                                MachineId = printer.Id,
                                JobUuid = jobUuid,
                                Time = timeUtc,
                                TypeId = evento.TypeId,
                                Message = evento.Message ?? string.Empty
                            });
                            eventosNovos++;
                        }
                    }

                    await _context.SaveChangesAsync();
                    Console.WriteLine($"   ✅ Eventos — Novos: {eventosNovos} | Total API: {eventosFiltrados.Count}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️  Erro ao sincronizar eventos de {printer.Name}: {ex.Message}\n");
                }
            }
        }

        private async Task<MesaProducao> CriarMesaAsync(UltimakerJob job, int printerId)
        {
            var inicio = job.DatetimeStarted ?? job.CreatedAt ?? DateTime.UtcNow;

            var mesa = new MesaProducao
            {
                UltimakerJobUuid = job.Uuid,
                JobId = job.Uuid,
                JobName = job.Name ?? "",
                Status = NormalizarStatus(job.Result),
                DatetimeStarted = ForceUtc(inicio),
                DatetimeFinished = ToUtc(job.DatetimeFinished),
                Material0Amount = job.Material0Amount.GetValueOrDefault(0),
                Material1Amount = job.Material1Amount.GetValueOrDefault(0),
                PrintTime = job.TimeElapsed.GetValueOrDefault(0),
                MachineId = printerId,
                MesaId = 0
            };

            await CalcularPesoMateriais(mesa, job);
            mesa.DeterminarTipoJob();

            return mesa;
        }

        private async Task AtualizarMesaAsync(MesaProducao mesa, UltimakerJob job, int machineId)
        {
            var status = NormalizarStatus(job.Result);
            var finalizado = JobFinalizado(status);

            mesa.Status = status;
            mesa.MachineId = machineId;

            if (finalizado && job.DatetimeFinished.HasValue)
            {
                mesa.DatetimeFinished = ToUtc(job.DatetimeFinished);

                if (job.Material0Amount.HasValue && job.Material0Amount.Value > 0)
                    mesa.Material0Amount = job.Material0Amount.Value;

                if (job.Material1Amount.HasValue && job.Material1Amount.Value > 0)
                    mesa.Material1Amount = job.Material1Amount.Value;

                if (job.TimeElapsed.HasValue && job.TimeElapsed.Value > 0)
                    mesa.PrintTime = job.TimeElapsed.Value;

                await CalcularPesoMateriais(mesa, job);
            }

            mesa.DeterminarTipoJob();
        }

        private async Task CalcularPesoMateriais(MesaProducao mesa, UltimakerJob job)
        {
            if (job.Material0Guid.HasValue && mesa.Material0Amount > 0)
            {
                var material0 = await _materialRepository.ObterOuCriarMaterial(
                    job.Material0Guid.Value,
                    job.Material0Name ?? "Material 0",
                    job.Material0Brand ?? "Generic"
                );
                mesa.Material0Guid = material0.UltimakerMaterialGuid;
                mesa.Material0WeightG = material0.ConverterVolumeMm3ParaGramas(mesa.Material0Amount);
            }

            if (job.Material1Guid.HasValue && mesa.Material1Amount > 0)
            {
                var material1 = await _materialRepository.ObterOuCriarMaterial(
                    job.Material1Guid.Value,
                    job.Material1Name ?? "Material 1",
                    job.Material1Brand ?? "Generic"
                );
                mesa.Material1Guid = material1.UltimakerMaterialGuid;
                mesa.Material1WeightG = material1.ConverterVolumeMm3ParaGramas(mesa.Material1Amount);
            }
        }

        private static bool JobFinalizado(string status)
        {
            return status == "finished" ||
                   status == "completed" ||
                   status == "failed" ||
                   status == "aborted";
        }

        private static string NormalizarStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "in_progress";

            var normalized = status.Trim().ToLowerInvariant();

            return normalized switch
            {
                "finished" => "finished",
                "completed" => "finished",
                "failed" => "failed",
                "aborted" => "aborted",
                "printing" => "in_progress",
                "paused" => "in_progress",
                "pending" => "in_progress",
                _ => normalized
            };
        }

        private static DateTime ForceUtc(DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                _ => dateTime
            };
        }

        private static DateTime? ToUtc(DateTime? date)
        {
            if (!date.HasValue) return null;
            return ForceUtc(date.Value);
        }
    }
}