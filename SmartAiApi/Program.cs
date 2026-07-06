using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using SmartAiApi;
using SmartAiApi.DbContext;
using SmartAiApi.Models;
using UglyToad.PdfPig;

var builder = WebApplication.CreateBuilder(args);

#pragma warning disable SKEXP0001, SKEXP0050, SKEXP0052 
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// SWAGGER CONFIGURATION
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// LOCAL OLLAMA CLIENTS
using HttpClient embeddingClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434/v1"), Timeout = TimeSpan.FromMinutes(5) };
using HttpClient chatClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434/v1"), Timeout = TimeSpan.FromMinutes(5) };

var memoryBuilder = new MemoryBuilder();

memoryBuilder.WithOpenAITextEmbeddingGeneration(
    modelId: "nomic-embed-text",
    apiKey: "not-needed",
    httpClient: embeddingClient
);

memoryBuilder.WithMemoryStore(new VolatileMemoryStore());
var memory = memoryBuilder.Build();

string collectionName = "CompanyPoliciesNoDocker";

// 💡 FIXED PATH: Isse file hamesha main project folder mein banegi (Program.cs ke sath)
string backupFilePath = Path.Combine(Directory.GetCurrentDirectory(), "pdf_backup_data.txt");

// STARTUP LOAD CONFIGURATION
if (File.Exists(backupFilePath))
{
    try
    {
        string savedText = File.ReadAllText(backupFilePath);
        if (!string.IsNullOrWhiteSpace(savedText))
        {
            // Startup par data ko memory mein async task chalakar inject karein
            await memory.SaveInformationAsync(collectionName, id: "permanent_pdf_data", text: savedText);
            Console.WriteLine(">>> Backup file successfully loaded into AI Memory! <<<");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>> Error loading backup file: {ex.Message} <<<");
    }
}

// CHAT KERNEL CONFIGURATION
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAIChatCompletion(modelId: "llama3", apiKey: "not-needed", httpClient: chatClient);
var kernel = kernelBuilder.Build();
builder.Services.AddTransient<Kernel>(sp => kernelBuilder.Build());

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Products Seed Logic (Same Rahega)
    if (!db.Products.Any())
    {
        db.Products.AddRange(
            new Product { Id = 1, Name = "iPhone 15 Pro", Category = "Electronics", Stock = 12, Price = 130000 },
            new Product { Id = 2, Name = "Samsung Galaxy S24", Category = "Electronics", Stock = 0, Price = 110000 },
            new Product { Id = 3, Name = "MacBook Air M3", Category = "Electronics", Stock = 5, Price = 115000 },
            new Product { Id = 4, Name = "Nike Running Shoes", Category = "Footwear", Stock = 10, Price = 8000 }
        );
        db.SaveChanges();
    }

    // 🟢 NEW: Orders Seed Logic (Agra table khali hai to)
    if (!db.Orders.Any())
    {
        db.Orders.AddRange(
            new Order {  ProductId = 1, QuantityOrdered = 2, CustomerName = "Rahul Sharma" }, // Bought 2 iPhones
            new Order {  ProductId = 4, QuantityOrdered = 1, CustomerName = "Amit Verma" },  // Bought 1 Nike Shoe
            new Order {  ProductId = 1, QuantityOrdered = 1, CustomerName = "Pooja Patel" }  // Bought 1 iPhone
        );
        db.SaveChanges();
    }
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// =========================================================================
// ENDPOINT 1: PDF UPLOAD (With Fixed Local Writing)
// =========================================================================
//app.MapPost("/api/upload-pdf", async (IFormFile file) =>
//{
//    if (file == null || file.Length == 0) return Results.BadRequest("Kripya valid PDF dein.");

//    try
//    {
//        using var stream = file.OpenReadStream();
//        using var pdfDocument = PdfDocument.Open(stream);
//        string pdfText = "";

//        foreach (var page in pdfDocument.GetPages())
//        {
//            pdfText += page.Text + "\n";
//        }

//        if (string.IsNullOrWhiteSpace(pdfText)) return Results.BadRequest("PDF khali hai.");

//        // 💡 FIXED: Purani file ko delete karke fresh file write karein
//        if (File.Exists(backupFilePath))
//        {
//            File.Delete(backupFilePath);
//        }

//        await File.WriteAllTextAsync(backupFilePath, pdfText);

//        await memory.SaveInformationAsync(
//            collection: collectionName,
//            id: "permanent_pdf_data",
//            text: pdfText,
//            description: $"File: {file.FileName}"
//        );

//        return Results.Ok(new
//        {
//            message = $"'{file.FileName}' successfully save ho gayi hai!",
//            savedPath = backupFilePath // Swagger par dikhega ki file kahan bani hai
//        });
//    }
//    catch (Exception ex)
//    {
//        return Results.Problem($"Error parsing or saving PDF: {ex.Message}");
//    }
//}).WithName("UploadPdfFile")
//.WithTags("HR Management")
//app.MapPost("/api/sync-sql-to-ai", async (AppDbContext db) =>
//{
//    try
//    {
//        // 1. SQL Server se saare products le kar aao
//        var products = await db.Products.ToListAsync();

//        if (!products.Any()) return Results.BadRequest("Database mein koi product nahi mila.");

//        string sqlTextData = "";

//        // 2. Har row ko ek saaf text line mein badlo jise AI samajh sake
//        foreach (var p in products)
//        {
//            string stockStatus = p.Stock > 0 ? $"{p.Stock} items available" : "OUT OF STOCK";
//            sqlTextData += $"Product ID: {p.Id}, Name: {p.Name}, Category: {p.Category}, Price: {p.Price} INR, Stock Status: {stockStatus}.\n";
//        }

//        // 3. Same purane file backup system mein write kar do taaki restart-proof rahe
//        // (Yahan hum same backupFilePath use kar rahe hain jo PDF mein kiya tha)
//        if (File.Exists(backupFilePath)) File.Delete(backupFilePath);
//        await File.WriteAllTextAsync(backupFilePath, sqlTextData);

//        // 4. AI ki memory collection mein register kar do
//        await memory.SaveInformationAsync(
//            collection: collectionName,
//            id: "permanent_sql_data",
//            text: sqlTextData,
//            description: "Synced Data from SQL Server Products Table"
//        );

//        return Results.Ok(new
//        {
//            message = "SQL Server data successfully synced with AI Memory!",
//            totalProductsSynced = products.Count,
//            preview = sqlTextData
//        });
//    }
//    catch (Exception ex)
//    {
//        return Results.Problem($"SQL Sync Error: {ex.Message}");
//    }
//})
//.WithName("SyncSqlToAi")
//.WithTags("Database AI Management")
//.DisableAntiforgery();
app.MapPost("/api/upload-pdf", async (IFormFile file) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("Kripya valid PDF dein.");

    try
    {
        using var stream = file.OpenReadStream();
        using var pdfDocument = PdfDocument.Open(stream);
        string pdfText = "";

        foreach (var page in pdfDocument.GetPages())
        {
            pdfText += page.Text + "\n";
        }

        if (string.IsNullOrWhiteSpace(pdfText)) return Results.BadRequest("PDF khali hai.");

        // =========================================================================
        // 🟢 ADVANCED CHUNKING IMPLEMENTATION
        // =========================================================================
        // 1. Text ko 500 characters ke chunks mein todein (100 char overlapping ke sath)
        List<string> textChunks = TextChunker.SplitIntoChunks(pdfText, chunkSize: 500, chunkOverlap: 100);

        // Backup file saaf karke naye chunks jodna
        if (File.Exists(backupFilePath)) File.Delete(backupFilePath);

        // 2. Loop chala kar har ek chunk ko AI Vector Memory mein save karein
        for (int i = 0; i < textChunks.Count; i++)
        {
            string chunkId = $"pdf_chunk_{Guid.NewGuid()}_{i}";
            string currentChunkText = textChunks[i];

            // Hard disk text file backup mein append karein (taaki restart proof rahe)
            // Hum chunks ko alag karne ke liye [CHUNK_SPLIT] tag lagayenge
            await File.AppendAllTextAsync(backupFilePath, currentChunkText + "\n[CHUNK_SPLIT]\n");

            // Vector Database Indexing
            await memory.SaveInformationAsync(
                collection: collectionName,
                id: chunkId,
                text: currentChunkText,
                description: $"File: {file.FileName} | Part: {i + 1}"
            );
        }

        return Results.Ok(new
        {
            message = $"'{file.FileName}' successfully chunked aur save ho gayi hai!",
            totalChunksCreated = textChunks.Count
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing PDF: {ex.Message}");
    }
})
.WithName("UploadPdfFile")
.WithTags("HR Management")
.DisableAntiforgery();

//app.MapPost("/api/ask-db-raw", async (string userQuery, HttpContext httpContext, AppDbContext db) =>
//{
//    var kernel = httpContext.RequestServices.GetRequiredService<Kernel>();
//    var chatService = kernel.GetRequiredService<IChatCompletionService>();

//    ChatHistory history = new ChatHistory();

//    // 1. AI ko database ka structure (Schema) aur boundary rules batana
//    history.AddSystemMessage(
//        "You are an expert SQL Server Query Generator. Your job is to convert user natural language questions into valid T-SQL queries.\n\n" +
//        "DATABASE SCHEMA:\n" +
//        "Table: Products\n" +
//        "Columns:\n" +
//        " - Id (int, Primary Key)\n" +
//        " - Name (nvarchar)\n" +
//        " - Category (nvarchar)\n" +
//        " - Stock (int)\n" +
//        " - Price (decimal)\n\n" +
//        "CRITICAL RULES:\n" +
//        "1. ONLY return the raw SQL query string. Do NOT include markdown blocks like ```sql or explanation text.\n" +
//        "2. Only generate SELECT queries. DO NOT generate INSERT, UPDATE, DELETE, or DROP queries for safety.\n" +
//        "3. If you cannot answer with the given schema, output an empty string."
//    );

//    // 2. Few-Shot Examples taaki AI syntax sahi likhe
//    history.AddUserMessage("How many items do we have in total?");
//    history.AddAssistantMessage("SELECT SUM(Stock) FROM Products;");

//    history.AddUserMessage("Show me products out of stock.");
//    history.AddAssistantMessage("SELECT * FROM Products WHERE Stock = 0;");

//    // 3. User ka actual question pass karna
//    history.AddUserMessage(userQuery);

//    // 4. AI se SQL Query generate karwana
//    var aiResponse = await chatService.GetChatMessageContentAsync(history);
//    string generatedSql = aiResponse.Content?.Trim() ?? string.Empty;

//    // Safety Clean-up (Markdown tags hatana agar model galti se add kare)
//    if (generatedSql.StartsWith("```sql")) generatedSql = generatedSql.Replace("```sql", "");
//    if (generatedSql.StartsWith("```")) generatedSql = generatedSql.Replace("```", "");
//    generatedSql = generatedSql.Replace("```", "").Trim();

//    // Guardrail: Agar AI ne SELECT query nahi banayi to block kar dein (SQL Injection Security)
//    if (string.IsNullOrWhiteSpace(generatedSql) || !generatedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
//    {
//        return Results.BadRequest(new { error = "Security Block: Only safe SELECT queries are allowed." });
//    }

//    try
//    {
//        // 5. Dynamic SQL Query ko Entity Framework Core ke zariye SQL Server par chalana
//        // Hum database se anonymous objects/raw rows fetch kar rahe hain
//        var rawResult = await db.Products
//            .FromSqlRaw(generatedSql)
//            .AsNoTracking()
//            .ToListAsync();

//        return Results.Ok(new
//        {
//            interpretedSqlQuery = generatedSql,
//            recordsFound = rawResult.Count,
//            data = rawResult
//        });
//    }
//    catch (Exception ex)
//    {
//        // Agar AI ne galat column name use kar liya to SQL execution error catch hogi
//        return Results.Problem(new
//        {
//            error = "SQL Execution Failed. AI generated an invalid query syntax.",
//            attemptedQuery = generatedSql,
//            details = ex.Message
//        }.ToString());
//    }
//})
//.WithName("AskDbRaw")
//.WithTags("Database AI Management");

// =========================================================================
// 🟢 NEW ENDPOINT 4: ADVANCED PROMPTING FRAMEWORK (AI TRIAGE ROUTER)
// =========================================================================
//app.MapPost("/api/triage-router", async (string userRequest, [FromServices] Kernel kernel) =>
//{
//    var chatService = kernel.GetRequiredService<IChatCompletionService>();
//    ChatHistory history = new ChatHistory();

//    // 1. SYSTEM RULES & GUARDRAILS (AI ki boundaries freeze karna)
//    history.AddSystemMessage(
//        "You are an Advanced Enterprise Operations Router. Your job is to analyze incoming user queries " +
//        "and categorize them with absolute structural accuracy. " +
//        "CRITICAL RULE: You must ONLY output a valid minified JSON object. Do not include markdown wraps like ```json or any introductory text."
//    );

//    // 2. FEW-SHOT PROMPTING (AI ko real enterprise output examples dena)
//    // Example 1: Relational Query
//    history.AddUserMessage("Context Pattern Example 1:\nInput: I need to check the inventory status of laptops.");
//    history.AddAssistantMessage("{\"Category\": \"Relational_SQL_DB\", \"Sentiment\": \"Neutral\", \"Urgency\": \"Medium\", \"Reasoning\": \"User is requesting tabular database data regarding structural inventory objects.\"}");

//    // Example 2: Escalation / Angry Customer
//    history.AddUserMessage("Context Pattern Example 2:\nInput: This app keeps timing out when I upload PDFs! Fix this immediately!");
//    history.AddAssistantMessage("{\"Category\": \"Unstructured_File_Issues\", \"Sentiment\": \"Angry\", \"Urgency\": \"Critical\", \"Reasoning\": \"User is expressing frustration regarding operational timeout limits during file execution tasks.\"}");

//    // 3. CHAIN-OF-THOUGHT INSTRUCTION + ACTUAL USER REQUEST
//    // Hum AI ko bol rahe hain ki pehle analyze karo phir rule follow karo
//    string structuredPrompt =
//        $"Instruction: Analyze the input query step-by-step, assign category, sentiment metrics, and enforce JSON output rule.\n" +
//        $"User Input: {userRequest}";

//    history.AddUserMessage(structuredPrompt);

//    // 4. GENERATIVE INFERENCE EXECUTION
//    var response = await chatService.GetChatMessageContentAsync(history);

//    // JSON clean-up code (Agar model galti se markdown formatting lagata hai)
//    string cleanJson = response.Content ?? "{}";
//    if (cleanJson.StartsWith("```json")) cleanJson = cleanJson.Replace("```json", "");
//    if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.LastIndexOf("```"));

//    // Final Structured Content return karna
//    return Results.Content(cleanJson.Trim(), "application/json");
//})
//.WithName("TriageRouter")
//.WithTags("Advanced Prompting Framework");

// =========================================================================
// 🟢 UPGRADED ENDPOINT 5: TEXT-TO-SQL WITH ADVANCED INTERCEPTION
// =========================================================================
//app.MapPost("/api/ask-db-raw", async (string userQuery, HttpContext httpContext, AppDbContext db) =>
//{
//    var kernel = httpContext.RequestServices.GetRequiredService<Kernel>();
//    var chatService = kernel.GetRequiredService<IChatCompletionService>();

//    ChatHistory history = new ChatHistory();

//    // 1. SYSTEM RULES WITH NEGATIVE CONSTRAINTS (AI ko fhaltu baat karne se rokna)
//    history.AddSystemMessage(
//        "You are an expert SQL Server Query Generator for the 'Products' table.\n\n" +
//        "DATABASE SCHEMA:\n" +
//        "Table: Products (Id, Name, Category, Stock, Price)\n\n" +
//        "CRITICAL SECURITY RULES:\n" +
//        "1. ONLY return a valid T-SQL SELECT query. Do NOT include markdown tags like ```sql.\n" +
//        "2. Only answer questions related to the Products schema (Price, Stock, Categories).\n" +
//        "3. 💡 NEW RULE: If the user asks anything out of topic (e.g., weather, politics, general knowledge) or asks for a product name not related to the store, you must strictly return an EMPTY STRING (\"\"). Do not explain why."
//    );

//    // Few-Shot Examples for reinforcement
//    history.AddUserMessage("Who is the Prime Minister of India?");
//    history.AddAssistantMessage(""); // AI ko sikhaya ki out-of-topic par khali chorna hai

//    history.AddUserMessage("Show me shoe prices.");
//    history.AddAssistantMessage("SELECT * FROM Products WHERE Category = 'Footwear';");

//    // User ka sawaal bheja
//    history.AddUserMessage(userQuery);

//    // AI Response fetch kiya
//    var aiResponse = await chatService.GetChatMessageContentAsync(history);
//    string generatedSql = aiResponse.Content?.Trim() ?? string.Empty;

//    // Clean up markdown noise
//    generatedSql = generatedSql.Replace("```sql", "").Replace("```", "").Trim();

//    // =========================================================================
//    // 💡 2. C# INTERCEPTION LAYER (Database ko bekar hit se bachana)
//    // =========================================================================
//    // Agar AI ne empty string di, iska matlab sawaal out-of-topic tha
//    if (string.IsNullOrEmpty(generatedSql))
//    {
//        return Results.Ok(new
//        {
//            status = "Blocked",
//            message = "Aapka sawaal product database se juda nahi hai. Kripya sirf products, inventory, ya prices se jude sawaal poochein."
//        });
//    }

//    // Strict Select Query check
//    if (!generatedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
//    {
//        return Results.BadRequest(new { error = "Security Block: Only safe SELECT operations are authorized." });
//    }

//    try
//    {
//        // Query execution
//        var rawResult = await db.Products
//            .FromSqlRaw(generatedSql)
//            .AsNoTracking()
//            .ToListAsync();

//        return Results.Ok(new
//        {
//            interpretedSqlQuery = generatedSql,
//            recordsFound = rawResult.Count,
//            data = rawResult
//        });
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new
//        {
//            error = "AI ne galat SQL query design ki hai.",
//            attemptedQuery = generatedSql,
//            details = ex.Message
//        });
//    }
//})
//.WithName("AskDbRaw")
//.WithTags("Database AI Management");
// =========================================================================
// 🟢 NEW ENDPOINT 6: MULTI-TABLE JOIN TEXT-TO-SQL
// =========================================================================
app.MapPost("/api/ask-db-join", async (string userQuery, HttpContext httpContext, AppDbContext db) =>
{
    var kernel = httpContext.RequestServices.GetRequiredService<Kernel>();
    var chatService = kernel.GetRequiredService<IChatCompletionService>();

    ChatHistory history = new ChatHistory();

    // 💡 AI KO MULTIPLE TABLES KA SCHEMA AUR RELATION SAMJHANA
    history.AddSystemMessage(
      "You are an expert SQL Server T-SQL code generator for 'Products' and 'Orders' tables.\n\n" +
      "SCHEMA DEFINITIONS:\n" +
      "1. Table: Products -> Id (int), Name (nvarchar), Category (nvarchar), Stock (int), Price (decimal)\n" +
      "2. Table: Orders -> OrderId (int), ProductId (int), QuantityOrdered (int), CustomerName (nvarchar)\n\n" +
      "RELATIONSHIP:\n" +
      " - Orders.ProductId references Products.Id\n\n" +
      "CRITICAL RULES:\n" +
      "1. Only return the raw SQL string starting with SELECT. No markdown wraps.\n" +
      "2. 💡 HINGLISH RULE: Words like 'us', 'use', 'unka', 'un', 'kisne' mean 'which' or 'who'. DO NOT use them as literal text filters in WHERE clauses.\n" +
      "3. If the user asks for a customer name, you must put 'o.CustomerName' in the SELECT clause.\n" +
      "4. Always use appropriate wildcards like LIKE '%Nike%' for product names to avoid exact matching failures."
  );

    // 💡 NEW FEW-SHOT EXAMPLES (AI ko sahi raasta dikhane ke liye)
    history.AddUserMessage("use customer ka name do jisne iPhone order kiya he");
    history.AddAssistantMessage("SELECT o.CustomerName FROM Orders o JOIN Products p ON o.ProductId = p.Id WHERE p.Name LIKE '%iPhone%';");

    history.AddUserMessage("kisne shoes kharide hain uska naam batao");
    history.AddAssistantMessage("SELECT o.CustomerName FROM Orders o JOIN Products p ON o.ProductId = p.Id WHERE p.Category = 'Footwear' OR p.Name LIKE '%Shoes%';");


    history.AddUserMessage(userQuery);

    var aiResponse = await chatService.GetChatMessageContentAsync(history);
    string generatedSql = aiResponse.Content?.Trim() ?? string.Empty;

    // Clean markdown
    generatedSql = generatedSql.Replace("```sql", "").Replace("```", "").Trim();

    if (string.IsNullOrEmpty(generatedSql) || !generatedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Sawaal database se match nahi hua ya security block lag gaya.", queryAttempted = generatedSql });
    }

    try
    {
        // 💡 ADVANCED C# INTERCEPT: Relational data dynamic dictionary list mein nikalna
        // Kyunki Join query ka dynamic shape hota hai, hum raw connection use karenge
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = generatedSql;

        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await db.Database.GetDbConnection().OpenAsync();

        using var reader = await command.ExecuteReaderAsync();
        var resultsList = new List<Dictionary<string, object>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }
            resultsList.Add(row);
        }

        return Results.Ok(new
        {
            sqlQuery = generatedSql,
            totalRows = resultsList.Count,
            data = resultsList
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"SQL Join Execution Failed: {ex.Message}. Query: {generatedSql}");
    }
})
.WithName("AskDbJoin")
.WithTags("Database AI Management");

// =========================================================================
// ENDPOINT 2: CHAT / ASK QUESTION (With Debug Message Return)
// =========================================================================
app.MapPost("/api/ask-policy", async (string userQuery, HttpContext httpContext) =>
{
    var searchResults = memory.SearchAsync(collectionName, userQuery, limit: 1, minRelevanceScore: 0.2);

    string relevantContext = "";
    await foreach (var item in searchResults)
    {
        relevantContext += item.Metadata.Text + "\n";
    }

    // 💡 FIXED: Content-Length: 0 se bachne ke liye proper text message return karein
    if (string.IsNullOrEmpty(relevantContext))
    {
        httpContext.Response.ContentType = "text/plain; charset=utf-8";
        await httpContext.Response.WriteAsync("Mujhe is baare mein database ya local backup file mein koi data nahi mila. Kripya pehle PDF upload karein.");
        return;
    }

    var chatService = kernel.GetRequiredService<IChatCompletionService>();
    ChatHistory history = new ChatHistory();

    history.AddSystemMessage("You are an HR Assistant. Answer the user question strictly using the provided context.");
    history.AddUserMessage($"Context:\n{relevantContext}\n\nQuestion: {userQuery}");

    httpContext.Response.ContentType = "text/plain; charset=utf-8";
    var responseChunks = chatService.GetStreamingChatMessageContentsAsync(history);

    await foreach (var chunk in responseChunks)
    {
        await httpContext.Response.WriteAsync(chunk.Content ?? "");
        await httpContext.Response.Body.FlushAsync();
    }
})
.WithName("AskCompanyPolicy")
.WithTags("HR Management")
.Produces<string>(StatusCodes.Status200OK, "text/plain");

app.Run();