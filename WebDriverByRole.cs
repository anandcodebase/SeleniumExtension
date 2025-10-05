using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// WebDriver ByRole
    /// </summary>
    public static class WebDriverByRole
    {
        #region Types & Config
        /// <summary>
        /// 
        /// </summary>
        public enum WaitUntil { None, Exists, Visible, Enabled, Clickable }

        /// <summary>
        /// 
        /// </summary>
        public class LocatorOptions
        {
            /// <summary>
            /// Gets or sets the timeout seconds.
            /// </summary>
            /// <value>
            /// The timeout seconds.
            /// </value>
            public int TimeoutSeconds { get; set; } = 10;
            /// <summary>
            /// Gets or sets a value indicating whether [exact match].
            /// </summary>
            /// <value>
            ///   <c>true</c> if [exact match]; otherwise, <c>false</c>.
            /// </value>
            public bool ExactMatch { get; set; } = false;
            /// <summary>
            /// Gets or sets the wait.
            /// </summary>
            /// <value>
            /// The wait.
            /// </value>
            public WaitUntil Wait { get; set; } = WaitUntil.Visible;
            /// <summary>
            /// Gets or sets a value indicating whether [case sensitive].
            /// </summary>
            /// <value>
            ///   <c>true</c> if [case sensitive]; otherwise, <c>false</c>.
            /// </value>
            public bool CaseSensitive { get; set; } = false;
            /// <summary>
            /// Gets or sets the polling ms.
            /// </summary>
            /// <value>
            /// The polling ms.
            /// </value>
            public int PollingMs { get; set; } = 200;
        }

        /// <summary>
        /// Defaults the options.
        /// </summary>
        /// <returns></returns>
        private static LocatorOptions DefaultOptions() => new LocatorOptions();
        #endregion

        #region Public API (same as before - GetBy/TryGet variants preserved)
        // (For brevity, only a subset of methods are shown here --- the full file below includes all methods
        //  from your previous version: GetByRole/TryGetByRole/TryGetByRoleTuple/getAll/tryAll, GetByText/TryGetByText etc.)
        /// <summary>
        /// Gets the by role.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="role">The role.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.NoSuchElementException">No element found with role='{role}' name='{accessibleName}'</exception>
        public static IWebElement GetByRole(this IWebDriver driver, string role, string accessibleName = null, LocatorOptions options = null)
        {
            var el = driver.TryGetByRole(role, accessibleName, options);
            if (el == null) throw new NoSuchElementException($"No element found with role='{role}' name='{accessibleName}'");
            return el;
        }

        /// <summary>
        /// Tries the get by role.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="role">The role.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IWebElement TryGetByRole(this IWebDriver driver, string role, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByRoleAndName(driver, role, accessibleName, options);
            return list.FirstOrDefault();
        }

        /// <summary>
        /// Tries the get by role.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="role">The role.</param>
        /// <param name="element">The element.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static bool TryGetByRole(this IWebDriver driver, string role, out IWebElement element, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByRoleAndName(driver, role, accessibleName, options);
            element = list.FirstOrDefault();
            return element != null;
        }

        /// <summary>
        /// Tries the get by role tuple.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="role">The role.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static (bool found, IWebElement element) TryGetByRoleTuple(this IWebDriver driver, string role, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByRoleAndName(driver, role, accessibleName, options);
            var el = list.FirstOrDefault();
            return (el != null, el);
        }

        /// <summary>
        /// Gets all by role.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="role">The role.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.NoSuchElementException">No elements found with role='{role}' name='{accessibleName}'</exception>
        public static IReadOnlyCollection<IWebElement> GetAllByRole(this IWebDriver driver, string role, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByRoleAndName(driver, role, accessibleName, options);
            if (list == null || list.Count == 0) throw new NoSuchElementException($"No elements found with role='{role}' name='{accessibleName}'");
            return list;
        }

        /// <summary>
        /// Tries the get all by role.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="role">The role.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IReadOnlyCollection<IWebElement> TryGetAllByRole(this IWebDriver driver, string role, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            return FindElementsByRoleAndName(driver, role, accessibleName, options);
        }

        // TEXT
        /// <summary>
        /// Gets the by text.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="text">The text.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.NoSuchElementException">No element found with text='{text}'</exception>
        public static IWebElement GetByText(this IWebDriver driver, string text, LocatorOptions options = null)
        {
            var el = driver.TryGetByText(text, options);
            if (el == null) throw new NoSuchElementException($"No element found with text='{text}'");
            return el;
        }

        /// <summary>
        /// Tries the get by text.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="text">The text.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IWebElement TryGetByText(this IWebDriver driver, string text, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByText(driver, text, options);
            return list.FirstOrDefault();
        }

        /// <summary>
        /// Tries the get by text.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="text">The text.</param>
        /// <param name="element">The element.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static bool TryGetByText(this IWebDriver driver, string text, out IWebElement element, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByText(driver, text, options);
            element = list.FirstOrDefault();
            return element != null;
        }

        /// <summary>
        /// Tries the get by text tuple.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="text">The text.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static (bool found, IWebElement element) TryGetByTextTuple(this IWebDriver driver, string text, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByText(driver, text, options);
            var el = list.FirstOrDefault();
            return (el != null, el);
        }

        /// <summary>
        /// Gets all by text.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="text">The text.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.NoSuchElementException">No elements found with text='{text}'</exception>
        public static IReadOnlyCollection<IWebElement> GetAllByText(this IWebDriver driver, string text, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var list = FindElementsByText(driver, text, options);
            if (list == null || list.Count == 0) throw new NoSuchElementException($"No elements found with text='{text}'");
            return list;
        }

        /// <summary>
        /// Tries the get all by text.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="text">The text.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IReadOnlyCollection<IWebElement> TryGetAllByText(this IWebDriver driver, string text, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            return FindElementsByText(driver, text, options);
        }

        // TestId example (all other helpers from your previous file remain unchanged)
        /// <summary>
        /// Gets the by test identifier.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="testId">The test identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.NoSuchElementException">No element found with testId='{testId}'</exception>
        public static IWebElement GetByTestId(this IWebDriver driver, string testId, LocatorOptions options = null)
        {
            var el = driver.TryGetByTestId(testId, options);
            if (el == null) throw new NoSuchElementException($"No element found with testId='{testId}'");
            return el;
        }

        /// <summary>
        /// Tries the get by test identifier.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="testId">The test identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IWebElement TryGetByTestId(this IWebDriver driver, string testId, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            var css = $"[data-testid='{EscapeCss(testId)}'],[data-test-id='{EscapeCss(testId)}'],[data-test='{EscapeCss(testId)}']";
            return FindElementWithWait(driver, By.CssSelector(css), options, throwOnTimeout: false);
        }

        /// <summary>
        /// Gets all by test identifier.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="testId">The test identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IReadOnlyCollection<IWebElement> GetAllByTestId(this IWebDriver driver, string testId, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            return FindElementsWithWait(driver, By.CssSelector($"[data-testid='{EscapeCss(testId)}'],[data-test-id='{EscapeCss(testId)}'],[data-test='{EscapeCss(testId)}']"), options);
        }

        /// <summary>
        /// Tries the get all by test identifier.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="testId">The test identifier.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        public static IReadOnlyCollection<IWebElement> TryGetAllByTestId(this IWebDriver driver, string testId, LocatorOptions options = null)
        {
            options ??= DefaultOptions();
            return FindElementsWithWait(driver, By.CssSelector($"[data-testid='{EscapeCss(testId)}'],[data-test-id='{EscapeCss(testId)}'],[data-test='{EscapeCss(testId)}']"), options, throwOnTimeout: false);
        }

        #endregion

        #region Core finders & fallbacks

        /// <summary>
        /// Finds the name of the elements by role and.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="role">The role.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static IReadOnlyCollection<IWebElement> FindElementsByRoleAndName(IWebDriver driver, string role, string accessibleName, LocatorOptions options)
        {
            options ??= DefaultOptions();

            if (driver is IJavaScriptExecutor js)
            {
                var found = ExecuteAccessibleRoleNameSearch(js, role, accessibleName ?? "", options);
                if (found != null && found.Count > 0) return found;
            }

            // Fallback heuristic
            var xpath = BuildRoleHeuristicXPath(role, options);
            return FindElementsWithWait(driver, By.XPath(xpath), options, throwOnTimeout: false);
        }

        /// <summary>
        /// Finds the elements by text.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="text">The text.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static IReadOnlyCollection<IWebElement> FindElementsByText(IWebDriver driver, string text, LocatorOptions options)
        {
            options ??= DefaultOptions();

            if (driver is IJavaScriptExecutor js)
            {
                var found = ExecuteAccessibleNameSearch(js, text, options);
                if (found != null && found.Count > 0) return found;
            }

            var xpath = options.ExactMatch
                ? $"//*[normalize-space(string(.)) = {QuoteJsString(text)}]"
                : $"//*[contains(translate(normalize-space(string(.)), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), {QuoteJsString(text.ToLowerInvariant())})]";

            return FindElementsWithWait(driver, By.XPath(xpath), options, throwOnTimeout: false);
        }

        /// <summary>
        /// Finds the name of the elements by accessible.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="accessibleName">Name of the accessible.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static IReadOnlyCollection<IWebElement> FindElementsByAccessibleName(IWebDriver driver, string accessibleName, LocatorOptions options)
        {
            options ??= DefaultOptions();

            if (driver is IJavaScriptExecutor js)
            {
                var found = ExecuteAccessibleNameSearch(js, accessibleName, options);
                if (found != null && found.Count > 0) return found;
            }

            var preds = new List<string>
            {
                BuildAttrContainsXPath("aria-label", accessibleName, options),
                BuildAttrContainsXPath("title", accessibleName, options),
                BuildAttrContainsXPath("alt", accessibleName, options),
                BuildAttrContainsXPath("placeholder", accessibleName, options),
                $"//*[contains(translate(normalize-space(string(.)), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), {QuoteJsString(accessibleName.ToLowerInvariant())})]"
            };

            var union = string.Join(" | ", preds);
            return FindElementsWithWait(driver, By.XPath(union), options, throwOnTimeout: false);
        }

        #endregion

        #region JS helper injection & invocation (injected once per page/session)

        // Ensures the ANC + shadow traversal helper is injected into the page under window.__ssa_helper
        // idempotent: only injects when missing.
        /// <summary>
        /// Ensures the accessibility helper injected.
        /// </summary>
        /// <param name="js">The js.</param>
        private static void EnsureAccessibilityHelperInjected(IJavaScriptExecutor js)
        {
            try
            {
                // quick check if helper exists
                var exists = js.ExecuteScript("return (typeof window.__ssa_helper !== 'undefined' && typeof window.__ssa_helper.findByRole === 'function');");
                if (exists is bool b && b) return; // already injected
            }
            catch
            {
                // ignore and attempt injection
            }

            // injection script: defines window.__ssa_helper with two functions: findByRole and findByName
            var inject = @"
(function(){
  if(window.__ssa_helper && typeof window.__ssa_helper.findByRole === 'function') return;

  function normalize(s){ return s ? String(s).replace(/\s+/g,' ').trim() : ''; }
  function collectAll(root){
    var arr = [];
    function walker(r){
      try{
        var children = r.children || r.childNodes || [];
        for(var i=0;i<children.length;i++){
          var el = children[i];
          if(!el) continue;
          if(el.nodeType === 1){
            arr.push(el);
            walker(el);
            try{ if(el.shadowRoot) walker(el.shadowRoot); }catch(e){}
          } else {
            if(el.childNodes && el.childNodes.length) walker(el);
          }
        }
      }catch(e){}
    }
    walker(root);
    return arr;
  }

  function computeImplicitRole(el){
    try{
      if(el.getAttribute && el.getAttribute('role')) return el.getAttribute('role');
      var tag = el.tagName ? el.tagName.toLowerCase() : '';
      var type = (el.getAttribute && el.getAttribute('type')) || '';
      if(tag === 'button') return 'button';
      if(tag === 'a' && el.hasAttribute('href')) return 'link';
      if(tag === 'input'){
        var t = type.toLowerCase();
        if(t === 'button' || t === 'submit' || t === 'reset') return 'button';
        if(t === 'checkbox') return 'checkbox';
        if(t === 'radio') return 'radio';
        if(t === 'search' || t === 'text' || t === 'email' || t === 'tel' || t === 'url' || t === 'password') return 'textbox';
      }
      if(tag === 'textarea') return 'textbox';
      if(tag === 'select') return 'combobox';
      if(tag === 'img') return 'img';
      if(/^h[1-6]$/.test(tag)) return 'heading';
      if(tag === 'ul' || tag === 'ol' || (el.getAttribute && el.getAttribute('role') === 'list')) return 'list';
      if(tag === 'li' || (el.getAttribute && el.getAttribute('role') === 'listitem')) return 'listitem';
      if(el.getAttribute && el.getAttribute('contenteditable') === 'true') return 'textbox';
    }catch(e){}
    return null;
  }

  function computeAccessibleName(el){
    try{
      if(el.getAttribute && el.getAttribute('aria-label')) return normalize(el.getAttribute('aria-label'));
      if(el.getAttribute && el.getAttribute('aria-labelledby')){
        var ids = normalize(el.getAttribute('aria-labelledby')).split(/\s+/);
        var parts = [];
        for(var i=0;i<ids.length;i++){ var id = ids[i]; var ref = document.getElementById(id); if(ref) parts.push(normalize(ref.innerText || ref.textContent || '')); }
        var joined = parts.join(' ').trim();
        if(joined) return joined;
      }
      if(el.getAttribute && el.getAttribute('alt')) return normalize(el.getAttribute('alt'));
      if(el.getAttribute && el.getAttribute('title')) return normalize(el.getAttribute('title'));
      if(el.getAttribute && el.getAttribute('placeholder')) return normalize(el.getAttribute('placeholder'));
      if(el.tagName && el.tagName.toLowerCase() === 'input' && el.value) return normalize(el.value);
      try{
        var id = el.id;
        if(id){
          var labs = document.querySelectorAll('label[for=""' + id + '""]');
          if (labs && labs.length)
            {
                var texts = [];
                for (var i = 0; i < labs.length; i++) texts.push(normalize(labs[i].innerText || labs[i].textContent || ''));
                var t = texts.join(' ').trim();
                if (t) return t;
            }
        }
        var anc = el.closest && el.closest('label');
        if(anc) { var at = normalize(anc.innerText || anc.textContent || ''); if(at) return at; }
}catch(e){ }
var txt = normalize(el.innerText || el.textContent || '');
return txt;
    }catch(e){ return ''; }
  }

  window.__ssa_helper = {
findByRole: function(role, name, exact, caseSensitive){
        try
        {
            var all = collectAll(document.documentElement || document);
            var results = [];
            for (var i = 0; i < all.length; i++)
            {
                try
                {
                    var el = all[i];
                    var implicit = computeImplicitRole(el) || '';
                    if (!implicit) continue;
            var roleMatch = exact ? (caseSensitive ? implicit === role : implicit.toLowerCase() === role.toLowerCase()) : (caseSensitive ? implicit.indexOf(role) !== -1 : implicit.toLowerCase().indexOf(role.toLowerCase()) !== -1);
            if (!roleMatch) continue;
            if (!name || name === '') { results.push(el); continue; }
            var an = computeAccessibleName(el) || '';
            var nameMatch = exact ? (caseSensitive ? an === name : an.toLowerCase() === name.toLowerCase()) : (caseSensitive ? an.indexOf(name) !== -1 : an.toLowerCase().indexOf(name.toLowerCase()) !== -1);
            if (nameMatch) results.push(el);
        }
        catch (e) { }
    }
    return results;
}catch(e){ return []; }
    },
    findByName: function(name, exact, caseSensitive){
    try
    {
        var all = collectAll(document.documentElement || document);
        var results = [];
        for (var i = 0; i < all.length; i++)
        {
            try
            {
                var el = all[i];
                var an = computeAccessibleName(el) || '';
                if (!name || name === '') { if (an) results.push(el); continue; }
                var match = exact ? (caseSensitive ? an === name : an.toLowerCase() === name.toLowerCase()) : (caseSensitive ? an.indexOf(name) !== -1 : an.toLowerCase().indexOf(name.toLowerCase()) !== -1);
                if (match) results.push(el);
            }
            catch (e) { }
        }
        return results;
    }
    catch (e) { return []; }
}
  };
})(); ";

            try
            {
                js.ExecuteScript(inject);
            }
            catch
            {
                // swallow injection errors: fallbacks will work
            }
        }

        /// <summary>
        /// Executes the accessible role name search.
        /// </summary>
        /// <param name="js">The js.</param>
        /// <param name="role">The role.</param>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static IReadOnlyCollection<IWebElement> ExecuteAccessibleRoleNameSearch(IJavaScriptExecutor js, string role, string name, LocatorOptions options)
        {
            try
            {
                EnsureAccessibilityHelperInjected(js);
                var raw = js.ExecuteScript("return (window.__ssa_helper && window.__ssa_helper.findByRole) ? window.__ssa_helper.findByRole(arguments[0], arguments[1], arguments[2], arguments[3]) : [];", role, name, options.ExactMatch, options.CaseSensitive);
                return ScriptResultToElementList(raw);
            }
            catch
            {
                return Array.Empty<IWebElement>();
            }
        }

        /// <summary>
        /// Executes the accessible name search.
        /// </summary>
        /// <param name="js">The js.</param>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static IReadOnlyCollection<IWebElement> ExecuteAccessibleNameSearch(IJavaScriptExecutor js, string name, LocatorOptions options)
        {
            try
            {
                EnsureAccessibilityHelperInjected(js);
                var raw = js.ExecuteScript("return (window.__ssa_helper && window.__ssa_helper.findByName) ? window.__ssa_helper.findByName(arguments[0], arguments[1], arguments[2]) : [];", name, options.ExactMatch, options.CaseSensitive);
                return ScriptResultToElementList(raw);
            }
            catch
            {
                return Array.Empty<IWebElement>();
            }
        }

        /// <summary>
        /// Scripts the result to element list.
        /// </summary>
        /// <param name="raw">The raw.</param>
        /// <returns></returns>
        private static IReadOnlyCollection<IWebElement> ScriptResultToElementList(object raw)
        {
            if (raw == null) return Array.Empty<IWebElement>();
            if (raw is IWebElement single) return new List<IWebElement> { single }.AsReadOnly();
            if (raw is System.Collections.IEnumerable en)
            {
                var list = new List<IWebElement>();
                foreach (var o in en) if (o is IWebElement we) list.Add(we);
                return list.AsReadOnly();
            }
            return Array.Empty<IWebElement>();
        }

        #endregion

        #region Fallback XPath utilities & Wait helpers (unchanged from prior file)
        /// <summary>
        /// Builds the attribute contains x path.
        /// </summary>
        /// <param name="attr">The attribute.</param>
        /// <param name="value">The value.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static string BuildAttrContainsXPath(string attr, string value, LocatorOptions options)
        {
            if (string.IsNullOrEmpty(value)) return $"//*[@{attr}]";
            if (options.ExactMatch) return $"//*[@{attr} = {QuoteJsString(value)}]";
            if (options.CaseSensitive) return $"//*[contains(@{attr}, {QuoteJsString(value)})]";
            return $"//*[contains(translate(@{attr}, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), {QuoteJsString(value.ToLowerInvariant())})]";
        }

        /// <summary>
        /// Builds the role heuristic x path.
        /// </summary>
        /// <param name="role">The role.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static string BuildRoleHeuristicXPath(string role, LocatorOptions options)
        {
            var preds = new List<string>();
            if (options.ExactMatch) preds.Add($"//*[@role = {QuoteJsString(role)}]");
            else preds.Add($"//*[contains(translate(@role,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), {QuoteJsString(role.ToLowerInvariant())})]");

            var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "button", new[] { "//button", "//input[@type='button']", "//input[@type='submit']", "//input[@type='reset']", "//a[@role='button']" } },
                { "link", new[] { "//a[@href]", "//*[@role='link']" } },
                { "textbox", new[] { "//textarea", "//input[translate(@type,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='text']", "//input[not(@type)]" } },
                { "checkbox", new[] { "//input[@type='checkbox']" } },
                { "radio", new[] { "//input[@type='radio']" } },
                { "combobox", new[] { "//select", "//*[@role='combobox']" } },
                { "img", new[] { "//img" } },
                { "heading", new[] { "//h1","//h2","//h3","//h4","//h5","//h6" } },
                { "list", new[] { "//ul","//ol","//*[@role='list']" } },
                { "listitem", new[] { "//li","//*[@role='listitem']" } }
            };

            if (map.TryGetValue(role, out var arr)) preds.AddRange(arr);
            return string.Join(" | ", preds);
        }

        /// <summary>
        /// Quotes the js string.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        private static string QuoteJsString(string s) => s == null ? "''" : $"'{s.Replace("'", "\\'")}'";
        /// <summary>
        /// Escapes the CSS.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        private static string EscapeCss(string s) => s?.Replace("'", "\\'") ?? "";

        /// <summary>
        /// Finds the element with wait.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="by">The by.</param>
        /// <param name="options">The options.</param>
        /// <param name="throwOnTimeout">if set to <c>true</c> [throw on timeout].</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.WebDriverTimeoutException">Timed out after {options.TimeoutSeconds}s waiting for '{by}' ({options.Wait})</exception>
        private static IWebElement FindElementWithWait(IWebDriver driver, By by, LocatorOptions options, bool throwOnTimeout = true)
        {
            options ??= DefaultOptions();
            if (options.Wait == WaitUntil.None)
            {
                try { return driver.FindElement(by); }
                catch { if (throwOnTimeout) throw; return null; }
            }

            var wait = new DefaultWait<IWebDriver>(driver)
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)),
                PollingInterval = TimeSpan.FromMilliseconds(Math.Max(50, options.PollingMs))
            };
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

            try
            {
                var el = wait.Until(drv =>
                {
                    try
                    {
                        var e = drv.FindElement(by);
                        return EvaluateWaitCondition(e, options) ? e : null;
                    }
                    catch { return null; }
                });
                return el;
            }
            catch (WebDriverTimeoutException)
            {
                if (throwOnTimeout) throw new WebDriverTimeoutException($"Timed out after {options.TimeoutSeconds}s waiting for '{by}' ({options.Wait})");
                return null;
            }
        }

        /// <summary>
        /// Finds the elements with wait.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="by">The by.</param>
        /// <param name="options">The options.</param>
        /// <param name="throwOnTimeout">if set to <c>true</c> [throw on timeout].</param>
        /// <returns></returns>
        /// <exception cref="OpenQA.Selenium.WebDriverTimeoutException">Timed out after {options.TimeoutSeconds}s waiting for elements '{by}' ({options.Wait})</exception>
        private static IReadOnlyCollection<IWebElement> FindElementsWithWait(IWebDriver driver, By by, LocatorOptions options, bool throwOnTimeout = true)
        {
            options ??= DefaultOptions();
            if (options.Wait == WaitUntil.None)
            {
                var dd = driver.FindElements(by); return dd == null ? Array.Empty<IWebElement>() : dd.ToList().AsReadOnly();
            }

            var wait = new DefaultWait<IWebDriver>(driver)
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)),
                PollingInterval = TimeSpan.FromMilliseconds(Math.Max(50, options.PollingMs))
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            try
            {
                var elems = wait.Until(drv =>
                {
                    try
                    {
                        var list = drv.FindElements(by);
                        if (list == null || list.Count == 0) return null;
                        if (options.Wait == WaitUntil.Exists) return list.ToList().AsReadOnly();
                        var ok = list.Where(el => EvaluateWaitCondition(el, options)).ToList();
                        return ok.Count > 0 ? ok.AsReadOnly() : null;
                    }
                    catch { return null; }
                });
                return elems;
            }
            catch (WebDriverTimeoutException)
            {
                if (throwOnTimeout) throw new WebDriverTimeoutException($"Timed out after {options.TimeoutSeconds}s waiting for elements '{by}' ({options.Wait})");
                return Array.Empty<IWebElement>();
            }
        }

        /// <summary>
        /// Evaluates the wait condition.
        /// </summary>
        /// <param name="el">The el.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        private static bool EvaluateWaitCondition(IWebElement el, LocatorOptions options)
        {
            switch (options.Wait)
            {
                case WaitUntil.Exists: return true;
                case WaitUntil.Visible: return SafeDisplayed(el);
                case WaitUntil.Enabled: return SafeDisplayed(el) && SafeEnabled(el);
                case WaitUntil.Clickable: return SafeDisplayed(el) && SafeEnabled(el);
                default: return true;
            }
        }

        /// <summary>
        /// Safes the displayed.
        /// </summary>
        /// <param name="el">The el.</param>
        /// <returns></returns>
        private static bool SafeDisplayed(IWebElement el) { try { return el.Displayed; } catch { return false; } }
        /// <summary>
        /// Safes the enabled.
        /// </summary>
        /// <param name="el">The el.</param>
        /// <returns></returns>
        private static bool SafeEnabled(IWebElement el) { try { return el.Enabled; } catch { return false; } }
        #endregion
    }
}
