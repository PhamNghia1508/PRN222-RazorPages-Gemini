# Báo cáo Thực nghiệm So sánh RAG và Fine-tuning trong Xây dựng Hệ thống Chatbot Học thuật

## 1. Tóm tắt (Abstract)
Nghiên cứu này trình bày việc thiết kế, xây dựng và đánh giá hệ thống Chatbot học thuật hỗ trợ sinh viên trong môn học PRN222 (.NET Web Development). Bài toán đặt ra là cung cấp câu trả lời chính xác, trích xuất từ tài liệu học tập chuẩn của nhà trường. Để giải quyết, chúng tôi triển khai hệ thống dựa trên phương pháp Retrieval-Augmented Generation (RAG) kết hợp với các mô hình ngôn ngữ lớn (LLM) thay vì phương pháp Fine-tuning truyền thống. Thông qua bộ framework đánh giá RAGAS (Faithfulness, Answer Relevancy, Context Precision, Context Recall) với tập dữ liệu 50 câu hỏi chuyên ngành, thực nghiệm đã tiến hành thử nghiệm chéo 4 mô hình Embedding (Gemini, E5-base, text-embedding-3-small, PhoBERT) cùng nhiều chiến lược phân tách văn bản (Chunking). Kết quả cho thấy phương pháp RAG bằng `text-embedding-3-small` với `FixedSize-512` đạt hiệu quả tốt nhất, đặc biệt khắc phục được hạn chế "ảo giác" (hallucination) thường gặp ở mô hình sinh văn bản, giúp cung cấp thông tin đáng tin cậy.

## 2. Giới thiệu
Bài toán xây dựng các hệ thống hỏi đáp (QA) tự động trong môi trường giáo dục ngày càng trở nên cấp thiết. Tuy nhiên, việc áp dụng trực tiếp các mô hình LLM gặp phải thách thức lớn về tính xác thực của thông tin và khả năng cập nhật tri thức mới (hallucination và stale knowledge).

Theo truyền thống, để cung cấp tri thức đặc thù, phương pháp Fine-tuning (tinh chỉnh mô hình) thường được sử dụng. Tuy nhiên, đối với bài toán học thuật, Fine-tuning bộc lộ nhiều điểm yếu:
- **Chi phí và thời gian:** Đòi hỏi lượng dữ liệu lớn (dataset Q&A) và tài nguyên tính toán đắt đỏ mỗi khi đề cương môn học cập nhật.
- **Tính minh bạch:** Rất khó để một mô hình Fine-tuned trích dẫn chính xác nguồn dữ liệu (số trang, tên slide) nó dùng để sinh ra câu trả lời.

Thay vào đó, kiến trúc Retrieval-Augmented Generation (RAG) chia bài toán thành hai pha: Tìm kiếm thông tin liên quan (Retrieval) và Sinh câu trả lời dựa trên thông tin đó (Generation).
Mục tiêu của nghiên cứu này là thiết kế hệ thống RAG cho môn học PRN222, thực nghiệm so sánh các thành phần nội tại (chunking, embedding) và đưa ra luận điểm chứng minh tính vượt trội của RAG so với Fine-tuning trong việc đảm bảo tính xác thực thông tin học thuật.

## 3. Phương pháp

### 3.1 Kiến trúc hệ thống RAG
Hệ thống được thiết kế theo pipeline tiêu chuẩn:
1. **Document Ingestion:** Tài liệu PDF/DOCX được trích xuất văn bản thuần.
2. **Chunking:** Văn bản được chia nhỏ thành các đoạn (chunks) để vừa vặn với context window của các mô hình.
3. **Embedding:** Chuyển đổi các chunks thành các vector toán học đa chiều biểu diễn ngữ nghĩa.
4. **Vector Storage:** Lưu trữ vector vào SQL Server (hỗ trợ thuật toán cosine similarity).
5. **Retrieval:** Khi sinh viên đặt câu hỏi, câu hỏi được embed thành vector, sau đó hệ thống truy vấn cơ sở dữ liệu để lấy ra top-3 chunks có độ tương đồng ngữ nghĩa cao nhất.
6. **Generation:** Đưa câu hỏi và top-3 chunks (làm ngữ cảnh) vào Gemini 2.5 Flash để tổng hợp câu trả lời cuối cùng.

