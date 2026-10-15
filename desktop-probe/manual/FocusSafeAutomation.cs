using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Native = Interop.UIAutomationClient;

// A narrow UIA COM adapter. Every element, walker and pattern belongs to this
// one CUIAutomation8 client. Setting a separate COM client's AutoSetFocus would
// not configure System.Windows.Automation's framework InvokePattern.
internal sealed class FocusSafeAutomation
{
    static readonly Lazy<FocusSafeAutomation> instance = new Lazy<FocusSafeAutomation>(() => new FocusSafeAutomation());
    readonly Native.IUIAutomation client;
    readonly Native.IUIAutomation2 client2;
    readonly Native.IUIAutomationTreeWalker rawWalker;

    FocusSafeAutomation()
    {
        RequireMta();
        client = new Native.CUIAutomation8(); // No legacy CUIAutomation fallback.
        client2 = (Native.IUIAutomation2)client;
        FocusPolicy.Configure(() => client2.AutoSetFocus = 0, () => client2.AutoSetFocus != 0);
        rawWalker = client.RawViewWalker;
        if (rawWalker == null) throw new InvalidOperationException("Missing raw UIA walker.");
    }

    public static void Initialize() { instance.Value.VerifyFocusPolicy(); }

    static void RequireMta()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
            throw new InvalidOperationException("UIA requires the dedicated MTA worker path.");
    }

    void VerifyFocusPolicy()
    {
        RequireMta();
        FocusPolicy.RequireDisabled(() => client2.AutoSetFocus != 0);
    }

    static T Read<T>(Func<T> read)
    {
        RequireMta();
        try { return read(); }
        catch (COMException error) when (error.ErrorCode == unchecked((int)0x80040201))
        { throw new ElementNotAvailableException("UIA element is no longer available.", error); }
    }

    Element Wrap(Native.IUIAutomationElement value) { return value == null ? null : new Element(this, value); }
    void CheckOwner(Element value)
    {
        if (value == null || !ReferenceEquals(value.Owner, this))
            throw new InvalidOperationException("UIA element belongs to another client.");
    }

    internal sealed class Element
    {
        internal readonly FocusSafeAutomation Owner;
        internal readonly Native.IUIAutomationElement Value;
        internal Element(FocusSafeAutomation owner, Native.IUIAutomationElement value) { Owner = owner; Value = value; }
        public static readonly AutomationProperty IsInvokePatternAvailableProperty = System.Windows.Automation.AutomationElement.IsInvokePatternAvailableProperty;

        public static Element FromHandle(IntPtr hwnd)
        {
            var owner = instance.Value;
            return owner.Wrap(Read(() => owner.client.ElementFromHandle(hwnd)));
        }
        public int[] GetRuntimeId() { return Read(() => Value.GetRuntimeId()); }
        public object GetCurrentPropertyValue(AutomationProperty property) { return Read(() => Value.GetCurrentPropertyValue(property.Id)); }
        public AutomationElementInformation Current { get { return new AutomationElementInformation(this); } }

        public bool TryGetCurrentPattern(AutomationPattern pattern, out object result)
        {
            object value;
            try { value = Read(() => Value.GetCurrentPattern(pattern.Id)); }
            catch (COMException error) when (error.ErrorCode == unchecked((int)0x80040204))
            { result = null; return false; } // UIA_E_NOTSUPPORTED only.
            if (value == null) { result = null; return false; }
            if (pattern == InvokePattern.Pattern) result = new InvokePattern(Owner, (Native.IUIAutomationInvokePattern)value);
            else if (pattern == WindowPattern.Pattern) result = new WindowPattern((Native.IUIAutomationWindowPattern)value);
            else throw new InvalidOperationException("Unsupported UIA pattern requested.");
            return true;
        }

        internal struct AutomationElementInformation
        {
            readonly Element element;
            internal AutomationElementInformation(Element element) { this.element = element; }
            public string Name { get { var e = element; return Read(() => e.Value.CurrentName); } }
            public bool IsPassword { get { var e = element; return Read(() => e.Value.CurrentIsPassword != 0); } }
            public bool IsEnabled { get { var e = element; return Read(() => e.Value.CurrentIsEnabled != 0); } }
            public bool IsOffscreen { get { var e = element; return Read(() => e.Value.CurrentIsOffscreen != 0); } }
            public ControlType ControlType { get { var e = element; return ControlType.LookupById(Read(() => e.Value.CurrentControlType)); } }
            public Rect BoundingRectangle
            {
                get
                {
                    var e = element;
                    var r = Read(() => e.Value.CurrentBoundingRectangle);
                    return new Rect(r.left, r.top, (double)r.right - r.left, (double)r.bottom - r.top);
                }
            }
        }
    }

    internal sealed class Walker
    {
        readonly FocusSafeAutomation owner;
        Walker(FocusSafeAutomation owner) { this.owner = owner; }
        public static Walker RawViewWalker { get { return new Walker(instance.Value); } }
        public Element GetFirstChild(Element element) { owner.CheckOwner(element); return owner.Wrap(Read(() => owner.rawWalker.GetFirstChildElement(element.Value))); }
        public Element GetNextSibling(Element element) { owner.CheckOwner(element); return owner.Wrap(Read(() => owner.rawWalker.GetNextSiblingElement(element.Value))); }
        public Element GetParent(Element element) { owner.CheckOwner(element); return owner.Wrap(Read(() => owner.rawWalker.GetParentElement(element.Value))); }
    }

    internal sealed class InvokePattern
    {
        readonly FocusSafeAutomation owner;
        readonly Native.IUIAutomationInvokePattern pattern;
        internal InvokePattern(FocusSafeAutomation owner, Native.IUIAutomationInvokePattern pattern) { this.owner = owner; this.pattern = pattern; }
        public static readonly AutomationPattern Pattern = System.Windows.Automation.InvokePattern.Pattern;
        public void Invoke()
        {
            owner.VerifyFocusPolicy();
            pattern.Invoke(); // The sole approval action; never retry or fall back.
        }
    }

    internal sealed class WindowPattern
    {
        readonly Native.IUIAutomationWindowPattern pattern;
        internal WindowPattern(Native.IUIAutomationWindowPattern pattern) { this.pattern = pattern; }
        public static readonly AutomationPattern Pattern = System.Windows.Automation.WindowPattern.Pattern;
        public WindowInformation Current { get { return new WindowInformation(pattern); } }
        internal struct WindowInformation
        {
            readonly Native.IUIAutomationWindowPattern pattern;
            internal WindowInformation(Native.IUIAutomationWindowPattern pattern) { this.pattern = pattern; }
            public WindowInteractionState WindowInteractionState
            { get { var p = pattern; return (WindowInteractionState)(int)Read(() => p.CurrentWindowInteractionState); } }
        }
    }
}
