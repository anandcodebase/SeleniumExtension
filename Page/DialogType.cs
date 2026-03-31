namespace SimpleSeleniumSupport.Page
{
    /// <summary>
    /// Type of browser dialog intercepted by <see cref="SeleniumDialog"/>.
    /// </summary>
    public enum DialogType
    {
        /// <summary>JavaScript <c>alert()</c> dialog — only OK button.</summary>
        Alert,

        /// <summary>JavaScript <c>confirm()</c> dialog — OK and Cancel buttons.</summary>
        Confirm,

        /// <summary>JavaScript <c>prompt()</c> dialog — text input + OK/Cancel.</summary>
        Prompt,

        /// <summary>Browser <c>beforeunload</c> dialog.</summary>
        BeforeUnload,

        /// <summary>Unknown or undetected dialog type.</summary>
        Unknown
    }
}
