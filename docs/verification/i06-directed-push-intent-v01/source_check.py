from pathlib import Path
import re,hashlib,json
stage=Path(__file__).resolve().parent;repo=stage.parents[2];iso=repo/'.local/i01-integration'
rel='Assets/_OutOfReach/Scripts/Composition/Runtime/I06MicrogravityBridge.cs'
old=(stage/'Evidence/baseline.cs').read_text();new=(stage/rel).read_text()
checks=[]
def check(ok,why):
 assert ok,why
 checks.append(why)
def method(s,signature):
 a=s.index(signature);i=s.index('{',a)+1;depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[a:i]
for sig in ['public void Configure(', 'public void SetContactModeEnabled(', 'public bool TryPush(', 'public FloatActionResult Release(', 'public void Clear(', 'void Update(', 'void LateUpdate(', 'bool TryWorkPalmPoint(', 'bool ModeAllowed(', 'bool ValidateContact(', 'bool AtContact(', 'HandContactTarget? ReadHeld(', 'bool WristPathClear(', 'bool WristPointClear(', 'bool Nearest(', 'static Vector3 OrientRailAxisAtRequest(', 'bool RailFrame(', 'void OnPoseWillReset(', 'void CancelOwnedContact(']:
 check(method(old,sig)==method(new,sig),'Exact unchanged '+sig)
check(re.findall(r'player\.(TryPushOff|TryGrab|Release)\([^;]+;',old)==re.findall(r'player\.(TryPushOff|TryGrab|Release)\([^;]+;',new),'Same sole actual Player action dispatch statements')
check('localPushDirection = surface.transform.InverseTransformVector(direction).normalized;' in new,'Request direction remains captured in exact target local frame')
check('pushStrength = strength;' in new and 'player.TryPushOff(surface, sample.Point, direction, pushStrength)' in new,'Original input strength forwarded unchanged only at actual contact')
check('if (outward >= away) { direction = intent; return true; }' in new,'Already-away intent retained')
check('if (tangentLength >= .25f)' in new and 'ratio * ratio * (3f - 2f * ratio) * Mathf.Acos(away)' in new,'Explicit continuous inward angle, original cone above threshold')
check('!Finite(desiredTravelWorld.sqrMagnitude)' in new,'Overflowing finite components cannot reach direction calculation')
check((stage/(rel+'.meta')).read_bytes()==(iso/(rel+'.meta')).read_bytes(),'Preserved exact existing meta/GUID')
manifest=json.loads((stage/'manifest.json').read_text())
check(hashlib.sha256((stage/'Evidence/baseline.cs').read_bytes()).hexdigest()==manifest['baselineSha256'],'Baseline pinned')
check(hashlib.sha256((iso/rel).read_bytes()).hexdigest()==manifest['baselineSha256'],'Live bridge unchanged at validation time')
for f in manifest['files']:
 check(hashlib.sha256((stage/f['path']).read_bytes()).hexdigest()==f['sha256'],'Candidate manifest '+f['path'])
v04=repo/'.local/verification/BootstrapMicroRouteV04';v04manifest=json.loads((v04/'manifest.json').read_text())
for f in v04manifest['files']:
 check(hashlib.sha256((v04/f['path']).read_bytes()).hexdigest()==f['sha256'],'Frozen V04 untouched '+f['path'])
(stage/'Evidence/source-checks.json').write_text(json.dumps({'passed':len(checks),'nativeRun':False,'checks':checks},indent=2)+'\n')
print('PASS',len(checks),'source checks; manifest',hashlib.sha256((stage/'manifest.json').read_bytes()).hexdigest())
