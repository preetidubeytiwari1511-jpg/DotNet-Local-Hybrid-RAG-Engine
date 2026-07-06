# Local Enterprise Hybrid RAG & Text-to-SQL Engine

An enterprise-grade, high-performance hybrid AI backend built natively on **.NET 8/9** using **Microsoft Semantic Kernel**. This architecture operates **100% locally** to guarantee absolute data privacy and sovereignty, ensuring zero cloud dependency and zero operational costs.

The system features a dual-pipeline engine:
1. **Unstructured Data Pipeline (RAG):** Ingests complex multi-page PDFs using overlapping recursive token chunking.
2. **Structured Data Pipeline (Text-to-SQL):** Connects to **SQL Server** via Entity Framework Core to translate natural language into optimized multi-table T-SQL joins dynamically.

---

## 🏛️ Architectural Overview

```text
                        [ USER QUESTION / SWAGGER API ]
                                       │
        ┌──────────────────────────────┴──────────────────────────────┐
        ▼                                                             ▼
 [ 📄 PDF PIPELINE (RAG) ]                                 [ 🛢️ TEXT-TO-SQL PIPELINE ]
  * Ingests Raw PDF Content via PdfPig                      * Maps EF Core Database Models
  * Splits into 500-char Overlapping Chunks                 * Exposes Metadata Schemas to LLM
  * Converts text to dense numeric Vectors                  * Runtime Interception & Execution Layer
  * Vector Store: Volatile Memory Store                     * Guardrail: Safe Strict SELECT Queries Only
  * Backup: Crash-Proof Local Persistence File              * Execution: Dynamic multi-table data reader
```

---

## 🚀 Key Engineering Features

- **Zero Cloud Footprint:** Leverages local **Ollama** runtimes (`llama3` and `nomic-embed-text`) to preserve sensitive enterprise business compliance data locally.
- **Overlapping Sliding Chunk Windows:** Employs a custom windowing algorithm (500-char chunks with a 100-char overlap) to retain sentence context boundaries and maximize embedding relevance matching.
- **Advanced Interception Layer:** Intercepts out-of-topic queries (e.g., politics, random facts) before hitting SQL Server, preventing resource depletion attacks and database crashes.
- **Dynamic Response Streaming:** Utilizes asynchronous HTTP text streaming pipelines (`IAsyncEnumerable`) to minimize Time-to-First-Token (TTFT) responsiveness over Swagger Kestrel.

---

## 🛠️ Prerequisites & Local AI Environment Setup

Ensure you have **Ollama** installed on your host machine ([Download Ollama](https://ollama.com)). Open your terminal and pull the mandatory models:

```bash
# Pull the Chat/Reasoning Model
ollama pull llama3

# Pull the Mathematical Embedding Vector Model
ollama pull nomic-embed-text
```

---

## 📂 Project Structure

```text
SmartAiApi/
│
├── Program.cs                  # Web Bootstrapper, Dependency Injections & API Endpoints
├── SmartAiApi.csproj           # Project Dependencies (SemanticKernel, EFCore, PdfPig)
│
├── Data/
│   └── AppDbContext.cs         # Entity Framework Core DbContext infrastructure
│
└── Utilities/
    └── TextChunker.cs          # Overlapping sliding chunk generator utility
```

---

## 🚦 How to Setup and Run Local Project

1. **Clone the repository:**
   ```bash
   git clone https://github.com
   cd SmartAiApi
   ```

2. **Generate Database Migrations & Deploy Tables:**
   Open **Package Manager Console (PMC)** inside Visual Studio and run:
   ```powershell
   Add-Migration InitialSetup
   Update-Database
   ```

3. **Run the ASP.NET Core API application:**
   ```bash
   dotnet run
   ```

4. **Launch Swagger UI Interface:**
   Navigate to the local port listed in your console pipeline: `https://localhost:YOUR_PORT/swagger/index.html`

---

## 🧪 Operational Use Cases & API Verification

### 📋 1. Unstructured RAG Pipeline
* **POST** `/api/upload-pdf`: Upload a multi-page PDF policy document. The backend splits the structural layout into text records and indexes vectors instantly.
* **POST** `/api/ask-policy`: Ask contextual questions like *"What is the standard leave approval policy?"*. The semantic search locates the specific text fraction matching your criteria and streams a tailored answer.

### 🛢️ 2. Structured Text-to-SQL Pipeline
* **POST** `/api/sync-sql-to-ai`: Populates automatic database seed objects (Products and Orders) and flushes them to local metadata indexes.
* **POST** `/api/ask-db-join`: Ask analytical multi-table inquiries like *"Give me the customer names who checked out Nike Running Shoes"*. The generator parses structural foreign relationships and responds with raw mapped dynamic object rows.

---
