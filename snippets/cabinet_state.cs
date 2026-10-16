var cab = UnityEngine.Object.FindObjectsOfType<PowerCabinet>(true)[0];
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var t = typeof(PowerCabinet);
int sel = (int)t.GetField("selectedIndex", bf).GetValue(cab);
int live = (int)t.GetField("liveIndex", bf).GetValue(cab);
var blk = t.GetField("blackout", bf).GetValue(cab);
var wires = (PowerWire[])t.GetField("wires", bf).GetValue(cab);
var breaker = UnityEngine.Object.FindObjectsOfType<BreakerSwitch>(true)[0];
bool closed = (bool)typeof(BreakerSwitch).GetField("closed", bf).GetValue(breaker);
return "selectedIndex=" + sel + " liveIndex=" + live + " HasSelection=" + cab.HasSelection
     + " blackoutLinked=" + (blk != null) + " wires=" + (wires != null ? wires.Length : 0)
     + " breakerClosed=" + closed + " breakerEnabled=" + breaker.enabled;