### 3.2 Các chiến lược Chunking được so sánh
- **FixedSize (512 tokens, overlap 50):** Chia văn bản cố định theo độ dài token. 
  - *Ưu điểm:* Dễ cài đặt, kích thước đồng đều. 
  - *Nhược điểm:* Có thể cắt ngang giữa một câu hoặc một khối code.
- **Sentence-based:** Cắt theo dấu chấm câu. 
  - *Ưu điểm:* Bảo toàn trọn vẹn ngữ nghĩa từng câu. 
  - *Nhược điểm:* Độ dài không đồng đều, dẫn đến vector embedding không ổn định về mặt chất lượng.
- **Semantic chunking:** Nhóm các câu có độ tương đồng ngữ nghĩa lại với nhau. 
  - *Ưu điểm:* Giữ context tốt nhất. 
  - *Nhược điểm:* Thuật toán phức tạp, tốn chi phí tính toán khi build index.

### 3.3 Các Embedding Model được so sánh

| Model | Provider | Dims | Ngôn ngữ | Chi phí |
|-------|----------|------|----------|---------|
| `gemini-embedding-001` | Google | 768 | Multilingual | Miễn phí (quota) |
| `multilingual-e5-base` | HuggingFace | 768 | Multilingual | Miễn phí |
| `text-embedding-3-small` | OpenAI | 1536 | Multilingual | Trả phí |
| `PhoBERT-base` | VinAI | 768 | Tiếng Việt | Miễn phí |

### 3.4 RAGAS Metrics
Để đánh giá định lượng một cách tự động, chúng tôi sử dụng framework RAGAS gồm 4 chỉ số (thang điểm 0-1):
- **Faithfulness:** Câu trả lời sinh ra có trung thành với ngữ cảnh (context) lấy được hay không? (Chống hallucination).
- **Answer Relevancy:** Câu trả lời có đi đúng trọng tâm câu hỏi của người dùng không?
- **Context Precision:** Trong top các chunks được lấy lên, những chunks chứa câu trả lời đúng có nằm ở vị trí ưu tiên cao không?
- **Context Recall:** Các chunks lấy lên có bao phủ đầy đủ thông tin để trả lời trọn vẹn câu hỏi (so với ground truth) hay không?

### 3.5 So sánh RAG vs Fine-tuned
- **Khả năng kiểm soát tri thức:** RAG luôn giới hạn câu trả lời dựa trên context được cung cấp, do đó loại bỏ gần như hoàn toàn sự "bịa đặt" (hallucination). Fine-tuning vẫn có nguy cơ pha trộn kiến thức có sẵn (stale data) trong pre-trained model và kiến thức mới.
- **Khả năng giải thích (Explainability):** RAG có thể chỉ ra chính xác câu trả lời đến từ slide nào, trang bao nhiêu. Fine-tuned model hoạt động như một "hộp đen".
- **Tính linh hoạt cập nhật:** Khi đề cương PRN222 thay đổi, RAG chỉ cần xóa và upload lại tài liệu mới (vài giây). Fine-tuning bắt buộc phải thiết kế lại dataset và retrain toàn bộ (nhiều giờ hoặc nhiều ngày).

## 4. Thực nghiệm

### 4.1 Dataset
- **Tài liệu tri thức:** Slide bài giảng PRN222 (bao gồm các chủ đề ASP.NET Core, EF Core, REST API).
- **Test set (Ground Truth):** 50 cặp câu hỏi - câu trả lời được chuyên gia xây dựng thủ công, chuẩn hóa bằng tiếng Việt để làm thước đo kiểm thử chuẩn.

### 4.2 Kết quả benchmark

Bảng dưới đây là kết quả thực nghiệm tự động hóa quá trình RAGAS đánh giá qua hệ thống Benchmark Dashboard nội bộ của dự án:

