#!/bin/bash
# usage: bash run_mh.sh <tag> [WORK|EVA|both]
cd /c/Users/alexp/NASA/blender/reference-suits-v03
B="/c/Program Files/Blender Foundation/Blender 5.3/blender.exe"
T=$1; WHICH=${2:-both}
SA=C:/Users/alexp/NASA/blender/mh_system_assets/x
python src/make_display.py out/T_V03_EvaDisplay.png > /dev/null
for S in WORK EVA; do
  [ "$WHICH" != both ] && [ "$WHICH" != $S ] && continue
  O=out/mh_${S}_$T; mkdir -p $O
  "$B" -b --factory-startup --python src/build_mh.py -- --suit $S --fit in/A7_BodyFit02_$S.fbx --mh mh_cc0 --sa $SA --delta out/mh_male_comp_delta.npy --tex out/tex --out $O --display out/T_V03_EvaDisplay.png --export > $O/run.log 2>&1 || { echo "$S BUILD FAIL"; grep -E "Error|line [0-9]+" $O/run.log | tail -6; continue; }
  grep -E "v03 " $O/run.log | tail -4
  "$B" -b --factory-startup --python src/render_fit.py -- $O/$S.blend $O/v --keepmats CYCLES REFPOSE --cams front,back,fp --light 0.5 > $O/r.log 2>&1 || { echo "$S RENDER FAIL"; tail -5 $O/r.log; }
  "$B" -b --factory-startup --python src/render_fit.py -- $O/$S.blend $O/hd --keepmats CYCLES --cams front,q3 --light 0.5 --zoom 1.66,1.1 --res 700 > $O/rh.log 2>&1
done
R=/c/Users/alexp/NASA/out-of-reach-suits/docs/art-direction/astronaut-reference-v01
[ -f out/mh_WORK_$T/v_front.png ] && python src/compare.py $R/cabin-front-back.png out/mh_WORK_$T/v_front.png out/mh_WORK_$T/v_back.png out/mh_WORK_$T/compare_work.png
[ -f out/mh_EVA_$T/v_front.png ] && python src/compare.py $R/eva-front-back.png out/mh_EVA_$T/v_front.png out/mh_EVA_$T/v_back.png out/mh_EVA_$T/compare_eva.png
echo done
