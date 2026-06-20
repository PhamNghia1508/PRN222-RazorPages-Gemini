using PRN222.DAL.Entities;

namespace PRN222.DAL.Repositories.Interfaces;

public interface IChatMessageRepository : IRepository<ChatMessage>
{
    Task<IEnumerable<ChatMessage>> GetMessagesBySessionIdAsync(int sessionId);
}
