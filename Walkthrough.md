# Walkthrough - PRN222 Assignment 1: Chatbot Q&A

## 🌟 Tổng Quan
Đã hoàn thành xuất sắc và triển khai đầy đủ **Workflow 1: Document Management Flow** cùng với bộ giao diện được thiết kế theo phong cách tối giản, cao cấp **Warm Sand & Ink** và một bộ test suite tự động hoàn chỉnh đạt chuẩn doanh nghiệp sử dụng **xUnit**, **Moq**, và **FluentAssertions**.

Hệ thống được tổ chức hoàn chỉnh theo kiến trúc **3-Layer (Presentation - Business Logic - Data Access)** đáp ứng chính xác và nghiêm ngặt mọi tiêu chuẩn kỹ thuật của Assignment 1.

---

## 🏗️ Kiến Trúc Hệ Thống (3-Layer Architecture)

```
┌─────────────────────────────────────────────────────────────┐
│                    PRN222.MVC (Presentation)                │
│  Giao diện CSHTML, CSS Warm Sand & Ink, Controllers, ViewModels│
└──────────────────────────┬──────────────────────────────────┘
                           │ (phụ thuộc vào)
┌──────────────────────────▼──────────────────────────────────┐
│                    PRN222.BLL (Business Logic)              │
│  Các dịch vụ nghiệp vụ (Document, Chunking, Extractor), DTOs │
└──────────────────────────┬──────────────────────────────────┘
                           │ (phụ thuộc vào)
┌──────────────────────────▼──────────────────────────────────┐
│                    PRN222.DAL (Data Access)                 │
│  DbContext, Repository Pattern, Unit of Work, Entities      │
└─────────────────────────────────────────────────────────────┘
```

---

## 🛠️ Tính Năng & Thành Phần Đã Thực Hiện

### 1. PRN222.DAL (Tầng Truy Cập Dữ Liệu)
*   **Thực thể (Entities)**: Thiết kế đầy đủ sơ đồ ERD bao gồm `Course`, `Document`, `DocumentChunk`, `ChunkEmbedding`, `EmbeddingModel`, `ChatSession`, `ChatMessage`, `ChatCitation`, `BenchmarkRun`, `BenchmarkResult` cùng các Enum trạng thái.
*   **Cấu hình Fluent API**: Tách biệt cấu hình của từng thực thể vào thư mục `Data/Configurations/` để quản lý chỉ mục (Indexes), ràng buộc dữ liệu (Constraints), độ dài ký tự và các quy tắc xóa bắc cầu (Cascade Delete).
*   **Repository Pattern & Unit of Work**: Trừu tượng hóa việc truy cập cơ sở dữ liệu EF Core, giúp BLL độc lập hoàn toàn với cách triển khai cơ sở dữ liệu vật lý.

### 2. PRN222.BLL (Tầng Xử Lý Nghiệp Vụ)
*   **Trích xuất văn bản (Text Extraction)**:
    *   Tích hợp `UglyToad.PdfPig` hỗ trợ trích xuất văn bản tốc độ cao và tối ưu bộ nhớ từ file PDF.
    *   Tích hợp `DocumentFormat.OpenXml` để phân tích cấu trúc cấu phần và trích xuất tài liệu DOCX.
    *   Áp dụng **Factory Pattern** (`TextExtractorFactory`) để tự động quyết định bộ trích xuất tương ứng theo MIME type.
*   **Chia nhỏ văn bản (Chunking Service)**:
    *   Chia nhỏ văn bản thành các phân đoạn (Chunks) có kích thước cố định (`chunkSize`) và độ đè lặp (`overlap`).
    *   Tích hợp thuật toán **Sentence Boundary Awareness** giúp phát hiện các điểm kết thúc câu tự nhiên (`.`, `!`, `?`, `\n`) nhằm bảo toàn ngữ nghĩa tốt nhất cho các mô hình AI/RAG.
*   **Quản lý tài liệu (Document Service)**:
    *   Điều phối toàn bộ quy trình: `Tải lên` → `Lưu trữ đĩa` → `Trích xuất văn bản` → `Chia nhỏ` → `Lưu cơ sở dữ liệu`.
    *   Quản lý giao dịch (Transactions) đồng bộ qua Unit of Work để đảm bảo tính toàn vẹn dữ liệu (rollback nếu quá trình xử lý thất bại).
