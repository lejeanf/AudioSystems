#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using jeanf.validationTools;

namespace jeanf.audiosystems
{
    /// <summary>
    /// Inspector for <see cref="BpmSampler"/>. Unity appends a VU meter to the default inspector of any
    /// MonoBehaviour that implements OnAudioFilterRead (Editor.DoDrawDefaultInspector → AudioFilterGUI),
    /// and that meter ends with GUIView.current.Repaint(). Reached through a custom Editor's
    /// DrawDefaultInspector() inside the UI Toolkit inspector — which is how the propertydrawer
    /// package's ValidationInspectorBanner draws every MonoBehaviour — GUIView.current is null and
    /// selecting the sampler throws a NullReferenceException on every repaint. This editor draws the
    /// serialized fields itself, keeps the validation banner, and shows the beat clock instead of the meter.
    /// </summary>
    [CustomEditor(typeof(BpmSampler))]
    [CanEditMultipleObjects]
    public class BpmSamplerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ValidationUi.DrawIssuesBanner(target as Component);

            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying || targets.Length != 1) return;

            var sampler = (BpmSampler)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Beat clock", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Running", sampler.IsRunning);
                EditorGUILayout.LongField("Beats", sampler.BeatCount);
                EditorGUILayout.DoubleField("Period (s)", sampler.BeatPeriodSeconds);
                EditorGUILayout.Slider("Phase",
                    (float)BpmSampler.BeatPhase(AudioSettings.dspTime, sampler.LastBeatDspTime, sampler.BeatPeriodSeconds), 0f, 1f);
            }
            Repaint(); // the clock moves between GUI events
        }
    }
}
#endif
