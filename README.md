AudioSystems

Lightweight Audio system for Unity. Contains resources for implementing interactive and scripted audio behaviour using scriptable objects and sample lists. Developed in collaboration with [Daniel Villegas Velez](https://github.com/dvvii).


Components: 

- "Sampler": employing Scriptable Objects (SampleData) to save information regarding volume, slug, and positions for PlayFrom/PlayTo/End.
- "Mixer Manager": an interface for Unity's Mixer Snapshots
- "FootstepController": a simple footstep sound system using Unity's AudioRandomContainer, featuring dynamic stereo placement and Raycast identification of ground tags for different sound type allocation.
- "Metronome" Simple player using dynamically adjustable BPM (managed by a timeline or script), optimized for stability through duplicate audiosources and using Unity's PlayScheduled() method.

------------------------------------------------------------------------------------------------------
How to install the package:
- add new scopedRegisteries in ProjectSettings/Package manager
- name: jeanf
- url: https://registry.npmjs.com
- scope fr.jeanf

LICENCE:

<img src="https://licensebuttons.net/l/by-nc-sa/3.0/88x31.png"></img>