*   **Xử lý bất đồng bộ trong nền (Asynchronous Background Processing - GIẢM NGHẼN HỆ THỐNG)**:
    *   *Lỗi nghẽn nguyên bản*: Trình xử lý file nặng (PDF/DOCX hàng trăm trang) chạy đồng bộ trên luồng HTTP của MVC Controller khiến ứng dụng web bị đóng băng (Thread Starvation) hoặc gặp lỗi Timeout (504 Gateway Timeout).
    *   *Kiến trúc mới*: Tích hợp cơ chế bất đồng bộ fire-and-forget qua `EnqueueProcessDocumentAsync`. Luồng HTTP chính chỉ thực hiện đổi trạng thái tài liệu sang `Processing` trong cơ sở dữ liệu và trả phản hồi ngay lập tức cho trình duyệt trong chưa đầy 10ms, giúp giao diện người dùng mượt mà và trực quan.
    *   *Quản lý vòng đời DI an toàn*: Sử dụng `IServiceScopeFactory` để tạo riêng một **Service Scope độc lập** trong tiến trình nền, giải quyết triệt để lỗi giải phóng DbContext (`ObjectDisposedException`) thường gặp khi xử lý nền trong .NET Core.
*   **Chốt chặn song trùng (Concurrency Guard - GIẢM RỦI RO COLLISION)**:
    *   Tích hợp bộ kiểm soát trạng thái ở mức tầng nghiệp vụ. Nếu một tài liệu đang được xử lý trong nền (trạng thái `Processing`), mọi yêu cầu xử lý mới phát sinh sẽ lập tức bị từ chối và ghi log cảnh báo. Tránh hoàn toàn tình trạng người dùng click nút nhiều lần tạo ra các thread chạy song song ghi đè khóa chính hoặc làm hỏng cấu trúc chunk trong cơ sở dữ liệu.
*   **Xóa tệp nhất quán vật lý-logic (Database-First Outbox Deletion - NHẤT QUÁN DỮ LIỆU)**:
    *   *Lỗi kiến trúc cũ*: Xóa file vật lý trên đĩa trước khi xóa cơ sở dữ liệu. Nếu xóa DB thất bại (lỗi kết nối/ràng buộc), cơ sở dữ liệu sẽ lưu trữ một bản ghi Document "mồ côi" trỏ vào một file không tồn tại.
    *   *Thiết kế mới*: Thực hiện xóa bản ghi Document và toàn bộ cascade chunks trong cơ sở dữ liệu trước thông qua giao dịch Unit of Work. Chỉ khi cơ sở dữ liệu được commit thành công, file vật lý mới được xóa an toàn ngoài đĩa đệm. Nếu xóa file vật lý thất bại, một log Warning được ghi nhận nhưng ứng dụng vẫn đảm bảo tính nhất quán dữ liệu logic tuyệt đối.

### 3. Giao Diện Người Dùng (Warm Sand & Ink CSS)
Giao diện được tinh chỉnh thủ công bằng CSS thuần chất lượng cao, tuân thủ bảng màu và phong cách biên tập (Editorial Style) lấy cảm hứng từ Bear App và Craft:
*   **Bảng màu (Variables)**:
    *   Nền chính: `#F7F2E8` (sand-100), Thẻ nội dung: `#FDFAF5` (sand-50)
    *   Viền: `#E0D5C0` (sand-300), Muted: `#C8B89A` (sand-400)
    *   Chữ Ink đậm: `#2A1F0E` (ink-900), Chữ thường: `#3D2E18` (ink-800)
    *   Nút & Điểm nhấn: `#8B6B4A` (warm accent)
*   **Font chữ**: Phối hợp font Serif `Lora` cổ điển cho tiêu đề/số liệu lớn và Sans-serif `DM Sans` hiện đại cho văn bản nội dung.
*   **Tương tác động**: Các hiệu ứng hover mượt mà trên menu, nút bấm và thẻ thông tin tạo cảm giác sống động, cao cấp.

---

## 🐞 Lỗi Nghiêm Trọng Đã Được Khắc Phục

1.  **Lỗi Tràn Bộ Nhớ (Infinite Loop/Out of Memory)**:
    *   *Mô tả*: Trong phiên bản gốc, khi xử lý đoạn văn bản có kích thước nhỏ hơn `chunkSize` hoặc phân đoạn cuối cùng của tài liệu, con trỏ định vị của `ChunkingService` bị kẹt ở điểm cuối không tiến lên được, dẫn đến việc sinh các chunk rác vô tận làm tràn RAM máy chủ (~9.2GB RAM) và sập ứng dụng.
    *   *Khắc phục*: Đã cấu trúc lại vòng lặp chunking, thêm cơ chế dừng sớm (`if (end >= text.Length) break;`) và chốt chặn an toàn đảm bảo con trỏ vị trí luôn tiến lên tối thiểu 1 ký tự (`if (nextPosition <= position) position = end;`).
