static void Check(bool b, string s) { if (!b) throw new Exception(s); }
IntegrityResult L(int? rid) => new IntegrityResult { Rid = rid };
foreach (int rid in new[] { 0, 0x1000, 0x2000, 0x2010, 0x2100, 0x2fff })
    Check(PermissionPolicy.CanStart(L(0x2000), L(rid), out _), "non-admin target metadata isn't an artificial UIA blocker");
Check(PermissionPolicy.CanStart(L(0x2010), L(0x2000), out _), "standard range isn't exact RID only");
Check(PermissionPolicy.CanStart(L(0x2000), L(null), out var reason) && reason == "QUERY_UNAVAILABLE", "metadata unavailable defers to OS UIA enforcement");
Check(PermissionPolicy.CanStart(L(null), L(0x2000), out reason) && reason == "QUERY_UNAVAILABLE", "current-process query unavailable");
Check(!PermissionPolicy.CanStart(L(0x3000), L(0x2000), out reason) && reason == "PROBE_ELEVATED", "known admin tool rejected");
Check(!PermissionPolicy.CanStart(L(0x2000), L(0x3000), out reason) && reason == "TARGET_ELEVATED", "known admin target rejected");
Check(!PermissionPolicy.CanStart(L(null), L(0x4000), out _), "unknown tool cannot opt into a known system target");
Check(L(0x2010).Level == "medium" && L(0x2100).Level == "medium-plus" && L(0x1000).Level == "low" && L(null).Level == "unavailable", "labels distinguish low/standard/unknown");
Console.WriteLine("PASS: permission metadata cases; standard/lower/unknown deferred to Windows UIA; known elevated tool/target rejected.");
