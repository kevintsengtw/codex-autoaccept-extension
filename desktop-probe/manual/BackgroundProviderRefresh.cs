using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Accessibility;

// Chromium can retain a complete but stale accessibility tree while its window is
// occluded/minimized. A document-scoped MSAA hit test requests fresh renderer AX
// data without mouse input, focus, scrolling, restoration or an approval action.
// The returned hit is deliberately ignored: only the subsequent full UIA scans
// may select and validate an approval button.
internal static class BackgroundProviderRefresh
{
    public static void Read(IntPtr target, int maxDepth, int maxNodes)
    {
        var iid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
        object root = null;
        var retained = new HashSet<object>();
        try
        {
            int result = AccessibleObjectFromWindow(target, unchecked((uint)-4), ref iid, out root);
            if (root != null && Marshal.IsComObject(root)) retained.Add(root);
            Marshal.ThrowExceptionForHR(result);
            var accessible = root as IAccessible;
            if (accessible == null) throw ReadFailure();
            var documents = AccessibleRefreshScan.CollectDocuments(accessible, maxDepth, maxNodes,
                node => Equals(node.get_accRole(0), 15), // ROLE_SYSTEM_DOCUMENT
                node => node.accChildCount,
                (node, children) => ReadChildren(node, children, retained));
            foreach (var document in documents)
            {
                IntPtr owner;
                Marshal.ThrowExceptionForHR(WindowFromAccessibleObject(document, out owner));
                if (owner == IntPtr.Zero || owner != target && GetAncestor(owner, 2) != target)
                    throw ReadFailure();
                int x, y, width, height;
                document.accLocation(out x, out y, out width, out height, 0);
                if (width <= 0 || height <= 0) throw ReadFailure();
                object hit = document.accHitTest(checked(x + width / 2), checked(y + height / 2));
                if (hit != null && Marshal.IsComObject(hit)) retained.Add(hit);
            }
        }
        catch (ScanFailureException) { throw; }
        catch (Exception) { throw ReadFailure(); }
        finally
        {
            foreach (var value in retained) Marshal.ReleaseComObject(value);
        }
    }

    static IReadOnlyList<AccessibleRefreshChild<IAccessible>> ReadChildren(IAccessible node, int children, HashSet<object> retained)
    {
        var values = new object[children];
        int obtained;
        int result = AccessibleChildren(node, 0, children, values, out obtained);
        foreach (var value in values) if (value != null && Marshal.IsComObject(value)) retained.Add(value);
        Marshal.ThrowExceptionForHR(result);
        if (obtained != children) throw ReadFailure();
        var entries = new AccessibleRefreshChild<IAccessible>[children];
        for (int i = 0; i < values.Length; i++)
        {
            var child = values[i] as IAccessible;
            if (child != null) entries[i] = new AccessibleRefreshChild<IAccessible>(child);
            else if (values[i] is int) entries[i] = new AccessibleRefreshChild<IAccessible>();
            else throw ReadFailure();
        }
        return entries;
    }

    static ScanFailureException ReadFailure()
    { return new ScanFailureException(ScanFailureReason.READ, ScanOrigin.MSAA); }

    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value);
    [DllImport("oleacc.dll")] static extern int AccessibleChildren(IAccessible parent, int start, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] children, out int obtained);
    [DllImport("oleacc.dll")] static extern int WindowFromAccessibleObject(IAccessible value, out IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
}
