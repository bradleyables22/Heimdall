using Microsoft.AspNetCore.Html;

namespace Heimdall.Example.Raw.Rendering.Shared
{
    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public static class ToastManager
    {



        public static IHtmlContent CreateToast(string header,string content)
        {
            return new HtmlString(
                @$"<div class=""toast fade show text-bg-success""
                      role=""alert""
                      aria-live=""assertive""
                      aria-atomic=""true""
                      data-bs-autohide=""true""
                      data-bs-delay=""3000"">
                      <div class=""toast-header text-bg-success border-0"">
                        <strong class=""me-auto"">{header}</strong>
                        <button type=""button""
                                class=""btn-close ms-2 mb-1""
                                data-bs-dismiss=""toast""
                                aria-label=""Close""></button>
                      </div>
                      <div class=""toast-body"">
                        {content}
                      </div>
                 </div>"
            );
        }



    }
}
