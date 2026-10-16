var sb = new System.Text.StringBuilder();
try
{
    var cab = UnityEngine.Object.FindObjectsOfType<PowerCabinet>(true);
    sb.AppendLine("cabinets=" + cab.Length);
    var fl = typeof(PowerCabinet).GetField("blackout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var mb = UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true);
    fl.SetValue(cab[0], mb.Length > 0 ? mb[0] : null);
    sb.AppendLine("relinked");
}
catch (System.Exception e) { sb.AppendLine("STEP1 FAIL: " + e.Message); return sb.ToString(); }
try
{
    var breaker = UnityEngine.Object.FindObjectsOfType<BreakerSwitch>(true);
    var closedField = typeof(BreakerSwitch).GetField("closed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    closedField.SetValue(breaker[0], false);
    breaker[0].enabled = true;
    sb.AppendLine("breaker reset");
}
catch (System.Exception e) { sb.AppendLine("STEP2 FAIL: " + e.Message); return sb.ToString(); }
try
{
    var breaker = UnityEngine.Object.FindObjectsOfType<BreakerSwitch>(true);
    System.Type pr = null;
    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
    {
        try { var t = asm.GetType("PlayerRef"); if (t != null) { pr = t; break; } } catch { }
    }
    var player = pr.GetProperty("Root").GetValue(null) as UnityEngine.Transform;
    breaker[0].Interact(player != null ? player.gameObject : null);
    sb.AppendLine("interacted, restored=" + MetroBlackout.PowerRestored);
}
catch (System.Exception e) { sb.AppendLine("STEP3 FAIL: " + e.Message); return sb.ToString(); }
return sb.ToString();
