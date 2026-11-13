using SocietyManagementCore.Services;
using SocietyManagementCore.Services.Calculation;
using System.Runtime.CompilerServices;

namespace SocietyManagementCore.Extensions
{
    public static class IServiceCollectionExtension
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            services.AddScoped<ICurrentUserContext, CurrentUserContext>();
            services.AddScoped<JwtTokenService>();
            services.AddScoped<AuthService>();
            services.AddScoped<ResidentService>();
            services.AddScoped<AdminUserService>();
            services.AddScoped<SocietyService>();
            services.AddScoped<FlatService>();
            services.AddScoped<AuditLogService>();
            services.AddScoped<MaintenanceCategoryService>();
            services.AddScoped<BillingFrequencyService>();
            services.AddScoped<ParkingTypeService>();
            services.AddScoped<ParkingService>();
            services.AddScoped<AmenityService>();
            services.AddScoped<FlatGroupService>();
            services.AddScoped<ChargeTargetTypeService>();
            services.AddScoped<ICalculationStrategy, FixedAmountStrategy>();
            services.AddScoped<ICalculationStrategy, PerSqFtStrategy>();
            services.AddScoped<ICalculationStrategy, PerUnitStrategy>();
            services.AddScoped<ICalculationStrategy, SlabTieredStrategy>();
            services.AddScoped<ICalculationStrategy, PercentageOfCategoryStrategy>();
            services.AddScoped<ICalculationStrategy, FormulaStrategy>();
            services.AddScoped<CalculationStrategyFactory>();
            services.AddScoped<MaintenanceTargetResolver>();
            services.AddScoped<MaintenanceChargeRuleService>();
            services.AddScoped<MaintenanceExemptionService>();
            services.AddScoped<AmenitySubscriptionService>();
            services.AddScoped<CalculationMethodService>();
            services.AddScoped<MaintenanceBillingService>();
            services.AddScoped<PaymentService>();
            services.AddScoped<NoticeCategoryService>();
            services.AddScoped<NoticeAttachmentService>();
            services.AddScoped<NoticeService>();
            services.AddScoped<INoticeBroadcastService, NoOpNoticeBroadcastService>();
            services.AddScoped<ComplaintCategoryService>();
            services.AddScoped<ComplaintService>();
            services.AddScoped<ComplaintAttachmentService>();
            services.AddScoped<ComplaintNotificationService>();
            services.AddScoped<EventCategoryService>();
            services.AddScoped<EventNotificationService>();
            services.AddScoped<EventService>();
            services.AddScoped<EventRegistrationService>();
            services.AddScoped<KnowledgeArticleService>();
            services.AddScoped<VisitorCategoryService>();
            services.AddScoped<VisitorNotificationService>();
            services.AddScoped<VisitorLogService>();
            services.AddScoped<VisitorAttachmentService>();
            return services;

        }
    }
}
