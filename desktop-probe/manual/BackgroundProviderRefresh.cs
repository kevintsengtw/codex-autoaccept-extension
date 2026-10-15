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
    sealed class Pending { public IAccessible Node; public int Depth; }

    public static void Read(IntPtr target, int maxDepth, int maxNodes)
    {
        var iid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
        object root;
        Marshal.ThrowExceptionForHR(AccessibleObjectFromWindow(target, unchecked((uint)-4), ref iid, out root));
        var retained = new HashSet<object>();
        try
        {
            var accessible = root as IAccessible;
            if (accessible == null) throw new InvalidOperationException("Missing accessibility client root.");
            retained.Add(accessible);
            var stack = new Stack<Pending>();
            var visited = new HashSet<object>();
            var documents = new List<IAccessible>();
            stack.Push(new Pending { Node = accessible });
            int count = 0;
            while (stack.Count != 0)
            {
                var item = stack.Pop();
                if (++count > maxNodes || !visited.Add(item.Node)) throw new InvalidOperationException("Accessibility traversal limit or repeated object.");
                if (Equals(item.Node.get_accRole(0), 15)) // ROLE_SYSTEM_DOCUMENT
                {
                    documents.Add(item.Node);
                    continue; // Refresh root documents; never walk their chat text.
                }
                int children = item.Node.accChildCount;
                if (children < 0 || children > maxNodes - count || children > 0 && item.Depth >= maxDepth)
                    throw new InvalidOperationException("Accessibility traversal limit.");
                if (children == 0) continue;
                var values = new object[children]; int obtained;
                Marshal.ThrowExceptionForHR(AccessibleChildren(item.Node, 0, children, values, out obtained));
                foreach (var value in values) if (value != null && Marshal.IsComObject(value)) retained.Add(value);
                if (obtained != children) throw new InvalidOperationException("Incomplete accessibility traversal.");
                foreach (var value in values)
                {
                    var child = value as IAccessible;
                    if (child != null) stack.Push(new Pending { Node = child, Depth = item.Depth + 1 });
                    else if (!(value is int) || ++count > maxNodes) throw new InvalidOperationException("Invalid accessibility child.");
                }
            }
            if (documents.Count == 0) throw new InvalidOperationException("Missing accessibility document.");
            foreach (var document in documents)
            {
                IntPtr owner;
                Marshal.ThrowExceptionForHR(WindowFromAccessibleObject(document, out owner));
                if (owner == IntPtr.Zero || owner != target && GetAncestor(owner, 2) != target)
                    throw new InvalidOperationException("Accessibility document left target window.");
                int x, y, width, height;
                document.accLocation(out x, out y, out width, out height, 0);
                if (width <= 0 || height <= 0) throw new InvalidOperationException("Missing accessibility document bounds.");
                object hit = document.accHitTest(checked(x + width / 2), checked(y + height / 2));
                if (hit != null && Marshal.IsComObject(hit)) retained.Add(hit);
            }
        }
        finally
        {
            foreach (var value in retained) Marshal.ReleaseComObject(value);
        }
    }

    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value);
    [DllImport("oleacc.dll")] static extern int AccessibleChildren(IAccessible parent, int start, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] children, out int obtained);
    [DllImport("oleacc.dll")] static extern int WindowFromAccessibleObject(IAccessible value, out IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
}
