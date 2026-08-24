using System.Collections.Generic;

namespace jeanf.audiosystems
{
    /// <summary>
    /// Edit-time checks for SamplerData assets, shared by the inspector, the project-wide sweep
    /// (Tools > Audio Systems > Validate SamplerData Assets) and consuming packages (the door
    /// system's Setup Validator runs it on every SamplerData its configs reference). Everything
    /// flagged here is an authoring state that plays wrong or not at all in playmode, so it is
    /// worth catching while the asset is being edited.
    /// </summary>
    public static class SamplerDataValidation
    {
        public readonly struct Issue
        {
            public readonly bool IsError;
            public readonly string Message;

            public Issue(bool isError, string message)
            {
                IsError = isError;
                Message = message;
            }
        }

        public static List<Issue> Validate(SamplerData data)
        {
            var issues = new List<Issue>();

            if (data == null)
            {
                issues.Add(new Issue(true, "SamplerData reference is null."));
                return issues;
            }

            if (data.audioClip == null)
            {
                issues.Add(new Issue(true,
                    "no AudioClip assigned - the Sampler skips it and nothing plays."));
            }

            // volume defaults to 0 on a freshly created asset, so this is an easy one to ship.
            if (data.volume <= 0f)
            {
                issues.Add(new Issue(false,
                    "volume is 0 - the clip plays but is inaudible."));
            }

            if (data.isPlayOneShot) return issues;

            // Sampler.Update keeps snapping audioSource.time back to loopFrom once time reaches
            // loopTo. With an empty window that snap fires every frame from time 0, so the clip
            // never audibly plays and never finishes - and anything gating on the AudioSource
            // still playing (pooled door sources, isPlaying checks) stays stuck on it.
            if (data.loopTo <= data.loopFrom)
            {
                issues.Add(new Issue(true,
                    $"looping is enabled (isPlayOneShot is off) but the loop window is empty " +
                    $"(loopFrom {data.loopFrom:0.###}s, loopTo {data.loopTo:0.###}s). The playhead is snapped " +
                    "back to loopFrom every frame, so the clip never audibly plays and playback never ends."));
            }
            else
            {
                if (data.playFrom > data.loopTo)
                {
                    issues.Add(new Issue(true,
                        $"playFrom ({data.playFrom:0.###}s) is past the end of the loop window " +
                        $"(loopTo {data.loopTo:0.###}s), so playback starts by snapping straight back to loopFrom."));
                }

                if (data.playOut < data.loopTo)
                {
                    issues.Add(new Issue(false,
                        $"playOut ({data.playOut:0.###}s) is before loopTo ({data.loopTo:0.###}s) - releasing " +
                        "the loop jumps the playhead backwards."));
                }

                if (data.audioClip != null && data.loopTo > data.audioClip.length)
                {
                    issues.Add(new Issue(false,
                        $"loopTo ({data.loopTo:0.###}s) is beyond the end of the clip " +
                        $"({data.audioClip.length:0.###}s), so the loop point is never reached and the " +
                        "clip just runs out."));
                }
            }

            return issues;
        }
    }
}