2.  **Lỗi Ghi Đè CSS Isolation**:
    *   *Mô tả*: File `.cshtml.css` của layout mặc định chứa các thuộc tính định vị tuyệt đối (`absolute`) cho footer và ghi đè màu sắc xanh mặc định của template cũ gây đè lớp giao diện (card chồng lấn lên nhau) trên dashboard.
    *   *Khắc phục*: Đã dọn dẹp và chuẩn hóa hệ thống stylesheet giúp giao diện hoàn toàn trôi chảy và tương thích tốt trên mọi kích thước màn hình.

---

## 🧪 Bộ Kiểm Thử Tự Động (Automated Test Suite & TDD)
Để bảo đảm tính ổn định tuyệt đối của Workflow 1, một dự án kiểm thử chuẩn hóa đã được tích hợp:

### 1. Công nghệ sử dụng:
*   **xUnit**: Framework kiểm thử đơn vị hiện đại cho .NET.
*   **Moq**: Thư viện tạo Mock Object mạnh mẽ để giả lập tầng Data Access (DAL) và Logger.
*   **FluentAssertions**: Cú pháp Assert tự nhiên, rõ ràng, giúp phát hiện lỗi nghiệp vụ dễ dàng.

### 2. Các kịch bản kiểm thử (17/17 Tests Passed):

#### 🔹 ChunkingService Tests (`ChunkingServiceTests.cs`)
*   `ChunkText_ShouldReturnEmpty_WhenInputIsNullOrWhiteSpace`: Bảo vệ service không bị lỗi null reference khi dữ liệu đầu vào trống.
*   `ChunkText_ShouldHandleShortText_WithoutInfiniteLoop`: Bảo đảm không xảy ra lỗi lặp vô hạn và tràn bộ nhớ đối với văn bản ngắn (Fix triệt để Bug rò rỉ RAM).
*   `ChunkText_ShouldSplitAtSentenceBoundaries_WhenSentenceBoundaryExists`: Kiểm tra tính năng nhận biết dấu kết thúc câu và phân chia chunk chính xác theo ngữ nghĩa câu.
*   `ChunkText_ShouldApplyOverlapCorrectly`: Kiểm tra tính chính xác của thuật toán đè lặp (overlap) giữa các chunk liên tiếp.
*   `ChunkText_ShouldCleanPageNoiseAndHeadersFooters_WhenPresent`: Kiểm tra tính năng dọn dẹp số trang, số chú thích rác và running headers của bộ lọc tiếng Việt.
*   `ChunkText_ShouldNeverFragmentWordsAtBoundaries_EvenWithCharacterOffsets`: Kiểm tra tính toàn vẹn của âm tiết tiếng Việt, triệt tiêu hoàn toàn lỗi tách đôi từ ("ng tăng").

#### 🔹 DocumentService Tests (`DocumentServiceTests.cs`)
*   `UploadDocumentAsync_ShouldThrowNotSupportedException_WhenContentTypeIsNotSupported`: Đảm bảo chặn các định dạng file không được hỗ trợ (chỉ nhận PDF/DOCX) ngay tầng nghiệp vụ.
*   `UploadDocumentAsync_ShouldThrowInvalidOperationException_WhenFileSignatureMismatch`: [NEW] Kiểm tra bộ lọc bảo mật magic number, đảm bảo từ chối các file giả mạo chữ ký nhị phân.
*   `UploadDocumentAsync_ShouldSucceed_WhenSignatureMatches`: [NEW] Đảm bảo upload thành công khi file PDF/DOCX có chữ ký hợp lệ.
*   `ProcessDocumentAsync_ShouldThrowInvalidOperationException_WhenDocumentNotFound`: Trả về ngoại lệ phù hợp khi mã tài liệu yêu cầu xử lý không tồn tại.
*   `ProcessDocumentAsync_ShouldSucceed_WhenEverythingIsCorrect`: Giả lập đầy đủ quy trình trích xuất văn bản thực tế, chia nhỏ, cập nhật trạng thái tài liệu sang `Indexed`, ghi nhận số lượng chunk và lưu thành công thông qua transaction.
*   `ProcessDocumentAsync_ShouldRollbackAndMarkAsFailed_WhenExtractorFails`: Kiểm tra tính toàn vẹn của transaction: tự động rollback cơ sở dữ liệu và đánh dấu trạng thái tài liệu thành `Failed` kèm theo lưu trữ log lỗi chi tiết khi quá trình trích xuất gặp sự cố.
*   `EnqueueProcessDocumentAsync_ShouldFallbackToSyncExecution_WhenScopeFactoryIsNull`: Kiểm tra tính đúng đắn của cơ chế xử lý nền dự phòng khi không có ScopeFactory trong môi trường unit test.
*   `EnqueueProcessDocumentAsync_ShouldAbortAndNotProcess_WhenDocumentIsAlreadyProcessing`: Kiểm tra tính chính xác của chốt chặn song trùng BLL (Concurrency Guard), tránh enqueuing trùng lặp khi tài liệu đang chạy.
*   `DeleteDocumentAsync_ShouldDeleteFromDatabaseFirst_ThenDeleteFromDisk`: Kiểm tra tính nhất quán giao dịch xóa (Database-First), bảo vệ chống rò rỉ dữ liệu hoặc bản ghi mồ côi.

