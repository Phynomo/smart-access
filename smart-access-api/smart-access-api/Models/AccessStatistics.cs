using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    // Agregado calculado para el dashboard del administrador.
    // No se persiste como colección; lo arma ReportService a partir de
    // AccessEvents y Residents al servir /api/reports.
    //
    // Nota: aunque NO es un documento Firestore, llevamos [FirestoreData] por si
    // en el futuro se quiere cachear el agregado en una colección `dailyStats`
    // (ver "Decisiones que conviene confirmar" en MODELS.md).
    [FirestoreData]
    public class AccessStatistics
    {
        [FirestoreProperty("totalActiveResidents")]
        public int TotalActiveResidents { get; set; }

        [FirestoreProperty("todayAccessEvents")]
        public int TodayAccessEvents { get; set; }

        // Porcentaje de accesos por método (QR vs manual).
        [FirestoreProperty("qrAccessPercent")]
        public double QrAccessPercent { get; set; }

        [FirestoreProperty("manualAccessPercent")]
        public double ManualAccessPercent { get; set; }

        // Conteos por perfil dentro del período consultado.
        [FirestoreProperty("visitorCount")]
        public int VisitorCount { get; set; }

        [FirestoreProperty("residentCount")]
        public int ResidentCount { get; set; }

        [FirestoreProperty("manualCount")]
        public int ManualCount { get; set; }

        // Conteos por estado del evento (para gráfico de barras).
        [FirestoreProperty("authorizedCount")]
        public int AuthorizedCount { get; set; }

        [FirestoreProperty("rejectedCount")]
        public int RejectedCount { get; set; }

        // Serie temporal para el gráfico de tendencia (día / semana / mes).
        [FirestoreProperty("trend")]
        public List<TimeSeriesPoint> Trend { get; set; } = new();

        // Período cubierto por el reporte.
        [FirestoreProperty("periodStart")]
        public Timestamp PeriodStart { get; set; }

        [FirestoreProperty("periodEnd")]
        public Timestamp PeriodEnd { get; set; }
    }

    [FirestoreData]
    public class TimeSeriesPoint
    {
        [FirestoreProperty("date")]
        public Timestamp Date { get; set; }

        [FirestoreProperty("count")]
        public int Count { get; set; }
    }
}
