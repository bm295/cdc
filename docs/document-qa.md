# Hệ thống hỏi đáp tài liệu có evaluation

Module `document_qa` là ứng dụng độc lập trong repo CDC. Python 3.10+,
chỉ dùng standard library; không cần Kafka, MySQL, .NET hoặc API key để chạy.
Không thay đổi consumer hay cơ chế CDC hiện có.

## Chạy từ thư mục gốc repo

```bash
python -m document_qa.ingest README.md docs/cdc-flow.md docs/architecture.md docs/demo-web.md
python -m document_qa.server
```

Mở http://127.0.0.1:8090. Chọn file `.md`/`.txt` UTF-8 hoặc dán nội dung,
nhập mã và tiêu đề, lưu rồi đặt câu hỏi. Nhập cùng mã để thay thế tài liệu.
CLI dùng đường dẫn tương đối làm mã tài liệu; dùng cùng cách viết đường dẫn
khi cập nhật. `--db knowledge.db` trên cả hai lệnh chọn cùng cơ sở dữ liệu;
`--port 8091` đổi cổng server. SQLite tồn tại qua lần khởi động tiếp theo.

Ví dụ câu hỏi phù hợp tài liệu tiếng Anh của repo:

- `How are retry attempts and dead-letter offsets handled?`
- `What does the demo web dashboard show?`
- `How does Debezium capture MySQL binlog changes?`

## Pipeline

1. Ingest văn bản, chia thành đoạn 180 từ với overlap 30 từ.
2. Mã đoạn ổn định từ SHA-256 của mã tài liệu, vị trí và nội dung.
3. SQLite lưu tài liệu và đoạn trong cùng transaction; cập nhật xóa đoạn cũ.
4. BM25 xếp hạng đoạn dựa trên từ khóa Unicode, không phân biệt hoa thường.
5. Trả nguyên văn tối đa top-k đoạn, gắn `[1]`, `[2]`…; mỗi citation gồm mã
   tài liệu, tiêu đề, mã đoạn, vị trí từ (bắt đầu từ 0), quote và điểm BM25.
6. Không có đoạn khớp thì trả `abstained=true` và không có citation.

Đây là **extractive QA**, chưa dùng embeddings hoặc LLM để tổng hợp câu trả lời.
Điểm BM25 không phải xác suất đúng. Trùng từ thông dụng có thể gây trả lời sai;
không có từ khóa trùng có thể làm bỏ sót câu diễn đạt khác. Không tự dịch giữa
các ngôn ngữ. Hỗ trợ văn bản tiếng Việt có dấu nhưng không chuẩn hóa mất dấu.
PDF, OCR, DOCX và hội thoại nhiều lượt chưa được hỗ trợ.

## API

| Method | Path | Nội dung |
| --- | --- | --- |
| GET | `/health` | Trạng thái tiến trình |
| GET | `/api/documents` | Danh sách mã và tiêu đề |
| POST | `/api/documents` | `{"id":"runbook","title":"Runbook","text":"Kafka retries failed messages."}` |
| DELETE | `/api/documents/{id}` | Xóa tài liệu và toàn bộ đoạn; URL-encode mã |
| POST | `/api/search` | `{"question":"Kafka retries","top_k":3}` |
| POST | `/api/ask` | Cùng request; trả answer, citations, abstained, mode, latency_ms |

```bash
curl -X POST http://127.0.0.1:8090/api/ask \
  -H 'Content-Type: application/json' \
  -d '{"question":"How does Kafka retry work?","top_k":3}'
```

Giới hạn: top-k 1..20; câu hỏi 1..4000 ký tự; nội dung tài liệu tối đa
1 triệu ký tự và JSON body tối đa 1,1 triệu byte. JSON sai trả 400,
body sai kích thước trả 413. Server chỉ bind loopback, dành cho sử dụng cục bộ;
chưa có xác thực, phân quyền hoặc thiết kế lưu trữ cho nhiều tenant.
Tìm kiếm đọc và chấm điểm toàn bộ corpus trong bộ nhớ nên phù hợp corpus nhỏ.

## Evaluation và kiểm thử

```bash
python -m unittest discover -s document_qa/tests -v
python -m document_qa.evaluate
python -m document_qa.evaluate --top-k 1 --output artifacts/eval-k1.json
```

