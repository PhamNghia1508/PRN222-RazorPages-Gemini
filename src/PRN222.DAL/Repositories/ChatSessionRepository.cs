using Microsoft.EntityFrameworkCore;
using PRN222.DAL.Data;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.DAL.Repositories;

public class ChatSessionRepository : Repository<ChatSession>, IChatSessionRepository
{
    public ChatSessionRepository(ChatbotDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ChatSession>> GetSessionsAsync(string userId)
    {
        return await _dbSet
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastMessageAt ?? s.CreatedAt)
            .ToListAsync();
    }

    public async Task<ChatSession?> GetByIdForUserAsync(int sessionId, string userId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
    }
}
