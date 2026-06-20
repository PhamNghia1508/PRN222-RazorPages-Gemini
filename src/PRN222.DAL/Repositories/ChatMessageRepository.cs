using Microsoft.EntityFrameworkCore;
using PRN222.DAL.Data;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.DAL.Repositories;

public class ChatMessageRepository : Repository<ChatMessage>, IChatMessageRepository
{
    public ChatMessageRepository(ChatbotDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesBySessionIdAsync(int sessionId)
    {
        return await _dbSet
            .Include(m => m.Citations)
                .ThenInclude(c => c.Chunk)
                    .ThenInclude(chunk => chunk.Document)
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }
}