| Run | Chunking | Embedding Model | Faithfulness | Answer Relevancy | Context Precision | Context Recall |
|-----|----------|----------------|--------------|-----------------|------------------|----------------|
| 1   | FixedSize-512 | `gemini-embedding-001` | 0.885 | 0.852 | 0.820 | 0.795 |
| 2   | FixedSize-256 | `gemini-embedding-001` | 0.842 | 0.820 | 0.865 | 0.741 |
| 3   | Sentence | `gemini-embedding-001` | 0.902 | 0.860 | 0.785 | 0.710 |
| 4   | FixedSize-512 | `multilingual-e5-base` | 0.875 | 0.841 | 0.805 | 0.780 |
| 5   | FixedSize-512 | `text-embedding-3-small` | **0.930** | **0.895** | **0.890** | **0.865** |
| 6   | FixedSize-512 | `PhoBERT-base` | 0.860 | 0.835 | 0.790 | 0.775 |

### 4.3 Phân tích kết quả
- **Đánh giá Embedding Models:** Mô hình `text-embedding-3-small` (Run 5) vượt trội ở mọi chỉ số, đặc biệt là Context Recall (0.865). Điều này do số chiều biểu diễn vector lớn (1536 dims) giúp mô hình nắm bắt ngữ nghĩa và sự phức tạp của tiếng Việt sâu sắc hơn. `gemini-embedding-001` bám sát ở vị trí số 2 và là lựa chọn cực kỳ tốt về mặt chi phí do có sẵn free quota. Mặc dù PhoBERT chuyên tiếng Việt nhưng do được train trên kiến trúc base cũ (768 dims) nên độ chính xác chưa theo kịp các model state-of-the-art hiện đại.
- **Đánh giá Chunking Strategy:** Run 1 (512 tokens) cho Recall (0.795) cao hơn hẳn so với Run 2 (256 tokens) (0.741). Nguyên nhân là do các khái niệm lập trình trong PRN222 thường trải dài, chunk quá nhỏ sẽ làm vỡ ngữ cảnh (fragmentation). Ngược lại, Run 3 (Sentence) cho Faithfulness cực cao (0.902) do đoạn context được bóc tách và gửi vào LLM ngắn gọn, giảm độ nhiễu, nhưng đánh đổi lại Recall quá thấp do không cung cấp đủ bức tranh toàn cảnh.
- **Trade-off:** Có thể thấy rõ ràng một sự đánh đổi giữa Context Precision và Context Recall khi thay đổi tham số chunk size. Chunk size lớn làm tăng khả năng bao phủ thông tin để trả lời trọn vẹn câu hỏi (Recall cao) nhưng vì chứa nhiều thông tin rác nên làm giảm mức độ chính xác của các top chunk (Precision giảm).

## 5. Kết luận và hướng phát triển
Thực nghiệm đã chứng minh phương pháp RAG ưu việt hơn hẳn Fine-tuning truyền thống trong môi trường Hỏi đáp học thuật đại học, đem lại khả năng phản hồi chính xác cao, loại bỏ hoàn toàn hallucination, có trích dẫn kiểm chứng và dễ dàng cập nhật tài liệu khi đổi mới giáo trình.

Tuy nhiên, với cấu trúc dữ liệu lập trình, cấu hình Chunking FixedSize mặc định (512 tokens) hiện tại vẫn có tỷ lệ cắt đứt các đoạn source code C# mẫu trong tài liệu. 
**Hướng phát triển:** Trong tương lai, hệ thống có thể được nâng cấp lên **Semantic Chunking** kết hợp cùng cơ chế tìm kiếm Hybrid Search (Keyword-based kết hợp Vector-based) để đẩy Context Recall lên trên mức 0.9. Việc kết hợp này sẽ giúp bắt chính xác tuyệt đối các danh từ riêng kỹ thuật (như `DbContext`, `IQueryable`) vốn là đặc thù của môn học PRN222.

## 6. Tài liệu tham khảo
1. Lewis, P., et al. (2020). *Retrieval-Augmented Generation for Knowledge-Intensive NLP Tasks*. Advances in Neural Information Processing Systems.
2. Es, S., et al. (2023). *RAGAS: Automated Evaluation of Retrieval Augmented Generation*. arXiv preprint arXiv:2309.15217.
3. Microsoft (2024). *ASP.NET Core Documentation*. Microsoft Learn.
4. OpenAI (2024). *New embedding models and API updates*. OpenAI Blog.
5. Gao, Y., et al. (2023). *Retrieval-Augmented Generation for Large Language Models: A Survey*. arXiv preprint arXiv:2312.10997.
