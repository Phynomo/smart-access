using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class ReportService
    {
        private readonly FirestoreContext _context;

        public ReportService(FirestoreContext context)
        {
            _context = context;
        }

        // Arma el agregado del dashboard del administrador.
        public async Task<StatisticsResponseDto> GetStatistics(ReportQueryDto q)
        {
            var to = (q.To?.ToUniversalTime()) ?? DateTime.UtcNow;
            var from = (q.From?.ToUniversalTime()) ?? to.Date.AddDays(-30);

            var residentsSnap = await _context.Residents
                .WhereEqualTo("isActive", true)
                .GetSnapshotAsync();
            var totalActiveResidents = residentsSnap.Count;

            var eventsSnap = await _context.AccessEvents.GetSnapshotAsync();
            var events = eventsSnap.Documents.Select(d => d.ConvertTo<AccessEvent>()).ToList();

            var todayStart = DateTime.UtcNow.Date;
            var todayAccessEvents = events.Count(e => e.Timestamp.ToDateTime() >= todayStart);

            var inRange = events
                .Where(e =>
                {
                    var t = e.Timestamp.ToDateTime();
                    return t >= from && t <= to;
                })
                .ToList();

            var total = inRange.Count;
            var qrCount = inRange.Count(e => e.AccessMethod == AccessMethods.Qr);
            var manualCount = inRange.Count(e => e.AccessMethod == AccessMethods.Manual);

            // Distribución de perfiles: residente (QR sin visitante), visitante
            // (QR con visitante) y manual.
            var residentCount = inRange.Count(e =>
                e.AccessMethod == AccessMethods.Qr && string.IsNullOrEmpty(e.VisitorName));
            var visitorCount = inRange.Count(e =>
                e.AccessMethod == AccessMethods.Qr && !string.IsNullOrEmpty(e.VisitorName));

            return new StatisticsResponseDto
            {
                TotalActiveResidents = totalActiveResidents,
                TodayAccessEvents = todayAccessEvents,
                QrAccessPercent = Percent(qrCount, total),
                ManualAccessPercent = Percent(manualCount, total),
                VisitorCount = visitorCount,
                ResidentCount = residentCount,
                ManualCount = manualCount,
                AuthorizedCount = inRange.Count(e => e.Result == AccessResults.Authorized),
                RejectedCount = inRange.Count(e => e.Result == AccessResults.Rejected),
                Trend = BuildTrend(inRange, q.Granularity),
                PeriodStart = from,
                PeriodEnd = to,
            };
        }

        private static double Percent(int part, int total) =>
            total == 0 ? 0 : Math.Round(part * 100.0 / total, 2);

        private static List<TrendPointDto> BuildTrend(List<AccessEvent> events, string? granularity)
        {
            Func<DateTime, DateTime> bucket = (granularity ?? "day").ToLowerInvariant() switch
            {
                "month" => d => new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                "week" => d => DateTime.SpecifyKind(d.Date.AddDays(-(int)d.DayOfWeek), DateTimeKind.Utc),
                _ => d => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc),
            };

            return events
                .GroupBy(e => bucket(e.Timestamp.ToDateTime()))
                .Select(g => new TrendPointDto { Date = g.Key, Count = g.Count() })
                .OrderBy(p => p.Date)
                .ToList();
        }
    }
}
