using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoiceHandler))]
public class VoiceHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var handler = (VoiceHandler)target;

        // ── Voice selector ────────────────────────────────────────────────
        EditorGUILayout.LabelField("Voice", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to load available voices.", MessageType.Info);
        }
        else if (handler.VoiceNames == null || handler.VoiceNames.Count == 0)
        {
            EditorGUILayout.HelpBox("No voices loaded yet — waiting for RTVoice.", MessageType.Warning);
            if (GUILayout.Button("Refresh Voices"))
                handler.PopulateVoiceList();
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Selected Voice", handler.VoiceIndex, handler.VoiceNames.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(handler, "Change Voice");
                handler.VoiceIndex = newIndex;
                handler.RefreshSelectedName();
            }

            EditorGUILayout.LabelField("Active", handler.SelectedVoiceName, EditorStyles.miniLabel);

            if (GUILayout.Button("Refresh Voice List"))
                handler.PopulateVoiceList();
        }

        EditorGUILayout.Space();

        // ── Everything else ───────────────────────────────────────────────
        DrawPropertiesExcluding(serializedObject, "m_Script");
    }
}
