namespace smart_access_api.DTOs
{
    // Parámetros del reporte (se enlazan desde el query string).
    public class ReportQueryDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        // Granularidad de la serie temporal: day | week | month.
        public string Granularity { get; set; } = "day";
    }

    public class TrendPointDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    // Respuesta del dashboard del administrador.
    public class StatisticsResponseDto
    {
        public int TotalActiveResidents { get; set; }
        public int TodayAccessEvents { get; set; }

        public double QrAccessPercent { get; set; }
        public double ManualAccessPercent { get; set; }

        public int VisitorCount { get; set; }
        public int ResidentCount { get; set; }
        public int ManualCount { get; set; }

        public int AuthorizedCount { get; set; }
        public int RejectedCount { get; set; }

        public List<TrendPointDto> Trend { get; set; } = new();

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }
}
