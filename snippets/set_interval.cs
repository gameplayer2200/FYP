foreach (var mb in UnityEngine.Object.FindObjectsOfType<MetroBlackout>(true))
{
    var so = new UnityEditor.SerializedObject(mb);
    so.FindProperty("segmentInterval").floatValue = 0.5f;
    so.ApplyModifiedPropertiesWithoutUndo();
    mb.enabled = true;
}
return "interval=0.5s enabled";
