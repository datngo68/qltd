using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace QuanLyAnTrua.Helpers
{
    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Check if action or controller has AllowAnonymous attribute
            bool allowAnonymous = false;
            
            if (context.ActionDescriptor is ControllerActionDescriptor actionDescriptor)
            {
                allowAnonymous = actionDescriptor.MethodInfo
                    .GetCustomAttributes(typeof(AllowAnonymousAttribute), true)
                    .Any() ||
                    actionDescriptor.ControllerTypeInfo
                    .GetCustomAttributes(typeof(AllowAnonymousAttribute), true)
                    .Any();
            }

            if (allowAnonymous)
            {
                return; // Allow access
            }

            var userId = context.HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }
}

