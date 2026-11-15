#!/bin/bash
# re-render + sheet for an existing build tag
cd /c/Users/alexp/NASA/blender/reference-suits-v03
B="/c/Program Files/Blender Foundation/Blender 5.3/blender.exe"
T=$1; CAMS=${2:-front,back,fp}
WEAVE=/c/Users/alexp/NASA/out-of-reach-suits/Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Textures/Body_Weave_Normal.png
OW=out/work_$T; OE=out/eva_$T
"$B" -b --factory-startup --python src/render_fit.py -- $OW/WORK.blend $OW/v --keepmats CYCLES REFPOSE --weave $WEAVE --cams $CAMS --light 0.55 > $OW/r.log 2>&1 || { echo WORK RENDER FAIL; tail -5 $OW/r.log; }
"$B" -b --factory-startup --python src/render_fit.py -- $OE/EVA.blend $OE/v --keepmats CYCLES REFPOSE --weave $WEAVE --cams $CAMS --light 0.45 > $OE/r.log 2>&1 || { echo EVA RENDER FAIL; tail -5 $OE/r.log; }
R=/c/Users/alexp/NASA/out-of-reach-suits/docs/art-direction/astronaut-reference-v01
python src/compare.py $R/cabin-front-back.png $OW/v_front.png $OW/v_back.png $OW/compare_work.png
python src/compare.py $R/eva-front-back.png $OE/v_front.png $OE/v_back.png $OE/compare_eva.png
python - <<PYEOF
from PIL import Image
a = Image.open('$OW/compare_work.png'); b = Image.open('$OE/compare_eva.png')
fa = Image.open('$OW/v_fp.png'); fb = Image.open('$OE/v_fp.png')
W = max(a.width, b.width)
fp = Image.new('RGB', (fa.width + fb.width, fa.height)); fp.paste(fa, (0, 0)); fp.paste(fb, (fa.width, 0))
fp = fp.resize((W, int(fp.height * W / fp.width)))
s = Image.new('RGB', (W, a.height + b.height + fp.height)); s.paste(a, (0, 0)); s.paste(b, (0, a.height)); s.paste(fp, (0, a.height + b.height))
s.resize((s.width * 2 // 5, s.height * 2 // 5)).save('out/sheet_$T.png')
PYEOF
echo done
