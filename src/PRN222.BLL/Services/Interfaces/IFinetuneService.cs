using PRN222.BLL.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PRN222.BLL.Services.Interfaces;

public interface IFinetuneService
{
    Task<IEnumerable<QAPairDto>> GetQAPairsByCourseAsync(int courseId);
    
    /// <summary>
    /// Triggers generation of QA pairs for chunks in a course that haven't been processed yet.
    /// Returns the number of new pairs generated.
    /// </summary>
    Task<int> GenerateDatasetForCourseAsync(int courseId, int targetCount = 50);
    
    /// <summary>
    /// Exports the QA pairs to a JSONL string formatted for fine-tuning.
    /// formatType: "openai" or "gemini"
    /// </summary>
    Task<string> ExportDatasetToJsonlAsync(int courseId, string formatType = "openai");
}
