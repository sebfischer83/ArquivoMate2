using ArquivoMate2.Shared.ApiModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using System.Reflection;

namespace ArquivoMate2.API.Filters
{
    public class ApiResponseWrapperFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Short-circuit: if model state is invalid, return RFC7807 ValidationProblemDetails
            if (!context.ModelState.IsValid)
            {
                var validationProblem = new ValidationProblemDetails(context.ModelState)
                {
                    Title = "One or more validation errors occurred.",
                    Status = (int)HttpStatusCode.BadRequest,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
                };

                context.Result = new BadRequestObjectResult(validationProblem);
                return;
            }

            // Execute the action
            var executedContext = await next();

            // If action set a result, normalize it
            var result = executedContext.Result;
            if (result == null)
                return;

            // If it's already an ApiResponse or ProblemDetails, do nothing
            if (result is ObjectResult objResult)
            {
                if (objResult.Value is ApiResponse || objResult.Value is ProblemDetails)
                    return;

                // Successful 2xx results with a body -> wrap
                var status = objResult.StatusCode ?? (int)HttpStatusCode.OK;
                if (status >= 200 && status < 300)
                {
                    var wrappedType = typeof(ApiResponse<>).MakeGenericType(objResult.Value?.GetType() ?? typeof(object));
                    var wrapper = Activator.CreateInstance(wrappedType);
                    var dataProp = wrapper!.GetType().GetProperty("Data");
                    dataProp!.SetValue(wrapper, objResult.Value);
                    var successProp = wrapper.GetType().GetProperty("Success");
                    successProp!.SetValue(wrapper, true);

                    executedContext.Result = new ObjectResult(wrapper)
                    {
                        StatusCode = status,
                        DeclaredType = wrapper.GetType()
                    };
                }
                else
                {
                    // Non-success ObjectResult -> convert value to ProblemDetails
                    if (!(objResult.Value is ProblemDetails))
                    {
                        var message = ExtractMessage(objResult.Value);
                        var pd = new ProblemDetails
                        {
                            Title = message ?? (objResult.Value?.ToString() ?? "An error occurred"),
                            Status = status
                        };
                        executedContext.Result = new ObjectResult(pd) { StatusCode = status };
                    }
                }
            }
            else if (result is EmptyResult)
            {
                var wrapper = new ApiResponse<object?>(null, true);
                executedContext.Result = new ObjectResult(wrapper) { StatusCode = (int)HttpStatusCode.OK };
            }
            else if (result is StatusCodeResult statusCodeResult)
            {
                var status = statusCodeResult.StatusCode;
                if (status >= 200 && status < 300)
                {
                    var wrapper = new ApiResponse<object?>(null, true);
                    executedContext.Result = new ObjectResult(wrapper) { StatusCode = status };
                }
                else
                {
                    var pd = new ProblemDetails { Title = "An error occurred", Status = status };
                    executedContext.Result = new ObjectResult(pd) { StatusCode = status };
                }
            }
            // other IActionResult implementations are left untouched
        }

        private static string? ExtractMessage(object? value)
        {
            if (value == null) return null;

            // If it's a ProblemDetails already, use its Title
            if (value is ProblemDetails pd) return pd.Title;

            // If it's a string, return it
            if (value is string s) return s;

            // If it's IDictionary-like, try keys "message" or "Message"
            if (value is System.Collections.IDictionary dict)
            {
                if (dict.Contains("message") && dict["message"] is string ms) return ms;
                if (dict.Contains("Message") && dict["Message"] is string Ms) return Ms;
            }

            // Try to find a property named 'message' or 'Message'
            var t = value.GetType();
            var prop = t.GetProperty("message", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                       ?? t.GetProperty("Message", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var v = prop.GetValue(value);
                if (v is string vs) return vs;
            }

            return null;
        }
    }
}
