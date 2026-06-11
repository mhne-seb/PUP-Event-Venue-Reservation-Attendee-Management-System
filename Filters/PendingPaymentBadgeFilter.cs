using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PUPEventVenue.Services;

namespace PUPEventVenue.Filters
{
    /// <summary>
    /// Injects ViewBag.PendingPaymentCount for every Admin page
    /// so the nav badge stays in sync.
    /// </summary>
    public class PendingPaymentBadgeFilter : IAsyncActionFilter
    {
        private readonly IPaymentService _paymentService;
        public PendingPaymentBadgeFilter(IPaymentService paymentService)
            => _paymentService = paymentService;

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var resultCtx = await next();

            // Inject into the ViewBag of any ViewResult for Admin users
            if (resultCtx.Result is ViewResult viewResult
                && ctx.HttpContext.User.IsInRole("Admin"))
            {
                viewResult.ViewData["PendingPaymentCount"] =
                    await _paymentService.GetPendingPaymentCountAsync();
            }
        }
    }
}