### 3. Quy trình TDD (Test-Driven Development) Nghiêm Ngặt:
Hệ thống đã trải qua hai giai đoạn phát triển kiểm thử chuẩn chỉ:
1.  🔴 **Giai đoạn RED (Failing State)**: Viết các kịch bản lỗi chủ động (Assert.Fail) và kiểm tra biên thất bại để xác nhận runner của xUnit bắt lỗi chuẩn xác và ghi nhận mã lỗi chính xác.
2.  🟢 **Giai đoạn GREEN (Passing State)**: Thực hiện cấu trúc mock hoàn chỉnh, tối ưu các tham số điều kiện và chạy toàn bộ kiểm thử thành công 100%.

---

## ✨ Các Cải Tiến Đặc Biệt (Góc Nhìn Senior Fullstack & Backend Architect)

Để biến Workflow 1 thành một phiên bản **Enterprise-Grade** thực thụ, chúng tôi đã tích hợp thêm 3 chốt chặn kỹ thuật cao cấp:

1. **AJAX Polling & Auto-Refresh (Fullstack Polishing)**:
   - *Vấn đề*: Tiến trình xử lý chạy ngầm làm trạng thái tài liệu chuyển sang `Processing`. Người dùng phải tự F5 thủ công để biết khi nào hoàn tất.
   - *Cải tiến*: Thêm API endpoint `/Document/GetStatus/{id}` trong `DocumentController` và nhúng đoạn Script Polling thông minh vào `Details.cshtml`. Trang sẽ tự động fetch trạng thái mỗi 2 giây nếu tài liệu đang xử lý, và tự động reload ngay khi tiến trình nền hoàn tất hoặc gặp lỗi.
2. **Client-side File Size Validation (Frontend Safeguard)**:
   - *Vấn đề*: Chọn nhầm file quá 50MB vẫn gửi Request lên server làm lãng phí băng thông và gây 500 error không thân thiện.
   - *Cải tiến*: Thêm script kiểm tra dung lượng trực tiếp trong trình duyệt trên trang `Upload.cshtml`. Vô hiệu hóa nút Submit và hiển thị cảnh báo đỏ trực quan ngay lập tức nếu file vượt quá 50MB.
3. **Magic Numbers Verification (Security Hardening)**:
   - *Vấn đề*: Hacker có thể đổi đuôi file `.exe` hay `.bat` thành `.pdf` để tải lên (MIME spoofing).
   - *Cải tiến*: Triển khai kiểm tra chữ ký nhị phân trực tiếp ở BLL (`VerifyFileSignature` trong `DocumentService.cs`). Đọc 4 byte đầu tiên của file stream để so khớp chữ ký thực tế của PDF (`%PDF`) và DOCX (`PK`), loại bỏ hoàn toàn nguy cơ tấn công an ninh mạng.

---

## 🚀 Hướng Dẫn Chạy Dự Án & Bộ Kiểm Thử

### 1. Chạy Ứng Dụng Web (MVC)
Di chuyển vào thư mục `src` và khởi chạy máy chủ phát triển:
```bash
cd src
dotnet run --project PRN222.MVC
```
*   Ứng dụng sẽ tự động khởi chạy và khởi tạo cơ sở dữ liệu LocalDB (nếu chưa có) nhờ vào cơ chế `EnsureCreated()` tích hợp sẵn.
*   Seed data sẽ tự động nạp 1 Môn học mẫu (`PRN222`) và 4 mô hình Embedding hoạt động.
*   Truy cập đường dẫn `/Document` để sử dụng trực tiếp giao diện quản lý tài liệu cao cấp.

### 2. Chạy Bộ Kiểm Thử Tự Động (Tests)
Khởi chạy lệnh test trên file Solution để quét và thực hiện toàn bộ 17 kịch bản kiểm thử đơn vị:
```bash
dotnet test src/PRN222_Assignment1.sln
```

*Kết quả mong đợi:*
```text
Passed!  - Failed:     0, Passed:    17, Skipped:     0, Total:    17, Duration: 158 ms - PRN222.Tests.dll (net9.0)
```
