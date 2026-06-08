using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class LabNoteService
    {
        private readonly FirestoreContext _context;

        public LabNoteService(FirestoreContext context)
        {
            _context = context;
        }

        // Crea una nota. El servidor genera Id y CreateAt; el UserId viene del token JWT.
        public async Task<Lab_Note> Create(LabNoteDto dto, string userId)
        {
            if (!LabCategories.IsValid(dto.Category))
                throw new Exception("Category inválida. Valores permitidos: Quimica, Biologia, Fisica, Otro");

            if (dto.Priority < 1 || dto.Priority > 3)
                throw new Exception("Priority inválida. Valores permitidos: 1 (baja), 2 (media), 3 (alta)");

            var note = new Lab_Note
            {
                Id = Guid.NewGuid().ToString(),
                Title = dto.Title,
                Observation = dto.Observation,
                Category = dto.Category,
                Priority = dto.Priority,
                IsPublic = dto.IsPublic,
                Tags = dto.Tags ?? string.Empty,
                CreateAt = DateTime.UtcNow,
                UserId = userId
            };

            await _context.LabNotes.Document(note.Id).SetAsync(note);
            return note;
        }

        // Retorna únicamente las notas del usuario autenticado.
        public async Task<List<Lab_Note>> GetByUser(string userId)
        {
            var snapshot = await _context.LabNotes
                .WhereEqualTo("userId", userId)
                .GetSnapshotAsync();

            return snapshot.Documents.Select(d => d.ConvertTo<Lab_Note>()).ToList();
        }

        public async Task<Lab_Note?> GetById(string id)
        {
            var doc = await _context.LabNotes.Document(id).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Lab_Note>() : null;
        }

        // Elimina la nota validando la pertenencia. Toda la lógica vive aquí:
        // el controller solo traduce el resultado a un código HTTP.
        public async Task<DeleteResult> Delete(string id, string userId)
        {
            var doc = await _context.LabNotes.Document(id).GetSnapshotAsync();
            if (!doc.Exists)
                return DeleteResult.NotFound;

            var note = doc.ConvertTo<Lab_Note>();

            // Si el UserId del token no coincide con el dueño, no se permite borrar.
            if (note.UserId != userId)
                return DeleteResult.Forbidden;

            await _context.LabNotes.Document(id).DeleteAsync();
            return DeleteResult.Deleted;
        }
    }

    // Resultado posible de una operación de borrado de nota.
    public enum DeleteResult
    {
        NotFound,
        Forbidden,
        Deleted
    }
}
