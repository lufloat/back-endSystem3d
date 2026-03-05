using System;

namespace SistemaProducao3D.Modelos.Modelos
{
    public class EventoImpressora
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public string JobUuid { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public int TypeId { get; set; }
        public string Message { get; set; } = string.Empty;

        // TypeId 131072 = started, 131073 = paused, 131074 = resumed, 131075 = aborted, 131076 = finished, 131077 = cleared
        public bool IsPaused => TypeId == 131073;
        public bool IsResumed => TypeId == 131074;
        public bool IsAborted => TypeId == 131075;
        public bool IsFinished => TypeId == 131076;
        public bool IsStarted => TypeId == 131072;
    }
}