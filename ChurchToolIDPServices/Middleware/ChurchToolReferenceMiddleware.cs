using EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using System.Security.Claims;

namespace EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Middleware;

public class ChurchToolReferenceMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var user = context.GetHttpContext()?.User;
        if (user is not null)
        {
            var stRef = user.FindFirstValue("st_ref");
            if (!string.IsNullOrEmpty(stRef))
            {
                FunctionChurchToolReferenceContext.Set(stRef);
            }

        }
        await next(context);
    }
}
