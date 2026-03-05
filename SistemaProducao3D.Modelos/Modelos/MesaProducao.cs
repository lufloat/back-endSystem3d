using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace SistemaProducao3D.Modelos.Modelos
{
    public class MesaProducao
    {
        public int Id { get; set; }

        private DateTime _datetimeStarted;
        private DateTime? _datetimeFinished;

        public DateTime DatetimeStarted
        {
            get => _datetimeStarted;
            set => _datetimeStarted = ForceUtc(value);
        }

        public DateTime? DatetimeFinished
        {
            get => _datetimeFinished;
            set => _datetimeFinished = value.HasValue ? ForceUtc(value.Value) : null;
        }

        public decimal Material0Amount { get; set; }
        public decimal Material1Amount { get; set; }

        public Guid? Material0Guid { get; set; }
        public Guid? Material1Guid { get; set; }
        public decimal Material0WeightG { get; set; }
        public decimal Material1WeightG { get; set; }

        public decimal PrintTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public int MesaId { get; set; }
        public int MachineId { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string UltimakerJobUuid { get; set; } = string.Empty;
        public bool IsPrototype { get; set; }
        public bool IsRecondicionado { get; set; }
        public string JobType { get; set; } = "Pecas";

        // ✅ PROPRIEDADES CALCULADAS
        [NotMapped]
        public decimal MaterialTotal => Material0WeightG + Material1WeightG;

        [NotMapped]
        public decimal MaterialTotalKg => MaterialTotal / 1000m;

        // ✅ SUCESSO: apenas "finished" ou "completed"
        [NotMapped]
        public bool IsSucess
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Status))
                    return false;

                var statusLower = Status.ToLower().Trim();
                return statusLower == "finished" || statusLower == "completed";
            }
        }

        // ✅ ATUALIZADO: Calcula IsFailed baseado na coluna Status (sem precisar de coluna separada)
        [NotMapped]
        public bool IsFailed
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Status))
                    return false;

                var statusLower = Status.ToLower().Trim();
                return statusLower == "failed";
            }
        }

        // ✅ ATUALIZADO: Calcula IsAborted baseado na coluna Status (sem precisar de coluna separada)
        [NotMapped]
        public bool IsAborted
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Status))
                    return false;

                var statusLower = Status.ToLower().Trim();
                return statusLower == "aborted";
            }
        }

        // ✅ Jobs em andamento ou pendentes
        [NotMapped]
        public bool IsInProgress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Status))
                    return true;

                var statusLower = Status.ToLower().Trim();
                return statusLower == "pending" ||
                       statusLower == "printing" ||
                       statusLower == "paused" ||
                       statusLower == "in_progress" ||
                       statusLower == "queued" ||
                       statusLower == "wait_cleanup";
            }
        }

        // ✅ CRÍTICO: Jobs finalizados com FALLBACKS para dados do CSV
        [NotMapped]
        public bool IsFinished
        {
            get
            {
                // ✅ ESTRATÉGIA 1: Verificar status explícito
                if (!string.IsNullOrWhiteSpace(Status))
                {
                    var statusLower = Status.Trim().ToLowerInvariant();

                    if (statusLower == "finished" ||
                        statusLower == "completed" ||
                        statusLower == "failed" ||
                        statusLower == "aborted")
                        return true;
                }

                // ⭐ ESTRATÉGIA 2: FALLBACK para dados do CSV
                // Se tem datetime_finished preenchido, considerar finalizado
                if (DatetimeFinished.HasValue)
                    return true;

                // ⭐ ESTRATÉGIA 3: FALLBACK adicional
                // Se tem print_time maior que 1 hora E não está mais imprimindo
                // (assume que jobs longos sem datetime_finished foram finalizados)
                if (PrintTime > 3600 && !string.IsNullOrWhiteSpace(Status))
                {
                    var statusLower = Status.Trim().ToLowerInvariant();
                    // Se não está em progresso explicitamente, assume finalizado
                    if (statusLower != "pending" &&
                        statusLower != "printing" &&
                        statusLower != "paused" &&
                        statusLower != "in_progress" &&
                        statusLower != "queued" &&
                        statusLower != "wait_cleanup")
                        return true;
                }

                return false;
            }
        }

        // ✅ BONUS: Adicionar propriedade para verificar se tem data
        [NotMapped]
        public bool TemDataFinalizacao => DatetimeFinished.HasValue;

        public void DeterminarTipoJob()
        {
            if (string.IsNullOrWhiteSpace(JobName))
            {
                JobType = "Pecas";
                return;
            }

            var name = JobName?.ToLowerInvariant() ?? "";

            // 1. Recondicionado (maior prioridade)
            if (IsRecondicionado || name.ToLower().Contains("recond"))
            {
                JobType = "Recondicionado";
                IsPrototype = false;
                return;
            }

            // 2. Protótipo - ✅ ALTERADO: Apenas "Testes" com maiúscula e regex v\d+
            if (name.Contains("teste") ||        // pega Teste, Testes, TesteResistencia, TESTE
                 name.Contains("prototipo") ||
                 name.Contains("protótipo"))      // pega TestesMateriaisXXX de janeiro
            {
                IsPrototype = true;
                JobType = "Prototipo";
                return;
            }

            // 3. Ferramentas e Diversos
            if (name.ToLower().Contains("ferramenta") ||
                name.ToLower().Contains("tool") ||
                name.ToLower().Contains("diversos"))
            {
                JobType = "FerramentasDiversos";
                IsPrototype = false;
                return;
            }

            // 4. Placas
            if (name.ToLower().Contains("placa") || name.ToLower().Contains("board"))
            {
                JobType = "Placas";
                IsPrototype = false;
                return;
            }

            // 5. Peças (padrão)
            JobType = "Pecas";
            IsPrototype = false;
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
    }
}