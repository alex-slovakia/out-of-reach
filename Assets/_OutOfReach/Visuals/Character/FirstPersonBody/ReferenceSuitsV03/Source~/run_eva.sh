#!/bin/bash
cd /c/Users/alexp/NASA/blender/reference-suits-v03
B="/c/Program Files/Blender Foundation/Blender 5.3/blender.exe"
O=$1; mkdir -p $O
python src/make_display.py out/T_V03_EvaDisplay.png > /dev/null
"$B" -b --factory-startup --python src/build_eva.py -- in/A7_BodyFit02_EVA.fbx in/A7_BodyFit02_WORK.fbx $O mh_cc0 out/T_V03_EvaDisplay.png --export > $O/run.log 2>&1 || { echo BUILD FAIL; grep -E "Error|line [0-9]" $O/run.log | tail -6; exit 1; }
"$B" -b --factory-startup --python src/render_fit.py -- $O/EVA.blend $O/v --keepmats --cams ${2:-front,back,q3} --light 0.5 > $O/r.log 2>&1 || { echo RENDER FAIL; tail -8 $O/r.log; exit 1; }
"$B" -b --factory-startup --python src/render_fit.py -- $O/EVA.blend $O/head --keepmats --cams front,q3 --light 0.5 --zoom 1.45,2.2 --res 900 > $O/rh.log 2>&1
grep -E "v03" $O/run.log | tail -9
