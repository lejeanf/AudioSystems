using System;
using jeanf.propertyDrawer;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace jeanf.audiosystems
{
    [ExecuteAlways]
    [CreateAssetMenu(fileName = "SamplerData_", menuName = "Audio/SamplerData", order = 1)]
    [ScriptableObjectDrawer]
    [Serializable]
    public class SamplerData : ScriptableObject
    {
        [Range(0, 1)] public float volume;
        public AudioClip audioClip;

        [Tooltip("This slug acts as the identifier for the clip to play")]
        public string slug;
        public bool isPlayOneShot = true; 
        [DrawIf("isPlayOneShot", false, ComparisonType.Equals, DisablingType.DontDraw)]
        public float playFrom;
        [DrawIf("isPlayOneShot", false, ComparisonType.Equals, DisablingType.DontDraw)]
        public float loopFrom;
        [DrawIf("isPlayOneShot", false, ComparisonType.Equals, DisablingType.DontDraw)]
        public float loopTo;
        [DrawIf("isPlayOneShot", false, ComparisonType.Equals, DisablingType.DontDraw)]
        public float playOut;


        #if UNITY_EDITOR
        public void OnValidate()
        {
            if (Selection.activeObject != this) return;
            if (string.IsNullOrEmpty(slug)) slug = audioClip.name;
            
            ValidateData();
        }

        public LoopingData ValidateData()
        {
            if (isPlayOneShot) return null;
            
            playFrom = Mathf.Clamp(FindNearestZeroCrossing(audioClip, playFrom), 0, loopTo);
            loopFrom = Mathf.Clamp(FindNearestZeroCrossing(audioClip, loopFrom), playFrom, loopTo);
            loopTo = Mathf.Clamp(FindNearestZeroCrossing(audioClip, loopTo), loopFrom, playOut);
            playOut = Mathf.Clamp(FindNearestZeroCrossing(audioClip, playOut), loopTo, audioClip.length);
            //Debug.Log($"sample data validated");
            
            return new LoopingData(playFrom, loopFrom, loopTo, playOut);
        }
        public float FindNearestZeroCrossing(AudioClip audioClip, float time)
        {
            var oldValue = time;
            float[] samples = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(samples, 0);

            int sampleIndex = Mathf.RoundToInt(time * audioClip.frequency * audioClip.channels);

            // Search for the nearest zero-crossing point
            for (int i = sampleIndex; i < samples.Length - 1; i++)
            {
                if (samples[i] > 0 && samples[i + 1] <= 0 || samples[i] < 0 && samples[i + 1] >= 0)
                {
                    return (float)i / (audioClip.frequency * audioClip.channels);
                }
            }

            // If no zero-crossing was found, return the original time
            Debug.Log($"new value at zero-crossing point: {time}, previous value: {oldValue}");
            return time;
        }
#endif
    }
    
    
    public class LoopingData
    {
        public float playFrom;
        public float loopFrom;
        public float loopTo;
        public float playOut;

        public LoopingData(float playFrom, float loopFrom, float loopTo, float playOut)
        {   
            this.playFrom = playFrom;
            this.loopFrom = loopFrom;
            this.loopTo = loopTo;
            this.playOut = playOut;
        }
    }

    #if UNITY_EDITOR
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SamplerData), true)]
    public class SamplerDataEditor : Editor
    {
        private LoopingData previousLoopingData = new LoopingData(0,0,0,0);
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var samplerData = (SamplerData)target;
            if (samplerData.isPlayOneShot) return;
            if (samplerData.audioClip == null) return;

            if (previousLoopingData.playFrom != samplerData.playFrom ||
                previousLoopingData.loopFrom != samplerData.loopFrom ||
                previousLoopingData.playOut != samplerData.playOut || 
                previousLoopingData.loopTo != samplerData.loopTo)
            {
                previousLoopingData = samplerData.ValidateData();
            }
            DrawAudioWaveform.DrawWaveform(samplerData);
        
        }
    }
    #endif
}
