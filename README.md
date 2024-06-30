# XIVJitterFix

Fixes SquareEnix' botched anti aliasing implementation.

## Wtf is this

With the release of 7.0 SquareEnix has implemented several temporal antialiasing solutions. However they all have a severe bug that affects image quality significantly.

This plugin will restore using jitter when talking to NPCs or in cutscenes and gpose. 

Jitter (not noticeable micromovements of the camera) is necessary for temporal antialiasing solutions (DLSS, DLAA, TSCMAA+jitter) to work properly. For some reason, SquareEnix decided we don't need jitter during the time where the game benefits from using it the most.

## How To Use

Install and enable in Dlamauds.