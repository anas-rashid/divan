// A book's cover, made in the browser: a PDF's first page (pdf.js) or an EPUB's own cover image (epub.js).
// The server has no PDF tools; the moderator's browser makes the cover once, when the book is added.
export async function makeCover(data: ArrayBuffer, kind: 'pdf' | 'epub'): Promise<Blob | null> {
  if (kind === 'pdf') {
    const pdfjs = await import('pdfjs-dist');
    pdfjs.GlobalWorkerOptions.workerSrc = (await import('pdfjs-dist/build/pdf.worker.min.mjs?url')).default;
    const page = await (await pdfjs.getDocument({ data: new Uint8Array(data) }).promise).getPage(1);
    const viewport = page.getViewport({ scale: 1 }), scale = 480 / viewport.width; // 480px wide thumbnails
    const v = page.getViewport({ scale }), canvas = document.createElement('canvas');
    canvas.width = Math.round(v.width); canvas.height = Math.round(v.height);
    await page.render({ canvasContext: canvas.getContext('2d')!, viewport: v, canvas } as any).promise;
    return new Promise((done) => canvas.toBlob(done, 'image/jpeg', 0.85));
  }
  const { default: ePub } = await import('epubjs');
  const url = await ePub(data).coverUrl();
  return url ? (await fetch(url)).blob() : null;
}

export async function sendCover(id: number | string, cover: Blob) {
  const res = await fetch(`/ebooks/cover/${id}`, { method: 'POST', body: cover, headers: { 'content-type': 'application/octet-stream' } });
  if (!res.ok) throw new Error((await res.json().catch(() => ({}))).error ?? 'سرورق محفوظ نہیں ہوا');
}
