// Make an account an admin, from the server (the first admin cannot be made from the site):
//   node src/admin-cli.ts admin@example.com
// An existing account becomes an admin. Without one (e.g. a new server with sign-up closed, DIVAN_SIGNUP=0) the account
// is created as an admin with a temporary password, printed once: sign in with it and change it on the account page.
import { pool } from './db.ts';
import { audit, temporaryPassword } from './admin.ts';
import { hashPassword, validEmail } from './auth.ts';

const email = (process.argv[2] ?? '').trim().toLowerCase();
if (!validEmail(email)) {
  console.error('usage: node src/admin-cli.ts <email>');
  process.exit(1);
}
const { rows } = await pool.query(`UPDATE users SET role = 'admin', disabled_at = NULL WHERE email = $1 RETURNING id, email`, [email]);
if (rows[0]) {
  await audit(null, 'promote', rows[0], { to: 'admin', via: 'server' });
  console.log(`${email} is now an admin`);
} else {
  const password = temporaryPassword();
  const u = (await pool.query(`INSERT INTO users (email, password_hash, role) VALUES ($1, $2, 'admin') RETURNING id, email`,
    [email, await hashPassword(password)])).rows[0];
  await audit(null, 'create', u, { role: 'admin', via: 'server' });
  console.log(`${email}: a new admin account. Temporary password (shown once): ${password}`);
}
await pool.end();
