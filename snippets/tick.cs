UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
UnityEditor.EditorApplication.Update();
return "ticked, time=" + UnityEngine.Time.time.ToString("F1");
