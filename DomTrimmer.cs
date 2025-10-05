using OpenQA.Selenium;


namespace SimpleSeleniumSupport
{
    /// <summary>
    /// DOM trimmer: collects visible nodes (tag, short text, selected attributes) and returns a JSON string.
    /// Use this for sending a compact, privacy-respecting snapshot to the AI.
    /// </summary>
    public static class DomTrimmer
    {
        /// <summary>
        /// Collects up to maxNodes visible elements and returns a JSON string trimmed to maxChars if needed.
        /// </summary>
        public static string TrimDom(IWebDriver driver, int maxNodes = 400, int maxChars = 10000)
        {
            try
            {
                var js = @"
                (function(maxNodes){
                    function getAttrs(el){
                        var out = {};
                        for(var i=0;i<el.attributes.length;i++){
                            var a = el.attributes[i];
                            // include common stable attributes only
                            if(a.name === 'id' || a.name === 'name' || a.name.startsWith('data-') || a.name === 'role' || a.name === 'aria-label')
                                out[a.name]=a.value;
                        }
                        return out;
                    }
                    var nodes=[];
                    var all=document.querySelectorAll('body *');
                    for(var i=0;i<all.length && nodes.length<maxNodes;i++){
                        var el=all[i];
                        var rect=el.getBoundingClientRect();
                        if(rect.width>0 && rect.height>0){
                            var text = (el.innerText||'').trim().replace(/\\s+/g,' ').slice(0,300);
                            nodes.push({tag: el.tagName.toLowerCase(), text: text, attrs: getAttrs(el)});
                        }
                    }
                    return JSON.stringify(nodes);
                })(arguments[0]);";

                var jsExec = (IJavaScriptExecutor)driver;
                var json = jsExec.ExecuteScript(js, maxNodes) as string;
                if (string.IsNullOrWhiteSpace(json)) return "[]";

                if (json.Length > maxChars) return json.Substring(0, maxChars) + "...";
                return json;
            }
            catch (Exception ex)
            {
                return $"[dom-trim-error]: {ex.Message}";
            }
        }
    }
}
