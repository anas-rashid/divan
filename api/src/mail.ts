// Email (owner decision 2026-10-17): invitations (an account an admin made, or a request to join they approved), admins
// told of new requests to join, and a welcome on sign-up. Sent through any SMTP provider, set in the .env:
//   SMTP_URL=smtps://USER:PASSWORD@smtp.example.com:465     (or smtp://...:587, upgraded with STARTTLS)
//   MAIL_FROM="دیوان <hello@divan.pk>"                        (an address of the domain the provider verified)
// Without SMTP_URL (for now everywhere: email is parked, owner 2026-10-25) a message is logged and kept in `outbox`, not
// sent: mailOn() says which, and admins pass invitation links on themselves. To turn it on: `npm install nodemailer`
// in api/ (it is loaded only when SMTP_URL is set), the provider's DNS records, then SMTP_URL and MAIL_FROM.
const transport = process.env.SMTP_URL ? (await import('nodemailer')).default.createTransport(process.env.SMTP_URL) : null;
const FROM = process.env.MAIL_FROM ?? 'دیوان <hello@divan.pk>';
export const SITE = (process.env.SITE_URL ?? 'http://localhost:4321').replace(/\/$/, '');
export const mailOn = () => !!transport;
export const outbox: { to: string[]; subject: string; text: string }[] = []; // ponytail: kept only without SMTP_URL (tests read it)

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);

// paragraphs (blank-line separated) and an optional button; right to left in HTML, the link written out in the text part
export function render(paragraphs: string[], button?: { text: string; url: string }) {
  const html = `<div dir="rtl" lang="ur" style="font-family:'Noto Naskh Arabic',Tahoma,sans-serif;font-size:17px;line-height:1.9;max-width:560px;margin:auto">` +
    paragraphs.map((p) => `<p>${esc(p).replace(/\n/g, '<br>')}</p>`).join('') +
    (button ? `<p style="text-align:center;margin:28px 0"><a href="${esc(button.url)}" style="background:#bf3f2a;color:#fff;padding:10px 22px;border-radius:8px;text-decoration:none">${esc(button.text)}</a></p>` +
      `<p style="font-size:13px;color:#666">${esc('بٹن کام نہ کرے تو یہ لنک کھولیں:')}<br><a dir="ltr" href="${esc(button.url)}">${esc(button.url)}</a></p>` : '') +
    `<p style="font-size:13px;color:#666">— <a href="${SITE}">${esc('دیوان')}</a></p></div>`;
  const text = [...paragraphs, ...(button ? [`${button.text}: ${button.url}`] : []), `— دیوان ${SITE}`].join('\n\n');
  return { html, text };
}

// true when sent (or, without SMTP_URL, logged); a failure is logged and gives false, so the caller can fall back
export async function sendMail(to: string | string[], subject: string, paragraphs: string[], button?: { text: string; url: string }) {
  const list = [to].flat().filter(Boolean);
  if (!list.length) return false;
  const { html, text } = render(paragraphs, button);
  if (!transport) {
    outbox.push({ to: list, subject, text });
    if (!process.env.NODE_TEST_CONTEXT) console.log(`mail (not sent, no SMTP_URL) to ${list.join(', ')}: ${subject}\n${text}`);
    return true;
  }
  try {
    // to several people (the admins): each gets their own message, so no one sees the others' addresses
    for (const t of list) await transport.sendMail({ from: FROM, to: t, subject, text, html });
    return true;
  } catch (e) {
    console.error('mail failed', subject, e);
    return false;
  }
}