Dataset `document_qa/data/evaluation.json` có câu hỏi, tài liệu liên quan,
reference answer và kỳ vọng abstention. Nó nạp corpus từ các tài liệu thật trong
repo vào SQLite riêng trong bộ nhớ, không dùng DB của người dùng.
Báo cáo mặc định: `artifacts/document-qa-evaluation.json`, gồm response và metric
từng ca, trung bình, cấu hình top-k, thresholds và kết quả gate.

| Metric | Cách tính / ý nghĩa |
| --- | --- |
| Recall@k | Số tài liệu liên quan khác nhau tìm thấy / tổng tài liệu liên quan |
| Precision@k | Số đoạn thuộc tài liệu liên quan / số đoạn trả về |
| MRR | Nghịch đảo thứ hạng đoạn liên quan đầu tiên |
| Answer token F1 | F1 tập token giữa answer và reference; chỉ là lexical proxy |
| Extractive integrity | Answer có đúng định dạng ghép nguyên văn các citation không |
| Abstention accuracy | Quyết định từ chối có khớp kỳ vọng không |
| Latency ms | Thời gian retrieval và dựng answer; không bao gồm HTTP |

Recall, precision, MRR được tính trung bình trên các ca có tài liệu liên quan;
các ca unanswerable bị loại khỏi trung bình retrieval, nhưng vẫn tính trong
abstention accuracy, token F1 và integrity. Precision dùng mẫu số là số đoạn
thực sự trả về; với ca answerable không có kết quả thì bằng 0.
Token F1 không đo tương đương ngữ nghĩa; integrity không phải semantic faithfulness
và có thể bằng 1 ngay cả khi truy xuất nhầm tài liệu. Reference answer ngắn
trong khi output chứa cả đoạn nên token F1 thường thấp.

Gate mặc định: recall >= .65, MRR >= .65, integrity = 1 và abstention accuracy
>= .8. CLI thoát 1 khi gate không đạt. CI chạy cả unit/API tests và gate,
upload báo cáo kể cả khi evaluation thất bại. Không gate latency vì phụ thuộc máy.

Sáu ca ban đầu chỉ là smoke benchmark: thêm câu hỏi thật, câu hỏi tiếng Việt,
paraphrase, trường hợp trùng từ nhưng khác nghĩa, tài liệu mâu thuẫn và nhãn
ở cấp đoạn trước khi dùng để quyết định chất lượng sản phẩm. Giữ tập đánh giá
riêng khi điều chỉnh chunk size, overlap và top-k để tránh tối ưu theo bộ mẫu.

## Tool interfaces (v1.0)

`document_qa.tools.ToolRegistry` cung cấp contract độc lập HTTP và model provider.
`GET /api/tools` trả tên, mô tả, version, cờ `mutates`, `input_schema` và
`output_schema` theo JSON Schema Draft 2020-12 cho từng tool:

| Tool | Input | Output | Ghi dữ liệu |
| --- | --- | --- | --- |
| `search_documents` | question, top_k tùy chọn (mặc định 3) | Danh sách passage: chunk_id, document_id, title, word_offset, quote, score | Không |
| `answer_question` | question, top_k tùy chọn (mặc định 3) | question, mode, abstained, answer, citations, latency_ms | Không |
| `list_documents` | Object rỗng | Danh sách id, title | Không |
| `upsert_document` | id, title, text | id, chunks | Có; thay thế cùng id |
| `delete_document` | id | deleted (false nếu không tồn tại) | Có |

Mọi object từ chối field lạ; input kiểm tra required, kiểu, chuỗi trắng,
độ dài và giới hạn số. Boolean không được coi là integer. Output được kiểm tra
trước khi trả cho caller, gồm cấu trúc citation. `default` trong schema là
annotation; dispatcher áp dụng mặc định top_k=3. Các API cũ dùng chung registry,
giữ nguyên response thành công nhưng nay từ chối field thừa.

```bash
curl http://127.0.0.1:8090/api/tools
curl -X POST http://127.0.0.1:8090/api/tools/invoke \
  -H 'Content-Type: application/json' \
  -d '{"name":"search_documents","arguments":{"question":"Kafka retries","top_k":2}}'
```

