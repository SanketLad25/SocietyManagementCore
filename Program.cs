using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SocietyManagementCore.Data;
using SocietyManagementCore.Middleware;
using SocietyManagementCore.Options;
using SocietyManagementCore.Services;
using SocietyManagementCore.Services.Calculation;

var builder = WebApplication.CreateBuilder(args);

const string ReactAppCorsPolicy = "ReactApp";

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));

builder.Services.AddDbContext<SocietyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SocietyManagement"),
        sqlOptions => sqlOptions.UseVectorSearch()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ResidentService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<SocietyService>();
builder.Services.AddScoped<FlatService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<MaintenanceCategoryService>();
builder.Services.AddScoped<BillingFrequencyService>();
builder.Services.AddScoped<ParkingTypeService>();
builder.Services.AddScoped<ParkingService>();
builder.Services.AddScoped<AmenityService>();
builder.Services.AddScoped<FlatGroupService>();
builder.Services.AddScoped<ChargeTargetTypeService>();
builder.Services.AddScoped<ICalculationStrategy, FixedAmountStrategy>();
builder.Services.AddScoped<ICalculationStrategy, PerSqFtStrategy>();
builder.Services.AddScoped<ICalculationStrategy, PerUnitStrategy>();
builder.Services.AddScoped<ICalculationStrategy, SlabTieredStrategy>();
builder.Services.AddScoped<ICalculationStrategy, PercentageOfCategoryStrategy>();
builder.Services.AddScoped<ICalculationStrategy, FormulaStrategy>();
builder.Services.AddScoped<CalculationStrategyFactory>();
builder.Services.AddScoped<MaintenanceTargetResolver>();
builder.Services.AddScoped<MaintenanceChargeRuleService>();
builder.Services.AddScoped<MaintenanceExemptionService>();
builder.Services.AddScoped<AmenitySubscriptionService>();
builder.Services.AddScoped<CalculationMethodService>();
builder.Services.AddScoped<MaintenanceBillingService>();
builder.Services.AddScoped<NoticeCategoryService>();
builder.Services.AddScoped<NoticeAttachmentService>();
builder.Services.AddScoped<NoticeService>();
builder.Services.AddScoped<INoticeBroadcastService, NoOpNoticeBroadcastService>();
builder.Services.AddScoped<ComplaintCategoryService>();
builder.Services.AddScoped<ComplaintService>();
builder.Services.AddScoped<ComplaintAttachmentService>();
builder.Services.AddScoped<ComplaintNotificationService>();
builder.Services.AddScoped<EventCategoryService>();
builder.Services.AddScoped<EventNotificationService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<EventRegistrationService>();
builder.Services.AddScoped<KnowledgeArticleService>();
builder.Services.AddScoped<VisitorCategoryService>();
builder.Services.AddScoped<VisitorNotificationService>();
builder.Services.AddScoped<VisitorLogService>();
builder.Services.AddScoped<VisitorAttachmentService>();

// Embeddings provider abstraction (SocChatBot.md "Embedding provider abstraction"): IngestionService/
// IRetrievalService only ever depend on IEmbeddingService, never on VoyageEmbeddingService directly.
// This app's first outbound HttpClient (alongside the Phase-3 ClaudeClient) — registered as a typed
// client, not a bare AddScoped<VoyageEmbeddingService>(), to avoid socket exhaustion under load.
builder.Services.AddHttpClient<VoyageEmbeddingService>();
builder.Services.AddScoped<IEmbeddingService>(sp =>
{
    var embeddingOptions = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
    return embeddingOptions.Provider switch
    {
        "Voyage" => sp.GetRequiredService<VoyageEmbeddingService>(),
        // "AzureOpenAI" => sp.GetRequiredService<AzureOpenAIEmbeddingService>(), — added later,
        // same registration shape, nothing else in this block changes.
        _ => throw new InvalidOperationException($"Unknown embedding provider '{embeddingOptions.Provider}'."),
    };
});
builder.Services.AddScoped<IngestionService>();
builder.Services.AddScoped<IRetrievalService, RetrievalService>();
builder.Services.AddScoped<EmbeddingReindexService>();

// SocChatBot.md Phase 3: ClaudeClient wraps the Anthropic Messages API and makes an outbound call
// on every chat turn — a typed client (not a bare AddScoped<ClaudeClient>()) for the same
// socket-exhaustion reason VoyageEmbeddingService above is registered this way.
builder.Services.AddHttpClient<ClaudeClient>();

// LLM provider abstraction (SocChatBot.md "LLM provider abstraction", added mid-Phase-3 after the
// configured Anthropic account turned out to have no API credit): ChatToolLoopRunner/ChatToolCatalog
// only ever depend on IChatCompletionService, never on ClaudeClient directly — same provider-switch
// shape as the embeddings registration above. Default is "Stub" (appsettings.Development.json) so
// development proceeds at no cost; flipping Llm:Provider to "Anthropic" once Anthropic:ApiKey has
// credit requires no code change.
builder.Services.AddScoped<StubChatCompletionService>();
builder.Services.AddScoped<IChatCompletionService>(sp =>
{
    var llmOptions = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
    return llmOptions.Provider switch
    {
        "Stub" => sp.GetRequiredService<StubChatCompletionService>(),
        "Anthropic" => sp.GetRequiredService<ClaudeClient>(),
        _ => throw new InvalidOperationException($"Unknown LLM provider '{llmOptions.Provider}'."),
    };
});
builder.Services.AddScoped<ChatToolLoopRunner>();
builder.Services.AddScoped<ChatToolCatalog>();
builder.Services.AddScoped<ChatOrchestrationService>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(ReactAppCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SocietyDbContext>();
    await DbSeeder.SeedRolesAsync(db);
    await DbSeeder.SeedSuperAdminAsync(db);
    await MaintenanceReferenceSeeder.SeedGlobalReferenceDataAsync(db);
    await MaintenanceReferenceSeeder.BackfillExistingSocietiesAsync(db);
    await NoticeReferenceSeeder.BackfillExistingSocietiesAsync(db);
    await ComplaintReferenceSeeder.BackfillExistingSocietiesAsync(db);
    await EventReferenceSeeder.BackfillExistingSocietiesAsync(db);
    await VisitorReferenceSeeder.BackfillExistingSocietiesAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();

app.UseCors(ReactAppCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
