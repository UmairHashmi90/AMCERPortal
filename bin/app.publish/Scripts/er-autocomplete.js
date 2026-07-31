(function (window, document) {
    "use strict";

    var DROPDOWN_ID = "erAutocompleteDropdown";
    var activeInput = null;
    var maxVisible = 80;

    function isAutocompleteInput(el) {
        if (!el || el.tagName !== "INPUT") return false;
        if (el.disabled || el.readOnly) return false;
        if (el.classList && (el.classList.contains("searchable-dropdown") || el.classList.contains("lov-autocomplete"))) {
            return true;
        }
        return !!(el.getAttribute("list") || el.getAttribute("data-er-list"));
    }

    function getListId(input) {
        return input.getAttribute("data-er-list") || input.getAttribute("list") || "";
    }

    function prepareInput(input) {
        if (!input || input.getAttribute("data-er-ac") === "1") return;
        var listId = getListId(input);
        if (!listId) return;

        input.setAttribute("data-er-list", listId);
        input.setAttribute("data-er-ac", "1");
        input.setAttribute("autocomplete", "off");
        input.setAttribute("autocapitalize", "off");
        input.setAttribute("autocorrect", "off");
        input.setAttribute("spellcheck", "false");
        // Disable native datalist (tablet Chrome uses keyboard suggestion bar).
        input.removeAttribute("list");

        if (!input.parentElement) return;
        if (!input.parentElement.classList.contains("er-ac-wrap")) {
            var wrap = document.createElement("div");
            wrap.className = "er-ac-wrap";
            input.parentElement.insertBefore(wrap, input);
            wrap.appendChild(input);
        }
    }

    function getOptions(listId) {
        var list = document.getElementById(listId);
        if (!list) return [];
        return Array.prototype.map.call(list.options || [], function (opt) {
            return {
                label: (opt.value || opt.label || opt.textContent || "").trim(),
                value: (opt.getAttribute("data-value") || "").trim()
            };
        }).filter(function (x) { return !!x.label; });
    }

    function getDropdown() {
        var el = document.getElementById(DROPDOWN_ID);
        if (el) return el;

        el = document.createElement("div");
        el.id = DROPDOWN_ID;
        el.className = "er-ac-dropdown d-none";
        el.setAttribute("role", "listbox");
        document.body.appendChild(el);
        return el;
    }

    function hideDropdown() {
        var el = getDropdown();
        el.classList.add("d-none");
        el.innerHTML = "";
        activeInput = null;
    }

    function positionDropdown(input) {
        var el = getDropdown();
        var rect = input.getBoundingClientRect();
        var viewportH = window.innerHeight || document.documentElement.clientHeight;
        var spaceBelow = viewportH - rect.bottom;
        var maxHeight = Math.min(280, Math.max(140, spaceBelow - 12));
        var openUp = spaceBelow < 160 && rect.top > spaceBelow;

        el.style.left = Math.max(8, rect.left) + "px";
        el.style.width = Math.max(rect.width, 180) + "px";
        el.style.maxHeight = maxHeight + "px";

        if (openUp) {
            el.style.top = "auto";
            el.style.bottom = (viewportH - rect.top + 4) + "px";
        } else {
            el.style.bottom = "auto";
            el.style.top = (rect.bottom + 4) + "px";
        }
    }

    function syncHidden(input, optionValue) {
        var hiddenId = input.getAttribute("data-lov-hidden");
        if (!hiddenId) return;
        var hidden = document.getElementById(hiddenId);
        if (!hidden) return;
        hidden.value = optionValue || "";
    }

    function selectOption(input, option) {
        input.value = option.label || "";
        syncHidden(input, option.value || "");

        // Fire events so existing ErForm handlers keep working.
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));

        hideDropdown();
        try { input.blur(); } catch (e) { /* ignore */ }
    }

    function renderDropdown(input, term) {
        prepareInput(input);
        var listId = getListId(input);
        if (!listId) return;

        var options = getOptions(listId);
        var q = (term || "").trim().toLowerCase();
        var matched = !q
            ? options.slice(0, maxVisible)
            : options.filter(function (opt) {
                return opt.label.toLowerCase().indexOf(q) >= 0;
            }).slice(0, maxVisible);

        var el = getDropdown();
        el.innerHTML = "";

        if (!matched.length) {
            var empty = document.createElement("div");
            empty.className = "er-ac-empty";
            empty.textContent = "No matches";
            el.appendChild(empty);
        } else {
            matched.forEach(function (opt) {
                var item = document.createElement("button");
                item.type = "button";
                item.className = "er-ac-item";
                item.setAttribute("role", "option");
                item.textContent = opt.label;
                item.addEventListener("mousedown", function (e) {
                    e.preventDefault();
                    selectOption(input, opt);
                });
                item.addEventListener("touchstart", function (e) {
                    e.preventDefault();
                    selectOption(input, opt);
                }, { passive: false });
                el.appendChild(item);
            });
        }

        activeInput = input;
        positionDropdown(input);
        el.classList.remove("d-none");
    }

    function onFocusIn(e) {
        var input = e.target;
        if (!isAutocompleteInput(input)) return;
        prepareInput(input);
        renderDropdown(input, input.value || "");
    }

    function onInput(e) {
        var input = e.target;
        if (!isAutocompleteInput(input)) return;
        prepareInput(input);

        var listId = getListId(input);
        var options = getOptions(listId);
        var current = (input.value || "").trim();
        var exact = options.find(function (opt) {
            return opt.label === current;
        });
        syncHidden(input, exact ? exact.value : "");

        renderDropdown(input, input.value || "");
    }

    function onDocumentPointer(e) {
        var dropdown = document.getElementById(DROPDOWN_ID);
        if (!dropdown || dropdown.classList.contains("d-none")) return;
        if (activeInput && (activeInput === e.target || activeInput.contains(e.target))) return;
        if (dropdown.contains(e.target)) return;
        hideDropdown();
    }

    function enhanceAll(root) {
        var scope = root || document;
        var nodes = scope.querySelectorAll(
            "input.searchable-dropdown, input.lov-autocomplete, input[list], input[data-er-list]"
        );
        Array.prototype.forEach.call(nodes, prepareInput);
    }

    document.addEventListener("focusin", onFocusIn);
    document.addEventListener("input", onInput);
    document.addEventListener("mousedown", onDocumentPointer);
    document.addEventListener("touchstart", onDocumentPointer, { passive: true });
    function repositionActive() {
        if (activeInput) positionDropdown(activeInput);
    }

    window.addEventListener("resize", repositionActive);
    window.addEventListener("scroll", repositionActive, true);
    if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", repositionActive);
        window.visualViewport.addEventListener("scroll", repositionActive);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () { enhanceAll(document); });
    } else {
        enhanceAll(document);
    }

    // Re-bind for dynamically added package/medicine rows.
    if (window.MutationObserver) {
        var observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (m) {
                Array.prototype.forEach.call(m.addedNodes || [], function (node) {
                    if (node.nodeType !== 1) return;
                    if (isAutocompleteInput(node)) prepareInput(node);
                    else if (node.querySelectorAll) enhanceAll(node);
                });
            });
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    window.ERAutocomplete = {
        refresh: enhanceAll,
        hide: hideDropdown
    };
})(window, document);
