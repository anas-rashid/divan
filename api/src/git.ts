// Publishing to git (#34): each published version is a commit in the divan-data checkout, so the public git
// history is the record of every change. Commits run one at a time (ponytail: in-process queue; one API process).
// Moderators appear by their public name with a placeholder email, never their real address.
//   DIVAN_GIT_PUSH=1        push after each commit (else the daily sync or a release pushes)
//   DIVAN_GIT_EMAIL_DOMAIN  domain of the placeholder emails (default users.noreply.divan)
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const run = promisify(execFile);
const git = (dir: string, ...args: string[]) => run('git', ['-C', dir, ...args], { maxBuffer: 16 * 1024 * 1024 });
let queue: Promise<unknown> = Promise.resolve();

export type Person = { id: number | string; name: string };
// a moderator as a git identity: public name, placeholder email
export const identity = (p: Person) => `${p.name.replace(/[<>\n]/g, '')} <moderator-${p.id}@${process.env.DIVAN_GIT_EMAIL_DOMAIN ?? 'users.noreply.divan'}>`;

// commit these files (paths relative to dir) as the author; returns the commit id, or null if dir is not a git
// checkout (publishing still works, there is just no commit)
export function commit(dir: string, files: string[], author: Person, message: string): Promise<string | null> {
  const job = queue.then(async () => {
    try { await git(dir, 'rev-parse', '--is-inside-work-tree'); } catch { return null; }
    await git(dir, 'add', '--', ...files);
    await git(dir, '-c', 'user.name=Divan', '-c', `user.email=publish@${process.env.DIVAN_GIT_EMAIL_DOMAIN ?? 'users.noreply.divan'}`,
      'commit', '--quiet', `--author=${identity(author)}`, '-m', message, '--', ...files);
    const sha = (await git(dir, 'rev-parse', 'HEAD')).stdout.trim();
    if (process.env.DIVAN_GIT_PUSH === '1') await git(dir, 'push', '--quiet').catch((e) => console.error('divan-data push failed:', e.message));
    return sha;
  });
  queue = job.catch(() => {});
  return job;
}