Python: `ToolRegistry(kb).invoke('search_documents', {'question': 'Kafka'}, context)` với context tin cậy.
Kết quả trả trực tiếp theo output_schema. Lỗi tool qua HTTP có dạng
`{"error":{"code":"invalid_arguments","message":"..."}}`:
`unknown_tool`/`invalid_arguments` trả 400, `tool_execution_failed` trả 500,
không lộ exception nội bộ. Lỗi JSON/body và route giữ định dạng lỗi HTTP cũ.
`validate` chỉ triển khai các keyword được dùng trong registry, không phải
validator JSON Schema tổng quát; khi thêm keyword mới phải mở rộng validator
hoặc dùng thư viện đầy đủ. `mutates` mô tả tác động; policy thực thi quyền và xác nhận như mục bảo vệ bên dưới. Đây là tool interface/dispatcher,
chưa phải agent loop, MCP server hay tích hợp model tool calling.

## Authorization, confirmation và guardrails

API hiện yêu cầu `Authorization: Bearer <token>` cho các hành động tài liệu.
Cấu hình danh tính ở phía server, không nhận subject/scope do request tự khai:

```bash
# Tạo token bằng python -c 'import secrets; print(secrets.token_urlsafe(32))'
# Thay placeholder bên dưới bằng token vừa tạo; không commit token vào git.
export DOCUMENT_QA_IDENTITIES='[{"token":"REPLACE_WITH_RANDOM_TOKEN_AT_LEAST_32_CHARS","subject":"operator","scopes":["documents:read","documents:write","documents:delete"],"document_ids":null}]'
python -m document_qa.server
```

`document_ids:null` cho phép mọi tài liệu; danh sách cụ thể chỉ cho phép các id đó;
thiếu trường này mặc định không được truy cập tài liệu nào. Quyền đọc được áp dụng
trước chấm điểm retrieval, bao gồm list/search/answer và citations. Registry mặc
định từ chối nếu thiếu `ExecutionContext`; context chỉ được tạo bởi ứng dụng tin cậy.
CLI ingest là công cụ quản trị offline có quyền truy cập DB, không qua HTTP policy.

Luồng ghi cho cả tạo mới, ghi đè và xóa:

1. Gọi `POST /api/tools/prepare` với `{"name":"delete_document","arguments":{"id":"runbook"}}` cùng bearer token.
2. Hiển thị hành động và tham số cho người dùng, chờ xác nhận rõ ràng. Hủy thì không gọi invoke.
3. Gọi `/api/tools/invoke` với đúng name/arguments và header
   `X-Confirmation-Token` lấy từ bước chuẩn bị. Endpoint POST/DELETE cũ cũng yêu cầu header này.

Token ngẫu nhiên, hết hạn sau 120 giây, gắn với subject/tool/hash tham số,
chỉ dùng một lần; kiểm tra lại quyền khi thực thi. UI lưu bearer token trong
bộ nhớ và dùng hộp thoại xác nhận trước khi lưu tài liệu. Endpoint prepare không
tự chứng minh có người thật đồng ý: ứng dụng agent phải giữ quyền approve ở UI
người dùng, không tự đưa token trở lại model để model tự phê duyệt.

Policy chung giới hạn 60 request tool/phút/subject (bao gồm prepare), 1.000 tài
liệu và tổng 10 triệu ký tự nội dung; tối đa 1.000 confirmation đang chờ.
HTTP giới hạn body như trước và có socket timeout 10 giây. SQLite `tool_audit`
lưu thời gian, subject, tên tool, kết quả cho prepare/invoke; không lưu nội dung,
arguments, bearer hay confirmation token. Lỗi xác thực HTTP trước dispatcher không
được ghi vào bảng audit. Cần chính sách lưu trữ/rotation audit khi vận hành dài hạn.

Mã HTTP: 401 thiếu/sai credential; 403 thiếu quyền; 409 thiếu/sai confirmation
hoặc vượt quota; 429 vượt tần suất/capacity; 400 sai input; 500 thực thi thất bại.
Danh sách schema thêm `required_scope` và `requires_confirmation`.

Giới hạn triển khai: dành cho một tiến trình local. Rate counters và confirmation
ở RAM, restart làm token mất hiệu lực và reset counters. Dùng chung Policy trong
một server; nhiều worker cần store và khóa phân tán. Không có hard timeout hủy
Python tool đang chạy, identity provider ngoài, hay cơ chế rollback tự động khi
output validation lỗi sau mutation. Schema validation không thay thế authorization.
Các hướng dẫn API không có bearer ở các phần trước cần thêm header theo mục này.
