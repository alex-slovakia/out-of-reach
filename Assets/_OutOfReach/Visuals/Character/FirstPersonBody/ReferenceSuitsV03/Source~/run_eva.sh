#!/bin/bash
cd /c/Users/alexp/NASA/blender/reference-suits-v03
B="/c/Program Files/Blender Foundation/Blender 5.3/blender.exe"
O=$1; mkdir -p $O
"$B" -b --factory-startup --python src/build_eva.py -- in/A7_BodyFit02_EVA.fbx in/A7_BodyFit02_WORK.fbx $O --export > $O/run.log 2>&1 || { echo BUILD FAIL; tail -15 $O/run.log; exit 1; }
"$B" -b --factory-startup --python src/render_fit.py -- $O/A7_SuitV03_EVA.fbx $O/v --keepmats --cams ${2:-front,back,q3} --light 0.5 > $O/r.log 2>&1 || { echo RENDER FAIL; tail -20 $O/r.log; exit 1; }
grep -E "v03" $O/run.log | tail -8
