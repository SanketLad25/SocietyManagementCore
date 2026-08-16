using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;

namespace SocietyManagementCore.Services;

/// Global, read-only — CalculationMethod is code-paired (mm §2.1), no per-society writes exist.
public class CalculationMethodService(SocietyDbContext db)
{
    public async Task<List<CalculationMethodResponse>> ListAsync()
    {
        return await db.CalculationMethods
            .Where(m => m.IsActive)
            .OrderBy(m => m.MethodName)
            .Select(m => new CalculationMethodResponse
            {
                CalculationMethodId = m.CalculationMethodId,
                MethodCode = m.MethodCode,
                MethodName = m.MethodName,
                Description = m.Description,
            })
            .ToListAsync();
    }
}
