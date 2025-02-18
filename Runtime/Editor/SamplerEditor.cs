#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace jeanf.audiosystems
{
    #if UNITY_EDITOR
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Sampler), true)]
    public class SamplerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var eventToSend = (Sampler)target;
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PlayFrom", GUILayout.Height(30)))
            {
                eventToSend.PlayAudioClip(); // how do i call this?
            }
            
            if (GUILayout.Button("PlayOut", GUILayout.Height(30)))
            {
                eventToSend.StopAudioClip(); // how do i call this?
            }
            
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
    }
    #endif
}