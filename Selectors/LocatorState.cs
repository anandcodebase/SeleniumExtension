namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Target state for <see cref="Locator.WaitFor"/>.
    /// </summary>
    public enum LocatorState
    {
        /// <summary>Wait until the element exists in the DOM.</summary>
        Attached,
        /// <summary>Wait until the element is no longer in the DOM.</summary>
        Detached,
        /// <summary>Wait until the element is visible (displayed).</summary>
        Visible,
        /// <summary>Wait until the element is hidden or removed from DOM.</summary>
        Hidden
    }
}
