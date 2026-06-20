using PRN222.DAL.Data;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.DAL.Repositories;

public class QAPairRepository : Repository<QAPair>, IQAPairRepository
{
    public QAPairRepository(ChatbotDbContext context) : base(context)
    {
    }
}
