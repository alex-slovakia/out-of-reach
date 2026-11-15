"""Static check of a downloaded asset zip before extraction: entry count, total size, unsafe paths, extension census,
and a summary of the asset folders we may use. python -I inspect_zip.py <zip>"""
import sys, zipfile, collections, os

z = zipfile.ZipFile(sys.argv[1])
bad, ext, tot = [], collections.Counter(), 0
dirs = collections.Counter()
for i in z.infolist():
    n = i.filename
    tot += i.file_size
    if n.startswith(("/", "\\")) or ".." in n.replace("\\", "/").split("/") or ":" in n:
        bad.append(n)
    ext[os.path.splitext(n)[1].lower()] += 1
    parts = n.replace("\\", "/").split("/")
    if len(parts) >= 3:
        dirs["/".join(parts[:2])] += 1
print("entries", len(z.infolist()), "uncompressed MB", round(tot / 1e6, 1), "unsafe", bad[:5])
print("extensions", dict(ext.most_common(20)))
print("top dirs", dict(dirs.most_common(40)))
exe = [n for n in z.namelist() if os.path.splitext(n)[1].lower() in (".exe", ".dll", ".bat", ".cmd", ".ps1", ".js", ".vbs", ".py", ".sh", ".lnk", ".msi", ".scr")]
print("executables/scripts", exe[:20])
for key in ("clothes/", "hair/", "eyebrows/", "eyelashes/", "skins/", "eyes/"):
    names = sorted({n.replace("\\", "/").split(key, 1)[1].split("/")[0] for n in z.namelist() if key in n.replace("\\", "/")})
    print(key, names[:80])
