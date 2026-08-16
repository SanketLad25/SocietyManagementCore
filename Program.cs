using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocietyManagementCore.Data;
using SocietyManagementCore.Options;
using SocietyManagementCore.Services;
using SocietyManagementCore.Services.Calculation;

var builder = WebApplication.CreateBuilder(args);

const string ReactAppCorsPolicy = "ReactApp";

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddDbContext<SocietyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SocietyManagement")));

builder.Services.AddHttpContextAccessor();
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
builder.Services.AddScoped<EventService>();

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
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(ReactAppCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
