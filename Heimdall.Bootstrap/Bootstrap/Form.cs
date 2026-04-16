namespace Heimdall.Bootstrap
{
    public static partial class Bootstrap
    {
        /// <summary>
        /// Provides Bootstrap form-related CSS class names for building and styling form controls and layouts.
        /// </summary>
        /// <remarks>This static class centralizes commonly used Bootstrap form, input group, validation, and layout
        /// class names. It is intended to simplify the process of applying consistent styling to form elements such as
        /// inputs, labels, selects, checks, and validation states across UI components.</remarks>
        public static class Form
        {
            public const string Label = "form-label";
            public const string Text = "form-text";
            public const string Control = "form-control";
            public const string ControlSm = "form-control form-control-sm";
            public const string ControlLg = "form-control form-control-lg";
            public const string ControlPlaintext = "form-control-plaintext";
            public const string Select = "form-select";
            public const string SelectSm = "form-select form-select-sm";
            public const string SelectLg = "form-select form-select-lg";
            public const string Check = "form-check";
            public const string CheckInput = "form-check-input";
            public const string CheckLabel = "form-check-label";
            public const string Switch = "form-check form-switch";
            public const string Range = "form-range";
            public const string Floating = "form-floating";

            public const string InputGroup = "input-group";
            public const string InputGroupText = "input-group-text";
            public const string InputGroupSm = "input-group input-group-sm";
            public const string InputGroupLg = "input-group input-group-lg";

            public const string ValidFeedback = "valid-feedback";
            public const string InvalidFeedback = "invalid-feedback";
            public const string ValidTooltip = "valid-tooltip";
            public const string InvalidTooltip = "invalid-tooltip";

            public const string WasValidated = "was-validated";
            public const string IsValid = "is-valid";
            public const string IsInvalid = "is-invalid";
        }
    }
}