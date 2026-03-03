using Business_Logic.Repositories.Interfaces;
using Business_Logic.Serviços.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Business_Logic.Serviços
{
    public class VisaoGeralService : IVisaoGeralService
    {
        private readonly IProducaoRepository _producaoRepository;

        public VisaoGeralService(IProducaoRepository producaoRepository)
        {
            _producaoRepository = producaoRepository;
        }

        // ==========================================
        // CONSOLIDADO MENSAL (SEM DISTINÇÃO DE IMPRESSORA)
        // ==========================================

        public async Task<List<object>> ObterProducaoMensal(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "finished" && j.JobType != "Prototipo")
                .GroupBy(j => new { j.DatetimeStarted.Year, j.DatetimeStarted.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterPrototipoMensal(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            // ✅ CORREÇÃO: usar JobType em vez de IsPrototype
            // IsPrototype é [NotMapped] e pode não estar setado corretamente em todos os registros.
            // JobType == "Prototipo" é o campo persistido no banco de dados.
            return jobs
                .Where(j => j.Status == "finished" && j.JobType == "Prototipo")
                .GroupBy(j => new { j.DatetimeStarted.Year, j.DatetimeStarted.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterErrosMensais(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "failed")
                .GroupBy(j => new { j.DatetimeStarted.Year, j.DatetimeStarted.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterFailedMensais(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "failed")
                .GroupBy(j => new { j.DatetimeStarted.Year, j.DatetimeStarted.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterAbortedMensais(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "aborted")
                .GroupBy(j => new { j.DatetimeStarted.Year, j.DatetimeStarted.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterPesoMensal(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "finished")
                .GroupBy(j => new { j.DatetimeStarted.Year, j.DatetimeStarted.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                    valor = Math.Round(g.Sum(j => j.MaterialTotalKg), 2)
                })
                .ToList<object>();
        }

        // ==========================================
        // POR IMPRESSORA - ANO COMPLETO
        // ==========================================

        public async Task<List<object>> ObterProducaoPorImpressoraAnual(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "finished" && j.JobType != "Prototipo" && j.MachineId > 0)
                .GroupBy(j => new
                {
                    Periodo = $"{j.DatetimeStarted.Month:D2}/{j.DatetimeStarted.Year}",
                    j.MachineId,
                    NomeImpressora = $"Impressora {j.MachineId}"
                })
                .OrderBy(g => g.Key.Periodo).ThenBy(g => g.Key.MachineId)
                .Select(g => new
                {
                    periodo = g.Key.Periodo,
                    impressoraId = g.Key.MachineId,
                    nomeImpressora = g.Key.NomeImpressora,
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterPrototiposPorImpressoraAnual(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            // ✅ CORREÇÃO: usar JobType em vez de IsPrototype
            return jobs
                .Where(j => j.Status == "finished" && j.JobType == "Prototipo" && j.MachineId > 0)
                .GroupBy(j => new
                {
                    Periodo = $"{j.DatetimeStarted.Month:D2}/{j.DatetimeStarted.Year}",
                    j.MachineId,
                    NomeImpressora = $"Impressora {j.MachineId}"
                })
                .OrderBy(g => g.Key.Periodo).ThenBy(g => g.Key.MachineId)
                .Select(g => new
                {
                    periodo = g.Key.Periodo,
                    impressoraId = g.Key.MachineId,
                    nomeImpressora = g.Key.NomeImpressora,
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterErrosPorImpressoraAnual(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "failed" && j.MachineId > 0)
                .GroupBy(j => new
                {
                    Periodo = $"{j.DatetimeStarted.Month:D2}/{j.DatetimeStarted.Year}",
                    j.MachineId,
                    NomeImpressora = $"Impressora {j.MachineId}"
                })
                .OrderBy(g => g.Key.Periodo).ThenBy(g => g.Key.MachineId)
                .Select(g => new
                {
                    periodo = g.Key.Periodo,
                    impressoraId = g.Key.MachineId,
                    nomeImpressora = g.Key.NomeImpressora,
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterFailedPorImpressoraAnual(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "failed" && j.MachineId > 0)
                .GroupBy(j => new
                {
                    Periodo = $"{j.DatetimeStarted.Month:D2}/{j.DatetimeStarted.Year}",
                    j.MachineId,
                    NomeImpressora = $"Impressora {j.MachineId}"
                })
                .OrderBy(g => g.Key.Periodo).ThenBy(g => g.Key.MachineId)
                .Select(g => new
                {
                    periodo = g.Key.Periodo,
                    impressoraId = g.Key.MachineId,
                    nomeImpressora = g.Key.NomeImpressora,
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterAbortedPorImpressoraAnual(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "aborted" && j.MachineId > 0)
                .GroupBy(j => new
                {
                    Periodo = $"{j.DatetimeStarted.Month:D2}/{j.DatetimeStarted.Year}",
                    j.MachineId,
                    NomeImpressora = $"Impressora {j.MachineId}"
                })
                .OrderBy(g => g.Key.Periodo).ThenBy(g => g.Key.MachineId)
                .Select(g => new
                {
                    periodo = g.Key.Periodo,
                    impressoraId = g.Key.MachineId,
                    nomeImpressora = g.Key.NomeImpressora,
                    valor = g.Count()
                })
                .ToList<object>();
        }

        public async Task<List<object>> ObterPesoPorImpressoraAnual(int ano, int mesInicio, int mesFim)
        {
            var jobs = await _producaoRepository.ObterPorIntervalo(
                new DateTime(ano, mesInicio, 1),
                new DateTime(ano, mesFim, DateTime.DaysInMonth(ano, mesFim))
            );

            return jobs
                .Where(j => j.Status == "finished" && j.MachineId > 0)
                .GroupBy(j => new
                {
                    Periodo = $"{j.DatetimeStarted.Month:D2}/{j.DatetimeStarted.Year}",
                    j.MachineId,
                    NomeImpressora = $"Impressora {j.MachineId}"
                })
                .OrderBy(g => g.Key.Periodo).ThenBy(g => g.Key.MachineId)
                .Select(g => new
                {
                    periodo = g.Key.Periodo,
                    impressoraId = g.Key.MachineId,
                    nomeImpressora = g.Key.NomeImpressora,
                    valor = Math.Round(g.Sum(j => j.MaterialTotalKg), 2)
                })
                .ToList<object>();
        }
    }
}