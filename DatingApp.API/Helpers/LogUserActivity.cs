using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DatingApp.API.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace DatingApp.API.Helpers
{
    public class LogUserActivity : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var resultsContext = await next();

            var userId = int.Parse(resultsContext.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var repository = resultsContext.HttpContext.RequestServices.GetService<IDatingRepository>();
            var user = await repository.GetUser(userId);
            user.LastActive = DateTime.Now;
            await repository.SaveAll();
        }
    }
}