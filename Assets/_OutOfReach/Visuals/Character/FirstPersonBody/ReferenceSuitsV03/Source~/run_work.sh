#!/bin/bash
# usage: bash run_work.sh <outdir> [cams]
cd /c/Users/alexp/NASA/blender/reference-suits-v03
B="/c/Program Files/Blender Foundation/Blender 5.3/blender.exe"
O=$1; mkdir -p $O
"$B" -b --factory-startup --python src/build_work.py -- in/A7_BodyFit02_WORK.fbx $O mh_cc0 --export > $O/run.log 2>&1 || { echo BUILD FAIL; grep -E "Error|line [0-9]" $O/run.log | tail -6; exit 1; }
"$B" -b --factory-startup --python src/render_fit.py -- $O/WORK.blend $O/v --keepmats --cams ${2:-front,back,q3} --light 0.6 > $O/r.log 2>&1 || { echo RENDER FAIL; tail -8 $O/r.log; exit 1; }
"$B" -b --factory-startup --python src/render_fit.py -- $O/WORK.blend $O/head --keepmats --cams front,q3 --light 0.6 --zoom 1.62,1.6 --res 900 > $O/rh.log 2>&1
grep -E "v03" $O/run.log | tail -7
