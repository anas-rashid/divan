#!/usr/bin/env python3
"""Where a portrait's face is, so the round avatar can zoom onto it (api/src/portraits.ts runs this; optional: it needs
OpenCV, `pip install "opencv-python-headless<5"` (OpenCV 5 dropped these detectors); without it portraits keep the default
framing and admins can frame by hand).

    python3 face_focus.py <image> [<image> ...]   ->  one JSON object: {path: {"x", "y", "z"} | null}

x, y: the face's middle (a little above it, for a turban or cap) as fractions of the picture; z: a zoom that makes the face
about 35% of the circle (at most 2x). The avatar puts that point in view (object-position) and zooms around it
(transform-origin), so the picture always fills the circle: no empty corner, however near an edge the face is."""
import json, sys
import cv2

CASCADES = [cv2.CascadeClassifier(cv2.data.haarcascades + n) for n in
            ('haarcascade_frontalface_default.xml', 'haarcascade_frontalface_alt2.xml', 'haarcascade_profileface.xml')]


def face(gray):
    """the largest face any of the detectors finds, (x, y, w, h), or None"""
    h, w = gray.shape
    best = None
    for c in CASCADES:
        for img, flip in ((gray, False), (cv2.flip(gray, 1), True)):  # profiles: both directions
            for (x, y, fw, fh) in c.detectMultiScale(img, scaleFactor=1.08, minNeighbors=5, minSize=(max(20, w // 20), max(20, h // 20))):
                if flip: x = w - x - fw
                if not best or fw * fh > best[2] * best[3]: best = (x, y, fw, fh)
    return best


def focus(path):
    img = cv2.imread(path)
    if img is None: return None
    h, w = img.shape[:2]
    f = face(cv2.equalizeHist(cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)))
    if not f: return None
    x, y, fw, fh = f
    if fh / h < .08: return None               # a tiny 'face' in a big picture is usually a false match: keep the plain framing
    a = w / h
    face_h = fh / h * (1 / a if a < 1 else 1.0)  # the face's height in the circle at no zoom (the picture covers the circle)
    z = max(1.0, min(2.0, .35 / face_h))
    return {'x': round((x + fw / 2) / w, 4), 'y': round((y + fh * .42) / h, 4), 'z': round(z, 3)}

if __name__ == '__main__':
    print(json.dumps({p: focus(p) for p in sys.argv[1:]}))
