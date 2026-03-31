using OpenQA.Selenium;
using System;

namespace SimpleSeleniumSupport.Page
{
    /// <summary>
    /// Playwright-style wrapper around a browser dialog (<c>alert</c>, <c>confirm</c>, <c>prompt</c>).
    /// Obtain via <see cref="SeleniumPage.OnDialog"/> or <see cref="SeleniumPage.WaitForDialog"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// page.OnDialog(dialog =>
    /// {
    ///     Console.WriteLine($"Dialog: {dialog.Type} — {dialog.Message}");
    ///     if (dialog.Type == DialogType.Confirm)
    ///         dialog.Accept();
    ///     else
    ///         dialog.Dismiss();
    /// });
    /// </code>
    /// </example>
    public sealed class SeleniumDialog
    {
        private readonly IAlert _alert;

        /// <summary>The type of dialog.</summary>
        public DialogType Type { get; }

        /// <summary>The message text displayed in the dialog.</summary>
        public string Message { get; }

        internal SeleniumDialog(IAlert alert, DialogType type)
        {
            _alert = alert ?? throw new ArgumentNullException(nameof(alert));
            Type = type;
            Message = SafeGet(() => alert.Text) ?? "";
        }

        /// <summary>
        /// Accepts the dialog (clicks OK). Optionally sends <paramref name="promptText"/>
        /// for <see cref="DialogType.Prompt"/> dialogs.
        /// </summary>
        public void Accept(string? promptText = null)
        {
            if (promptText != null)
                _alert.SendKeys(promptText);
            _alert.Accept();
        }

        /// <summary>Dismisses the dialog (clicks Cancel, or closes an alert).</summary>
        public void Dismiss() => _alert.Dismiss();

        private static T? SafeGet<T>(Func<T> getter)
        {
            try { return getter(); }
            catch { return default; }
        }

        internal static DialogType DetectType(IAlert alert)
        {
            // Selenium doesn't expose dialog type directly; we infer from the text or
            // rely on the caller to specify. Default to Unknown.
            return DialogType.Unknown;
        }
    }
}
