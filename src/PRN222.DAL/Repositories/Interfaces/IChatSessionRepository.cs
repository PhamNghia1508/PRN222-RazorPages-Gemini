using PRN222.DAL.Entities;

namespace PRN222.DAL.Repositories.Interfaces;

public interface IChatSessionRepository : IRepository<ChatSession>
{
    Task<IEnumerable<ChatSession>> GetSessionsAsync(string userId);
    Task<ChatSession?> GetByIdForUserAsync(int sessionId, string userId);
}
