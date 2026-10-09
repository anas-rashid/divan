// The PDF reader (pdf.js), right to left as an Urdu book: the next page is to the left (button, ← key, a click on
// the left half), and on wide screens pages face each other with the cover alone, then 2|3 with page 2 on the
// right. A book marked left-to-right (dir="ltr" on the reader, e.g. English) turns the other way. Pages are fetched by byte range as they are shown, so a large scan starts quickly. Signed-in readers
// bookmark pages (the right-hand page of a spread) in their library; their bookmarks jump to the page.
export async function pdfReader(root: HTMLElement) {
  const pdfjs = await import('pdfjs-dist');
  pdfjs.GlobalWorkerOptions.workerSrc = (await import('pdfjs-dist/build/pdf.worker.min.mjs?url')).default;
  const rtl = root.dir !== 'ltr';
  const pagesBox = root.querySelector<HTMLElement>('.pdf-pages')!, input = root.querySelector<HTMLInputElement>('.pdf-page')!;
  const doc = await pdfjs.getDocument({ url: root.dataset.src!, disableAutoFetch: true, disableStream: true, rangeChunkSize: 262144 }).promise;
  root.querySelector('.pdf-total')!.textContent = String(doc.numPages).replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[+d]);
  input.max = String(doc.numPages);
  const wide = () => pagesBox.clientWidth > 900;
  // the pages shown together, starting with page p
  const spread = (p: number) => (!wide() || p === 1 ? [p] : [p % 2 ? p - 1 : p, (p % 2 ? p - 1 : p) + 1].filter((n) => n >= 2 && n <= doc.numPages));
  let page = 1, job = 0;
  async function show(p: number) {
    page = Math.min(Math.max(1, p), doc.numPages);
    const nums = spread(page), mine = ++job;
    input.value = String(nums[0]);
    const canvases = await Promise.all(nums.map(async (n) => {
      const pg = await doc.getPage(n), base = pg.getViewport({ scale: 1 });
      const tall = document.fullscreenElement === root ? window.innerHeight - 70 : window.innerHeight * 0.82;
      const fit = Math.min((pagesBox.clientWidth - 8 * nums.length) / nums.length / base.width, tall / base.height);
      const v = pg.getViewport({ scale: fit * devicePixelRatio }), c = document.createElement('canvas');
      c.width = Math.floor(v.width); c.height = Math.floor(v.height);
      c.style.width = `${Math.floor(v.width / devicePixelRatio)}px`; c.style.height = `${Math.floor(v.height / devicePixelRatio)}px`;
      await pg.render({ canvasContext: c.getContext('2d')!, viewport: v, canvas: c } as any).promise;
      return c;
    }));
    if (mine === job) { pagesBox.replaceChildren(...canvases); marks(); } // the container is RTL: the first page is on the right
  }
  const step = (d: number) => { const nums = spread(page); show(d > 0 ? nums[nums.length - 1] + 1 : spread(Math.max(1, nums[0] - 1))[0]); };
  root.querySelector<HTMLButtonElement>('.pdf-next')!.onclick = () => step(1);
  root.querySelector<HTMLButtonElement>('.pdf-prev')!.onclick = () => step(-1);
  input.onchange = () => show(Number(input.value) || 1);
  // page bookmarks
  const markBtn = root.querySelector<HTMLButtonElement>('.pdf-mark'), list = root.querySelector<HTMLElement>('.pdf-marks');
  let saved: number[] = JSON.parse(root.dataset.marks ?? '[]');
  const digits = (n: number) => String(n).replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[+d]);
  function marks() {
    if (!markBtn || !list) return;
    const on = saved.includes(spread(page)[0]);
    markBtn.setAttribute('aria-pressed', String(on)); markBtn.textContent = on ? 'صفحہ محفوظ شدہ' : 'صفحہ محفوظ کریں';
    list.replaceChildren(...saved.map((n) => { const b = document.createElement('button'); b.type = 'button'; b.className = 'tag-chip'; b.textContent = `صفحہ ${digits(n)}`; b.onclick = () => show(n); return b; }));
    list.hidden = !saved.length;
  }
  markBtn?.addEventListener('click', async () => {
    if (!document.body.dataset.user) { location.href = '/signin?next=' + encodeURIComponent(location.pathname); return; }
    const p = spread(page)[0];
    const r = await fetch('/api/library', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ kind: 'page', ebookId: Number(root.dataset.ebook), page: p }) });
    if (!r.ok) return;
    saved = (await r.json()).saved ? [...saved, p].sort((a, b) => a - b) : saved.filter((n) => n !== p);
    marks();
  });
  // the whole screen for the reader (the page is redrawn to the new size)
  const full = root.querySelector<HTMLButtonElement>('.pdf-full')!;
  const toggle = () => (document.fullscreenElement ? document.exitFullscreen() : root.requestFullscreen()).catch(() => {});
  full.onclick = toggle;
  document.addEventListener('fullscreenchange', () => { full.textContent = document.fullscreenElement === root ? 'واپس' : 'پوری اسکرین'; show(page); });
  // a click on the half the book turns towards goes forward
  pagesBox.onclick = (e) => { const left = e.clientX < pagesBox.getBoundingClientRect().left + pagesBox.clientWidth / 2; step(left === rtl ? 1 : -1); };
  document.addEventListener('keydown', (e) => {
    if ((e.target as HTMLElement).closest('input, textarea')) return;
    if (e.key === 'ArrowLeft') step(rtl ? 1 : -1);
    if (e.key === 'ArrowRight') step(rtl ? -1 : 1);
    if (e.key === 'f' || e.key === 'F') toggle();
  });
  let resize = 0;
  window.addEventListener('resize', () => { clearTimeout(resize); resize = window.setTimeout(() => show(page), 200); });
  await show(Number(new URLSearchParams(location.search).get('page')) || 1); // ?page= from a bookmark
}
