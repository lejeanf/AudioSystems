#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace jeanf.audiosystems
{
    public class DrawAudioWaveform : MonoBehaviour
    {
        private static bool _isDraggingMarker = false;
        private static string _draggingMarkerName;
        
        public static void DrawWaveform(SamplerData samplerData)
        {
            if (samplerData.isPlayOneShot) return;
            if (samplerData.audioClip == null) return;
            
            var samples = new float[samplerData.audioClip.samples * samplerData.audioClip.channels];
            samplerData.audioClip.GetData(samples, 0);

            GUILayout.Label($"{samplerData.audioClip.name}'s waveform:");
            var r = GUILayoutUtility.GetRect(0, 0);
            var waveformRect = new Rect(5,r.y,EditorGUIUtility.currentViewWidth-5, 128);
            
            GUILayout.Space(128);
            EditorGUI.DrawRect(waveformRect, new Color(0.17f, 0.17f, 0.17f));
            Handles.BeginGUI();
            for (var i = 0; i < waveformRect.width; i++)
            {
                var sampleIndex = Mathf.FloorToInt((i / waveformRect.width) * samples.Length);
                var sampleValue = samples[sampleIndex] * waveformRect.height / 2;

                var x = waveformRect.x + i;
                var y = waveformRect.y + waveformRect.height / 2 + sampleValue;

                Handles.DrawLine(new Vector3(x, waveformRect.y + waveformRect.height / 2, 0), new Vector3(x, y, 0));
            }
            Handles.EndGUI();

            DrawEditableMarker(waveformRect, ref samplerData.playFrom, new Color(0.59f, 1f, 0.46f), "Play From", samplerData.audioClip, 1, samplerData);
            DrawEditableMarker(waveformRect, ref samplerData.loopFrom, Color.yellow, "Loop From", samplerData.audioClip, 2, samplerData);
            DrawEditableMarker(waveformRect, ref samplerData.loopTo, new Color(1f, 0.59f, 0.29f), "Loop To", samplerData.audioClip, 3, samplerData);
            DrawEditableMarker(waveformRect, ref samplerData.playOut, Color.red, "Play Out", samplerData.audioClip, 4, samplerData);
        }
        private static void DrawEditableMarker(Rect rect, ref float time, Color color, string label, AudioClip audioClip, int index, ScriptableObject targetObject)
        {
            var x = rect.x + time * rect.width / audioClip.length;
            var handleSize = 8f;
            var handleRect = new Rect(x - handleSize / 2, rect.y, handleSize, rect.height);

            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.SlideArrow);

            Handles.color = color;
            Handles.DrawLine(new Vector3(x, rect.y, 0), new Vector3(x, rect.y + rect.height, 0));
            Handles.Label(new Vector3(x + 2, rect.y - handleSize + 15 * index, 0), label, new GUIStyle { normal = new GUIStyleState { textColor = color } });

            // Check for mouse events
            if (Event.current.type == EventType.MouseDown && handleRect.Contains(Event.current.mousePosition))
            {
                // Record the object state for undo before starting the drag
                Undo.RecordObject(targetObject, "Modify Marker");

                _isDraggingMarker = true;
                _draggingMarkerName = label;
                Event.current.Use();
            }

            if (_isDraggingMarker && _draggingMarkerName == label)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    float newTime = Mathf.Clamp((Event.current.mousePosition.x - rect.x) / rect.width * audioClip.length, 0, audioClip.length);

                    // Snap to nearest zero-crossing (if needed)
                    //newTime = FindNearestZeroCrossing(audioClip, newTime);

                    // Modify the referenced variable
                    time = newTime;

                    // Mark the object as dirty to ensure changes are saved
                    EditorUtility.SetDirty(targetObject);

                    GUI.changed = true; // Mark the GUI as changed to trigger updates
                }

                if (Event.current.type == EventType.MouseUp)
                {
                    _isDraggingMarker = false;
                    _draggingMarkerName = null;
                    Event.current.Use();
                }
            }
        }
    
    }
}
#endif
